using System.Text.RegularExpressions;
using LMS.Areas.Instructor.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة دورات المدرس - Instructor Courses Controller
/// </summary>
public class CoursesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;
    private readonly ISystemConfigurationService _configService;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService,
        ISystemConfigurationService configService,
        ILogger<CoursesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة دورات المدرس - Instructor's courses list
    /// </summary>
    public async Task<IActionResult> Index(CourseStatus? status, int page = 1, string? search = null, int? category = null)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in CoursesController.Index");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            var now = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfWeek = now.AddDays(-(int)now.DayOfWeek);

            var query = _context.Courses
                .Include(c => c.Category)
                .Where(c => c.InstructorId == userId);

            // Apply filters
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));
            }

            if (category.HasValue)
            {
                query = query.Where(c => c.CategoryId == category.Value);
            }

            var totalCount = await query.CountAsync();
            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * 10)
                .Take(10)
                .ToListAsync();

            // Get course IDs for enrollment counts
            var courseIds = courses.Select(c => c.Id).ToList();
            
            // Calculate statistics - ALL instructor courses (not filtered)
            var allCoursesQuery = _context.Courses.Where(c => c.InstructorId == userId);
            
            ViewBag.TotalCourses = await allCoursesQuery.CountAsync();
            ViewBag.PublishedCourses = await allCoursesQuery.CountAsync(c => c.Status == CourseStatus.Published);
            ViewBag.DraftCourses = await allCoursesQuery.CountAsync(c => c.Status == CourseStatus.Draft);
            
            // Total students across all courses
            ViewBag.TotalStudents = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();
            
            // Dynamic change values - this month/week
            ViewBag.NewCoursesThisMonth = await allCoursesQuery.CountAsync(c => c.CreatedAt >= firstDayOfMonth);
            ViewBag.PublishedThisMonth = await allCoursesQuery.CountAsync(c => c.PublishedAt >= firstDayOfMonth);
            ViewBag.NewStudentsThisWeek = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= firstDayOfWeek)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();

            // Enrollment counts per course
            ViewBag.EnrollmentCounts = await _context.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .GroupBy(e => e.CourseId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // Categories for filter dropdown
            ViewBag.Categories = await _context.Categories
                .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)Constants.Defaults.DefaultPageSize);
            ViewBag.TotalItems = totalCount;
            ViewBag.PageSize = Constants.Defaults.DefaultPageSize;
            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategory = category;

            return View(courses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CoursesController.Index");
            SetWarningMessage("تعذر تحميل قائمة الدورات. يرجى المحاولة مرة أخرى.");
            
            // Return empty view instead of redirecting
            ViewBag.TotalCourses = 0;
            ViewBag.PublishedCourses = 0;
            ViewBag.DraftCourses = 0;
            ViewBag.TotalStudents = 0;
            ViewBag.NewCoursesThisMonth = 0;
            ViewBag.PublishedThisMonth = 0;
            ViewBag.NewStudentsThisWeek = 0;
            ViewBag.EnrollmentCounts = new Dictionary<int, int>();
            ViewBag.Categories = new List<object>();
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.Page = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;
            ViewBag.PageSize = Constants.Defaults.DefaultPageSize;
            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategory = category;
            
            return View(new List<Course>());
        }
    }

    /// <summary>
    /// الدورات المنشورة - Published courses
    /// </summary>
    public async Task<IActionResult> Published(int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in CoursesController.Published");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var query = _context.Courses
                .Include(c => c.Category)
                .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published);

            var totalCount = await query.CountAsync();
            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * Constants.Defaults.DefaultPageSize)
                .Take(Constants.Defaults.DefaultPageSize)
                .ToListAsync();

            // Get statistics for published courses
            var courseIds = courses.Select(c => c.Id).ToList();
            
            // Total students across published courses
            ViewBag.TotalStudents = await _context.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();
            
            // Average rating across published courses
            var allReviews = await _context.Reviews
                .Where(r => courseIds.Contains(r.CourseId) && r.IsApproved)
                .ToListAsync();
            
            ViewBag.AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0.0;
            
            // Total revenue from published courses
            ViewBag.TotalRevenue = await _context.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .SumAsync(e => e.PaidAmount);

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)Constants.Defaults.DefaultPageSize);
            ViewBag.TotalCount = totalCount;

            _logger.LogInformation("Instructor {InstructorId} viewed published courses. Count: {Count}", userId, totalCount);

            return View(courses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CoursesController.Published");
            SetWarningMessage("تعذر تحميل الدورات المنشورة. يرجى المحاولة مرة أخرى.");
            
            // Return empty view instead of redirecting
            ViewBag.TotalStudents = 0;
            ViewBag.TotalRevenue = 0m;
            ViewBag.AverageRating = 0.0;
            ViewBag.TotalCount = 0;
            ViewBag.EnrollmentCounts = new Dictionary<int, int>();
            ViewBag.RevenueByCourse = new Dictionary<int, decimal>();
            ViewBag.AverageRatingByCourse = new Dictionary<int, double>();
            ViewBag.Page = page;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;
            ViewBag.PageSize = Constants.Defaults.DefaultPageSize;
            
            return View(new List<Course>());
        }
    }

    /// <summary>
    /// الدورات المسودة - Draft courses
    /// </summary>
    public async Task<IActionResult> Draft(int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in CoursesController.Draft");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var query = _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .Include(c => c.Requirements)
                .Include(c => c.WhatYouWillLearn)
                .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Draft);

            var totalCount = await query.CountAsync();
            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * Constants.Defaults.DefaultPageSize)
                .Take(Constants.Defaults.DefaultPageSize)
                .ToListAsync();

            // Calculate additional stats
            var readyToPublish = courses.Count(c => 
                c.Modules.Count >= 1 && 
                c.Modules.Sum(m => m.Lessons.Count) >= 3 &&
                !string.IsNullOrEmpty(c.ThumbnailUrl));
            
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)Constants.Defaults.DefaultPageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.ReadyToPublish = readyToPublish;
            ViewBag.NeedsWork = totalCount - readyToPublish;

            _logger.LogInformation("Instructor {InstructorId} viewed draft courses. Count: {Count}", userId, totalCount);

            return View(courses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CoursesController.Draft");
            SetWarningMessage("تعذر تحميل الدورات المسودة. يرجى المحاولة مرة أخرى.");
            
            // Return empty view instead of redirecting
            ViewBag.Page = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalCount = 0;
            ViewBag.ReadyToPublish = 0;
            ViewBag.NeedsWork = 0;
            
            return View(new List<Course>());
        }
    }

    /// <summary>
    /// إنشاء دورة جديدة - Create new course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCategoriesAsync();
        return View(new CourseCreateViewModel());
    }

    /// <summary>
    /// حفظ الدورة الجديدة - Save new course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            return View(model);
        }

        var userId = _currentUserService.UserId;

        try
        {
            // Validate instructor profile exists (approval required only for publishing, not for creating drafts)
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(ip => ip.UserId == userId);

            if (instructorProfile == null)
            {
                _logger.LogWarning("Instructor profile not found for user {UserId}", userId);
                SetErrorMessage("لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً");
                return RedirectToAction("Index", "Profile");
            }

            if (instructorProfile.Status != "Approved")
            {
                _logger.LogWarning("Instructor {UserId} creating draft while status is {Status}. Publish will require approval.", userId, instructorProfile.Status);
                SetWarningMessage("حسابك قيد الاعتماد. يمكنك إنشاء المسودة، لكن لن تتمكن من نشر الدورة حتى اعتماد حسابك.");
            }

            // Validate pricing
            if (!model.IsFree)
            {
                if (model.Price < BusinessRuleHelper.MinimumCoursePrice || 
                    model.Price > BusinessRuleHelper.MaximumCoursePrice)
                {
                    ModelState.AddModelError(nameof(model.Price), 
                        $"السعر يجب أن يكون بين {BusinessRuleHelper.MinimumCoursePrice} و {BusinessRuleHelper.MaximumCoursePrice}");
                    await PopulateCategoriesAsync();
                    return View(model);
                }

                if (model.DiscountPrice.HasValue && model.DiscountPrice.Value >= model.Price)
                {
                    ModelState.AddModelError(nameof(model.DiscountPrice), 
                        "سعر الخصم يجب أن يكون أقل من السعر الأصلي");
                    await PopulateCategoriesAsync();
                    return View(model);
                }
            }

            // Verify category exists
            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == model.CategoryId && !c.IsDeleted);

            if (!categoryExists)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "التصنيف المحدد غير موجود");
                await PopulateCategoriesAsync();
                return View(model);
            }

            // Generate unique slug with bulletproof approach
            var baseSlug = _slugService.GenerateSlug(model.Title);
            var slug = baseSlug;
            var slugAttempt = 0;
            const int maxSlugAttempts = 10;
            
            // Keep trying until we find a unique slug
            while (await _context.Courses.AnyAsync(c => c.Slug == slug))
            {
                slugAttempt++;
                if (slugAttempt >= maxSlugAttempts)
                {
                    // Fallback: use timestamp + GUID for guaranteed uniqueness
                    slug = $"{baseSlug}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
                    break;
                }
                slug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
                _logger.LogDebug("Slug collision detected, trying: {Slug}", slug);
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            var createdCourseId = await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var course = new Course
                    {
                        Title = model.Title,
                        ShortDescription = model.ShortDescription,
                        Description = model.Description,
                        CategoryId = model.CategoryId,
                        SubCategoryId = model.SubCategoryId,
                        Level = model.Level,
                        Language = model.Language,
                        Price = model.Price,
                        DiscountPrice = model.DiscountPrice,
                        IsFree = model.IsFree,
                        ThumbnailUrl = model.ThumbnailUrl,
                        PreviewVideoUrl = model.PreviewVideoUrl,
                        PreviewVideoProvider = model.PreviewVideoProvider,
                        InstructorId = userId!,
                        Slug = slug,
                        Status = CourseStatus.Draft,
                        InstructorCommissionRate = instructorProfile.CommissionRate
                    };

                    _context.Courses.Add(course);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Course {CourseId} created by instructor {InstructorId}", 
                        course.Id, userId);

                    return course.Id;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم إنشاء الدورة بنجاح. يمكنك الآن إضافة المحتوى");
            return RedirectToAction(nameof(Edit), new { id = createdCourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating course for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الدورة. يرجى المحاولة مرة أخرى");
            await PopulateCategoriesAsync();
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الدورة - Edit course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        var course = await _context.Courses
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        var viewModel = new CourseEditViewModel
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = course.ShortDescription,
            Description = course.Description,
            CategoryId = course.CategoryId,
            SubCategoryId = course.SubCategoryId,
            Level = course.Level,
            Language = course.Language,
            Price = course.Price,
            DiscountPrice = course.DiscountPrice,
            IsFree = course.IsFree,
            ThumbnailUrl = course.ThumbnailUrl,
            PreviewVideoUrl = course.PreviewVideoUrl,
            PreviewVideoProvider = course.PreviewVideoProvider,
            HasCertificate = course.HasCertificate,
            AllowDiscussions = course.AllowDiscussions,
            AllowReviews = course.AllowReviews,
            MetaTitle = course.MetaTitle,
            MetaDescription = course.MetaDescription,
            MetaKeywords = course.MetaKeywords
        };

        ViewBag.Modules = course.Modules.OrderBy(m => m.OrderIndex).ToList();
        await PopulateCategoriesAsync();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الدورة - Save course edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            course.Title = model.Title;
            course.ShortDescription = model.ShortDescription;
            course.Description = model.Description;
            course.CategoryId = model.CategoryId;
            course.SubCategoryId = model.SubCategoryId;
            course.Level = model.Level;
            course.Language = model.Language;
            course.Price = model.Price;
            course.DiscountPrice = model.DiscountPrice;
            course.IsFree = model.IsFree;
            course.ThumbnailUrl = model.ThumbnailUrl;
            course.PreviewVideoUrl = model.PreviewVideoUrl;
            course.PreviewVideoProvider = model.PreviewVideoProvider;
            course.HasCertificate = model.HasCertificate;
            course.AllowDiscussions = model.AllowDiscussions;
            course.AllowReviews = model.AllowReviews;
            course.MetaTitle = model.MetaTitle;
            course.MetaDescription = model.MetaDescription;
            course.MetaKeywords = model.MetaKeywords;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الدورة بنجاح");
            return RedirectToAction(nameof(Edit), new { id });
        }

        await PopulateCategoriesAsync();
        return View(model);
    }

    /// <summary>
    /// تفاصيل الدورة - Course details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in CoursesController.Details");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            var now = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
            
            var course = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .Include(c => c.Enrollments)
                .Include(c => c.Reviews)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Null-safe collections (prevent 500 when navigation not loaded or optional)
            var enrollments = (course.Enrollments ?? Enumerable.Empty<Domain.Entities.Learning.Enrollment>()).ToList();
            var reviews = (course.Reviews ?? Enumerable.Empty<Domain.Entities.Social.Review>()).ToList();
            var modules = (course.Modules ?? Enumerable.Empty<Domain.Entities.Courses.Module>()).ToList();
            
            // Calculate dynamic statistics
            ViewBag.EnrollmentCount = enrollments.Count;
            
            // New enrollments this month
            var enrollmentsThisMonth = enrollments.Count(e => e.EnrolledAt >= firstDayOfMonth);
            ViewBag.EnrollmentsThisMonth = enrollmentsThisMonth;
            
            // Completion rate
            var completedEnrollments = enrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed);
            ViewBag.CompletionRate = enrollments.Any() 
                ? (completedEnrollments * 100.0 / enrollments.Count).ToString("F1") 
                : "0";
            
            // Total revenue
            var totalRevenue = enrollments.Sum(e => e.PaidAmount);
            ViewBag.TotalRevenue = totalRevenue;
            
            // Revenue growth calculation
            var revenueThisMonth = enrollments
                .Where(e => e.EnrolledAt >= firstDayOfMonth)
                .Sum(e => e.PaidAmount);
            var revenueLastMonth = enrollments
                .Where(e => e.EnrolledAt >= firstDayOfLastMonth && e.EnrolledAt < firstDayOfMonth)
                .Sum(e => e.PaidAmount);
            
            var revenueGrowth = revenueLastMonth > 0 
                ? ((revenueThisMonth - revenueLastMonth) / revenueLastMonth) * 100 
                : (revenueThisMonth > 0 ? 100 : 0);
            ViewBag.RevenueGrowth = revenueGrowth;
            
            // Recent students for tab
            ViewBag.RecentStudents = enrollments
                .OrderByDescending(e => e.EnrolledAt)
                .Take(Constants.DisplayLimits.TopCoursesOnAnalytics)
                .ToList();
            
            // Recent reviews for tab
            ViewBag.RecentReviews = reviews
                .Where(r => r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Take(Constants.DisplayLimits.TopCoursesOnAnalytics)
                .ToList();
            
            // Modules for content tab
            ViewBag.Modules = modules.OrderBy(m => m.OrderIndex).ToList();
            
            // Chart data - enrollments per month (last 6 months)
            var chartData = new List<int>();
            var chartLabels = new List<string>();
            
            try
            {
                var arabicMonths = await _configService.GetMonthNamesAsync("ar");
                
                for (int i = Constants.DisplayLimits.MonthlyChartDataPoints - 1; i >= 0; i--)
                {
                    var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                    var monthEnd = monthStart.AddMonths(1);
                    var monthEnrollments = enrollments.Count(e => e.EnrolledAt >= monthStart && e.EnrolledAt < monthEnd);
                    chartData.Add(monthEnrollments);
                    var monthName = arabicMonths.TryGetValue(monthStart.Month, out var name) ? name : monthStart.ToString("MMMM");
                    chartLabels.Add(monthName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading month names for chart, using default values");
                
                // Fallback: Use default month labels
                var defaultMonths = new Dictionary<int, string>
                {
                    { 1, "يناير" }, { 2, "فبراير" }, { 3, "مارس" }, { 4, "أبريل" },
                    { 5, "مايو" }, { 6, "يونيو" }, { 7, "يوليو" }, { 8, "أغسطس" },
                    { 9, "سبتمبر" }, { 10, "أكتوبر" }, { 11, "نوفمبر" }, { 12, "ديسمبر" }
                };
                
                for (int i = Constants.DisplayLimits.MonthlyChartDataPoints - 1; i >= 0; i--)
                {
                    var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                    var monthEnd = monthStart.AddMonths(1);
                    var monthEnrollments = enrollments.Count(e => e.EnrolledAt >= monthStart && e.EnrolledAt < monthEnd);
                    chartData.Add(monthEnrollments);
                    var monthName = defaultMonths.TryGetValue(monthStart.Month, out var name) ? name : monthStart.ToString("MMMM");
                    chartLabels.Add(monthName);
                }
            }
            
            ViewBag.EnrollmentData = System.Text.Json.JsonSerializer.Serialize(chartData);
            ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(chartLabels);

            _logger.LogInformation("Instructor {InstructorId} viewed course details for {CourseId}", userId, id);

            return View(course);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CoursesController.Details for course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الدورة");
            
            // Return NotFound for details page if error occurs
            return NotFound();
        }
    }

    /// <summary>
    /// إرسال للمراجعة - Submit for review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        var userId = _currentUserService.UserId;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);
        if (instructorProfile == null || instructorProfile.Status != "Approved")
        {
            _logger.LogWarning("SubmitForReview: Instructor {InstructorId} not approved. Status: {Status}",
                userId, instructorProfile?.Status ?? "NoProfile");
            SetErrorMessage(instructorProfile == null
                ? "لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً."
                : $"يجب أن يكون حسابك معتمداً لإرسال الدورات للمراجعة. الحالة الحالية: {instructorProfile.Status}");
            return RedirectToAction("Index", "Profile");
        }

        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Check if already submitted or published
            if (course.Status == CourseStatus.PendingReview)
            {
                SetWarningMessage("الدورة قيد المراجعة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (course.Status == CourseStatus.Published)
            {
                SetWarningMessage("الدورة منشورة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Validate course completeness using business rules
            var validationErrors = new List<string>();

            // Count modules and lessons
            var moduleCount = course.Modules.Count;
            var lessonCount = course.Modules.SelectMany(m => m.Lessons).Count();
            var hasThumbnail = !string.IsNullOrEmpty(course.ThumbnailUrl);
            var hasPrice = course.Price > 0;

            // Use BusinessRuleHelper for core validation
            var (canPublish, publishReason) = BusinessRuleHelper.CanPublishCourse(
                moduleCount,
                lessonCount,
                hasThumbnail,
                hasPrice,
                course.IsFree);

            if (!canPublish)
            {
                validationErrors.Add(publishReason!);
            }

            // Additional validation rules
            if (lessonCount < BusinessRuleHelper.MinimumLessonsPerCourse)
            {
                validationErrors.Add($"يجب أن تحتوي الدورة على {BusinessRuleHelper.MinimumLessonsPerCourse} دروس على الأقل");
            }

            if (string.IsNullOrEmpty(course.Description) || course.Description.Length < BusinessRuleHelper.MinimumCourseDescriptionLength)
            {
                validationErrors.Add($"يجب إضافة وصف تفصيلي للدورة ({BusinessRuleHelper.MinimumCourseDescriptionLength} حرف على الأقل)");
            }

            if (string.IsNullOrEmpty(course.ShortDescription) || course.ShortDescription.Length < BusinessRuleHelper.MinimumShortDescriptionLength)
            {
                validationErrors.Add($"يجب إضافة وصف مختصر للدورة ({BusinessRuleHelper.MinimumShortDescriptionLength} حرف على الأقل)");
            }

            // Check learning outcomes (filter out empty values)
            var learningOutcomes = await _context.Set<CourseLearningOutcome>()
                .CountAsync(lo => lo.CourseId == id && !string.IsNullOrWhiteSpace(lo.Text));
            
            if (learningOutcomes == 0)
            {
                validationErrors.Add($"يجب إضافة مخرجات التعلم (ما سيتعلمه الطالب) - على الأقل {BusinessRuleHelper.MinimumLearningOutcomesCount} نقاط");
            }
            else if (learningOutcomes < BusinessRuleHelper.MinimumLearningOutcomesCount)
            {
                validationErrors.Add($"يجب إضافة {BusinessRuleHelper.MinimumLearningOutcomesCount} مخرجات تعلم على الأقل");
            }

            // Check requirements (filter out empty values)
            var requirements = await _context.Set<CourseRequirement>()
                .CountAsync(r => r.CourseId == id && !string.IsNullOrWhiteSpace(r.Text));

            if (requirements < BusinessRuleHelper.MinimumCourseRequirementsCount)
            {
                validationErrors.Add($"يجب إضافة متطلبات الدورة - على الأقل {BusinessRuleHelper.MinimumCourseRequirementsCount} متطلب");
            }

            // Check that all modules have at least one lesson
            var modulesWithoutLessons = course.Modules.Where(m => !m.Lessons.Any()).ToList();
            if (modulesWithoutLessons.Any())
            {
                validationErrors.Add($"توجد {modulesWithoutLessons.Count} وحدة بدون دروس");
            }

            // Check preview video
            if (string.IsNullOrEmpty(course.PreviewVideoUrl))
            {
                validationErrors.Add("يُفضل إضافة فيديو تعريفي للدورة (غير إلزامي لكن مهم للقبول)");
            }

            // Validate pricing if not free
            if (!course.IsFree)
            {
                if (course.Price < BusinessRuleHelper.MinimumCoursePrice)
                {
                    validationErrors.Add($"السعر يجب أن يكون {BusinessRuleHelper.MinimumCoursePrice} على الأقل");
                }
                if (course.Price > BusinessRuleHelper.MaximumCoursePrice)
                {
                    validationErrors.Add($"السعر يجب ألا يتجاوز {BusinessRuleHelper.MaximumCoursePrice}");
                }
            }

            // If there are validation errors, return with messages
            if (validationErrors.Any())
            {
                _logger.LogWarning("Course {CourseId} validation failed: {Errors}", 
                    id, string.Join(", ", validationErrors));

                SetErrorMessage("لا يمكن إرسال الدورة للمراجعة للأسباب التالية:\n" + 
                    string.Join("\n", validationErrors.Select(e => "• " + e)));
                return RedirectToAction(nameof(Details), new { id });
            }

            // All validations passed, submit for review
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    course.Status = CourseStatus.PendingReview;
                    course.SubmittedForReviewAt = DateTime.UtcNow;
                    course.LastModifiedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Course {CourseId} '{CourseTitle}' submitted for review by instructor {InstructorId}. Modules: {ModuleCount}, Lessons: {LessonCount}", 
                        id, course.Title, userId, moduleCount, lessonCount);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم إرسال الدورة للمراجعة بنجاح. سيتم مراجعتها من قبل الإدارة قريباً.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting course {CourseId} for review", id);
            SetErrorMessage("حدث خطأ أثناء إرسال الدورة للمراجعة. يرجى المحاولة مرة أخرى");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حذف الدورة - Delete course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Reviews)
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Use business rule helper for validation
            var enrollmentCount = course.Enrollments.Count;
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteCourse(enrollmentCount, course.Status);

            if (!canDelete)
            {
                _logger.LogWarning("Cannot delete course {CourseId}: {Reason}", id, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Index));
            }

            // Additional check: prevent deletion if course has reviews
            if (course.Reviews.Any())
            {
                _logger.LogWarning("Cannot delete course {CourseId}: has reviews", id);
                SetErrorMessage("لا يمكن حذف الدورة لأنها تحتوي على تقييمات");
                return RedirectToAction(nameof(Index));
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Delete all related content first
                    var modules = course.Modules.ToList();
                    foreach (var module in modules)
                    {
                        var lessons = await _context.Lessons
                            .Where(l => l.ModuleId == module.Id)
                            .ToListAsync();
                        
                        _context.Lessons.RemoveRange(lessons);
                    }
                    _context.Modules.RemoveRange(modules);

                    // Delete learning outcomes, requirements, etc.
                    var learningOutcomes = await _context.Set<CourseLearningOutcome>()
                        .Where(lo => lo.CourseId == id)
                        .ToListAsync();
                    _context.RemoveRange(learningOutcomes);

                    var requirements = await _context.Set<CourseRequirement>()
                        .Where(r => r.CourseId == id)
                        .ToListAsync();
                    _context.RemoveRange(requirements);

                    // Finally, delete the course
                    _context.Courses.Remove(course);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Course {CourseId} deleted by instructor {InstructorId}", id, userId);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم حذف الدورة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الدورة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// نشر الدورة - Publish course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var userId = _currentUserService.UserId;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);
        if (instructorProfile == null || instructorProfile.Status != "Approved")
        {
            _logger.LogWarning("Publish: Instructor {InstructorId} not approved. Status: {Status}",
                userId, instructorProfile?.Status ?? "NoProfile");
            SetErrorMessage(instructorProfile == null
                ? "لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً."
                : $"يجب أن يكون حسابك معتمداً لنشر الدورات. الحالة الحالية: {instructorProfile.Status}");
            return RedirectToAction("Index", "Profile");
        }

        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Check if already published
            if (course.Status == CourseStatus.Published)
            {
                SetWarningMessage("الدورة منشورة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Validate course is ready for publishing
            var moduleCount = course.Modules.Count;
            var lessonCount = course.Modules.SelectMany(m => m.Lessons).Count();
            var hasThumbnail = !string.IsNullOrEmpty(course.ThumbnailUrl);
            var hasPrice = course.Price > 0;

            var (canPublish, publishReason) = BusinessRuleHelper.CanPublishCourse(
                moduleCount,
                lessonCount,
                hasThumbnail,
                hasPrice,
                course.IsFree);

            if (!canPublish)
            {
                _logger.LogWarning("Cannot publish course {CourseId}: {Reason}", id, publishReason);
                SetErrorMessage(publishReason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    course.Status = CourseStatus.Published;
                    course.PublishedAt = DateTime.UtcNow;
                    course.LastModifiedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Course {CourseId} published by instructor {InstructorId}", id, userId);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم نشر الدورة بنجاح! أصبحت متاحة للطلاب الآن");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء نشر الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إلغاء نشر الدورة - Unpublish course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            if (course.Status != CourseStatus.Published)
            {
                SetWarningMessage("الدورة غير منشورة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check for active enrollments
            var activeEnrollments = course.Enrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Active);
            if (activeEnrollments > 0)
            {
                SetWarningMessage($"تنبيه: يوجد {activeEnrollments} طالب نشط في هذه الدورة. سيظل بإمكانهم الوصول لمحتواها");
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    course.Status = CourseStatus.Draft;
                    course.LastModifiedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Course {CourseId} unpublished by instructor {InstructorId}", id, userId);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم إلغاء نشر الدورة. لن تظهر للطلاب الجدد");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpublishing course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء إلغاء نشر الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// نسخ الدورة - Duplicate course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Resources)
                .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            var duplicatedCourseId = await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Generate unique slug with bulletproof approach
                    var duplicateSuffix = await _configService.GetLocalizationAsync("Common_DuplicateSuffix", "ar", " - نسخة");
                    var baseSlug = _slugService.GenerateSlug(course.Title + duplicateSuffix);
                    var newSlug = baseSlug;
                    var slugAttempt = 0;
                    const int maxSlugAttempts = 10;
                    
                    // Keep trying until we find a unique slug
                    while (await _context.Courses.AnyAsync(c => c.Slug == newSlug))
                    {
                        slugAttempt++;
                        if (slugAttempt >= maxSlugAttempts)
                        {
                            // Fallback: use timestamp + GUID for guaranteed uniqueness
                            newSlug = $"{baseSlug}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
                            break;
                        }
                        newSlug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
                        _logger.LogDebug("Slug collision detected for duplicate, trying: {Slug}", newSlug);
                    }

                    // Create new course
                    var newCourse = new Course
                    {
                        Title = course.Title + duplicateSuffix,
                        ShortDescription = course.ShortDescription,
                        Description = course.Description,
                        CategoryId = course.CategoryId,
                        SubCategoryId = course.SubCategoryId,
                        Level = course.Level,
                        Language = course.Language,
                        Price = course.Price,
                        DiscountPrice = course.DiscountPrice,
                        IsFree = course.IsFree,
                        ThumbnailUrl = course.ThumbnailUrl,
                        PreviewVideoUrl = course.PreviewVideoUrl,
                        PreviewVideoProvider = course.PreviewVideoProvider,
                        InstructorId = userId!,
                        Slug = newSlug,
                        Status = CourseStatus.Draft,
                        HasCertificate = course.HasCertificate,
                        AllowDiscussions = course.AllowDiscussions,
                        AllowReviews = course.AllowReviews,
                        MetaTitle = course.MetaTitle,
                        MetaDescription = course.MetaDescription,
                        MetaKeywords = course.MetaKeywords
                    };

                    _context.Courses.Add(newCourse);
                    await _context.SaveChangesAsync();

                    // Copy modules and lessons
                    foreach (var module in course.Modules.OrderBy(m => m.OrderIndex))
                    {
                        var newModule = new Module
                        {
                            CourseId = newCourse.Id,
                            Title = module.Title,
                            Description = module.Description,
                            OrderIndex = module.OrderIndex,
                            IsPublished = false
                        };

                        _context.Modules.Add(newModule);
                        await _context.SaveChangesAsync();

                        foreach (var lesson in module.Lessons.OrderBy(l => l.OrderIndex))
                        {
                            var newLesson = new Domain.Entities.Courses.Lesson
                            {
                                ModuleId = newModule.Id,
                                Title = lesson.Title,
                                Description = lesson.Description,
                                Type = lesson.Type,
                                VideoUrl = lesson.VideoUrl,
                                VideoProvider = lesson.VideoProvider,
                                VideoId = lesson.VideoId,
                                HtmlContent = lesson.HtmlContent,
                                FileUrl = lesson.FileUrl,
                                DurationSeconds = lesson.DurationSeconds,
                                IsPreviewable = lesson.IsPreviewable,
                                IsDownloadable = lesson.IsDownloadable,
                                MustComplete = lesson.MustComplete,
                                OrderIndex = lesson.OrderIndex
                            };

                            _context.Lessons.Add(newLesson);
                            await _context.SaveChangesAsync();

                            // Copy lesson resources
                            foreach (var resource in lesson.Resources)
                            {
                                var newResource = new Domain.Entities.Courses.LessonResource
                                {
                                    LessonId = newLesson.Id,
                                    Title = resource.Title,
                                    Description = resource.Description,
                                    FileUrl = resource.FileUrl,
                                    FileType = resource.FileType,
                                    FileSize = resource.FileSize,
                                    IsDownloadable = resource.IsDownloadable
                                };

                                _context.LessonResources.Add(newResource);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Course {CourseId} duplicated to {NewCourseId} by instructor {InstructorId}", 
                        id, newCourse.Id, userId);

                    return newCourse.Id;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            SetSuccessMessage("تم نسخ الدورة بنجاح. يمكنك الآن تعديل النسخة الجديدة");
            return RedirectToAction(nameof(Edit), new { id = duplicatedCourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء نسخ الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    #region Course Creation Wizard - معالج إنشاء الدورة

    /// <summary>
    /// معالج إنشاء الدورة - Course Creation Wizard (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CreateWizard(int step = 1, int? id = null)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Non-blocking warning when account is not yet approved (publish will be blocked)
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(ip => ip.UserId == userId);
            if (instructorProfile != null && instructorProfile.Status != "Approved")
            {
                SetWarningMessage("حسابك قيد الاعتماد. يمكنك إكمال إنشاء الدورة، لكن لن تتمكن من نشرها حتى اعتماد حسابك.");
            }

            CourseWizardViewModel model;

            if (id.HasValue && id.Value > 0)
            {
                // Load existing course for editing
                var course = await LoadCourseForWizard(id.Value, userId);
                if (course == null)
                {
                    SetErrorMessage("لم يتم العثور على الدورة");
                    return RedirectToAction(nameof(Index));
                }
                model = MapCourseToWizardViewModel(course);
                
                // Smart step detection: if step is 1 (default), determine best starting step
                if (step == 1)
                {
                    step = DetermineBestStartingStep(model);
                }
            }
            else
            {
                // New course
                model = new CourseWizardViewModel { CurrentStep = step };
            }

            model.CurrentStep = Math.Clamp(step, 1, 7);
            model.CompletionPercentage = model.CalculateCompletionPercentage();
            model.ReadinessChecklist = GetReadinessChecklist(model);

            await PopulateWizardDropdowns(model.CategoryId);
            
            _logger.LogInformation("Instructor {InstructorId} accessed wizard step {Step} for course {CourseId}",
                userId, step, id ?? 0);

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading wizard for course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل المعالج");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ خطوة المعالج - Save Wizard Step (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWizardStep(CourseWizardViewModel model, int step, string action = "next")
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            _logger.LogInformation("Starting wizard save. Step: {Step}, CourseId: {CourseId}, InstructorId: {InstructorId}, Action: {Action}",
                step, model.Id, userId, action);

            // Validate instructor profile
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(ip => ip.UserId == userId);

            if (instructorProfile == null)
            {
                _logger.LogError("Instructor profile not found for userId: {UserId}", userId);
                SetErrorMessage("لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً.");
                return RedirectToAction("Index", "Profile");
            }

            if (instructorProfile.Status != "Approved")
            {
                _logger.LogWarning("Instructor {InstructorId} is building course while status is {Status}. Publish will be blocked until approved.",
                    userId, instructorProfile.Status);
                SetWarningMessage("حسابك قيد الاعتماد. يمكنك إكمال إنشاء الدورة، لكن لن تتمكن من نشرها حتى اعتماد حسابك.");
            }

            // Validate and fix commission rate if needed
            if (instructorProfile.CommissionRate <= 0)
            {
                _logger.LogWarning("Instructor {InstructorId} has invalid CommissionRate: {Rate}. Setting to default 70%",
                    userId, instructorProfile.CommissionRate);
                instructorProfile.CommissionRate = 70.00m;
                await _context.SaveChangesAsync();
            }

            // Additional validation for Step 1
            if (step == 1 && action != "draft")
            {
                var step1Errors = new List<string>();

                if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length < 10)
                    step1Errors.Add("عنوان الدورة يجب أن يكون 10 أحرف على الأقل");

                if (string.IsNullOrWhiteSpace(model.ShortDescription) || model.ShortDescription.Length < 20)
                    step1Errors.Add("الوصف المختصر يجب أن يكون 20 حرف على الأقل");

                if (string.IsNullOrWhiteSpace(model.Description) || model.Description.Length < 100)
                    step1Errors.Add("الوصف التفصيلي يجب أن يكون 100 حرف على الأقل");

                if (model.CategoryId <= 0)
                    step1Errors.Add("يجب اختيار التصنيف");

                if (model.CategoryId > 0)
                {
                    var categoryExists = await _context.Categories
                        .AnyAsync(c => c.Id == model.CategoryId && !c.IsDeleted);
                    if (!categoryExists)
                    {
                        step1Errors.Add("التصنيف المحدد غير موجود في النظام");
                        _logger.LogError("Selected category {CategoryId} does not exist for instructor {InstructorId}", 
                            model.CategoryId, userId);
                    }
                }

                if (step1Errors.Any())
                {
                    var errorMessage = "يرجى إصلاح الأخطاء التالية:\n• " + string.Join("\n• ", step1Errors);
                    SetErrorMessage(errorMessage);
                    _logger.LogWarning("Step 1 validation failed for instructor {InstructorId}: {Errors}", 
                        userId, string.Join(", ", step1Errors));
                    await PopulateWizardDropdowns(model.CategoryId);
                    return View("CreateWizard", model);
                }
            }

            // Reload data from database before validation for steps that need it
            // This is critical because data may be saved in previous steps and not included in form POST
            if (model.Id.HasValue && model.Id.Value > 0)
            {
                var courseWithContent = await LoadCourseForWizard(model.Id.Value, userId);
                if (courseWithContent != null)
                {
                    // Reload data from database before validation
                    // Strategy: For CURRENT step, prefer form POST data. For OTHER steps (step 7), always reload from DB.
                    
                    // Learning outcomes and requirements
                    if (step == 7)
                    {
                        // Step 7 validates all steps - always reload from database
                        _logger.LogInformation("Reloading learning outcomes and requirements for step 7 validation. Course ID: {CourseId}", 
                            model.Id.Value);

                        model.LearningOutcomes = courseWithContent.LearningOutcomes?
                            .OrderBy(o => o.OrderIndex)
                            .Select(o => o.Text)
                            .Where(o => !string.IsNullOrWhiteSpace(o))
                            .ToList() ?? new List<string>();

                        model.Requirements = courseWithContent.Requirements?
                            .OrderBy(r => r.OrderIndex)
                            .Select(r => r.Text)
                            .Where(r => !string.IsNullOrWhiteSpace(r))
                            .ToList() ?? new List<string>();

                        _logger.LogInformation("Learning outcomes and requirements reloaded. Outcomes: {OutcomeCount}, Requirements: {RequirementCount}", 
                            model.LearningOutcomes.Count, model.Requirements.Count);
                    }
                    else if (step == 2)
                    {
                        // Step 2: Only reload from DB if form POST data is missing or invalid
                        // Check if form POST has valid data
                        var formHasValidOutcomes = model.LearningOutcomes != null && 
                                                   model.LearningOutcomes.Any(o => !string.IsNullOrWhiteSpace(o));
                        var formHasValidRequirements = model.Requirements != null && 
                                                       model.Requirements.Any(r => !string.IsNullOrWhiteSpace(r));

                        _logger.LogInformation("Step 2 validation. Form has outcomes: {HasOutcomes} ({Count}), Form has requirements: {HasRequirements} ({Count})", 
                            formHasValidOutcomes, model.LearningOutcomes?.Count(o => !string.IsNullOrWhiteSpace(o)) ?? 0,
                            formHasValidRequirements, model.Requirements?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0);

                        // Only reload from database if form POST doesn't have valid data
                        if (!formHasValidOutcomes)
                        {
                            model.LearningOutcomes = courseWithContent.LearningOutcomes?
                                .OrderBy(o => o.OrderIndex)
                                .Select(o => o.Text)
                                .Where(o => !string.IsNullOrWhiteSpace(o))
                                .ToList() ?? new List<string>();
                            _logger.LogInformation("Reloaded learning outcomes from database: {Count}", model.LearningOutcomes.Count);
                        }

                        if (!formHasValidRequirements)
                        {
                            model.Requirements = courseWithContent.Requirements?
                                .OrderBy(r => r.OrderIndex)
                                .Select(r => r.Text)
                                .Where(r => !string.IsNullOrWhiteSpace(r))
                                .ToList() ?? new List<string>();
                            _logger.LogInformation("Reloaded requirements from database: {Count}", model.Requirements.Count);
                        }
                    }

                    // Reload modules/lessons
                    if (step == 7)
                    {
                        // Step 7 validates all steps - always reload from database
                        _logger.LogInformation("Reloading modules/lessons for step 7 validation. Course ID: {CourseId}", 
                            model.Id.Value);

                        // Populate modules from database to ensure validation uses actual data
                        model.Modules = courseWithContent.Modules?
                            .OrderBy(m => m.OrderIndex)
                            .Select(m => new ModuleWizardViewModel
                            {
                                Id = m.Id,
                                CourseId = m.CourseId,
                                Title = m.Title,
                                Description = m.Description,
                                OrderIndex = m.OrderIndex,
                                IsPublished = m.IsPublished,
                                Lessons = m.Lessons?
                                    .OrderBy(l => l.OrderIndex)
                                    .Select(l => new LessonWizardViewModel
                                    {
                                        Id = l.Id,
                                        ModuleId = l.ModuleId,
                                        Title = l.Title,
                                        Description = l.Description,
                                        Type = l.Type,
                                        VideoUrl = l.VideoUrl,
                                        VideoProvider = l.VideoProvider,
                                        DurationSeconds = l.DurationSeconds,
                                        OrderIndex = l.OrderIndex,
                                        IsPreviewable = l.IsPreviewable,
                                        IsDownloadable = l.IsDownloadable,
                                        MustComplete = l.MustComplete,
                                        AvailableAfterDays = l.AvailableAfterDays,
                                        AvailableFrom = l.AvailableFrom
                                    }).ToList() ?? new List<LessonWizardViewModel>()
                            }).ToList() ?? new List<ModuleWizardViewModel>();

                        _logger.LogInformation("Modules/lessons reloaded. Modules: {ModuleCount}, Lessons: {LessonCount}", 
                            model.TotalModulesCount, model.TotalLessonsCount);
                    }
                    else if (step == 3)
                    {
                        // Step 3: Modules/lessons are added via AJAX, so always reload from database
                        // (They're not included in form POST)
                        _logger.LogInformation("Reloading modules/lessons for step 3 validation. Course ID: {CourseId}, Modules count before reload: {Count}", 
                            model.Id.Value, model.Modules?.Count ?? 0);

                        // Populate modules from database to ensure validation uses actual data
                        model.Modules = courseWithContent.Modules?
                            .OrderBy(m => m.OrderIndex)
                            .Select(m => new ModuleWizardViewModel
                            {
                                Id = m.Id,
                                CourseId = m.CourseId,
                                Title = m.Title,
                                Description = m.Description,
                                OrderIndex = m.OrderIndex,
                                IsPublished = m.IsPublished,
                                Lessons = m.Lessons?
                                    .OrderBy(l => l.OrderIndex)
                                    .Select(l => new LessonWizardViewModel
                                    {
                                        Id = l.Id,
                                        ModuleId = l.ModuleId,
                                        Title = l.Title,
                                        Description = l.Description,
                                        Type = l.Type,
                                        VideoUrl = l.VideoUrl,
                                        VideoProvider = l.VideoProvider,
                                        DurationSeconds = l.DurationSeconds,
                                        OrderIndex = l.OrderIndex,
                                        IsPreviewable = l.IsPreviewable,
                                        IsDownloadable = l.IsDownloadable,
                                        MustComplete = l.MustComplete,
                                        AvailableAfterDays = l.AvailableAfterDays,
                                        AvailableFrom = l.AvailableFrom
                                    }).ToList() ?? new List<LessonWizardViewModel>()
                            }).ToList() ?? new List<ModuleWizardViewModel>();

                        _logger.LogInformation("Modules/lessons reloaded. Modules: {ModuleCount}, Lessons: {LessonCount}", 
                            model.TotalModulesCount, model.TotalLessonsCount);
                    }

                    // Pricing data
                    if (step == 7)
                    {
                        // Step 7 validates all steps - always reload from database
                        _logger.LogInformation("Reloading pricing data for step 7 validation. Course ID: {CourseId}", 
                            model.Id.Value);

                        model.IsFree = courseWithContent.IsFree;
                        model.Price = courseWithContent.Price;
                        model.DiscountPrice = courseWithContent.DiscountPrice;
                        model.DiscountStartDate = courseWithContent.DiscountStartDate;
                        model.DiscountEndDate = courseWithContent.DiscountEndDate;
                        model.Currency = courseWithContent.Currency ?? "EGP";

                        _logger.LogInformation("Pricing data reloaded. IsFree: {IsFree}, Price: {Price}", 
                            model.IsFree, model.Price);
                    }
                    else if (step == 5)
                    {
                        // Step 5: Only reload from DB if form POST data is missing or invalid
                        var formHasValidPricing = model.IsFree || model.Price > 0;

                        _logger.LogInformation("Step 5 validation. Form has valid pricing: {HasValid}, IsFree: {IsFree}, Price: {Price}", 
                            formHasValidPricing, model.IsFree, model.Price);

                        // Only reload from database if form POST doesn't have valid pricing
                        if (!formHasValidPricing)
                        {
                            model.IsFree = courseWithContent.IsFree;
                            model.Price = courseWithContent.Price;
                            model.DiscountPrice = courseWithContent.DiscountPrice;
                            model.DiscountStartDate = courseWithContent.DiscountStartDate;
                            model.DiscountEndDate = courseWithContent.DiscountEndDate;
                            model.Currency = courseWithContent.Currency ?? "EGP";
                            _logger.LogInformation("Reloaded pricing from database. IsFree: {IsFree}, Price: {Price}", 
                                model.IsFree, model.Price);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Course {CourseId} not found when reloading data for validation", model.Id.Value);
                }
            }

            // Validate current step (now with populated Modules for step 3)
            var stepValidation = model.ValidateStep(step);
            
            // For draft saves, we allow incomplete steps
            if (action != "draft" && !stepValidation.IsValid && step < 7)
            {
                // Store validation errors in TempData for display after redirect
                var errorMessage = string.Join("\n• ", stepValidation.Errors);
                SetErrorMessage(errorMessage);
                
                // Redirect to maintain proper URL structure and reload fresh data
                if (model.Id.HasValue && model.Id.Value > 0)
                {
                    return RedirectToAction(nameof(CreateWizard), new { step = step, id = model.Id.Value });
                }
                else
                {
                    return RedirectToAction(nameof(CreateWizard), new { step = step });
                }
            }

            // Use execution strategy for SqlServerRetryingExecutionStrategy compatibility
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            
            Course? savedCourse = null;
            var transactionResult = await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    Course course;

                    if (model.Id.HasValue && model.Id.Value > 0)
                    {
                        // Update existing course
                        course = await _context.Courses
                            .Include(c => c.LearningOutcomes)
                            .Include(c => c.Requirements)
                            .Include(c => c.WhatYouWillLearn)
                            .FirstOrDefaultAsync(c => c.Id == model.Id.Value && c.InstructorId == userId);

                        if (course == null)
                        {
                            await transaction.RollbackAsync();
                            return (success: false, courseId: 0, error: "NOT_FOUND");
                        }

                        UpdateCourseFromWizard(course, model, step);
                    }
                    else
                    {
                        // Create new course
                        // Ensure we have a valid category
                        int finalCategoryId = model.CategoryId;
                        if (finalCategoryId <= 0)
                        {
                            _logger.LogWarning("CategoryId is 0 or negative. Attempting to find default category.");
                            var defaultCategory = await _context.Categories
                                .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
                                .OrderBy(c => c.DisplayOrder)
                                .FirstOrDefaultAsync();

                            if (defaultCategory == null)
                            {
                                await transaction.RollbackAsync();
                                _logger.LogError("No categories exist in the system! Cannot create course.");
                                return (success: false, courseId: 0, error: "NO_CATEGORY");
                            }

                            finalCategoryId = defaultCategory.Id;
                            _logger.LogInformation("Using default category {CategoryId} - {CategoryName}", 
                                defaultCategory.Id, defaultCategory.Name);
                        }

                        // Generate unique slug with bulletproof approach
                        var baseSlug = _slugService.GenerateSlug(model.Title);
                        var slug = baseSlug;
                        var slugAttempt = 0;
                        const int maxSlugAttempts = 10;
                        
                        // Keep trying until we find a unique slug
                        while (await _context.Courses.AnyAsync(c => c.Slug == slug))
                        {
                            slugAttempt++;
                            if (slugAttempt >= maxSlugAttempts)
                            {
                                // Fallback: use timestamp + GUID for guaranteed uniqueness
                                slug = $"{baseSlug}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
                                break;
                            }
                            // Add random suffix for uniqueness
                            slug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
                            _logger.LogDebug("Slug collision detected, trying: {Slug}", slug);
                        }

                        _logger.LogInformation("Creating new course. Title: {Title}, Slug: {Slug}, CategoryId: {CategoryId}, InstructorId: {InstructorId}",
                            model.Title, slug, finalCategoryId, userId);

                        course = new Course
                        {
                            Title = model.Title,
                            ShortDescription = model.ShortDescription ?? string.Empty,
                            Description = model.Description ?? string.Empty,
                            CategoryId = finalCategoryId,
                            SubCategoryId = model.SubCategoryId,
                            Level = model.Level,
                            Language = model.Language ?? "ar",
                            InstructorId = userId!,
                            Slug = slug,
                            Status = CourseStatus.Draft,
                            InstructorCommissionRate = instructorProfile.CommissionRate
                        };

                        _context.Courses.Add(course);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Course {CourseId} created successfully", course.Id);

                        model.Id = course.Id;
                        UpdateCourseFromWizard(course, model, step);
                    }

                    await _context.SaveChangesAsync();

                    // Save learning outcomes - ONLY when on step 2 (to prevent deletion when navigating from other steps)
                    if (step == 2 && model.LearningOutcomes != null && model.LearningOutcomes.Any(o => !string.IsNullOrWhiteSpace(o)))
                    {
                        await SaveLearningOutcomes(course.Id, model.LearningOutcomes);
                    }

                    // Save requirements - ONLY when on step 2
                    if (step == 2 && model.Requirements != null && model.Requirements.Any(r => !string.IsNullOrWhiteSpace(r)))
                    {
                        await SaveRequirements(course.Id, model.Requirements);
                    }

                    // Save target audience as WhatYouWillLearn - ONLY when on step 2
                    if (step == 2 && model.TargetAudience != null && model.TargetAudience.Any(t => !string.IsNullOrWhiteSpace(t)))
                    {
                        await SaveTargetAudience(course.Id, model.TargetAudience);
                    }

                    await transaction.CommitAsync();
                    
                    savedCourse = course;
                    return (success: true, courseId: course.Id, error: (string?)null);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed in wizard step {Step}", step);
                    throw;
                }
            });

            // Handle transaction result
            if (!transactionResult.success)
            {
                if (transactionResult.error == "NOT_FOUND")
                {
                    SetErrorMessage("لم يتم العثور على الدورة");
                    return RedirectToAction(nameof(Index));
                }
                else if (transactionResult.error == "NO_CATEGORY")
                {
                    SetErrorMessage("لا توجد تصنيفات في النظام. يرجى الاتصال بالمسؤول لإضافة تصنيف.");
                    return RedirectToAction(nameof(Index));
                }
            }

            _logger.LogInformation("Wizard step {Step} saved for course {CourseId} by instructor {InstructorId}",
                step, transactionResult.courseId, userId);

            // Determine next action
            if (action == "draft")
            {
                SetSuccessMessage("تم حفظ المسودة بنجاح");
                return RedirectToAction(nameof(Index));
            }
            else if (action == "previous" && step > 1)
            {
                return RedirectToAction(nameof(CreateWizard), new { step = step - 1, id = transactionResult.courseId });
            }
            else if (action == "publish" && step == 7)
            {
                if (instructorProfile.Status != "Approved")
                {
                    SetErrorMessage($"يجب أن يكون حسابك معتمداً لنشر الدورات. الحالة الحالية: {instructorProfile.Status}");
                    return RedirectToAction("Index", "Profile");
                }
                return await HandleWizardPublish(transactionResult.courseId);
            }
            else if (step < 7)
            {
                return RedirectToAction(nameof(CreateWizard), new { step = step + 1, id = transactionResult.courseId });
            }
            else
            {
                return RedirectToAction(nameof(CreateWizard), new { step = 7, id = transactionResult.courseId });
            }
        }
        catch (DbUpdateException dbEx)
        {
            var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
            _logger.LogError(dbEx, 
                "Database error saving wizard step {Step} for course {CourseId}. Inner: {InnerMessage}", 
                step, model.Id, innerMessage);
            
            // Check for specific database errors
            if (innerMessage.Contains("FK_Courses_Categories"))
            {
                SetErrorMessage("خطأ في التصنيف: التصنيف المحدد غير موجود في قاعدة البيانات. يرجى اختيار تصنيف آخر.");
            }
            else if (innerMessage.Contains("FK_Courses_AspNetUsers"))
            {
                SetErrorMessage("خطأ في المدرس: لم يتم العثور على حساب المدرس.");
            }
            else if (innerMessage.Contains("UNIQUE"))
            {
                SetErrorMessage("خطأ: يوجد دورة أخرى بنفس العنوان. يرجى تغيير العنوان.");
            }
            else
            {
                SetErrorMessage($"خطأ في قاعدة البيانات: {innerMessage.Substring(0, Math.Min(200, innerMessage.Length))}");
            }
            
            await PopulateWizardDropdowns(model.CategoryId);
            return View("CreateWizard", model);
        }
        catch (InvalidOperationException invEx)
        {
            _logger.LogError(invEx, 
                "Invalid operation saving wizard step {Step} for course {CourseId}. Message: {Message}", 
                step, model.Id, invEx.Message);
            
            SetErrorMessage($"عملية غير صالحة: {invEx.Message}");
            await PopulateWizardDropdowns(model.CategoryId);
            return View("CreateWizard", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error saving wizard step {Step} for course {CourseId}. Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                step, model.Id, ex.GetType().Name, ex.Message, ex.StackTrace);
            
            SetErrorMessage($"حدث خطأ غير متوقع: {ex.Message}. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني.");
            
            // Redirect to maintain proper URL structure and reload fresh data
            if (model.Id.HasValue && model.Id.Value > 0)
            {
                return RedirectToAction(nameof(CreateWizard), new { step = step, id = model.Id.Value });
            }
            else
            {
                return RedirectToAction(nameof(CreateWizard), new { step = step });
            }
        }
    }

    /// <summary>
    /// الحفظ التلقائي - Auto Save (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSave([FromBody] CourseAutoSaveModel model)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "غير مصرح" });
        }

        try
        {
            if (model.CourseId.HasValue && model.CourseId.Value > 0)
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == model.CourseId.Value && c.InstructorId == userId);

                if (course == null)
                {
                    return Json(new { success = false, message = "الدورة غير موجودة" });
                }

                // Update specific field
                UpdateCourseField(course, model.Field, model.Value);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Auto-saved field {Field} for course {CourseId}", model.Field, model.CourseId);

                return Json(new { success = true, message = "تم الحفظ", courseId = course.Id });
            }
            else
            {
                // Create draft course (profile must exist; approval required only for publishing)
                var instructorProfile = await _context.InstructorProfiles
                    .FirstOrDefaultAsync(ip => ip.UserId == userId);

                if (instructorProfile == null)
                {
                    return Json(new { success = false, message = "لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً." });
                }

                var title = model.Field == "Title" ? model.Value : "مسودة جديدة";
                
                // Generate unique slug with bulletproof approach
                var baseSlug = _slugService.GenerateSlug(title);
                var slug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
                
                // Ensure uniqueness (extremely rare case)
                var slugAttempt = 0;
                while (await _context.Courses.AnyAsync(c => c.Slug == slug) && slugAttempt < 5)
                {
                    slugAttempt++;
                    slug = $"{baseSlug}-{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..8]}";
                }

                var course = new Course
                {
                    Title = title,
                    Slug = slug,
                    InstructorId = userId!,
                    Status = CourseStatus.Draft,
                    CategoryId = 1, // Default category
                    InstructorCommissionRate = instructorProfile.CommissionRate
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Draft course {CourseId} created via auto-save by {InstructorId}",
                    course.Id, userId);

                return Json(new { success = true, message = "تم إنشاء المسودة", courseId = course.Id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-save failed for course {CourseId}", model.CourseId);
            return Json(new { success = false, message = "فشل الحفظ التلقائي" });
        }
    }

    /// <summary>
    /// إضافة وحدة سريعة - Quick Add Module (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddModule([FromBody] QuickAddModuleModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
            {
                return Json(new { success = false, message = "الدورة غير موجودة" });
            }

            var maxOrder = course.Modules.Any() ? course.Modules.Max(m => m.OrderIndex) : 0;

            var module = new Module
            {
                CourseId = model.CourseId,
                Title = model.Title,
                Description = model.Description,
                OrderIndex = maxOrder + 1,
                IsPublished = false
            };

            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            // Update course statistics after adding module
            await UpdateCourseStatistics(course.Id);

            _logger.LogInformation("Module {ModuleId} added to course {CourseId} via wizard",
                module.Id, model.CourseId);

            return Json(new
            {
                success = true,
                message = "تم إضافة الوحدة",
                module = new
                {
                    id = module.Id,
                    title = module.Title,
                    description = module.Description,
                    orderIndex = module.OrderIndex,
                    lessonsCount = 0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding module to course {CourseId}", model.CourseId);
            return Json(new { success = false, message = "فشل إضافة الوحدة" });
        }
    }

    /// <summary>
    /// تعديل وحدة سريع - Quick Edit Module (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEditModule([FromBody] QuickEditModuleModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return Json(new { success = false, message = "عنوان الوحدة مطلوب" });
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == model.Id && m.Course.InstructorId == userId);

            if (module == null)
            {
                return Json(new { success = false, message = "الوحدة غير موجودة" });
            }

            module.Title = model.Title;
            module.Description = model.Description;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Module {ModuleId} updated via wizard by {InstructorId}",
                model.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم تعديل الوحدة بنجاح",
                module = new
                {
                    id = module.Id,
                    title = module.Title,
                    description = module.Description
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing module {ModuleId}", model.Id);
            return Json(new { success = false, message = "فشل تعديل الوحدة" });
        }
    }

    /// <summary>
    /// إضافة درس سريعة - Quick Add Lesson (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddLesson([FromBody] QuickAddLessonExtendedModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            // Check ModelState first (this will catch validation errors)
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();
                
                _logger.LogWarning("QuickAddLesson model validation failed. Errors: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"خطأ في التحقق من البيانات: {string.Join(", ", errors)}" });
            }

            // Validate model
            if (model == null)
            {
                // Try to read raw request body for debugging
                string? rawBody = null;
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
                    rawBody = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read request body");
                }

                _logger.LogWarning("QuickAddLesson called with null model. Raw body: {RawBody}, Content-Type: {ContentType}", 
                    rawBody ?? "Unable to read", Request.ContentType);
                return Json(new { success = false, message = "البيانات المرسلة غير صحيحة. يرجى التحقق من البيانات المرسلة." });
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return Json(new { success = false, message = "عنوان الدرس مطلوب" });
            }

            if (model.ModuleId <= 0)
            {
                return Json(new { success = false, message = "معرف الوحدة غير صحيح" });
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == model.ModuleId && m.Course.InstructorId == userId);

            if (module == null)
            {
                _logger.LogWarning("Module {ModuleId} not found for instructor {InstructorId}", 
                    model.ModuleId, userId);
                return Json(new { success = false, message = "الوحدة غير موجودة أو ليس لديك صلاحية للوصول إليها" });
            }

            var maxOrder = module.Lessons.Any() ? module.Lessons.Max(l => l.OrderIndex) : 0;

            string? videoUrl = null;
            string? videoProvider = null;
            string? videoId = null;
            
            if (!string.IsNullOrWhiteSpace(model.VideoUrl))
            {
                videoUrl = model.VideoUrl.Trim();
                (videoProvider, videoId) = DetectVideoProvider(model.VideoUrl);
            }

            var lesson = new Lesson
            {
                ModuleId = model.ModuleId,
                Title = model.Title.Trim(),
                Type = model.Type,
                VideoUrl = videoUrl,
                VideoProvider = videoProvider,
                VideoId = videoId,
                HtmlContent = !string.IsNullOrWhiteSpace(model.HtmlContent) ? model.HtmlContent.Trim() : null,
                FileUrl = !string.IsNullOrWhiteSpace(model.FileUrl) ? model.FileUrl.Trim() : null,
                DurationSeconds = model.DurationSeconds >= 0 ? model.DurationSeconds : 0,
                IsPreviewable = model.IsPreviewable,
                IsDownloadable = model.IsDownloadable,
                OrderIndex = maxOrder + 1,
                MustComplete = true
            };

            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            int? quizId = null;
            int? assignmentId = null;

            // Handle Quiz creation if type is Quiz
            if (model.Type == LessonType.Quiz)
            {
                var quiz = new Quiz
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    Description = model.QuizSettings?.Description,
                    Instructions = model.QuizSettings?.Instructions,
                    PassingScore = model.QuizSettings?.PassingScore ?? 70,
                    TimeLimitMinutes = model.QuizSettings?.TimeLimitMinutes,
                    MaxAttempts = model.QuizSettings?.MaxAttempts,
                    ShuffleQuestions = model.QuizSettings?.ShuffleQuestions ?? false,
                    ShuffleOptions = model.QuizSettings?.ShuffleOptions ?? false,
                    ShowCorrectAnswers = model.QuizSettings?.ShowCorrectAnswers ?? true,
                    ShowAnswersAfter = "Immediately",
                    ShowScoreImmediately = model.QuizSettings?.ShowScoreImmediately ?? true,
                    AllowBackNavigation = model.QuizSettings?.AllowBackNavigation ?? true,
                    OneQuestionPerPage = model.QuizSettings?.OneQuestionPerPage ?? false,
                    IsActive = true,
                    RequiresProctoring = model.ProctoringSettings?.IsEnabled ?? false
                };
                _context.Set<Quiz>().Add(quiz);
                await _context.SaveChangesAsync();
                quizId = quiz.Id;

                // Create proctoring settings if enabled
                if (model.ProctoringSettings?.IsEnabled == true)
                {
                    var proctoring = new ProctoringSetting
                    {
                        QuizId = quiz.Id,
                        IsEnabled = true,
                        RequireWebcam = model.ProctoringSettings.RequireWebcam,
                        RecordVideo = model.ProctoringSettings.RecordVideo,
                        CaptureScreenshots = model.ProctoringSettings.CaptureScreenshots,
                        ScreenshotInterval = model.ProctoringSettings.ScreenshotInterval,
                        PreventTabSwitch = model.ProctoringSettings.PreventTabSwitch,
                        MaxTabSwitchWarnings = model.ProctoringSettings.MaxTabSwitchWarnings,
                        PreventCopyPaste = model.ProctoringSettings.PreventCopyPaste,
                        DisableRightClick = model.ProctoringSettings.DisableRightClick,
                        RequireFullscreen = model.ProctoringSettings.RequireFullscreen,
                        EnableFaceDetection = model.ProctoringSettings.EnableFaceDetection,
                        DetectMultiplePeople = model.ProctoringSettings.DetectMultiplePeople,
                        LockBrowser = model.ProctoringSettings.LockBrowser,
                        RequireIdVerification = model.ProctoringSettings.RequireIdVerification,
                        AutoTerminate = model.ProctoringSettings.AutoTerminate,
                        WarningMessage = model.ProctoringSettings.WarningMessage
                    };
                    _context.Set<ProctoringSetting>().Add(proctoring);
                    await _context.SaveChangesAsync();
                }
            }

            // Handle Assignment creation if type is Assignment
            if (model.Type == LessonType.Assignment && model.AssignmentSettings != null)
            {
                var assignment = new Assignment
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    Description = model.AssignmentSettings.Description,
                    Instructions = model.AssignmentSettings.Instructions,
                    MaxPoints = model.AssignmentSettings.MaxPoints,
                    PassingPoints = model.AssignmentSettings.PassingPoints,
                    DueDate = model.AssignmentSettings.DueDate ?? (model.AssignmentSettings.DueDateDays.HasValue ? DateTime.UtcNow.AddDays(model.AssignmentSettings.DueDateDays.Value) : (DateTime?)null),
                    AllowLateSubmission = model.AssignmentSettings.AllowLateSubmission,
                    LatePenaltyPercentage = model.AssignmentSettings.LatePenaltyPercentage ?? 0,
                    AllowTextSubmission = model.AssignmentSettings.AllowTextSubmission,
                    AllowFileUpload = model.AssignmentSettings.AllowFileUpload,
                    AcceptedFileTypes = model.AssignmentSettings.AcceptedFileTypes,
                    MaxFileSizeMB = model.AssignmentSettings.MaxFileSizeMB ?? 50,
                    MaxFiles = model.AssignmentSettings.MaxFiles ?? 5,
                    AllowResubmission = model.AssignmentSettings.AllowResubmission,
                    MaxSubmissions = model.AssignmentSettings.MaxSubmissions ?? 3
                };
                _context.Set<Assignment>().Add(assignment);
                await _context.SaveChangesAsync();
                assignmentId = assignment.Id;
            }

            // Handle content drip settings
            if (model.ContentDripSettings != null && model.ContentDripSettings.DripType != "Immediate")
            {
                lesson.AvailableAfterDays = model.ContentDripSettings.AvailableAfterDays;
                lesson.AvailableFrom = model.ContentDripSettings.AvailableFrom;
                lesson.PrerequisiteLessonId = model.ContentDripSettings.PrerequisiteLessonId;
                // Map DripType string to ContentDripType enum
                if (Enum.TryParse<ContentDripType>(model.ContentDripSettings.DripType, out var dripType))
                {
                    lesson.ContentDrip = dripType;
                }
                await _context.SaveChangesAsync();
            }

            // Update course statistics after adding lesson
            await UpdateCourseStatistics(module.CourseId);

            _logger.LogInformation("Lesson {LessonId} added to module {ModuleId} via wizard by instructor {InstructorId}",
                lesson.Id, model.ModuleId, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة الدرس بنجاح",
                lesson = new
                {
                    id = lesson.Id,
                    title = lesson.Title,
                    type = lesson.Type.ToString(),
                    typeIcon = GetLessonTypeIcon(lesson.Type),
                    typeName = GetLessonTypeName(lesson.Type),
                    duration = lesson.DurationSeconds / 60,
                    isPreviewable = lesson.IsPreviewable,
                    orderIndex = lesson.OrderIndex
                },
                quizId,
                assignmentId
            });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error adding lesson to module {ModuleId} for instructor {InstructorId}", 
                model?.ModuleId, userId);
            
            // Check for specific database constraint violations
            if (dbEx.InnerException != null && 
                dbEx.InnerException.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "يوجد درس بنفس العنوان في هذه الوحدة" });
            }
            
            return Json(new { success = false, message = "حدث خطأ في قاعدة البيانات. يرجى المحاولة مرة أخرى." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding lesson to module {ModuleId} for instructor {InstructorId}", 
                model?.ModuleId, userId);
            return Json(new { success = false, message = $"فشل إضافة الدرس: {ex.Message}" });
        }
    }

    /// <summary>
    /// الحصول على بيانات درس للتعديل - Get lesson data for editing (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> QuickGetLesson(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Quizzes)
                .Include(l => l.Assignments)
                .FirstOrDefaultAsync(l => l.Id == id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            return Json(new
            {
                success = true,
                lesson = new
                {
                    id = lesson.Id,
                    moduleId = lesson.ModuleId,
                    title = lesson.Title,
                    description = lesson.Description,
                    type = lesson.Type.ToString(),
                    videoUrl = lesson.VideoUrl,
                    videoProvider = lesson.VideoProvider,
                    htmlContent = lesson.HtmlContent,
                    fileUrl = lesson.FileUrl,
                    durationSeconds = lesson.DurationSeconds,
                    isPreviewable = lesson.IsPreviewable,
                    isDownloadable = lesson.IsDownloadable
                },
                quizId = lesson.Quizzes?.FirstOrDefault()?.Id,
                assignmentId = lesson.Assignments?.FirstOrDefault()?.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lesson {LessonId} for editing", id);
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل بيانات الدرس" });
        }
    }

    /// <summary>
    /// تعديل درس سريع - Quick Edit Lesson (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEditLesson([FromBody] QuickEditLessonModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null || model.Id <= 0)
            {
                return Json(new { success = false, message = "بيانات غير صحيحة" });
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return Json(new { success = false, message = "عنوان الدرس مطلوب" });
            }

            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            // Update lesson properties
            lesson.Title = model.Title.Trim();
            lesson.Description = model.Description?.Trim();
            lesson.DurationSeconds = model.DurationSeconds >= 0 ? model.DurationSeconds : lesson.DurationSeconds;
            lesson.IsPreviewable = model.IsPreviewable;
            lesson.IsDownloadable = model.IsDownloadable;

            // Update type-specific content only when a non-empty value is sent (avoid clearing content by mistake)
            if (!string.IsNullOrWhiteSpace(model.VideoUrl))
            {
                lesson.VideoUrl = model.VideoUrl.Trim();
            }
            if (!string.IsNullOrWhiteSpace(model.HtmlContent))
            {
                lesson.HtmlContent = model.HtmlContent.Trim();
            }
            if (!string.IsNullOrWhiteSpace(model.FileUrl))
            {
                lesson.FileUrl = model.FileUrl.Trim();
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} edited via wizard by instructor {InstructorId}",
                lesson.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم تحديث الدرس بنجاح",
                lesson = new
                {
                    id = lesson.Id,
                    title = lesson.Title,
                    type = lesson.Type.ToString(),
                    typeIcon = GetLessonTypeIcon(lesson.Type),
                    typeName = GetLessonTypeName(lesson.Type),
                    duration = lesson.DurationSeconds / 60,
                    isPreviewable = lesson.IsPreviewable,
                    orderIndex = lesson.OrderIndex
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing lesson {LessonId} via QuickEditLesson", model?.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث الدرس" });
        }
    }

    /// <summary>
    /// إعادة ترتيب الوحدات - Reorder Modules (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderModules([FromBody] ReorderItemsModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            foreach (var item in model.Items)
            {
                var module = await _context.Modules
                    .Include(m => m.Course)
                    .FirstOrDefaultAsync(m => m.Id == item.Id && m.Course.InstructorId == userId);

                if (module != null)
                {
                    module.OrderIndex = item.Order;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم تحديث الترتيب" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering modules");
            return Json(new { success = false, message = "فشل تحديث الترتيب" });
        }
    }

    /// <summary>
    /// إعادة ترتيب الدروس - Reorder Lessons (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderLessons([FromBody] ReorderItemsModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            foreach (var item in model.Items)
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == item.Id && l.Module.Course.InstructorId == userId);

                if (lesson != null)
                {
                    lesson.OrderIndex = item.Order;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم تحديث الترتيب" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering lessons");
            return Json(new { success = false, message = "فشل تحديث الترتيب" });
        }
    }

    /// <summary>
    /// حذف وحدة سريع - Quick Delete Module (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickDeleteModule([FromBody] int moduleId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var module = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == moduleId && m.Course.InstructorId == userId);

            if (module == null)
            {
                return Json(new { success = false, message = "الوحدة غير موجودة" });
            }

            if (module.Lessons.Any())
            {
                return Json(new { success = false, message = "لا يمكن حذف وحدة تحتوي على دروس" });
            }

            var courseId = module.CourseId;
            
            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();

            // Update course statistics after deleting module
            await UpdateCourseStatistics(courseId);

            _logger.LogInformation("Module {ModuleId} deleted via wizard by {InstructorId}",
                moduleId, userId);

            return Json(new { success = true, message = "تم حذف الوحدة" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting module {ModuleId}", moduleId);
            return Json(new { success = false, message = "فشل حذف الوحدة" });
        }
    }

    /// <summary>
    /// حذف درس سريع - Quick Delete Lesson (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickDeleteLesson([FromBody] int lessonId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            var courseId = lesson.Module.CourseId;

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            // Update course statistics after deleting lesson
            await UpdateCourseStatistics(courseId);

            _logger.LogInformation("Lesson {LessonId} deleted via wizard by {InstructorId}",
                lessonId, userId);

            return Json(new { success = true, message = "تم حذف الدرس" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lesson {LessonId}", lessonId);
            return Json(new { success = false, message = "فشل حذف الدرس" });
        }
    }

    // ========== STEP 3: QUIZ QUESTION ENDPOINTS ==========

    /// <summary>
    /// إضافة سؤال سريع للاختبار - Quick Add Quiz Question (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddQuizQuestion([FromBody] QuickAddQuizQuestionModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            if (string.IsNullOrWhiteSpace(model.QuestionText))
                return Json(new { success = false, message = "نص السؤال مطلوب" });

            // Validate quiz exists and belongs to instructor's course
            var quiz = await _context.Set<Quiz>()
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == model.QuizId && q.Lesson.Module.Course.InstructorId == userId);

            if (quiz == null)
                return Json(new { success = false, message = "الاختبار غير موجود أو ليس لديك صلاحية" });

            var maxOrder = quiz.Questions.Any() ? quiz.Questions.Max(q => q.OrderIndex) : 0;

            var question = new Question
            {
                QuizId = model.QuizId,
                QuestionText = model.QuestionText.Trim(),
                Type = (QuestionType)model.Type,
                Points = model.Points > 0 ? model.Points : 1,
                DifficultyLevel = model.DifficultyLevel,
                OrderIndex = maxOrder + 1,
                Explanation = model.Explanation?.Trim(),
                Hint = model.Hint?.Trim(),
                SampleAnswer = model.SampleAnswer?.Trim(),
                AnswerKeywords = model.AnswerKeywords?.Trim(),
                MinWordCount = model.MinWordCount,
                MaxWordCount = model.MaxWordCount,
                IsRequired = true
            };

            _context.Set<Question>().Add(question);
            await _context.SaveChangesAsync();

            // Add options if provided
            if (model.Options != null && model.Options.Any())
            {
                foreach (var opt in model.Options)
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = opt.OptionText?.Trim() ?? string.Empty,
                        IsCorrect = opt.IsCorrect,
                        OrderIndex = opt.OrderIndex
                    };
                    _context.Set<QuestionOption>().Add(option);
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Question {QuestionId} added to quiz {QuizId} by instructor {InstructorId}",
                question.Id, model.QuizId, userId);

            return Json(new
            {
                success = true,
                message = "تمت إضافة السؤال بنجاح",
                question = new
                {
                    id = question.Id,
                    text = question.QuestionText,
                    type = (int)question.Type,
                    points = question.Points,
                    difficulty = question.DifficultyLevel
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding question to quiz {QuizId}", model?.QuizId);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة السؤال" });
        }
    }

    /// <summary>
    /// تعديل سؤال اختبار - Quick Edit Quiz Question (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEditQuizQuestion([FromBody] QuickEditQuizQuestionModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            var question = await _context.Set<Question>()
                .Include(q => q.Options)
                .Include(q => q.Quiz)
                    .ThenInclude(qz => qz.Lesson)
                        .ThenInclude(l => l.Module)
                            .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(q => q.Id == model.QuestionId && q.Quiz.Lesson.Module.Course.InstructorId == userId);

            if (question == null)
                return Json(new { success = false, message = "السؤال غير موجود" });

            question.QuestionText = model.QuestionText?.Trim() ?? question.QuestionText;
            question.Type = (QuestionType)model.Type;
            question.Points = model.Points > 0 ? model.Points : question.Points;
            question.DifficultyLevel = model.DifficultyLevel ?? question.DifficultyLevel;
            question.Explanation = model.Explanation?.Trim();
            question.Hint = model.Hint?.Trim();

            // Replace options if provided
            if (model.Options != null)
            {
                _context.Set<QuestionOption>().RemoveRange(question.Options);
                foreach (var opt in model.Options)
                {
                    _context.Set<QuestionOption>().Add(new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = opt.OptionText?.Trim() ?? string.Empty,
                        IsCorrect = opt.IsCorrect,
                        OrderIndex = opt.OrderIndex
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Question {QuestionId} edited by instructor {InstructorId}", model.QuestionId, userId);

            return Json(new { success = true, message = "تم تعديل السؤال بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing question {QuestionId}", model?.QuestionId);
            return Json(new { success = false, message = "حدث خطأ أثناء تعديل السؤال" });
        }
    }

    /// <summary>
    /// حذف سؤال اختبار - Quick Delete Quiz Question (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickDeleteQuizQuestion([FromBody] QuickDeleteQuizQuestionModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            var question = await _context.Set<Question>()
                .Include(q => q.Options)
                .Include(q => q.StudentAnswers)
                .Include(q => q.Quiz)
                    .ThenInclude(qz => qz.Lesson)
                        .ThenInclude(l => l.Module)
                            .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(q => q.Id == model.QuestionId && q.Quiz.Lesson.Module.Course.InstructorId == userId);

            if (question == null)
                return Json(new { success = false, message = "السؤال غير موجود" });

            // Check for existing attempts
            if (question.StudentAnswers.Any())
            {
                return Json(new { success = false, message = "لا يمكن حذف السؤال لوجود إجابات طلاب مرتبطة به" });
            }

            _context.Set<QuestionOption>().RemoveRange(question.Options);
            _context.Set<Question>().Remove(question);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Question {QuestionId} deleted by instructor {InstructorId}", model.QuestionId, userId);

            return Json(new { success = true, message = "تم حذف السؤال بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting question {QuestionId}", model?.QuestionId);
            return Json(new { success = false, message = "حدث خطأ أثناء حذف السؤال" });
        }
    }

    /// <summary>
    /// الحصول على أسئلة الاختبار - Get Quiz Questions (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQuizQuestions(int quizId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var quiz = await _context.Set<Quiz>()
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(q => q.Id == quizId && q.Lesson.Module.Course.InstructorId == userId);

            if (quiz == null)
                return Json(new { success = false, message = "الاختبار غير موجود" });

            var questions = await _context.Set<Question>()
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.OrderIndex)
                .Include(q => q.Options)
                .Select(q => new
                {
                    id = q.Id,
                    text = q.QuestionText,
                    type = (int)q.Type,
                    points = q.Points,
                    difficulty = q.DifficultyLevel,
                    orderIndex = q.OrderIndex,
                    options = q.Options.OrderBy(o => o.OrderIndex).Select(o => new
                    {
                        id = o.Id,
                        text = o.OptionText,
                        isCorrect = o.IsCorrect,
                        orderIndex = o.OrderIndex
                    }).ToList()
                })
                .ToListAsync();

            return Json(new { success = true, questions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quiz questions for quiz {QuizId}", quizId);
            return Json(new { success = false, message = "حدث خطأ" });
        }
    }

    // ========== STEP 3: LESSON RESOURCE ENDPOINTS ==========

    /// <summary>
    /// إضافة مرفق للدرس - Quick Add Lesson Resource (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddLessonResource([FromBody] QuickAddLessonResourceModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            if (string.IsNullOrWhiteSpace(model.Title))
                return Json(new { success = false, message = "عنوان المرفق مطلوب" });

            if (string.IsNullOrWhiteSpace(model.ResourceUrl))
                return Json(new { success = false, message = "رابط المرفق مطلوب" });

            // Validate lesson belongs to instructor
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Resources)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            var maxOrder = lesson.Resources.Any() ? lesson.Resources.Max(r => r.OrderIndex) : 0;

            var resource = new LessonResource
            {
                LessonId = model.LessonId,
                Title = model.Title.Trim(),
                FileUrl = model.ResourceUrl.Trim(),
                OrderIndex = maxOrder + 1
            };

            _context.Set<LessonResource>().Add(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resource {ResourceId} added to lesson {LessonId} by instructor {InstructorId}",
                resource.Id, model.LessonId, userId);

            return Json(new
            {
                success = true,
                message = "تمت إضافة المرفق بنجاح",
                resource = new
                {
                    id = resource.Id,
                    title = resource.Title,
                    url = resource.FileUrl,
                    type = resource.ResourceType
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding resource to lesson {LessonId}", model?.LessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة المرفق" });
        }
    }

    /// <summary>
    /// حذف مرفق الدرس - Quick Delete Lesson Resource (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickDeleteLessonResource([FromBody] QuickDeleteLessonResourceModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            var resource = await _context.Set<LessonResource>()
                .Include(r => r.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(r => r.Id == model.ResourceId && r.Lesson.Module.Course.InstructorId == userId);

            if (resource == null)
                return Json(new { success = false, message = "المرفق غير موجود" });

            _context.Set<LessonResource>().Remove(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resource {ResourceId} deleted by instructor {InstructorId}", model.ResourceId, userId);

            return Json(new { success = true, message = "تم حذف المرفق بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource {ResourceId}", model?.ResourceId);
            return Json(new { success = false, message = "حدث خطأ أثناء حذف المرفق" });
        }
    }

    /// <summary>
    /// الحصول على مرفقات الدرس - Get Lesson Resources (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLessonResources(int lessonId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            var resources = await _context.Set<LessonResource>()
                .Where(r => r.LessonId == lessonId)
                .OrderBy(r => r.OrderIndex)
                .Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    url = r.FileUrl,
                    fileUrl = r.FileUrl,
                    type = r.FileType ?? "file"
                })
                .ToListAsync();

            return Json(new { success = true, resources });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resources for lesson {LessonId}", lessonId);
            return Json(new { success = false, message = "حدث خطأ" });
        }
    }

    // ========== STEP 3: CONTENT DRIP ENDPOINTS ==========

    /// <summary>
    /// إضافة قاعدة جدولة محتوى - Quick Add Content Drip Rule (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddContentDripRule([FromBody] QuickAddContentDripRuleModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            // Validate course belongs to instructor
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
                return Json(new { success = false, message = "الدورة غير موجودة" });

            var rule = new ContentDripRule
            {
                CourseId = model.CourseId,
                ModuleId = model.ModuleId,
                LessonId = model.LessonId,
                DripType = (ContentDripType)model.DripType,
                DaysAfterEnrollment = model.DaysAfterEnrollment,
                SpecificDate = !string.IsNullOrWhiteSpace(model.SpecificDate)
                    ? DateTime.TryParse(model.SpecificDate, out var dt) ? dt : (DateTime?)null
                    : null,
                SendNotification = model.SendNotification,
                IsActive = true
            };

            _context.Set<ContentDripRule>().Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Content drip rule {RuleId} added for course {CourseId} by instructor {InstructorId}",
                rule.Id, model.CourseId, userId);

            return Json(new
            {
                success = true,
                message = "تم حفظ قاعدة الجدولة بنجاح",
                rule = new
                {
                    id = rule.Id,
                    dripType = (int)rule.DripType,
                    daysAfterEnrollment = rule.DaysAfterEnrollment,
                    specificDate = rule.SpecificDate?.ToString("yyyy-MM-ddTHH:mm"),
                    isActive = rule.IsActive,
                    moduleId = rule.ModuleId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding content drip rule for course {CourseId}", model?.CourseId);
            return Json(new { success = false, message = "حدث خطأ أثناء حفظ قاعدة الجدولة" });
        }
    }

    /// <summary>
    /// الحصول على قواعد جدولة المحتوى - Get Content Drip Rules (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContentDripRules(int courseId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

            if (course == null)
                return Json(new { success = false, message = "الدورة غير موجودة" });

            var rules = await _context.Set<ContentDripRule>()
                .Where(r => r.CourseId == courseId)
                .OrderBy(r => r.ModuleId)
                .ThenBy(r => r.LessonId)
                .Select(r => new
                {
                    id = r.Id,
                    dripType = (int)r.DripType,
                    moduleId = r.ModuleId,
                    lessonId = r.LessonId,
                    daysAfterEnrollment = r.DaysAfterEnrollment,
                    specificDate = r.SpecificDate,
                    sendNotification = r.SendNotification,
                    isActive = r.IsActive
                })
                .ToListAsync();

            return Json(new { success = true, rules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drip rules for course {CourseId}", courseId);
            return Json(new { success = false, message = "حدث خطأ" });
        }
    }

    // ========== STEP 3: UPDATE LESSON CONTENT ==========

    /// <summary>
    /// تحديث محتوى الدرس - Quick Update Lesson Content (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickUpdateLessonContent([FromBody] QuickUpdateLessonContentModel? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات غير صحيحة" });

            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            // Update only provided fields
            if (model.HtmlContent != null)
                lesson.HtmlContent = model.HtmlContent.Trim();

            if (model.FileUrl != null)
                lesson.FileUrl = model.FileUrl.Trim();

            if (model.VideoUrl != null)
                lesson.VideoUrl = model.VideoUrl.Trim();

            if (model.VideoProvider != null)
                lesson.VideoProvider = model.VideoProvider.Trim();

            if (model.DurationSeconds.HasValue)
                lesson.DurationSeconds = model.DurationSeconds.Value;

            if (model.IsDownloadable.HasValue)
                lesson.IsDownloadable = model.IsDownloadable.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} content updated by instructor {InstructorId}", model.LessonId, userId);

            return Json(new { success = true, message = "تم تحديث المحتوى بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lesson content {LessonId}", model?.LessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث المحتوى" });
        }
    }

    /// <summary>
    /// الحصول على التصنيفات الفرعية - Get Subcategories (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubcategories(int categoryId)
    {
        var subcategories = await _context.Categories
            .Where(c => c.ParentCategoryId == categoryId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name })
            .ToListAsync();

        return Json(subcategories);
    }

    /// <summary>
    /// إضافة تصنيف جديد - Add new category (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory([FromBody] AddCategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف مطلوب" });
        }

        try
        {
            // Check if category with same name exists
            var exists = await _context.Categories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentCategoryId == null && !c.IsDeleted);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف بنفس الاسم" });
            }

            // Generate unique slug with bulletproof approach
            var baseSlug = _slugService.GenerateSlug(model.Name);
            var slug = baseSlug;
            var slugAttempt = 0;
            
            while (await _context.Categories.AnyAsync(c => c.Slug == slug) && slugAttempt < 10)
            {
                slugAttempt++;
                slug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
            }

            var category = new Category
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                IconClass = model.Icon ?? "book",
                ParentCategoryId = null,
                IsActive = true,
                IsDeleted = false,
                DisplayOrder = await _context.Categories.Where(c => c.ParentCategoryId == null).CountAsync() + 1
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} created by instructor {InstructorId}",
                category.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف بنجاح",
                category = new
                {
                    id = category.Id,
                    name = category.Name,
                    slug = category.Slug
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category by instructor {InstructorId}", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف" });
        }
    }

    /// <summary>
    /// إضافة تصنيف فرعي جديد - Add new subcategory (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubcategory([FromBody] AddSubcategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (model.ParentCategoryId <= 0)
        {
            return Json(new { success = false, message = "التصنيف الرئيسي مطلوب" });
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف الفرعي مطلوب" });
        }

        try
        {
            // Verify parent category exists
            var parentExists = await _context.Categories
                .AnyAsync(c => c.Id == model.ParentCategoryId && !c.IsDeleted);

            if (!parentExists)
            {
                return Json(new { success = false, message = "التصنيف الرئيسي غير موجود" });
            }

            // Check if subcategory with same name exists under parent
            var exists = await _context.Categories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentCategoryId == model.ParentCategoryId && !c.IsDeleted);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف فرعي بنفس الاسم" });
            }

            // Generate unique slug with bulletproof approach
            var baseSlug = _slugService.GenerateSlug(model.Name);
            var slug = baseSlug;
            var slugAttempt = 0;
            
            while (await _context.Categories.AnyAsync(c => c.Slug == slug) && slugAttempt < 10)
            {
                slugAttempt++;
                slug = $"{baseSlug}-{Guid.NewGuid().ToString()[..8]}";
            }

            var subcategory = new Category
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                ParentCategoryId = model.ParentCategoryId,
                IsActive = true,
                IsDeleted = false,
                DisplayOrder = await _context.Categories.Where(c => c.ParentCategoryId == model.ParentCategoryId).CountAsync() + 1
            };

            _context.Categories.Add(subcategory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subcategory {SubcategoryId} created under {ParentId} by instructor {InstructorId}",
                subcategory.Id, model.ParentCategoryId, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف الفرعي بنجاح",
                subcategory = new
                {
                    id = subcategory.Id,
                    name = subcategory.Name,
                    slug = subcategory.Slug,
                    parentId = subcategory.ParentCategoryId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding subcategory by instructor {InstructorId}", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف الفرعي" });
        }
    }

    /// <summary>
    /// حساب نسبة الاكتمال - Calculate Completion (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCompletionStatus(int courseId)
    {
        var userId = _currentUserService.UserId;

        var course = await LoadCourseForWizard(courseId, userId);
        if (course == null)
        {
            return Json(new { success = false });
        }

        var model = MapCourseToWizardViewModel(course);
        var checklist = GetReadinessChecklist(model);

        return Json(new
        {
            success = true,
            percentage = model.CalculateCompletionPercentage(),
            checklist = checklist.Select(c => new
            {
                name = c.NameArabic,
                completed = c.IsCompleted,
                required = c.IsRequired
            })
        });
    }

    #endregion

    #region Wizard Helper Methods

    private async Task<Course?> LoadCourseForWizard(int courseId, string? userId)
    {
        return await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.SubCategory)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Include(c => c.LearningOutcomes)
            .Include(c => c.Requirements)
            .Include(c => c.WhatYouWillLearn)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);
    }

    private CourseWizardViewModel MapCourseToWizardViewModel(Course course)
    {
        return new CourseWizardViewModel
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = course.ShortDescription ?? string.Empty,
            Description = course.Description ?? string.Empty,
            CategoryId = course.CategoryId,
            SubCategoryId = course.SubCategoryId,
            Level = course.Level,
            Language = course.Language,
            
            LearningOutcomes = course.LearningOutcomes?
                .OrderBy(o => o.OrderIndex)
                .Select(o => o.Text)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToList() ?? new List<string>(),
            
            Requirements = course.Requirements?
                .OrderBy(r => r.OrderIndex)
                .Select(r => r.Text)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList() ?? new List<string>(),
            
            TargetAudience = course.WhatYouWillLearn?
                .OrderBy(w => w.OrderIndex)
                .Select(w => w.Text)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToList() ?? new List<string>(),
            
            Modules = course.Modules?
                .OrderBy(m => m.OrderIndex)
                .Select(m => new ModuleWizardViewModel
                {
                    Id = m.Id,
                    CourseId = m.CourseId,
                    Title = m.Title,
                    Description = m.Description,
                    OrderIndex = m.OrderIndex,
                    IsPublished = m.IsPublished,
                    Lessons = m.Lessons?
                        .OrderBy(l => l.OrderIndex)
                        .Select(l => new LessonWizardViewModel
                        {
                            Id = l.Id,
                            ModuleId = l.ModuleId,
                            Title = l.Title,
                            Description = l.Description,
                            Type = l.Type,
                            VideoUrl = l.VideoUrl,
                            VideoProvider = l.VideoProvider,
                            DurationSeconds = l.DurationSeconds,
                            OrderIndex = l.OrderIndex,
                            IsPreviewable = l.IsPreviewable,
                            IsDownloadable = l.IsDownloadable,
                            MustComplete = l.MustComplete,
                            AvailableAfterDays = l.AvailableAfterDays,
                            AvailableFrom = l.AvailableFrom
                        }).ToList() ?? new List<LessonWizardViewModel>()
                }).ToList() ?? new List<ModuleWizardViewModel>(),
            
            ThumbnailUrl = course.ThumbnailUrl,
            PreviewVideoUrl = course.PreviewVideoUrl,
            PreviewVideoProvider = course.PreviewVideoProvider,
            
            IsFree = course.IsFree,
            Price = course.Price,
            DiscountPrice = course.DiscountPrice,
            DiscountStartDate = course.DiscountStartDate,
            DiscountEndDate = course.DiscountEndDate,
            Currency = course.Currency,
            
            HasCertificate = course.HasCertificate,
            AllowDiscussions = course.AllowDiscussions,
            AllowReviews = course.AllowReviews,
            EnableContentDrip = course.EnableContentDrip,
            EnableWatermark = course.EnableWatermark,
            PreventDownload = course.PreventDownload,
            
            MetaTitle = course.MetaTitle,
            MetaDescription = course.MetaDescription,
            MetaKeywords = course.MetaKeywords,
            
            DesiredStatus = course.Status
        };
    }

    /// <summary>
    /// تحديد أفضل خطوة للبدء عند التعديل - Determine best starting step for editing
    /// </summary>
    private int DetermineBestStartingStep(CourseWizardViewModel model)
    {
        // Step 1: Basic Information
        if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length < 10 ||
            string.IsNullOrWhiteSpace(model.ShortDescription) || model.ShortDescription.Length < 20 ||
            string.IsNullOrWhiteSpace(model.Description) || model.Description.Length < 100 ||
            model.CategoryId <= 0)
        {
            return 1;
        }

        // Step 2: Learning Content
        var validOutcomes = model.LearningOutcomes?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? new List<string>();
        var validRequirements = model.Requirements?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new List<string>();
        if (validOutcomes.Count < 3 || validRequirements.Count < 1)
        {
            return 2;
        }

        // Step 3: Course Content (Modules & Lessons)
        if (model.TotalModulesCount < 1 || model.TotalLessonsCount < 3)
        {
            return 3;
        }

        // Step 4: Media
        if (string.IsNullOrWhiteSpace(model.ThumbnailUrl))
        {
            return 4;
        }

        // Step 5: Pricing
        if (!model.IsFree && model.Price <= 0)
        {
            return 5;
        }

        // All steps complete, start at review step
        return 7;
    }

    private void UpdateCourseFromWizard(Course course, CourseWizardViewModel model, int step)
    {
        // Step 1: Basic Info
        if (step >= 1)
        {
            if (!string.IsNullOrWhiteSpace(model.Title))
                course.Title = model.Title;
            if (!string.IsNullOrWhiteSpace(model.ShortDescription))
                course.ShortDescription = model.ShortDescription;
            if (!string.IsNullOrWhiteSpace(model.Description))
                course.Description = model.Description;
            if (model.CategoryId > 0)
                course.CategoryId = model.CategoryId;
            course.SubCategoryId = model.SubCategoryId;
            course.Level = model.Level;
            if (!string.IsNullOrWhiteSpace(model.Language))
                course.Language = model.Language;
        }

        // Step 4: Media - ONLY when on step 4 (to prevent overwrite from other steps)
        if (step == 4)
        {
            if (!string.IsNullOrWhiteSpace(model.ThumbnailUrl))
                course.ThumbnailUrl = model.ThumbnailUrl;
            course.PreviewVideoUrl = model.PreviewVideoUrl;
            course.PreviewVideoProvider = model.PreviewVideoProvider;
        }

        // Step 5: Pricing - ONLY when on step 5 (to prevent reset from other steps)
        if (step == 5)
        {
            course.IsFree = model.IsFree;
            course.Price = model.IsFree ? 0 : model.Price;
            course.DiscountPrice = model.DiscountPrice;
            course.DiscountStartDate = model.DiscountStartDate;
            course.DiscountEndDate = model.DiscountEndDate;
            course.Currency = model.Currency ?? "EGP";
        }

        // Step 6: Settings - ONLY when on step 6
        if (step == 6)
        {
            course.HasCertificate = model.HasCertificate;
            course.AllowDiscussions = model.AllowDiscussions;
            course.AllowReviews = model.AllowReviews;
            course.EnableContentDrip = model.EnableContentDrip;
            course.EnableWatermark = model.EnableWatermark;
            course.PreventDownload = model.PreventDownload;
        }

        // Step 7: SEO - ONLY when on step 7
        if (step == 7)
        {
            course.MetaTitle = model.MetaTitle;
            course.MetaDescription = model.MetaDescription;
            course.MetaKeywords = model.MetaKeywords;
        }
    }

    private void UpdateCourseField(Course course, string field, string value)
    {
        switch (field.ToLower())
        {
            case "title":
                course.Title = value;
                break;
            case "shortdescription":
                course.ShortDescription = value;
                break;
            case "description":
                course.Description = value;
                break;
            case "categoryid":
                if (int.TryParse(value, out var catId))
                    course.CategoryId = catId;
                break;
            case "subcategoryid":
                course.SubCategoryId = int.TryParse(value, out var subCatId) ? subCatId : null;
                break;
            case "level":
                if (Enum.TryParse<CourseLevel>(value, out var level))
                    course.Level = level;
                break;
            case "language":
                course.Language = value;
                break;
            case "thumbnailurl":
                course.ThumbnailUrl = value;
                break;
            case "previewvideourl":
                course.PreviewVideoUrl = value;
                break;
            case "isfree":
                course.IsFree = bool.TryParse(value, out var isFree) && isFree;
                break;
            case "price":
                if (decimal.TryParse(value, out var price))
                    course.Price = price;
                break;
        }
    }

    private async Task SaveLearningOutcomes(int courseId, List<string> outcomes)
    {
        // Remove existing
        var existing = await _context.Set<CourseLearningOutcome>()
            .Where(o => o.CourseId == courseId)
            .ToListAsync();
        _context.RemoveRange(existing);

        // Add new
        var orderIndex = 0;
        foreach (var outcome in outcomes.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            _context.Set<CourseLearningOutcome>().Add(new CourseLearningOutcome
            {
                CourseId = courseId,
                Description = outcome.Trim(),
                OrderIndex = orderIndex++
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SaveRequirements(int courseId, List<string> requirements)
    {
        // Remove existing
        var existing = await _context.Set<CourseRequirement>()
            .Where(r => r.CourseId == courseId)
            .ToListAsync();
        _context.RemoveRange(existing);

        // Add new
        var orderIndex = 0;
        foreach (var requirement in requirements.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            _context.Set<CourseRequirement>().Add(new CourseRequirement
            {
                CourseId = courseId,
                Description = requirement.Trim(),
                OrderIndex = orderIndex++
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SaveTargetAudience(int courseId, List<string> audience)
    {
        // Remove existing
        var existing = await _context.Set<CourseWhatYouWillLearn>()
            .Where(w => w.CourseId == courseId)
            .ToListAsync();
        _context.RemoveRange(existing);

        // Add new
        var orderIndex = 0;
        foreach (var item in audience.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            _context.Set<CourseWhatYouWillLearn>().Add(new CourseWhatYouWillLearn
            {
                CourseId = courseId,
                Description = item.Trim(),
                OrderIndex = orderIndex++
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task<IActionResult> HandleWizardPublish(int courseId)
    {
        var userId = _currentUserService.UserId;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);
        if (instructorProfile == null || instructorProfile.Status != "Approved")
        {
            _logger.LogWarning("HandleWizardPublish: Instructor {InstructorId} not approved. Status: {Status}",
                userId, instructorProfile?.Status ?? "NoProfile");
            SetErrorMessage(instructorProfile == null
                ? "لم يتم العثور على ملف المدرس. يرجى إكمال ملفك الشخصي أولاً."
                : $"يجب أن يكون حسابك معتمداً لنشر الدورات. الحالة الحالية: {instructorProfile.Status}");
            return RedirectToAction("Index", "Profile");
        }

        var course = await _context.Courses
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Include(c => c.LearningOutcomes)
            .Include(c => c.Requirements)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

        if (course == null)
        {
            SetErrorMessage("الدورة غير موجودة");
            return RedirectToAction(nameof(Index));
        }

        // Validate for publishing
        var moduleCount = course.Modules.Count;
        var lessonCount = course.Modules.Sum(m => m.Lessons.Count);
        var hasThumbnail = !string.IsNullOrEmpty(course.ThumbnailUrl);
        var hasPrice = course.Price > 0;
        // Filter out empty values to ensure accurate count
        var learningOutcomesCount = course.LearningOutcomes?.Count(o => !string.IsNullOrWhiteSpace(o.Text)) ?? 0;
        var requirementsCount = course.Requirements?.Count(r => !string.IsNullOrWhiteSpace(r.Text)) ?? 0;

        var validationErrors = new List<string>();

        if (moduleCount < 1)
            validationErrors.Add("يجب إضافة وحدة واحدة على الأقل");
        if (lessonCount < 3)
            validationErrors.Add("يجب إضافة 3 دروس على الأقل");
        if (!hasThumbnail)
            validationErrors.Add("يجب إضافة صورة للدورة");
        if (!course.IsFree && !hasPrice)
            validationErrors.Add("يجب تحديد سعر للدورة أو جعلها مجانية");
        if (learningOutcomesCount < 3)
            validationErrors.Add("يجب إضافة 3 نقاط تعلم على الأقل");
        if (requirementsCount < 1)
            validationErrors.Add("يجب إضافة متطلب واحد على الأقل");

        if (validationErrors.Any())
        {
            SetErrorMessage("لا يمكن نشر الدورة:\n" + string.Join("\n• ", validationErrors));
            return RedirectToAction(nameof(CreateWizard), new { step = 7, id = courseId });
        }

        // Submit for review or publish based on configuration
        course.Status = CourseStatus.PendingReview;
        course.SubmittedForReviewAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Course {CourseId} submitted for review via wizard by {InstructorId}",
            courseId, userId);

        SetSuccessMessage("تم إرسال الدورة للمراجعة بنجاح!");
        return RedirectToAction(nameof(Details), new { id = courseId });
    }

    private async Task PopulateWizardDropdowns(int selectedCategoryId = 0)
    {
        // Categories
        var categories = await _context.Categories
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new CategorySelectItem
            {
                Id = c.Id,
                Name = c.Name,
                SubCategories = _context.Categories
                    .Where(sc => sc.ParentCategoryId == c.Id && !sc.IsDeleted)
                    .Select(sc => new CategorySelectItem
                    {
                        Id = sc.Id,
                        Name = sc.Name,
                        ParentId = c.Id
                    }).ToList()
            })
            .ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategoryId);
        ViewBag.CategoriesWithSub = categories;

        // Subcategories for selected category
        if (selectedCategoryId > 0)
        {
            var subcategories = await _context.Categories
                .Where(c => c.ParentCategoryId == selectedCategoryId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewBag.SubCategories = new SelectList(subcategories, "Id", "Name");
        }
        else
        {
            ViewBag.SubCategories = new SelectList(Enumerable.Empty<object>());
        }

        // Levels
        ViewBag.Levels = Enum.GetValues<CourseLevel>()
            .Select(l => new SelectListItem
            {
                Value = l.ToString(),
                Text = GetLevelName(l)
            });

        // Languages
        ViewBag.Languages = new List<SelectListItem>
        {
            new("العربية", "ar"),
            new("English", "en"),
            new("Français", "fr")
        };

        // Lesson Types
        ViewBag.LessonTypes = Enum.GetValues<LessonType>()
            .Select(t => new SelectListItem
            {
                Value = t.ToString(),
                Text = GetLessonTypeName(t)
            });
    }

    private List<CourseReadinessItem> GetReadinessChecklist(CourseWizardViewModel model)
    {
        return new List<CourseReadinessItem>
        {
            new()
            {
                Name = "title",
                NameArabic = "عنوان الدورة",
                IsCompleted = !string.IsNullOrWhiteSpace(model.Title) && model.Title.Length >= 10,
                IsRequired = true,
                Category = "basic"
            },
            new()
            {
                Name = "description",
                NameArabic = "الوصف التفصيلي",
                IsCompleted = !string.IsNullOrWhiteSpace(model.Description) && model.Description.Length >= 100,
                IsRequired = true,
                Category = "basic"
            },
            new()
            {
                Name = "category",
                NameArabic = "التصنيف",
                IsCompleted = model.CategoryId > 0,
                IsRequired = true,
                Category = "basic"
            },
            new()
            {
                Name = "outcomes",
                NameArabic = "نقاط التعلم (3 على الأقل)",
                IsCompleted = (model.LearningOutcomes?.Count(o => !string.IsNullOrWhiteSpace(o)) ?? 0) >= 3,
                IsRequired = true,
                Category = "content"
            },
            new()
            {
                Name = "requirements",
                NameArabic = "المتطلبات",
                IsCompleted = (model.Requirements?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0) >= 1,
                IsRequired = true,
                Category = "content"
            },
            new()
            {
                Name = "modules",
                NameArabic = "وحدة واحدة على الأقل",
                IsCompleted = model.TotalModulesCount >= 1,
                IsRequired = true,
                Category = "content"
            },
            new()
            {
                Name = "lessons",
                NameArabic = "3 دروس على الأقل",
                IsCompleted = model.TotalLessonsCount >= 3,
                IsRequired = true,
                Category = "content"
            },
            new()
            {
                Name = "thumbnail",
                NameArabic = "صورة الدورة",
                IsCompleted = !string.IsNullOrWhiteSpace(model.ThumbnailUrl),
                IsRequired = true,
                Category = "media"
            },
            new()
            {
                Name = "preview",
                NameArabic = "فيديو المعاينة",
                IsCompleted = !string.IsNullOrWhiteSpace(model.PreviewVideoUrl),
                IsRequired = false,
                HelpText = "يُنصح بإضافة فيديو تعريفي",
                Category = "media"
            },
            new()
            {
                Name = "price",
                NameArabic = "التسعير",
                IsCompleted = model.IsFree || model.Price > 0,
                IsRequired = true,
                Category = "pricing"
            }
        };
    }

    private static string GetLevelName(CourseLevel level)
    {
        return level switch
        {
            CourseLevel.Beginner => "مبتدئ",
            CourseLevel.Intermediate => "متوسط",
            CourseLevel.Advanced => "متقدم",
            CourseLevel.AllLevels => "جميع المستويات",
            _ => level.ToString()
        };
    }

    private static string GetLessonTypeName(LessonType type)
    {
        return type switch
        {
            LessonType.Video => "فيديو",
            LessonType.Text => "نص",
            LessonType.Article => "مقال",
            LessonType.Quiz => "اختبار",
            LessonType.Assignment => "تكليف",
            LessonType.Download => "ملف للتحميل",
            LessonType.LiveClass => "بث مباشر",
            LessonType.Audio => "صوتي",
            LessonType.PDF => "ملف PDF",
            LessonType.Interactive => "محتوى تفاعلي",
            LessonType.ExternalLink => "رابط خارجي",
            _ => type.ToString()
        };
    }

    private static string GetLessonTypeIcon(LessonType type)
    {
        return type switch
        {
            LessonType.Video => "video",
            LessonType.Text => "file-text",
            LessonType.Article => "file-text",
            LessonType.Quiz => "help-circle",
            LessonType.Assignment => "edit",
            LessonType.Download => "download",
            LessonType.LiveClass => "video",
            LessonType.Audio => "headphones",
            LessonType.PDF => "file",
            LessonType.Interactive => "box",
            LessonType.ExternalLink => "external-link",
            _ => "file"
        };
    }

    #endregion

    /// <summary>
    /// تحديث إحصائيات الدورة - Update course statistics (modules and lessons count)
    /// </summary>
    private async Task UpdateCourseStatistics(int courseId)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course != null)
            {
                // Count only valid modules (with titles)
                course.TotalModules = course.Modules?.Count(m => !string.IsNullOrWhiteSpace(m.Title)) ?? 0;
                
                // Count only valid lessons (with titles)
                course.TotalLessons = course.Modules?
                    .Sum(m => m.Lessons?.Count(l => !string.IsNullOrWhiteSpace(l.Title)) ?? 0) ?? 0;

                // Update total duration
                course.TotalDurationMinutes = course.Modules?
                    .Sum(m => m.Lessons?.Sum(l => l.DurationSeconds / 60) ?? 0) ?? 0;

                await _context.SaveChangesAsync();

                _logger.LogDebug("Updated course statistics for course {CourseId}: {ModuleCount} modules, {LessonCount} lessons, {Duration} minutes",
                    courseId, course.TotalModules, course.TotalLessons, course.TotalDurationMinutes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course statistics for course {CourseId}", courseId);
            // Don't throw - this is a non-critical operation
        }
    }

    private async Task PopulateCategoriesAsync()
    {
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => c.ParentCategoryId == null && !c.IsDeleted).ToListAsync(),
            "Id", "Name");
    }

    private static (string provider, string? videoId) DetectVideoProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ("Local", null);
        
        url = url.Trim();
        
        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
        {
            string? videoId = null;
            if (url.Contains("youtu.be/"))
            {
                videoId = url.Split("youtu.be/").LastOrDefault()?.Split('?').FirstOrDefault();
            }
            else if (url.Contains("v="))
            {
                videoId = url.Split("v=").LastOrDefault()?.Split('&').FirstOrDefault();
            }
            else if (url.Contains("/embed/"))
            {
                videoId = url.Split("/embed/").LastOrDefault()?.Split('?').FirstOrDefault();
            }
            return ("YouTube", videoId);
        }
        
        if (url.Contains("vimeo.com"))
        {
            var match = Regex.Match(url, @"vimeo\.com\/(\d+)");
            var videoId = match.Success ? match.Groups[1].Value : null;
            return ("Vimeo", videoId);
        }
        
        return ("Local", null);
    }
}
