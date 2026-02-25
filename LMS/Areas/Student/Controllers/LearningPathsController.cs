using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// المسارات التعليمية - Learning Paths Controller
/// </summary>
public class LearningPathsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LearningPathsController> _logger;

    public LearningPathsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<LearningPathsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المسارات (Route: /Student/LearningPath) - Learning paths list
    /// </summary>
    [Route("[area]/LearningPath")]
    [Route("[area]/[controller]")]
    public async Task<IActionResult> Index(string? level, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.LearningPaths
            .Include(lp => lp.Courses)
            .Where(lp => lp.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(level) && Enum.TryParse<Domain.Enums.CourseLevel>(level, out var parsedLevel))
        {
            query = query.Where(lp => lp.Level == parsedLevel);
        }

        var paths = await query
            .OrderBy(lp => lp.DisplayOrder)
            .Skip((page - 1) * 12)
            .Take(12)
            .ToListAsync();

        // Check enrollment status for each path
        var pathViewModels = new List<StudentLearningPathViewModel>();
        
        foreach (var path in paths)
        {
            var courseIds = path.Courses.Select(c => c.CourseId).ToList();
            var enrolledCourses = await _context.Enrollments
                .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
                .ToListAsync();

            var completedCount = enrolledCourses.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed);

            pathViewModels.Add(new StudentLearningPathViewModel
            {
                Id = path.Id,
                Name = path.Name,
                Description = path.Description,
                ThumbnailUrl = path.ThumbnailUrl,
                Level = path.Level.ToString(),
                EstimatedDurationHours = path.EstimatedDurationHours,
                CoursesCount = path.CoursesCount,
                Price = path.Price ?? 0,
                Currency = path.Currency ?? "EGP",
                IsEnrolled = enrolledCourses.Any(),
                CompletedCourses = completedCount,
                ProgressPercentage = path.CoursesCount > 0 ? (decimal)completedCount / path.CoursesCount * 100 : 0
            });
        }

        ViewBag.Level = level;
        ViewBag.Page = page;

        return View(pathViewModels);
    }

    /// <summary>
    /// تفاصيل المسار - Learning path details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Category)
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
                    .ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.IsActive);

        if (path == null)
            return NotFound();

        // Check enrollment and progress
        var courseIds = path.Courses.Select(c => c.CourseId).ToList();
        var enrollments = await _context.Enrollments
            .Include(e => e.LessonProgress)
            .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
            .ToListAsync();

        var courseProgresses = new List<CourseProgressInPath>();

        foreach (var pathCourse in path.Courses.OrderBy(c => c.OrderIndex))
        {
            var enrollment = enrollments.FirstOrDefault(e => e.CourseId == pathCourse.CourseId);

            courseProgresses.Add(new CourseProgressInPath
            {
                CourseId = pathCourse.CourseId,
                CourseName = pathCourse.Course.Title,
                OrderIndex = pathCourse.OrderIndex,
                IsEnrolled = enrollment != null,
                IsCompleted = enrollment?.Status == Domain.Enums.EnrollmentStatus.Completed,
                ProgressPercentage = enrollment?.ProgressPercentage ?? 0
            });
        }

        var pathEnrollment = await _context.LearningPathEnrollments
            .FirstOrDefaultAsync(e => e.StudentId == userId && e.LearningPathId == id);

        var completedCourseIds = courseProgresses
            .Where(c => c.IsCompleted)
            .Select(c => c.CourseId)
            .ToList();

        var totalTimeSpent = enrollments
            .Where(e => e.LessonProgress != null)
            .SelectMany(e => e.LessonProgress)
            .Sum(lp => lp.TotalTimeSpentSeconds) / 3600.0;

        var viewModel = new LearningPathProgressViewModel
        {
            PathId = path.Id,
            PathName = path.Name,
            TotalCourses = path.CoursesCount,
            CompletedCourses = courseProgresses.Count(c => c.IsCompleted),
            InProgressCourses = courseProgresses.Count(c => c.IsEnrolled && !c.IsCompleted),
            OverallProgress = courseProgresses.Any() 
                ? courseProgresses.Average(c => c.ProgressPercentage) 
                : 0,
            Courses = courseProgresses
        };

        ViewBag.LearningPath = path;
        ViewBag.Enrollment = pathEnrollment != null ? new
        {
            Progress = pathEnrollment.ProgressPercentage,
            CompletedCourses = completedCourseIds,
            CompletedCoursesCount = pathEnrollment.CompletedCoursesCount,
            CurrentCourseId = pathEnrollment.CurrentCourseId,
            TimeSpent = (int)totalTimeSpent,
            StartedAt = pathEnrollment.EnrolledAt
        } : null;

        return View(viewModel);
    }

    /// <summary>
    /// بدء المسار - Start learning path
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartPath(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var path = await _context.LearningPaths
                .Include(lp => lp.Courses.OrderBy(c => c.OrderIndex))
                    .ThenInclude(lpc => lpc.Course)
                .FirstOrDefaultAsync(lp => lp.Id == id && lp.IsActive);

            if (path == null)
            {
                SetErrorMessage("المسار غير موجود أو غير متاح");
                return RedirectToAction(nameof(Index));
            }

            // Check if user already has enrollments in the path
            var courseIds = path.Courses.Select(c => c.CourseId).ToList();
            var existingEnrollments = await _context.Enrollments
                .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
                .ToListAsync();

            // If no enrollments, create a path enrollment record
            var pathEnrollment = await _context.LearningPathEnrollments
                .FirstOrDefaultAsync(e => e.StudentId == userId && e.LearningPathId == id);

            if (pathEnrollment == null)
            {
                pathEnrollment = new Domain.Entities.Learning.LearningPathEnrollment
                {
                    StudentId = userId,
                    LearningPathId = id,
                    EnrolledAt = DateTime.UtcNow,
                    Status = Domain.Enums.EnrollmentStatus.Active.ToString().ToLowerInvariant(),
                    CurrentCourseId = path.Courses.OrderBy(c => c.OrderIndex).FirstOrDefault()?.CourseId
                };
                _context.LearningPathEnrollments.Add(pathEnrollment);
                await _context.SaveChangesAsync();
            }

            // Find the first course that needs to be started
            var firstCourse = path.Courses.OrderBy(c => c.OrderIndex).FirstOrDefault();
            
            if (firstCourse == null)
            {
                SetErrorMessage("لا توجد دورات في هذا المسار");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if enrolled in the first course
            var firstCourseEnrollment = existingEnrollments.FirstOrDefault(e => e.CourseId == firstCourse.CourseId);
            
            if (firstCourseEnrollment == null)
            {
                // Need to enroll in the first course first
                if (firstCourse.Course.Price == 0 || firstCourse.Course.IsFree)
                {
                    // Free course - enroll directly
                    var enrollment = new Domain.Entities.Learning.Enrollment
                    {
                        StudentId = userId,
                        CourseId = firstCourse.CourseId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = Domain.Enums.EnrollmentStatus.Active,
                        IsFree = true
                    };
                    _context.Enrollments.Add(enrollment);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Auto-enrolled student {StudentId} in free course {CourseId} from path {PathId}", 
                        userId, firstCourse.CourseId, id);
                    
                    // Redirect to learn the course
                    return RedirectToAction("Learn", "Courses", new { id = firstCourse.CourseId });
                }
                else
                {
                    // Paid course - redirect to preview/purchase
                    SetInfoMessage("يرجى التسجيل في الدورة الأولى للبدء في المسار");
                    return RedirectToAction("Preview", "Courses", new { id = firstCourse.CourseId });
                }
            }

            // Already enrolled - redirect to continue learning
            return RedirectToAction("Learn", "Courses", new { id = firstCourse.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting learning path {PathId} for student {StudentId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء بدء المسار التعليمي");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// متابعة التعلم - Continue learning path (redirect to current/next course)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Continue(int pathId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses.OrderBy(c => c.OrderIndex))
                .ThenInclude(lpc => lpc.Course)
            .FirstOrDefaultAsync(lp => lp.Id == pathId && lp.IsActive);

        if (path == null)
        {
            SetErrorMessage("المسار غير موجود أو غير متاح");
            return RedirectToAction(nameof(Index));
        }

        var courseIds = path.Courses.Select(c => c.CourseId).ToList();
        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
            .ToListAsync();

        // Find next course to learn: current (in progress) or first not completed
        var orderedCourses = path.Courses.OrderBy(c => c.OrderIndex).ToList();
        foreach (var pathCourse in orderedCourses)
        {
            var enrollment = enrollments.FirstOrDefault(e => e.CourseId == pathCourse.CourseId);
            if (enrollment == null)
            {
                if (pathCourse.Course.Price == 0 || pathCourse.Course.IsFree)
                {
                    var newEnrollment = new Domain.Entities.Learning.Enrollment
                    {
                        StudentId = userId,
                        CourseId = pathCourse.CourseId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = Domain.Enums.EnrollmentStatus.Active,
                        IsFree = true
                    };
                    _context.Enrollments.Add(newEnrollment);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Learn", "Courses", new { id = pathCourse.CourseId });
                }
                SetInfoMessage("يرجى التسجيل في الدورة التالية للاستمرار");
                return RedirectToAction("Preview", "Courses", new { id = pathCourse.CourseId });
            }
            if (enrollment.Status != Domain.Enums.EnrollmentStatus.Completed)
                return RedirectToAction("Learn", "Courses", new { id = pathCourse.CourseId });
        }

        SetSuccessMessage("لقد أكملت هذا المسار! مبروك.");
        return RedirectToAction(nameof(Details), new { id = pathId });
    }

    /// <summary>
    /// التسجيل في المسار - Enroll in learning path
    /// Enrolls the user in the first course of the path (or all free courses)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int pathId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var path = await _context.LearningPaths
                .Include(lp => lp.Courses)
                    .ThenInclude(lpc => lpc.Course)
                .FirstOrDefaultAsync(lp => lp.Id == pathId && lp.IsActive);

            if (path == null)
            {
                SetErrorMessage("المسار غير موجود");
                return RedirectToAction(nameof(Index));
            }

            // Get courses in order
            var orderedCourses = path.Courses.OrderBy(c => c.OrderIndex).ToList();
            
            if (!orderedCourses.Any())
            {
                SetErrorMessage("لا توجد دورات في هذا المسار");
                return RedirectToAction(nameof(Details), new { id = pathId });
            }

            // Check existing enrollments
            var courseIds = orderedCourses.Select(c => c.CourseId).ToList();
            var existingEnrollments = await _context.Enrollments
                .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
                .Select(e => e.CourseId)
                .ToListAsync();

            var enrolledCount = 0;

            // Enroll in free courses or the first course
            foreach (var pathCourse in orderedCourses)
            {
                if (existingEnrollments.Contains(pathCourse.CourseId))
                    continue;

                // For now, only enroll in free courses automatically
                if (pathCourse.Course.Price == 0)
                {
                    var enrollment = new Domain.Entities.Learning.Enrollment
                    {
                        StudentId = userId,
                        CourseId = pathCourse.CourseId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = Domain.Enums.EnrollmentStatus.Active,
                        ProgressPercentage = 0,
                        IsFree = true
                    };

                    _context.Enrollments.Add(enrollment);
                    enrolledCount++;
                }
            }

            if (enrolledCount > 0)
            {
                await _context.SaveChangesAsync();
                SetSuccessMessage($"تم تسجيلك في {enrolledCount} دورة مجانية من المسار بنجاح");
                _logger.LogInformation("Student {StudentId} enrolled in {Count} courses from learning path {PathId}", 
                    userId, enrolledCount, pathId);
            }
            else if (existingEnrollments.Any())
            {
                SetInfoMessage("أنت مسجل بالفعل في دورات هذا المسار");
            }
            else
            {
                // No free courses, redirect to first course for purchase
                var firstCourse = orderedCourses.First();
                SetInfoMessage("يرجى شراء الدورات المدفوعة للتسجيل في هذا المسار");
                return RedirectToAction("Preview", "Courses", new { id = firstCourse.CourseId });
            }

            return RedirectToAction(nameof(Details), new { id = pathId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling student {StudentId} in learning path {PathId}", userId, pathId);
            SetErrorMessage("حدث خطأ أثناء التسجيل في المسار");
            return RedirectToAction(nameof(Details), new { id = pathId });
        }
    }

    /// <summary>
    /// المسارات المسجل بها - My learning paths
    /// </summary>
    public async Task<IActionResult> MyPaths()
    {
        var userId = _currentUserService.UserId;

        // Get all paths where user has enrolled in at least one course
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Select(e => e.CourseId)
            .ToListAsync();

        var paths = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .Where(lp => lp.Courses.Any(c => enrolledCourseIds.Contains(c.CourseId)))
            .ToListAsync();

        var pathViewModels = new List<StudentLearningPathViewModel>();

        foreach (var path in paths)
        {
            var courseIds = path.Courses.Select(c => c.CourseId).ToList();
            var enrolledCourses = await _context.Enrollments
                .Where(e => e.StudentId == userId && courseIds.Contains(e.CourseId))
                .ToListAsync();

            var completedCount = enrolledCourses.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed);

            pathViewModels.Add(new StudentLearningPathViewModel
            {
                Id = path.Id,
                Name = path.Name,
                Description = path.Description,
                ThumbnailUrl = path.ThumbnailUrl,
                Level = path.Level.ToString(),
                EstimatedDurationHours = path.EstimatedDurationHours,
                CoursesCount = path.CoursesCount,
                Price = path.Price ?? 0,
                Currency = path.Currency ?? "EGP",
                IsEnrolled = true,
                CompletedCourses = completedCount,
                ProgressPercentage = path.CoursesCount > 0 ? (decimal)completedCount / path.CoursesCount * 100 : 0
            });
        }

        return View(pathViewModels);
    }
}

