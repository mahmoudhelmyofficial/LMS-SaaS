using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة جدولة المحتوى - Content Drip Management Controller
/// </summary>
public class ContentDripController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ContentDripController> _logger;
    private readonly ISystemConfigurationService _configService;

    public ContentDripController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ContentDripController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة قواعد جدولة المحتوى - Content drip rules list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? isActive, int page = 1)
    {
        var query = _context.ContentDripRules
            .Include(r => r.Course)
            .Include(r => r.Module)
            .Include(r => r.Lesson)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(r => r.CourseId == courseId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("content_drip", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var rules = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.IsActive = isActive;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;
        await PopulateCoursesDropdown();

        return View(rules);
    }

    /// <summary>
    /// تفاصيل القاعدة - Rule details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var rule = await _context.ContentDripRules
            .Include(r => r.Course)
            .Include(r => r.Module)
            .Include(r => r.Lesson)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule == null)
            return NotFound();

        return View(rule);
    }

    /// <summary>
    /// إنشاء قاعدة جديدة - Create new rule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCoursesDropdown();
        return View(new ContentDripCreateViewModel());
    }

    /// <summary>
    /// حفظ القاعدة الجديدة - Save new rule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentDripCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Validate drip type specific fields
            if (model.DripType == ContentDripType.ByDays && !model.DaysAfterEnrollment.HasValue)
            {
                ModelState.AddModelError("DaysAfterEnrollment", "عدد الأيام مطلوب");
            }
            else if (model.DripType == ContentDripType.ByDate && !model.SpecificDate.HasValue)
            {
                ModelState.AddModelError("SpecificDate", "التاريخ المحدد مطلوب");
            }
            else if (model.DripType == ContentDripType.ByWeekDay && !model.DayOfWeek.HasValue)
            {
                ModelState.AddModelError("DayOfWeek", "يوم الأسبوع مطلوب");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCoursesDropdown();
                await PopulateModulesDropdown(model.CourseId);
                if (model.ModuleId.HasValue)
                    await PopulateLessonsDropdown(model.ModuleId.Value);
                return View(model);
            }

            var rule = new ContentDripRule
            {
                CourseId = model.CourseId,
                ModuleId = model.ModuleId,
                LessonId = model.LessonId,
                DripType = model.DripType,
                DaysAfterEnrollment = model.DaysAfterEnrollment,
                SpecificDate = model.SpecificDate,
                DayOfWeek = model.DayOfWeek,
                ReleaseHour = model.ReleaseHour,
                TimeZone = model.TimeZone,
                SendNotification = model.SendNotification,
                NotificationTitle = model.NotificationTitle,
                NotificationMessage = model.NotificationMessage,
                IsActive = model.IsActive
            };

            _context.ContentDripRules.Add(rule);
            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم إنشاء قاعدة الجدولة بنجاح", "Drip rule created successfully."));
            return RedirectToAction(nameof(Details), new { id = rule.Id });
        }

        await PopulateCoursesDropdown();
        await PopulateModulesDropdown(model.CourseId);
        if (model.ModuleId.HasValue)
            await PopulateLessonsDropdown(model.ModuleId.Value);
        return View(model);
    }

    /// <summary>
    /// تعديل القاعدة - Edit rule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await _context.ContentDripRules.FindAsync(id);
        if (rule == null)
            return NotFound();

        var viewModel = new ContentDripEditViewModel
        {
            Id = rule.Id,
            CourseId = rule.CourseId,
            ModuleId = rule.ModuleId,
            LessonId = rule.LessonId,
            DripType = rule.DripType,
            DaysAfterEnrollment = rule.DaysAfterEnrollment,
            SpecificDate = rule.SpecificDate,
            DayOfWeek = rule.DayOfWeek,
            ReleaseHour = rule.ReleaseHour,
            TimeZone = rule.TimeZone,
            SendNotification = rule.SendNotification,
            NotificationTitle = rule.NotificationTitle,
            NotificationMessage = rule.NotificationMessage,
            IsActive = rule.IsActive
        };

        await PopulateCoursesDropdown();
        await PopulateModulesDropdown(rule.CourseId);
        if (rule.ModuleId.HasValue)
            await PopulateLessonsDropdown(rule.ModuleId.Value);

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات القاعدة - Save rule edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContentDripEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var rule = await _context.ContentDripRules.FindAsync(id);
        if (rule == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            rule.CourseId = model.CourseId;
            rule.ModuleId = model.ModuleId;
            rule.LessonId = model.LessonId;
            rule.DripType = model.DripType;
            rule.DaysAfterEnrollment = model.DaysAfterEnrollment;
            rule.SpecificDate = model.SpecificDate;
            rule.DayOfWeek = model.DayOfWeek;
            rule.ReleaseHour = model.ReleaseHour;
            rule.TimeZone = model.TimeZone;
            rule.SendNotification = model.SendNotification;
            rule.NotificationTitle = model.NotificationTitle;
            rule.NotificationMessage = model.NotificationMessage;
            rule.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم تحديث قاعدة الجدولة بنجاح", "Drip rule updated successfully."));
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateCoursesDropdown();
        await PopulateModulesDropdown(model.CourseId);
        if (model.ModuleId.HasValue)
            await PopulateLessonsDropdown(model.ModuleId.Value);

        return View(model);
    }

    /// <summary>
    /// تفعيل/تعطيل القاعدة - Toggle rule status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var rule = await _context.ContentDripRules.FindAsync(id);
        if (rule == null)
            return NotFound();

        rule.IsActive = !rule.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(rule.IsActive ? CultureExtensions.T("تم تفعيل القاعدة", "Rule enabled.") : CultureExtensions.T("تم تعطيل القاعدة", "Rule disabled."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف القاعدة - Delete rule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _context.ContentDripRules.FindAsync(id);
        if (rule == null)
            return NotFound();

        _context.ContentDripRules.Remove(rule);
        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم حذف قاعدة الجدولة بنجاح", "Drip rule deleted successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// احصائيات القواعد - Rules statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var stats = new ContentDripStatisticsViewModel
        {
            TotalRules = await _context.ContentDripRules.CountAsync(),
            ActiveRules = await _context.ContentDripRules.CountAsync(r => r.IsActive),
            InactiveRules = await _context.ContentDripRules.CountAsync(r => !r.IsActive),
            RulesByType = await _context.ContentDripRules
                .GroupBy(r => r.DripType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type.ToString(), x => x.Count)
        };

        return View(stats);
    }

    #region Helper Methods

    private async Task PopulateCoursesDropdown()
    {
        ViewBag.Courses = new SelectList(
            await _context.Courses
                .Where(c => c.Status == CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");
    }

    private async Task PopulateModulesDropdown(int courseId)
    {
        ViewBag.Modules = new SelectList(
            await _context.Modules
                .Where(m => m.CourseId == courseId)
                .OrderBy(m => m.OrderIndex)
                .Select(m => new { m.Id, m.Title })
                .ToListAsync(),
            "Id", "Title");
    }

    private async Task PopulateLessonsDropdown(int moduleId)
    {
        ViewBag.Lessons = new SelectList(
            await _context.Lessons
                .Where(l => l.ModuleId == moduleId)
                .OrderBy(l => l.OrderIndex)
                .Select(l => new { l.Id, l.Title })
                .ToListAsync(),
            "Id", "Title");
    }

    /// <summary>
    /// Get modules by course - AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetModulesByCourse(int courseId)
    {
        var modules = await _context.Modules
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.OrderIndex)
            .Select(m => new { m.Id, m.Title })
            .ToListAsync();

        return Json(modules);
    }

    /// <summary>
    /// Get lessons by module - AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLessonsByModule(int moduleId)
    {
        var lessons = await _context.Lessons
            .Where(l => l.ModuleId == moduleId)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new { l.Id, l.Title })
            .ToListAsync();

        return Json(lessons);
    }

    #endregion
}

