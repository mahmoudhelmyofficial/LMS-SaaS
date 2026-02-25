using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Assessments;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة إعدادات المراقبة - Proctoring Settings Management Controller
/// </summary>
public class ProctoringController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ProctoringController> _logger;

    public ProctoringController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ProctoringController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة إعدادات المراقبة - Proctoring settings list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.ProctoringSettings
            .Include(p => p.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(p => p.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (courseId.HasValue)
            query = query.Where(p => p.Quiz.Lesson.Module.CourseId == courseId.Value);

        var settings = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(p => new ProctoringSettingListViewModel
            {
                Id = p.Id,
                QuizId = p.QuizId,
                QuizTitle = p.Quiz.Title,
                CourseTitle = p.Quiz.Lesson.Module.Course.Title,
                IsEnabled = p.IsEnabled,
                RequireWebcam = p.RequireWebcam,
                RecordVideo = p.RecordVideo,
                EnableFaceDetection = p.EnableFaceDetection,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Page = page;

        // Get instructor's courses for filtering
        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();

        return View(settings);
    }

    /// <summary>
    /// تفاصيل إعدادات المراقبة - Proctoring settings details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var setting = await _context.ProctoringSettings
            .Include(p => p.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(p => p.Id == id && p.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (setting == null)
            return NotFound();

        return View(setting);
    }

    /// <summary>
    /// إنشاء إعدادات مراقبة جديدة - Create new proctoring settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? quizId)
    {
        await PopulateQuizzesDropdownAsync();

        var model = new ProctoringSettingViewModel
        {
            QuizId = quizId ?? 0,
            IsEnabled = true,
            RequireWebcam = true,
            PreventTabSwitch = true,
            MaxTabSwitchWarnings = 3,
            PreventCopyPaste = true,
            DisableRightClick = true,
            ScreenshotInterval = 60
        };

        return View(model);
    }

    /// <summary>
    /// حفظ إعدادات المراقبة الجديدة - Save new proctoring settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProctoringSettingViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            // Verify quiz ownership
            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(q => q.Id == model.QuizId && q.Lesson.Module.Course.InstructorId == userId);

            if (quiz == null)
            {
                SetErrorMessage("غير مصرح لك بإضافة إعدادات مراقبة لهذا الاختبار");
                return RedirectToAction(nameof(Index));
            }

            // Check if proctoring settings already exist for this quiz
            var existingSetting = await _context.ProctoringSettings
                .FirstOrDefaultAsync(p => p.QuizId == model.QuizId);

            if (existingSetting != null)
            {
                SetErrorMessage("إعدادات المراقبة موجودة بالفعل لهذا الاختبار. يرجى تعديلها بدلاً من إنشاء جديدة.");
                await PopulateQuizzesDropdownAsync();
                return View(model);
            }

            var setting = new ProctoringSetting
            {
                QuizId = model.QuizId,
                IsEnabled = model.IsEnabled,
                RequireWebcam = model.RequireWebcam,
                RecordVideo = model.RecordVideo,
                CaptureScreenshots = model.CaptureScreenshots,
                ScreenshotInterval = model.ScreenshotInterval,
                PreventTabSwitch = model.PreventTabSwitch,
                MaxTabSwitchWarnings = model.MaxTabSwitchWarnings,
                PreventCopyPaste = model.PreventCopyPaste,
                DisableRightClick = model.DisableRightClick,
                RequireFullscreen = model.RequireFullscreen,
                EnableFaceDetection = model.EnableFaceDetection,
                DetectMultiplePeople = model.DetectMultiplePeople,
                LockBrowser = model.LockBrowser,
                RequireIdVerification = model.RequireIdVerification,
                AutoTerminate = model.AutoTerminate,
                WarningMessage = model.WarningMessage,
                Notes = model.Notes
            };

            _context.ProctoringSettings.Add(setting);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Proctoring settings created for Quiz {QuizId} by Instructor {UserId}", model.QuizId, userId);

            SetSuccessMessage("تم إنشاء إعدادات المراقبة بنجاح");
            return RedirectToAction(nameof(Details), new { id = setting.Id });
        }

        await PopulateQuizzesDropdownAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل إعدادات المراقبة - Edit proctoring settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var setting = await _context.ProctoringSettings
            .Include(p => p.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(p => p.Id == id && p.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (setting == null)
            return NotFound();

        var model = new ProctoringSettingViewModel
        {
            Id = setting.Id,
            QuizId = setting.QuizId,
            IsEnabled = setting.IsEnabled,
            RequireWebcam = setting.RequireWebcam,
            RecordVideo = setting.RecordVideo,
            CaptureScreenshots = setting.CaptureScreenshots,
            ScreenshotInterval = setting.ScreenshotInterval,
            PreventTabSwitch = setting.PreventTabSwitch,
            MaxTabSwitchWarnings = setting.MaxTabSwitchWarnings,
            PreventCopyPaste = setting.PreventCopyPaste,
            DisableRightClick = setting.DisableRightClick,
            RequireFullscreen = setting.RequireFullscreen,
            EnableFaceDetection = setting.EnableFaceDetection,
            DetectMultiplePeople = setting.DetectMultiplePeople,
            LockBrowser = setting.LockBrowser,
            RequireIdVerification = setting.RequireIdVerification,
            AutoTerminate = setting.AutoTerminate,
            WarningMessage = setting.WarningMessage,
            Notes = setting.Notes,
            QuizTitle = setting.Quiz.Title,
            CourseTitle = setting.Quiz.Lesson.Module.Course.Title
        };

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات إعدادات المراقبة - Save proctoring settings changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProctoringSettingViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var setting = await _context.ProctoringSettings
                .Include(p => p.Quiz)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Module)
                            .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(p => p.Id == id && p.Quiz.Lesson.Module.Course.InstructorId == userId);

            if (setting == null)
                return NotFound();

            setting.IsEnabled = model.IsEnabled;
            setting.RequireWebcam = model.RequireWebcam;
            setting.RecordVideo = model.RecordVideo;
            setting.CaptureScreenshots = model.CaptureScreenshots;
            setting.ScreenshotInterval = model.ScreenshotInterval;
            setting.PreventTabSwitch = model.PreventTabSwitch;
            setting.MaxTabSwitchWarnings = model.MaxTabSwitchWarnings;
            setting.PreventCopyPaste = model.PreventCopyPaste;
            setting.DisableRightClick = model.DisableRightClick;
            setting.RequireFullscreen = model.RequireFullscreen;
            setting.EnableFaceDetection = model.EnableFaceDetection;
            setting.DetectMultiplePeople = model.DetectMultiplePeople;
            setting.LockBrowser = model.LockBrowser;
            setting.RequireIdVerification = model.RequireIdVerification;
            setting.AutoTerminate = model.AutoTerminate;
            setting.WarningMessage = model.WarningMessage;
            setting.Notes = model.Notes;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Proctoring settings {SettingId} updated by Instructor {UserId}", id, userId);

            SetSuccessMessage("تم تحديث إعدادات المراقبة بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// حذف إعدادات المراقبة - Delete proctoring settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var setting = await _context.ProctoringSettings
            .Include(p => p.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(p => p.Id == id && p.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (setting == null)
            return NotFound();

        _context.ProctoringSettings.Remove(setting);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proctoring settings {SettingId} deleted by Instructor {UserId}", id, userId);

        SetSuccessMessage("تم حذف إعدادات المراقبة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEnabled(int id)
    {
        var userId = _currentUserService.UserId;

        var setting = await _context.ProctoringSettings
            .Include(p => p.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(p => p.Id == id && p.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (setting == null)
            return NotFound();

        setting.IsEnabled = !setting.IsEnabled;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proctoring settings {SettingId} {Status} by Instructor {UserId}", 
            id, setting.IsEnabled ? "enabled" : "disabled", userId);

        SetSuccessMessage($"تم {(setting.IsEnabled ? "تفعيل" : "تعطيل")} إعدادات المراقبة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الحصول على الاختبارات للدورة - Get quizzes for course (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQuizzes(int courseId)
    {
        var userId = _currentUserService.UserId;

        var quizzes = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
            .Where(q => q.Lesson.Module.CourseId == courseId && q.Lesson.Module.Course.InstructorId == userId)
            .Select(q => new
            {
                id = q.Id,
                title = q.Title,
                hasProctoringSettings = _context.ProctoringSettings.Any(p => p.QuizId == q.Id)
            })
            .ToListAsync();

        return Json(quizzes);
    }

    #region Private Helpers

    private async Task PopulateQuizzesDropdownAsync()
    {
        var userId = _currentUserService.UserId;

        var quizzes = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(q => q.Lesson.Module.Course.InstructorId == userId)
            .Where(q => !_context.ProctoringSettings.Any(p => p.QuizId == q.Id))
            .Select(q => new SelectListItem
            {
                Value = q.Id.ToString(),
                Text = $"{q.Lesson.Module.Course.Title} - {q.Title}"
            })
            .ToListAsync();

        ViewBag.Quizzes = quizzes;
    }

    #endregion
}

