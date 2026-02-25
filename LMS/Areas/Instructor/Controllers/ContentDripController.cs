using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة جدولة المحتوى - Content Drip Management Controller
/// </summary>
public class ContentDripController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ContentDripController> _logger;

    public ContentDripController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ContentDripController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة قواعد الجدولة - Drip rules list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? isActive, string? type, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.ContentDripRules
            .Include(r => r.Course)
            .Include(r => r.Module)
            .Include(r => r.Lesson)
            .Where(r => r.Course.InstructorId == userId);

        if (courseId.HasValue)
            query = query.Where(r => r.CourseId == courseId.Value);

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ContentDripType>(type, out var dripType))
            query = query.Where(r => r.DripType == dripType);

        // Pagination counts (before Skip/Take)
        var totalItems = await query.CountAsync();
        ViewBag.TotalItems = totalItems;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / 20.0);
        ViewBag.PageSize = 20;
        ViewBag.CurrentPage = page;

        var rules = await query
            .OrderBy(r => r.Course.Title)
            .ThenBy(r => r.DaysAfterEnrollment ?? 0)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.SelectedIsActive = isActive;
        ViewBag.SelectedType = type;
        ViewBag.Page = page;

        // Get instructor's courses
        var instructorCourses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .ToListAsync();

        ViewBag.Courses = instructorCourses
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToList();

        // Statistics
        var instructorCourseIds = instructorCourses.Select(c => c.Id).ToList();
        var allRulesForStats = await _context.ContentDripRules
            .Where(r => !r.IsDeleted && instructorCourseIds.Contains(r.CourseId))
            .ToListAsync();
        ViewBag.TotalRules = allRulesForStats.Count;
        ViewBag.ActiveRules = allRulesForStats.Count(r => r.IsActive);
        ViewBag.ScheduledCourses = allRulesForStats.Select(r => r.CourseId).Distinct().Count();
        ViewBag.ScheduledContent = allRulesForStats.Count(r => r.ModuleId != null || r.LessonId != null);

        return View(rules);
    }

    /// <summary>
    /// تفاصيل قاعدة الجدولة - Drip rule details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var rule = await _context.ContentDripRules
            .Include(r => r.Course)
            .Include(r => r.Module)
            .Include(r => r.Lesson)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (rule == null)
            return NotFound();

        return View(rule);
    }

    /// <summary>
    /// إنشاء قاعدة جدولة جديدة - Create new drip rule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? courseId)
    {
        await PopulateCoursesDropdownAsync();

        var model = new ContentDripRuleViewModel
        {
            CourseId = courseId ?? 0,
            DripType = ContentDripType.AllAtOnce,
            SendNotification = true,
            IsActive = true
        };

        if (courseId.HasValue)
        {
            await PopulateModulesDropdownAsync(courseId.Value);
        }

        return View(model);
    }

    /// <summary>
    /// حفظ قاعدة الجدولة الجديدة - Save new drip rule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentDripRuleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCoursesDropdownAsync();
            if (model.CourseId > 0)
            {
                await PopulateModulesDropdownAsync(model.CourseId);
            }
            return View(model);
        }

        var userId = _currentUserService.UserId;

        try
        {
            // Verify course ownership
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", 
                    model.CourseId, userId);
                SetErrorMessage("غير مصرح لك بإضافة قواعد جدولة لهذه الدورة");
                return RedirectToAction(nameof(Index));
            }

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateContentDrip(
                model.DripType,
                model.DaysAfterEnrollment,
                model.SpecificDate);

            if (!isValid)
            {
                _logger.LogWarning("Content drip validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateCoursesDropdownAsync();
                if (model.CourseId > 0)
                {
                    await PopulateModulesDropdownAsync(model.CourseId);
                }
                return View(model);
            }

            // Check for conflicting rules
            var hasConflictingRule = await _context.ContentDripRules
                .AnyAsync(r => r.CourseId == model.CourseId &&
                              r.ModuleId == model.ModuleId &&
                              r.LessonId == model.LessonId &&
                              r.IsActive);

            if (hasConflictingRule)
            {
                _logger.LogWarning("Conflicting drip rule exists for course {CourseId}", model.CourseId);
                ModelState.AddModelError(string.Empty, "توجد قاعدة جدولة نشطة بالفعل لهذا المحتوى");
                await PopulateCoursesDropdownAsync();
                if (model.CourseId > 0)
                {
                    await PopulateModulesDropdownAsync(model.CourseId);
                }
                return View(model);
            }

            ContentDripRule? rule = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                rule = new ContentDripRule
                {
                    CourseId = model.CourseId,
                    ModuleId = model.ModuleId,
                    LessonId = model.LessonId,
                    DripType = model.DripType,
                    DaysAfterEnrollment = model.DaysAfterEnrollment,
                    SpecificDate = model.SpecificDate,
                    DayOfWeek = model.DayOfWeek,
                    ReleaseHour = model.ReleaseHour,
                    TimeZone = model.TimeZone ?? "Africa/Cairo",
                    SendNotification = model.SendNotification,
                    NotificationTitle = model.NotificationTitle,
                    NotificationMessage = model.NotificationMessage,
                    IsActive = model.IsActive
                };

                _context.ContentDripRules.Add(rule);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Content drip rule {RuleId} created by instructor {InstructorId} for course {CourseId}. Type: {DripType}", 
                rule!.Id, userId, model.CourseId, model.DripType);

            SetSuccessMessage("تم إنشاء قاعدة الجدولة بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content drip rule for course {CourseId}", model.CourseId);
            SetErrorMessage("حدث خطأ أثناء إنشاء قاعدة الجدولة");
            await PopulateCoursesDropdownAsync();
            if (model.CourseId > 0)
            {
                await PopulateModulesDropdownAsync(model.CourseId);
            }
            return View(model);
        }
    }

    /// <summary>
    /// تعديل قاعدة جدولة - Edit drip rule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var rule = await _context.ContentDripRules
            .Include(r => r.Course)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (rule == null)
            return NotFound();

        var model = new ContentDripRuleViewModel
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

        await PopulateCoursesDropdownAsync();
        await PopulateModulesDropdownAsync(rule.CourseId);
        if (rule.ModuleId.HasValue)
        {
            await PopulateLessonsDropdownAsync(rule.ModuleId.Value);
        }

        ViewBag.CourseName = rule.Course?.Title ?? "غير معروف";
        ViewBag.CourseId = rule.CourseId;

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات قاعدة الجدولة - Save drip rule changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContentDripRuleViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        var userId = _currentUserService.UserId;

        var rule = await _context.ContentDripRules
            .Include(r => r.Course)
                .ThenInclude(c => c.Enrollments)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (rule == null)
            return NotFound();

        try
        {
            if (!ModelState.IsValid)
            {
                await PopulateCoursesDropdownAsync();
                await PopulateModulesDropdownAsync(model.CourseId);
                if (model.ModuleId.HasValue)
                {
                    await PopulateLessonsDropdownAsync(model.ModuleId.Value);
                }
                return View(model);
            }

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateContentDrip(
                model.DripType,
                model.DaysAfterEnrollment,
                model.SpecificDate);

            if (!isValid)
            {
                _logger.LogWarning("Content drip rule {RuleId} validation failed: {Reason}", id, validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateCoursesDropdownAsync();
                await PopulateModulesDropdownAsync(model.CourseId);
                if (model.ModuleId.HasValue)
                {
                    await PopulateLessonsDropdownAsync(model.ModuleId.Value);
                }
                return View(model);
            }

            // Check for conflicting rules (only if content selection changed)
            var contentChanged = rule.ModuleId != model.ModuleId || rule.LessonId != model.LessonId;
            if (contentChanged && model.IsActive)
            {
                var hasConflictingRule = await _context.ContentDripRules
                    .AnyAsync(r => r.Id != id &&
                                  r.CourseId == rule.CourseId &&
                                  r.ModuleId == model.ModuleId &&
                                  r.LessonId == model.LessonId &&
                                  r.IsActive);

                if (hasConflictingRule)
                {
                    _logger.LogWarning("Conflicting drip rule exists when editing rule {RuleId}", id);
                    ModelState.AddModelError(string.Empty, "توجد قاعدة جدولة نشطة بالفعل لهذا المحتوى");
                    await PopulateCoursesDropdownAsync();
                    await PopulateModulesDropdownAsync(model.CourseId);
                    if (model.ModuleId.HasValue)
                    {
                        await PopulateLessonsDropdownAsync(model.ModuleId.Value);
                    }
                    return View(model);
                }
            }

            // Warn if editing active rule affecting enrolled students
            if (rule.IsActive && rule.Course.Enrollments.Any())
            {
                _logger.LogInformation(
                    "Editing active drip rule {RuleId} affecting {EnrollmentCount} students", 
                    id, rule.Course.Enrollments.Count);
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                rule.ModuleId = model.ModuleId;
                rule.LessonId = model.LessonId;
                rule.DripType = model.DripType;
                rule.DaysAfterEnrollment = model.DaysAfterEnrollment;
                rule.SpecificDate = model.SpecificDate;
                rule.DayOfWeek = model.DayOfWeek;
                rule.ReleaseHour = model.ReleaseHour;
                rule.TimeZone = model.TimeZone ?? "Africa/Cairo";
                rule.SendNotification = model.SendNotification;
                rule.NotificationTitle = model.NotificationTitle;
                rule.NotificationMessage = model.NotificationMessage;
                rule.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Content drip rule {RuleId} updated by instructor {InstructorId}. Type: {DripType}",
                rule.Id, userId, model.DripType);

            SetSuccessMessage("تم تحديث قاعدة الجدولة بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = rule.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating content drip rule {RuleId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث قاعدة الجدولة");
            await PopulateCoursesDropdownAsync();
            await PopulateModulesDropdownAsync(model.CourseId);
            if (model.ModuleId.HasValue)
            {
                await PopulateLessonsDropdownAsync(model.ModuleId.Value);
            }
            return View(model);
        }
    }

    /// <summary>
    /// حذف قاعدة جدولة - Delete drip rule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var rule = await _context.ContentDripRules
                .Include(r => r.Course)
                    .ThenInclude(c => c.Enrollments)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

            if (rule == null)
            {
                _logger.LogWarning("Content drip rule {RuleId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            // Warn if deleting active rule affecting enrolled students
            if (rule.IsActive && rule.Course.Enrollments.Any())
            {
                _logger.LogInformation(
                    "Deleting active drip rule {RuleId} affecting {EnrollmentCount} students", 
                    id, rule.Course.Enrollments.Count);
                SetWarningMessage($"تحذير: هذه القاعدة نشطة وتؤثر على {rule.Course.Enrollments.Count} طالب مسجل");
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.ContentDripRules.Remove(rule);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Content drip rule {RuleId} deleted by instructor {InstructorId}", 
                id, userId);

            SetSuccessMessage("تم حذف قاعدة الجدولة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting content drip rule {RuleId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف قاعدة الجدولة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var userId = _currentUserService.UserId;

        var rule = await _context.ContentDripRules
            .Include(r => r.Course)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (rule == null)
            return NotFound();

        rule.IsActive = !rule.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(rule.IsActive ? "تفعيل" : "تعطيل")} قاعدة الجدولة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الحصول على الوحدات للدورة - Get modules for course (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetModules(int courseId)
    {
        var userId = _currentUserService.UserId;

        var modules = await _context.Modules
            .Where(m => m.CourseId == courseId && m.Course.InstructorId == userId)
            .Select(m => new
            {
                id = m.Id,
                title = m.Title
            })
            .ToListAsync();

        return Json(modules);
    }

    /// <summary>
    /// الحصول على الدروس للوحدة - Get lessons for module (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLessons(int moduleId)
    {
        var userId = _currentUserService.UserId;

        var lessons = await _context.Lessons
            .Include(l => l.Module)
            .Where(l => l.ModuleId == moduleId && l.Module.Course.InstructorId == userId)
            .Select(l => new
            {
                id = l.Id,
                title = l.Title
            })
            .ToListAsync();

        return Json(lessons);
    }

    #region Private Helpers

    private async Task PopulateCoursesDropdownAsync()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();
    }

    private async Task PopulateModulesDropdownAsync(int courseId)
    {
        ViewBag.Modules = await _context.Modules
            .Where(m => m.CourseId == courseId)
            .Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = m.Title
            })
            .ToListAsync();
    }

    private async Task PopulateLessonsDropdownAsync(int moduleId)
    {
        ViewBag.Lessons = await _context.Lessons
            .Where(l => l.ModuleId == moduleId)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.Title
            })
            .ToListAsync();
    }

    #endregion
}

