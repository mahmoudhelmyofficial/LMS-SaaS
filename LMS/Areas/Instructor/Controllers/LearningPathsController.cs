using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة مسارات التعلم للمدرس - Instructor Learning Paths Controller
/// </summary>
public class LearningPathsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;
    private readonly ICurrencyService _currencyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LearningPathsController> _logger;

    public LearningPathsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService,
        ICurrencyService currencyService,
        IMemoryCache cache,
        ILogger<LearningPathsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
        _currencyService = currencyService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// قائمة مسارات التعلم - Learning paths list
    /// </summary>
    public async Task<IActionResult> Index(bool? isPublished, int page = 1)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;

        var query = _context.LearningPaths
            .Include(lp => lp.Courses)
            .Where(lp => lp.OwnerId == userId);

        if (isPublished.HasValue)
        {
            query = query.Where(lp => lp.IsPublished == isPublished.Value);
        }

        var paths = await query
            .OrderByDescending(lp => lp.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.IsPublished = isPublished;
        ViewBag.Page = page;

        return View(paths);
    }

    /// <summary>
    /// تفاصيل مسار التعلم - Learning path details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
            .Include(lp => lp.Enrollments)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        return View(path);
    }

    /// <summary>
    /// إنشاء مسار تعلم جديد - Create new learning path
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        await PopulateInstructorCoursesDropdown();
        return View(new LearningPathCreateViewModel());
    }

    /// <summary>
    /// حفظ مسار التعلم الجديد - Save new learning path
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LearningPathCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
            
            // Validate required fields first
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "اسم المسار مطلوب");
            }
            else if (model.Name.Length < 3)
            {
                ModelState.AddModelError("Name", "اسم المسار يجب أن يكون 3 أحرف على الأقل");
            }
            else if (model.Name.Length > 200)
            {
                ModelState.AddModelError("Name", "اسم المسار يجب أن لا يتجاوز 200 حرف");
            }

            if (model.CourseIds == null || !model.CourseIds.Any())
            {
                ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
            }
            else if (model.CourseIds.Count < 2)
            {
                ModelState.AddModelError("CourseIds", "مسار التعلم يجب أن يحتوي على دورتين على الأقل");
            }

            // Validate price if not free
            if (!model.IsFree)
            {
                if (!model.Price.HasValue || model.Price <= 0)
                {
                    ModelState.AddModelError("Price", "يرجى تحديد سعر صالح للمسار أو اختيار مجاني");
                }
                if (model.DiscountedPrice.HasValue && model.DiscountedPrice >= model.Price)
                {
                    ModelState.AddModelError("DiscountedPrice", "السعر المخفض يجب أن يكون أقل من السعر الأصلي");
                }
            }

            // Only proceed with database checks if basic validation passes
            if (model.CourseIds != null && model.CourseIds.Any())
            {
                // Verify all courses belong to the instructor
                var instructorCourseIds = await _context.Courses
                    .Where(c => c.InstructorId == userId && model.CourseIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (instructorCourseIds.Count != model.CourseIds.Count)
                {
                    ModelState.AddModelError("CourseIds", "لا يمكنك إضافة دورات لا تملكها");
                }
            }

            if (!ModelState.IsValid)
            {
                // Log validation errors for debugging
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Learning path creation validation failed for instructor {InstructorId}: {Errors}", 
                    userId, string.Join("; ", errors));
                
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Calculate total duration
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var estimatedHours = courses.Sum(c => c.TotalDurationMinutes) / 60;

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateLearningPath(
                model.Name,
                model.Description ?? string.Empty,
                model.CourseIds.Count,
                estimatedHours);

            if (!isValid)
            {
                _logger.LogWarning("Learning path validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Generate unique slug
            var slug = _slugService.GenerateSlug(model.Name);
            var slugExists = await _context.LearningPaths.AnyAsync(lp => lp.Slug == slug);
            if (slugExists)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
            }

            LearningPath? path = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                path = new LearningPath
                {
                    Name = model.Name,
                    Slug = slug,
                    Description = model.Description,
                    ShortDescription = model.ShortDescription,
                    ThumbnailUrl = model.ThumbnailUrl,
                    BannerUrl = model.BannerUrl,
                    Level = model.Level,
                    EstimatedHours = estimatedHours,
                    Price = model.Price,
                    DiscountedPrice = model.DiscountedPrice,
                    Currency = "EGP",
                    IsFree = model.IsFree,
                    IsPublished = model.IsPublished,
                    IsFeatured = false, // Only admins can feature
                    CoursesCount = model.CourseIds.Count,
                    OwnerId = userId,
                    DisplayOrder = model.DisplayOrder
                };

                _context.LearningPaths.Add(path);
                await _context.SaveChangesAsync();

                // Add courses to path
                for (int i = 0; i < model.CourseIds.Count; i++)
                {
                    var pathCourse = new LearningPathCourse
                    {
                        LearningPathId = path.Id,
                        CourseId = model.CourseIds[i],
                        OrderIndex = i,
                        IsOptional = false
                    };
                    _context.LearningPathCourses.Add(pathCourse);
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Learning path {PathId} '{Name}' created by instructor {InstructorId}. Courses: {CourseCount}",
                path!.Id, path.Name, userId, model.CourseIds.Count);

            SetSuccessMessage("تم إنشاء مسار التعلم بنجاح");
            return RedirectToAction(nameof(Details), new { id = path.Id });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error creating learning path for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ في قاعدة البيانات أثناء إنشاء المسار. يرجى التحقق من البيانات والمحاولة مرة أخرى.");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating learning path for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ غير متوقع أثناء إنشاء مسار التعلم. يرجى المحاولة مرة أخرى.");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
    }

    /// <summary>
    /// تعديل مسار التعلم - Edit learning path
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        var viewModel = new LearningPathEditViewModel
        {
            Id = path.Id,
            Name = path.Name,
            Description = path.Description,
            ShortDescription = path.ShortDescription,
            ThumbnailUrl = path.ThumbnailUrl,
            BannerUrl = path.BannerUrl,
            Level = path.Level,
            Price = path.Price,
            DiscountedPrice = path.DiscountedPrice,
            IsFree = path.IsFree,
            IsPublished = path.IsPublished,
            DisplayOrder = path.DisplayOrder,
            CourseIds = path.Courses.OrderBy(c => c.OrderIndex).Select(c => c.CourseId).ToList()
        };

        await PopulateInstructorCoursesDropdown();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات مسار التعلم - Save learning path edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LearningPathEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .Include(lp => lp.Enrollments)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        try
        {
            if (!model.CourseIds.Any())
            {
                ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
            }
            else if (model.CourseIds.Count < 2)
            {
                ModelState.AddModelError("CourseIds", "مسار التعلم يجب أن يحتوي على دورتين على الأقل");
            }

            // Verify all courses belong to the instructor
            var instructorCourseIds = await _context.Courses
                .Where(c => c.InstructorId == userId && model.CourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (instructorCourseIds.Count != model.CourseIds.Count)
            {
                ModelState.AddModelError("CourseIds", "لا يمكنك إضافة دورات لا تملكها");
            }

            if (!ModelState.IsValid)
            {
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Calculate total duration
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var estimatedHours = courses.Sum(c => c.TotalDurationMinutes) / 60;

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateLearningPath(
                model.Name,
                model.Description ?? string.Empty,
                model.CourseIds.Count,
                estimatedHours);

            if (!isValid)
            {
                _logger.LogWarning("Learning path {PathId} validation failed: {Reason}", id, validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Check if path has enrollments - warn if changing critical settings
            if (path.Enrollments != null && path.Enrollments.Any())
            {
                var criticalChanges = new List<string>();
                
                if (path.CoursesCount != model.CourseIds.Count)
                    criticalChanges.Add("عدد الدورات");
                
                if (path.Price != model.Price)
                    criticalChanges.Add("السعر");

                if (criticalChanges.Any())
                {
                    _logger.LogInformation("Learning path {PathId} has enrollments but critical settings changed: {Changes}", 
                        id, string.Join(", ", criticalChanges));
                    SetWarningMessage($"تحذير: المسار به تسجيلات. تم تغيير: {string.Join("، ", criticalChanges)}");
                }
            }

            // Generate unique slug if name changed
            var newSlug = _slugService.GenerateSlug(model.Name);
            if (newSlug != path.Slug)
            {
                var slugExists = await _context.LearningPaths
                    .AnyAsync(lp => lp.Slug == newSlug && lp.Id != id);
                if (slugExists)
                {
                    newSlug = $"{newSlug}-{Guid.NewGuid().ToString()[..8]}";
                }
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                path.Name = model.Name;
                path.Slug = newSlug;
                path.Description = model.Description;
                path.ShortDescription = model.ShortDescription;
                path.ThumbnailUrl = model.ThumbnailUrl;
                path.BannerUrl = model.BannerUrl;
                path.Level = model.Level;
                path.EstimatedHours = estimatedHours;
                path.Price = model.Price;
                path.DiscountedPrice = model.DiscountedPrice;
                path.IsFree = model.IsFree;
                path.IsPublished = model.IsPublished;
                path.CoursesCount = model.CourseIds.Count;
                path.DisplayOrder = model.DisplayOrder;

                // Update path courses - smart approach to avoid duplicates
                var existingCourseIds = path.Courses.Select(c => c.CourseId).ToList();
                var newCourseIds = model.CourseIds.ToList();

                // Remove courses that are no longer selected
                var coursesToRemove = path.Courses
                    .Where(c => !newCourseIds.Contains(c.CourseId))
                    .ToList();
                _context.LearningPathCourses.RemoveRange(coursesToRemove);

                // Update existing courses and add new ones
                for (int i = 0; i < newCourseIds.Count; i++)
                {
                    var courseId = newCourseIds[i];
                    
                    // Check if this course already exists in the path
                    var existingPathCourse = path.Courses.FirstOrDefault(c => c.CourseId == courseId);
                    
                    if (existingPathCourse != null)
                    {
                        // Update order index for existing course
                        existingPathCourse.OrderIndex = i;
                        existingPathCourse.IsOptional = false;
                    }
                    else
                    {
                        // Add new course
                        var pathCourse = new LearningPathCourse
                        {
                            LearningPathId = path.Id,
                            CourseId = courseId,
                            OrderIndex = i,
                            IsOptional = false
                        };
                        _context.LearningPathCourses.Add(pathCourse);
                    }
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Learning path {PathId} '{Name}' updated by instructor {InstructorId}. Courses: {CourseCount}",
                path.Id, path.Name, userId, model.CourseIds.Count);

            SetSuccessMessage("تم تحديث مسار التعلم بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating learning path {PathId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث مسار التعلم");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
    }

    /// <summary>
    /// تغيير حالة النشر - Toggle publish status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        path.IsPublished = !path.IsPublished;
        await _context.SaveChangesAsync();

        SetSuccessMessage(path.IsPublished ? "تم نشر المسار" : "تم إلغاء نشر المسار");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف مسار التعلم - Delete learning path
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .Include(lp => lp.Enrollments)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        if (path.Enrollments.Any())
        {
            SetErrorMessage("لا يمكن حذف المسار لأنه يحتوي على تسجيلات");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.LearningPathCourses.RemoveRange(path.Courses);
        _context.LearningPaths.Remove(path);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف مسار التعلم بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات مسار التعلم - Learning path statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int id)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
            .Include(lp => lp.Enrollments)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        // Generate monthly progress chart data (last 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var progressLabels = new List<string>();
        var progressData = new List<decimal>();
        
        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var monthName = date.ToString("MMMM", new System.Globalization.CultureInfo("ar-SA"));
            progressLabels.Add(monthName);
            
            // Calculate average progress for enrollments up to that month
            var enrollmentsUpToMonth = path.Enrollments?
                .Where(e => e.EnrolledAt <= new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)))
                .ToList() ?? new List<LMS.Domain.Entities.Learning.LearningPathEnrollment>();
            
            var avgProgress = enrollmentsUpToMonth.Any() ? enrollmentsUpToMonth.Average(e => e.ProgressPercentage) : 0;
            progressData.Add((decimal)avgProgress);
        }

        // Generate monthly revenue chart data (last 6 months)
        var revenueLabels = new List<string>();
        var revenueData = new List<decimal>();
        
        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var monthName = date.ToString("MMMM", new System.Globalization.CultureInfo("ar-SA"));
            revenueLabels.Add(monthName);
            
            var monthlyRevenue = path.Enrollments?
                .Where(e => e.EnrolledAt.Year == date.Year && e.EnrolledAt.Month == date.Month)
                .Sum(e => e.PaidAmount) ?? 0;
            revenueData.Add(monthlyRevenue);
        }

        // Calculate course progress and completion rates
        var courseStats = new Dictionary<int, (decimal avgProgress, decimal completionRate)>();
        if (path.Courses != null)
        {
            foreach (var pathCourse in path.Courses)
            {
                var courseEnrollments = await _context.Enrollments
                    .Where(e => e.CourseId == pathCourse.CourseId)
                    .ToListAsync();
                
                var avgProgress = courseEnrollments.Any() ? courseEnrollments.Average(e => e.ProgressPercentage) : 0;
                var completionRate = courseEnrollments.Any() 
                    ? (courseEnrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed) * 100.0m / courseEnrollments.Count) 
                    : 0;
                
                courseStats[pathCourse.CourseId] = ((decimal)avgProgress, completionRate);
            }
        }

        ViewBag.ProgressLabels = progressLabels;
        ViewBag.ProgressData = progressData;
        ViewBag.RevenueLabels = revenueLabels;
        ViewBag.RevenueData = revenueData;
        ViewBag.CourseStats = courseStats;

        return View(path);
    }

    /// <summary>
    /// المسجلون في المسار - Path enrollments
    /// </summary>
    public async Task<IActionResult> Enrollments(int id, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var path = await _context.LearningPaths
            .FirstOrDefaultAsync(lp => lp.Id == id && lp.OwnerId == userId);

        if (path == null)
            return NotFound();

        var enrollments = await _context.LearningPathEnrollments
            .Include(e => e.Student)
            .Where(e => e.LearningPathId == id)
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.PathId = id;
        ViewBag.PathName = path.Name;
        ViewBag.Page = page;

        return View(enrollments);
    }

    private async Task PopulateInstructorCoursesDropdown()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = new MultiSelectList(
            await _context.Courses
                .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");
    }
}

