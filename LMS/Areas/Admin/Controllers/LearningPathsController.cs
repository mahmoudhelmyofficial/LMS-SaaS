using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المسارات التعليمية - Learning Paths Management Controller
/// </summary>
public class LearningPathsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;
    private readonly ILogger<LearningPathsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public LearningPathsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService,
        ILogger<LearningPathsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة المسارات - Learning paths list
    /// </summary>
    public async Task<IActionResult> Index(bool? isActive, int page = 1)
    {
        var query = _context.LearningPaths
            .Include(lp => lp.Courses)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(lp => lp.IsPublished == isActive.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("learning_paths", 20);
        var totalCount = await query.CountAsync();
        var paths = await query
            .OrderBy(lp => lp.DisplayOrder)
            .ThenByDescending(lp => lp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(lp => new LearningPathDisplayViewModel
            {
                Id = lp.Id,
                Name = lp.Name,
                Description = lp.Description,
                ThumbnailUrl = lp.ThumbnailUrl,
                Level = lp.Level.ToString(),
                EstimatedDurationHours = lp.EstimatedHours,
                CoursesCount = lp.Courses.Count,
                EnrollmentsCount = lp.EnrollmentCount,
                Price = lp.Price ?? 0,
                IsActive = lp.IsPublished,
                IsFeatured = lp.IsFeatured
            })
            .ToListAsync();

        ViewBag.IsActive = isActive;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(paths);
    }

    /// <summary>
    /// تفاصيل المسار - Learning path details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        // Get enrollment chart data for last 6 months
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var enrollmentData = await _context.LearningPathEnrollments
            .Where(e => e.LearningPathId == id && e.EnrolledAt >= sixMonthsAgo)
            .GroupBy(e => new { e.EnrolledAt.Year, e.EnrolledAt.Month })
            .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var chartLabels = new List<string>();
        var chartData = new List<int>();
        
        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var monthName = date.ToString("MMMM", new System.Globalization.CultureInfo("ar-SA"));
            chartLabels.Add(monthName);
            
            var monthData = enrollmentData.FirstOrDefault(x => x.Year == date.Year && x.Month == date.Month);
            chartData.Add(monthData?.Count ?? 0);
        }

        ViewBag.ChartLabels = chartLabels;
        ViewBag.ChartData = chartData;

        return View(path);
    }

    /// <summary>
    /// إنشاء مسار جديد - Create new learning path
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new LearningPathCreateViewModel
        {
            AvailableCategories = await _context.Categories
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync(),
            AvailableCourses = await _context.Courses
                .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync()
        };
        
        await PopulateDropdowns();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ المسار الجديد - Save new learning path
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LearningPathCreateViewModel model)
    {
        // Ensure CourseIds is never null; fallback from comma-separated form value if binding missed
        if (model.CourseIds == null)
            model.CourseIds = new List<int>();
        var courseIdsCsv = Request.Form["CourseIdsCsv"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(courseIdsCsv) && !model.CourseIds.Any())
        {
            model.CourseIds = courseIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
        }

        if (!model.CourseIds.Any())
        {
            ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
        }

        if (ModelState.IsValid)
        {
            var path = new LearningPath
            {
                Name = model.Name,
                Slug = _slugService.GenerateSlug(model.Name),
                Description = model.Description,
                ThumbnailUrl = model.ThumbnailUrl,
                Level = Enum.Parse<Domain.Enums.CourseLevel>(model.Level),
                EstimatedHours = model.EstimatedDurationHours,
                Price = model.Price,
                Currency = "EGP",
                IsPublished = model.IsActive,
                IsFeatured = model.IsFeatured,
                DisplayOrder = model.DisplayOrder,
                CoursesCount = model.CourseIds.Count,
                OwnerId = _currentUserService.UserId
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
                    DisplayOrder = i + 1
                };
                _context.LearningPathCourses.Add(pathCourse);
            }

            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم إنشاء المسار التعليمي بنجاح", "Learning path created successfully."));
            return RedirectToAction(nameof(Details), new { id = path.Id });
        }

        // Repopulate dropdowns for view model when validation fails
        model.AvailableCategories = await _context.Categories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
        model.AvailableCourses = await _context.Courses
            .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
            .ToListAsync();
            
        await PopulateDropdowns();
        return View(model);
    }

    /// <summary>
    /// تعديل المسار - Edit learning path
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        var courseIds = path.Courses.OrderBy(c => c.DisplayOrder).Select(c => c.CourseId).ToList();

        var viewModel = new LearningPathEditViewModel
        {
            Id = path.Id,
            Name = path.Name,
            Description = path.Description,
            ThumbnailUrl = path.ThumbnailUrl,
            CurrentImageUrl = path.ThumbnailUrl,
            Level = path.Level.ToString(),
            EstimatedDurationHours = path.EstimatedHours,
            Price = path.Price ?? 0,
            CategoryId = path.CategoryId,
            IsActive = path.IsPublished,
            IsFeatured = path.IsFeatured,
            DisplayOrder = path.DisplayOrder,
            CourseIds = courseIds,
            SelectedCourses = courseIds,
            EnrollmentsCount = path.EnrollmentCount,
            CompletedCount = path.CompletedCount,
            AvailableCategories = await _context.Categories
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync(),
            AvailableCourses = await _context.Courses
                .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync()
        };

        await PopulateDropdowns();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات المسار - Save learning path edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LearningPathEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        // Fallback from comma-separated form value if CourseIds binding missed
        if (model.CourseIds == null)
            model.CourseIds = new List<int>();
        var courseIdsCsv = Request.Form["CourseIdsCsv"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(courseIdsCsv) && !model.CourseIds.Any())
        {
            model.CourseIds = courseIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var cid) ? cid : 0)
                .Where(cid => cid > 0)
                .ToList();
        }

        // Prevent duplicate (LearningPathId, CourseId) - unique index allows only one per course per path
        model.CourseIds = model.CourseIds.Distinct().ToList();

        if (!model.CourseIds.Any())
        {
            ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
        }

        if (ModelState.IsValid)
        {
            path.Name = model.Name;
            path.Slug = _slugService.GenerateSlug(model.Name);
            path.Description = model.Description;
            path.ThumbnailUrl = model.ThumbnailUrl;
            path.Level = Enum.Parse<Domain.Enums.CourseLevel>(model.Level);
            path.EstimatedHours = model.EstimatedDurationHours;
            path.Price = model.Price;
            path.IsPublished = model.IsActive;
            path.IsFeatured = model.IsFeatured;
            path.DisplayOrder = model.DisplayOrder;
            path.CoursesCount = model.CourseIds.Count;

            // Update path courses: reuse or add (soft-delete means RemoveRange doesn't remove rows, so we'd get duplicate key)
            var existingPathCourses = await _context.LearningPathCourses
                .IgnoreQueryFilters()
                .Where(lpc => lpc.LearningPathId == id)
                .ToListAsync();
            var selectedCourseIds = model.CourseIds.Distinct().ToList();

            for (int i = 0; i < selectedCourseIds.Count; i++)
            {
                var courseId = selectedCourseIds[i];
                var existing = existingPathCourses.FirstOrDefault(lpc => lpc.CourseId == courseId);
                if (existing != null)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    existing.DisplayOrder = i + 1;
                    _context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    _context.LearningPathCourses.Add(new LearningPathCourse
                    {
                        LearningPathId = path.Id,
                        CourseId = courseId,
                        DisplayOrder = i + 1
                    });
                }
            }

            foreach (var lpc in existingPathCourses.Where(lpc => !selectedCourseIds.Contains(lpc.CourseId)))
            {
                lpc.IsDeleted = true;
                lpc.DeletedAt = DateTime.UtcNow;
                _context.Entry(lpc).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم تحديث المسار التعليمي بنجاح", "Learning path updated successfully."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Repopulate dropdowns for view model when validation fails
        model.AvailableCategories = await _context.Categories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
        model.AvailableCourses = await _context.Courses
            .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
            .ToListAsync();
        model.SelectedCourses = model.CourseIds;
        model.EnrollmentsCount = path.EnrollmentCount;
        model.CompletedCount = path.CompletedCount;
            
        await PopulateDropdowns();
        return View(model);
    }

    /// <summary>
    /// حذف المسار - Delete learning path
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        if (path.EnrollmentCount > 0)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن حذف المسار لأنه يحتوي على تسجيلات", "Cannot delete the path because it contains enrollments."));
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.LearningPathCourses.RemoveRange(path.Courses);
        _context.LearningPaths.Remove(path);
        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم حذف المسار التعليمي بنجاح", "Learning path deleted successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var path = await _context.LearningPaths.FindAsync(id);
        if (path == null)
            return NotFound();

        path.IsPublished = !path.IsPublished;
        await _context.SaveChangesAsync();

        SetSuccessMessage(path.IsPublished ? CultureExtensions.T("تم تفعيل المسار", "Path enabled.") : CultureExtensions.T("تم تعطيل المسار", "Path disabled."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// نسخ المسار - Duplicate learning path
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        // Create a duplicate path
        var duplicatePath = new LearningPath
        {
            Name = $"{path.Name} (نسخة)",
            Slug = _slugService.GenerateSlug($"{path.Name}-copy-{DateTime.UtcNow.Ticks}"),
            Description = path.Description,
            ShortDescription = path.ShortDescription,
            ThumbnailUrl = path.ThumbnailUrl,
            Level = path.Level,
            EstimatedHours = path.EstimatedHours,
            Price = path.Price,
            Currency = path.Currency,
            IsPublished = false, // Start as unpublished
            IsFeatured = false,
            DisplayOrder = path.DisplayOrder,
            CoursesCount = path.CoursesCount
        };

        _context.LearningPaths.Add(duplicatePath);
        await _context.SaveChangesAsync();

        // Copy learning path courses
        foreach (var pathCourse in path.Courses)
        {
            var duplicatePathCourse = new LearningPathCourse
            {
                LearningPathId = duplicatePath.Id,
                CourseId = pathCourse.CourseId,
                DisplayOrder = pathCourse.DisplayOrder
            };
            _context.LearningPathCourses.Add(duplicatePathCourse);
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم نسخ المسار التعليمي بنجاح", "Learning path copied successfully."));
        return RedirectToAction(nameof(Edit), new { id = duplicatePath.Id });
    }

    /// <summary>
    /// إحصائيات المسار - Learning path statistics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Statistics(int id)
    {
        var path = await _context.LearningPaths
            .Include(lp => lp.Courses)
                .ThenInclude(lpc => lpc.Course)
            .FirstOrDefaultAsync(lp => lp.Id == id);

        if (path == null)
            return NotFound();

        ViewBag.TotalEnrollments = path.EnrollmentCount;
        ViewBag.TotalCourses = path.CoursesCount;
        ViewBag.TotalRevenue = path.EnrollmentCount * (path.Price ?? 0);
        
        return View(path);
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Courses = new MultiSelectList(
            await _context.Courses
                .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");

        ViewBag.Categories = new SelectList(
            await _context.Categories
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(),
            "Id", "Name");
    }
}

