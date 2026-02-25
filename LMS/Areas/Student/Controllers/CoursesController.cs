using System.Text.RegularExpressions;
using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// دورات الطالب - Student Courses Controller with Recommendations & Goals
/// </summary>
public class CoursesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IRecommendationService recommendationService,
        ILearningAnalyticsService analyticsService,
        ILogger<CoursesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _recommendationService = recommendationService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// تصفح الدورات المتاحة - Browse available courses
    /// </summary>
    public async Task<IActionResult> Browse(
        string? search,
        int? categoryId,
        string? level,
        string? sort = "popular",
        int page = 1,
        bool? free = null,
        bool? paid = null,
        string[]? duration = null,
        decimal? rating = null)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (page < 1) page = 1;

        const int pageSize = 12;

        var query = _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .Include(c => c.Reviews)
            .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(c => c.Title.Contains(searchTerm) ||
                (c.Description != null && c.Description.Contains(searchTerm)) ||
                (c.ShortDescription != null && c.ShortDescription.Contains(searchTerm)));
        }

        // Category filter
        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        // Level filter
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<CourseLevel>(level, out var parsedLevel))
        {
            query = query.Where(c => c.Level == parsedLevel);
        }

        // Price filter (free / paid)
        if (free == true && paid != true)
        {
            query = query.Where(c => c.IsFree || (c.DiscountPrice ?? c.Price) == 0);
        }
        else if (paid == true && free != true)
        {
            query = query.Where(c => !c.IsFree && (c.DiscountPrice ?? c.Price) > 0);
        }

        // Duration filter (multiple allowed: short <5h, medium 5-20h, long >20h)
        var durationParts = (duration ?? Array.Empty<string>())
            .SelectMany(d => (d ?? "").Split(',', StringSplitOptions.TrimEntries))
            .Select(d => d.ToLowerInvariant())
            .Where(d => d is "short" or "medium" or "long")
            .ToHashSet();
        if (durationParts.Count > 0)
        {
            query = query.Where(c =>
                (durationParts.Contains("short") && c.TotalDurationMinutes < 5 * 60) ||
                (durationParts.Contains("medium") && c.TotalDurationMinutes >= 5 * 60 && c.TotalDurationMinutes <= 20 * 60) ||
                (durationParts.Contains("long") && c.TotalDurationMinutes > 20 * 60));
        }

        // Rating filter
        if (rating.HasValue && rating.Value > 0)
        {
            query = query.Where(c => c.AverageRating >= rating.Value);
        }

        // Total count with same filters (for pagination)
        var totalCount = await query.CountAsync();

        // Sorting
        query = sort?.ToLower() switch
        {
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            "price_low" => query.OrderBy(c => c.DiscountPrice ?? c.Price),
            "price_high" => query.OrderByDescending(c => c.DiscountPrice ?? c.Price),
            "rating" => query.OrderByDescending(c => c.AverageRating),
            _ => query.OrderByDescending(c => c.TotalStudents) // popular
        };

        var courses = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Check which courses user is already enrolled in
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Select(e => e.CourseId)
            .ToListAsync();

        ViewBag.EnrolledCourseIds = enrolledCourseIds;
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.SearchQuery = search;
        ViewBag.SelectedCategory = categoryId;
        ViewBag.SelectedLevel = level;
        ViewBag.SelectedSort = sort;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;
        ViewBag.Free = free;
        ViewBag.Paid = paid;
        ViewBag.DurationParts = durationParts;
        ViewBag.DurationRaw = duration != null && duration.Length > 0 ? string.Join(",", duration) : (string?)null;
        ViewBag.Rating = rating;

        return View(courses);
    }

    /// <summary>
    /// دوراتي مع توصيات ذكية - My courses with smart recommendations
    /// </summary>
    public async Task<IActionResult> Index(string? filter = "all")
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var query = _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Category)
                .Include(e => e.LessonProgress)
                .Where(e => e.StudentId == userId);

            // Apply filters
            query = filter?.ToLower() switch
            {
                "active" => query.Where(e => e.Status == EnrollmentStatus.Active),
                "completed" => query.Where(e => e.Status == EnrollmentStatus.Completed),
                "inprogress" => query.Where(e => e.Status == EnrollmentStatus.Active && e.ProgressPercentage > 0 && e.ProgressPercentage < 100),
                "notstarted" => query.Where(e => e.ProgressPercentage == 0),
                _ => query
            };

            var enrollments = await query
                .OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt)
                .ToListAsync();

            // Get recommendations (with error handling)
            List<CourseRecommendation> recommendations;
            try
            {
                recommendations = await _recommendationService.GetCourseRecommendationsAsync(userId, 6);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get recommendations for user {UserId}", userId);
                recommendations = new List<CourseRecommendation>();
            }
            
            // Get learning stats (with error handling)
            StudentLearningStats? stats = null;
            try
            {
                stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get learning stats for user {UserId}", userId);
            }

            // Get at-risk courses (with error handling)
            List<AtRiskAlert> atRiskAlerts;
            try
            {
                atRiskAlerts = await _analyticsService.GetAtRiskAlertsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get at-risk alerts for user {UserId}", userId);
                atRiskAlerts = new List<AtRiskAlert>();
            }

            ViewBag.Recommendations = recommendations;
            ViewBag.Stats = stats;
            ViewBag.AtRiskAlerts = atRiskAlerts;
            ViewBag.Filter = filter;

            return View(enrollments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading courses for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الدورات");
            return View(new List<Domain.Entities.Learning.Enrollment>());
        }
    }

    /// <summary>
    /// الدورات المكتملة - Completed courses (convenience route)
    /// </summary>
    public IActionResult Completed()
    {
        return RedirectToAction(nameof(Index), new { filter = "completed" });
    }

    /// <summary>
    /// الدورات النشطة - Active courses (convenience route)
    /// </summary>
    public IActionResult Active()
    {
        return RedirectToAction(nameof(Index), new { filter = "active" });
    }

    /// <summary>
    /// الدورات قيد التقدم - In progress courses (convenience route)
    /// </summary>
    public IActionResult InProgress()
    {
        return RedirectToAction(nameof(Index), new { filter = "inprogress" });
    }

    /// <summary>
    /// الدورات التي لم تبدأ بعد - Not started courses (convenience route)
    /// </summary>
    public IActionResult NotStarted()
    {
        return RedirectToAction(nameof(Index), new { filter = "notstarted" });
    }

    /// <summary>
    /// تفاصيل الدورة المحسّنة - Enhanced course details with progress & recommendations
    /// Accepts both enrollment ID and course ID
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Try to find enrollment by enrollment ID first
        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Include(e => e.Course)
                .ThenInclude(c => c.Category)
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.Id == id && e.StudentId == userId);

        // If not found, try to find by course ID
        if (enrollment == null)
        {
            enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Category)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.CourseId == id && e.StudentId == userId);
        }

        if (enrollment == null)
        {
            // Student is not enrolled in this course
            // Check if the course exists and redirect to preview
            var courseExists = await _context.Courses
                .AnyAsync(c => c.Id == id && c.Status == Domain.Enums.CourseStatus.Published);
            
            if (courseExists)
            {
                SetErrorMessage("أنت غير مسجل في هذه الدورة. يمكنك الاطلاع على تفاصيلها والتسجيل.");
                return RedirectToAction(nameof(Preview), new { id });
            }
            
            return NotFound();
        }

        // Get similar courses
        var similarCourses = await _recommendationService.GetSimilarCoursesAsync(enrollment.CourseId, 4);

        // Get performance analytics
        var performance = await _analyticsService.GetPerformanceAnalyticsAsync(userId, id);

        // Get completion prediction
        var prediction = await _analyticsService.PredictCourseCompletionAsync(userId, id);

        // Get strengths & weaknesses
        var analysis = await _analyticsService.AnalyzeStrengthsWeaknessesAsync(userId, id);

        // Calculate next lesson
        var completedLessonIds = enrollment.LessonProgress
            .Where(lp => lp.IsCompleted)
            .Select(lp => lp.LessonId)
            .ToList();

        var allLessons = enrollment.Course.Modules
            .OrderBy(m => m.OrderIndex)
            .SelectMany(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .ToList();

        var nextLesson = allLessons.FirstOrDefault(l => !completedLessonIds.Contains(l.Id));
        var lastAccessedLesson = enrollment.LastLessonId.HasValue
            ? allLessons.FirstOrDefault(l => l.Id == enrollment.LastLessonId)
            : null;

        ViewBag.SimilarCourses = similarCourses;
        ViewBag.Performance = performance;
        ViewBag.Prediction = prediction;
        ViewBag.Analysis = analysis;
        ViewBag.NextLesson = nextLesson;
        ViewBag.LastAccessedLesson = lastAccessedLesson;
        ViewBag.CompletedLessons = completedLessonIds.Count;
        ViewBag.TotalLessons = allLessons.Count;

        return View(enrollment);
    }

    /// <summary>
    /// معاينة الدورة - Preview course (before enrollment)
    /// </summary>
    public async Task<IActionResult> Preview(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var course = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .Include(c => c.SubCategory)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Include(c => c.Reviews)
                .ThenInclude(r => r.Student)
            .Include(c => c.Requirements)
            .Include(c => c.LearningOutcomes)
            .Include(c => c.WhatYouWillLearn)
            .FirstOrDefaultAsync(c => c.Id == id && c.Status == CourseStatus.Published);

        if (course == null)
            return NotFound();

        // Check if already enrolled
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == userId && e.CourseId == id);

        if (enrollment != null)
        {
            // Already enrolled, redirect to course details
            return RedirectToAction(nameof(Details), new { id = enrollment.Id });
        }

        // Check if in wishlist
        var isInWishlist = await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.CourseId == id);

        // Check if in cart
        var isInCart = await _context.CartItems
            .AnyAsync(c => c.UserId == userId && c.CourseId == id);

        // Get similar courses
        var similarCourses = await _recommendationService.GetSimilarCoursesAsync(id, 4);

        // Get recent reviews (top 5)
        var recentReviews = course.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToList();

        // Calculate rating distribution
        var ratingDistribution = course.Reviews
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        ViewBag.IsInWishlist = isInWishlist;
        ViewBag.IsInCart = isInCart;
        ViewBag.SimilarCourses = similarCourses;
        ViewBag.RecentReviews = recentReviews;
        ViewBag.RatingDistribution = ratingDistribution;

        // كشف مزود الفيديو واستخراج معرف الفيديو - Detect video provider and extract video ID
        var previewVideoProvider = "Local";
        string? previewVideoId = null;

        if (!string.IsNullOrEmpty(course.PreviewVideoUrl))
        {
            var url = course.PreviewVideoUrl.Trim();

            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                previewVideoProvider = "YouTube";
                if (url.Contains("youtu.be/"))
                    previewVideoId = url.Split("youtu.be/").LastOrDefault()?.Split('?').FirstOrDefault();
                else if (url.Contains("v="))
                    previewVideoId = url.Split("v=").LastOrDefault()?.Split('&').FirstOrDefault();
                else if (url.Contains("/embed/"))
                    previewVideoId = url.Split("/embed/").LastOrDefault()?.Split('?').FirstOrDefault();
            }
            else if (url.Contains("vimeo.com"))
            {
                previewVideoProvider = "Vimeo";
                var match = Regex.Match(url, @"vimeo\.com\/(\d+)");
                previewVideoId = match.Success ? match.Groups[1].Value : null;
            }
        }

        ViewBag.PreviewVideoProvider = previewVideoProvider;
        ViewBag.PreviewVideoId = previewVideoId;

        return View(course);
    }

    /// <summary>
    /// ابدأ التعلم - Start learning (redirects to first lesson or continues from last accessed)
    /// </summary>
    public async Task<IActionResult> Learn(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Find the enrollment for this course
        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules.OrderBy(m => m.OrderIndex))
                    .ThenInclude(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.StudentId == userId && e.CourseId == id);

        if (enrollment == null)
        {
            // Not enrolled, redirect to preview
            SetWarningMessage("يجب التسجيل في الدورة أولاً");
            return RedirectToAction(nameof(Preview), new { id });
        }

        // Find the last accessed lesson or next incomplete lesson
        int? targetLessonId = null;

        // First, check for last accessed lesson
        var lastAccessed = enrollment.LessonProgress
            .OrderByDescending(lp => lp.LastAccessedAt)
            .FirstOrDefault();

        if (lastAccessed != null && !lastAccessed.IsCompleted)
        {
            targetLessonId = lastAccessed.LessonId;
        }
        else
        {
            // Find first incomplete lesson
            var completedLessonIds = enrollment.LessonProgress
                .Where(lp => lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToHashSet();

            foreach (var module in enrollment.Course.Modules.OrderBy(m => m.OrderIndex))
            {
                foreach (var lesson in module.Lessons.OrderBy(l => l.OrderIndex))
                {
                    if (!completedLessonIds.Contains(lesson.Id))
                    {
                        targetLessonId = lesson.Id;
                        break;
                    }
                }
                if (targetLessonId.HasValue) break;
            }

            // If all lessons completed, go to first lesson
            if (!targetLessonId.HasValue)
            {
                var firstModule = enrollment.Course.Modules.OrderBy(m => m.OrderIndex).FirstOrDefault();
                var firstLesson = firstModule?.Lessons.OrderBy(l => l.OrderIndex).FirstOrDefault();
                targetLessonId = firstLesson?.Id;
            }
        }

        if (targetLessonId.HasValue)
        {
            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = targetLessonId.Value });
        }

        // Fallback to course details
        SetWarningMessage("لا توجد دروس متاحة في هذه الدورة");
        return RedirectToAction(nameof(Details), new { id = enrollment.Id });
    }

    /// <summary>
    /// مجموعاتي - My collections/playlists
    /// </summary>
    public async Task<IActionResult> Collections()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var collections = await _context.CourseCollections
            .Include(cc => cc.Courses)
                .ThenInclude(c => c.Course)
            .Where(cc => cc.UserId == userId)
            .OrderByDescending(cc => cc.CreatedAt)
            .ToListAsync();

        return View(collections);
    }

    /// <summary>
    /// إنشاء مجموعة جديدة - Create new collection
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCollection(string name, string? description)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Json(new { success = false, message = "اسم المجموعة مطلوب" });
        }

        var collection = new Domain.Entities.Learning.CourseCollection
        {
            UserId = userId,
            Name = name,
            Description = description,
            IsPublic = false
        };

        _context.CourseCollections.Add(collection);
        await _context.SaveChangesAsync();

        return Json(new { success = true, collectionId = collection.Id, message = "تم إنشاء المجموعة بنجاح" });
    }

    /// <summary>
    /// إضافة دورة إلى مجموعة - Add course to collection
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCollection(int collectionId, int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var collection = await _context.CourseCollections
            .Include(cc => cc.Courses)
            .FirstOrDefaultAsync(cc => cc.Id == collectionId && cc.UserId == userId);

        if (collection == null)
            return Json(new { success = false, message = "المجموعة غير موجودة" });

        // Check if already in collection
        if (collection.Courses.Any(c => c.CourseId == courseId))
            return Json(new { success = false, message = "الدورة موجودة في المجموعة بالفعل" });

        var collectionCourse = new Domain.Entities.Learning.CourseCollectionCourse
        {
            CollectionId = collectionId,
            CourseId = courseId,
            OrderIndex = collection.Courses.Count + 1
        };

        _context.CourseCollectionCourses.Add(collectionCourse);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تمت إضافة الدورة إلى المجموعة" });
    }

    /// <summary>
    /// استئناف التعلم - Resume learning (smart resume)
    /// </summary>
    public async Task<IActionResult> ResumeLearning(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

        if (enrollment == null)
            return NotFound();

        // Check if course has modules and lessons
        if (enrollment.Course == null || !enrollment.Course.Modules.Any())
        {
            SetErrorMessage("هذه الدورة لا تحتوي على دروس حالياً");
            return RedirectToAction("Index", "Dashboard", new { area = "Student" });
        }

        // Smart resume: Last accessed lesson or next incomplete lesson
        int? targetLessonId = null;

        if (enrollment.LastLessonId.HasValue)
        {
            // Check if last lesson is completed
            var lastLessonProgress = enrollment.LessonProgress
                .FirstOrDefault(lp => lp.LessonId == enrollment.LastLessonId);

            if (lastLessonProgress != null && !lastLessonProgress.IsCompleted)
            {
                // Resume incomplete lesson
                targetLessonId = enrollment.LastLessonId;
            }
        }

        if (!targetLessonId.HasValue)
        {
            // Find next incomplete lesson
            var completedLessonIds = enrollment.LessonProgress
                .Where(lp => lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToList();

            var allLessons = enrollment.Course.Modules
                .OrderBy(m => m.OrderIndex)
                .SelectMany(m => m.Lessons.OrderBy(l => l.OrderIndex))
                .ToList();

            var nextLesson = allLessons.FirstOrDefault(l => !completedLessonIds.Contains(l.Id));
            targetLessonId = nextLesson?.Id;
        }

        if (targetLessonId.HasValue)
        {
            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = targetLessonId.Value });
        }

        // Course completed
        SetSuccessMessage("مبروك! لقد أنهيت جميع دروس هذه الدورة 🎉");
        return RedirectToAction(nameof(Details), new { id = enrollmentId });
    }

    /// <summary>
    /// مقارنة الدورات - Compare courses
    /// </summary>
    public async Task<IActionResult> CompareCourses(int[] courseIds)
    {
        if (courseIds == null || courseIds.Length < 2 || courseIds.Length > 3)
        {
            SetErrorMessage("يرجى اختيار 2-3 دورات للمقارنة");
            return RedirectToAction("Index", "Home");
        }

        var courses = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .Include(c => c.Reviews)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Where(c => courseIds.Contains(c.Id))
            .ToListAsync();

        var comparison = courses.Select(c => new CourseComparisonItem
        {
            CourseId = c.Id,
            Title = c.Title,
            InstructorName = c.Instructor != null ? $"{c.Instructor.FirstName} {c.Instructor.LastName}" : "غير متاح",
            Price = c.Price,
            DiscountPrice = c.DiscountPrice,
            Level = c.Level.ToString(),
            Duration = c.DurationHours,
            TotalStudents = c.TotalStudents,
            AverageRating = c.AverageRating,
            TotalReviews = c.TotalReviews,
            ModulesCount = c.Modules.Count,
            LessonsCount = c.Modules.Sum(m => m.Lessons.Count),
            Language = c.Language,
            HasCertificate = c.HasCertificate,
            LastUpdated = c.UpdatedAt ?? c.CreatedAt
        }).ToList();

        return View(comparison);
    }

    /// <summary>
    /// مشاركة التقدم - Share progress on social media
    /// </summary>
    public async Task<IActionResult> ShareProgress(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
            .Include(e => e.Student)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

        if (enrollment == null)
            return NotFound();

        var shareData = new ShareProgressViewModel
        {
            CourseName = enrollment.Course?.Title ?? "",
            Progress = enrollment.ProgressPercentage,
            StudentName = enrollment.Student?.FullName ?? "Student",
            Completed = enrollment.Status == EnrollmentStatus.Completed,
            ShareUrl = Url.Action("Details", "Courses", new { area = "Student", id = enrollment.CourseId }, Request.Scheme) ?? "",
            ImageUrl = enrollment.Course?.ThumbnailUrl
        };

        return View(shareData);
    }

    /// <summary>
    /// الدورات المكتملة - Completed courses
    /// Supports both /completed and /completed-courses routes via conventional routing
    /// </summary>
    [HttpGet]
    [Route("[area]/[controller]/completed")]
    [Route("[area]/[controller]/completed-courses")]
    public async Task<IActionResult> CompletedCourses()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var completedEnrollments = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Include(e => e.Course)
                .ThenInclude(c => c.Category)
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Completed)
            .OrderByDescending(e => e.CompletedAt)
            .ToListAsync();

        // Get next step recommendations for each
        var nextSteps = new Dictionary<int, List<CourseRecommendation>>();
        foreach (var enrollment in completedEnrollments.Take(3))
        {
            var recommendations = await _recommendationService.GetNextStepRecommendationsAsync(userId, enrollment.CourseId);
            nextSteps[enrollment.CourseId] = recommendations;
        }

        ViewBag.NextSteps = nextSteps;

        return View(completedEnrollments);
    }

    /// <summary>
    /// الدورات المفضلة - Wishlist
    /// </summary>
    public async Task<IActionResult> Wishlist()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var wishlistItems = await _context.WishlistItems
            .Include(w => w.Course)
                .ThenInclude(c => c.Instructor)
            .Include(w => w.Course)
                .ThenInclude(c => c.Category)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync();

        return View(wishlistItems);
    }

    /// <summary>
    /// إضافة إلى المفضلة - Add to wishlist
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToWishlist(int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        // Check if already in wishlist
        var exists = await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (exists)
            return Json(new { success = false, message = "الدورة موجودة في المفضلة بالفعل" });

        // Check if already enrolled
        var enrolled = await _context.Enrollments
            .AnyAsync(e => e.StudentId == userId && e.CourseId == courseId);

        if (enrolled)
            return Json(new { success = false, message = "أنت مسجل في هذه الدورة بالفعل" });

        var wishlistItem = new Domain.Entities.Learning.WishlistItem
        {
            UserId = userId,
            CourseId = courseId,
            AddedAt = DateTime.UtcNow
        };

        _context.WishlistItems.Add(wishlistItem);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تمت إضافة الدورة إلى المفضلة" });
    }

    /// <summary>
    /// إزالة من المفضلة - Remove from wishlist
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromWishlist(int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var wishlistItem = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (wishlistItem != null)
        {
            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();
        }

        return Json(new { success = true, message = "تمت إزالة الدورة من المفضلة" });
    }

    /// <summary>
    /// توصيات الدورات بناءً على اهتماماتي - Course recommendations
    /// </summary>
    public async Task<IActionResult> Recommendations()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var recommendations = await _recommendationService.GetCourseRecommendationsAsync(userId, 20);
        var trending = await _recommendationService.GetTrendingCoursesAsync(10);
        var learningPaths = await _recommendationService.GetRecommendedLearningPathsAsync(userId, 5);

        var wishlistCourseIds = await _context.WishlistItems
            .Where(w => w.UserId == userId)
            .Select(w => w.CourseId)
            .ToListAsync();

        ViewBag.Trending = trending;
        ViewBag.LearningPaths = learningPaths;
        ViewBag.WishlistCourseIds = wishlistCourseIds;

        return View(recommendations);
    }

    /// <summary>
    /// إحصائيات التعلم الإجمالية - Overall learning statistics
    /// </summary>
    public async Task<IActionResult> MyLearningStats()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
        
        // Get enrollments for timeline
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId)
            .OrderBy(e => e.EnrolledAt)
            .ToListAsync();

        // Get monthly progress
        var monthlyStats = enrollments
            .GroupBy(e => new { e.EnrolledAt.Year, e.EnrolledAt.Month })
            .Select(g => new MonthlyLearningStats
            {
                Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                CoursesEnrolled = g.Count(),
                CoursesCompleted = g.Count(e => e.Status == EnrollmentStatus.Completed),
                TotalStudyMinutes = g.Sum(e => e.TotalWatchTimeMinutes)
            })
            .OrderBy(m => m.Month)
            .ToList();

        ViewBag.MonthlyStats = monthlyStats;
        ViewBag.Enrollments = enrollments;

        return View(stats);
    }
}

#region View Models

public class CourseComparisonItem
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Level { get; set; } = string.Empty;
    public decimal? Duration { get; set; }
    public int TotalStudents { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int ModulesCount { get; set; }
    public int LessonsCount { get; set; }
    public string Language { get; set; } = string.Empty;
    public bool HasCertificate { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? ThumbnailUrl { get; set; }
}

public class MonthlyLearningStats
{
    public string Month { get; set; } = string.Empty;
    public int CoursesEnrolled { get; set; }
    public int CoursesCompleted { get; set; }
    public int TotalStudyMinutes { get; set; }
}

#endregion

