using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// مراقبة الاختبارات - Proctoring Controller
/// Enterprise-level exam proctoring and monitoring management
/// </summary>
public class ProctoringController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProctoringController> _logger;

    public ProctoringController(
        ApplicationDbContext context,
        ILogger<ProctoringController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// الصفحة الرئيسية لمراقبة الاختبارات - Proctoring main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Proctoring" || s.Key.StartsWith("Proctoring") || 
                           s.Key.StartsWith("Exam"))
                .ToListAsync();

            // Default settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = GetDefaultProctoringSettings();
            }

            // Exam statistics
            ViewBag.TotalQuizzes = await _context.Quizzes.CountAsync();
            ViewBag.ProctoredQuizzes = await _context.Quizzes.CountAsync(q => q.RequiresProctoring);
            ViewBag.ActiveExams = await _context.QuizAttempts
                .CountAsync(a => a.Status == QuizAttemptStatus.InProgress);
            ViewBag.CompletedExams = await _context.QuizAttempts
                .CountAsync(a => a.Status == QuizAttemptStatus.Completed);
            
            // Recent suspicious activities (placeholder - would require ProctoringSessions table)
            ViewBag.SuspiciousActivities = new List<object>();

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات المراقبة");
            SetWarningMessage("تعذر تحميل إعدادات المراقبة. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات المراقبة - Save proctoring settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProctoringSettingsViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                SetErrorMessage("يرجى تصحيح الأخطاء في النموذج");
                return RedirectToAction(nameof(Index));
            }

            var settings = new Dictionary<string, string>
            {
                // General Proctoring
                { "ProctoringEnabled", model.IsEnabled.ToString() },
                { "ProctoringProvider", model.Provider ?? "Internal" },
                
                // Browser Lockdown
                { "ProctoringLockBrowser", model.LockBrowser.ToString() },
                { "ProctoringPreventCopyPaste", model.PreventCopyPaste.ToString() },
                { "ProctoringPreventRightClick", model.PreventRightClick.ToString() },
                { "ProctoringPreventScreenshot", model.PreventScreenshot.ToString() },
                { "ProctoringFullScreenRequired", model.FullScreenRequired.ToString() },
                
                // Webcam Monitoring
                { "ProctoringWebcamRequired", model.WebcamRequired.ToString() },
                { "ProctoringRecordVideo", model.RecordVideo.ToString() },
                { "ProctoringPhotoInterval", model.PhotoIntervalSeconds.ToString() },
                { "ProctoringFaceDetection", model.FaceDetectionEnabled.ToString() },
                { "ProctoringMultipleFaceAlert", model.MultipleFaceAlert.ToString() },
                
                // Audio Monitoring
                { "ProctoringAudioRequired", model.AudioRequired.ToString() },
                { "ProctoringRecordAudio", model.RecordAudio.ToString() },
                { "ProctoringNoiseAlert", model.NoiseAlert.ToString() },
                
                // Screen Monitoring
                { "ProctoringScreenShare", model.ScreenShareRequired.ToString() },
                { "ProctoringRecordScreen", model.RecordScreen.ToString() },
                { "ProctoringTabSwitchAlert", model.TabSwitchAlert.ToString() },
                { "ProctoringMaxTabSwitches", model.MaxTabSwitches.ToString() },
                
                // AI Detection
                { "ProctoringAiEnabled", model.AiDetectionEnabled.ToString() },
                { "ProctoringAiSensitivity", model.AiSensitivity ?? "Medium" },
                
                // Violation Handling
                { "ProctoringAutoTerminate", model.AutoTerminateOnViolation.ToString() },
                { "ProctoringViolationThreshold", model.ViolationThreshold.ToString() },
                { "ProctoringNotifyInstructor", model.NotifyInstructorOnViolation.ToString() }
            };

            foreach (var setting in settings)
            {
                var existing = await _context.PlatformSettings
                    .FirstOrDefaultAsync(s => s.Key == setting.Key);

                if (existing != null)
                {
                    existing.Value = setting.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        Category = "Proctoring",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Proctoring settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات المراقبة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving proctoring settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الاختبارات النشطة - Active exams monitoring
    /// </summary>
    public async Task<IActionResult> ActiveExams()
    {
        try
        {
            var activeExams = await _context.QuizAttempts
                .Include(a => a.Quiz)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Module)
                            .ThenInclude(m => m.Course)
                .Include(a => a.Student)
                .Where(a => a.Status == QuizAttemptStatus.InProgress)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();

            return View(activeExams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active exams");
            SetWarningMessage("تعذر تحميل الاختبارات النشطة");
            return View(new List<Domain.Entities.Assessments.QuizAttempt>());
        }
    }

    /// <summary>
    /// سجل المخالفات - Violations log
    /// </summary>
    public async Task<IActionResult> Violations(DateTime? from, DateTime? to, int page = 1)
    {
        try
        {
            // This would typically query a ProctoringViolations table
            // For now, showing a placeholder view
            ViewBag.From = from ?? DateTime.UtcNow.AddDays(-30);
            ViewBag.To = to ?? DateTime.UtcNow;
            ViewBag.Page = page;
            ViewBag.TotalPages = 1;

            return View(new List<ProctoringViolationViewModel>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading violations log");
            SetWarningMessage("تعذر تحميل سجل المخالفات");
            return View(new List<ProctoringViolationViewModel>());
        }
    }

    /// <summary>
    /// تقارير المراقبة - Proctoring reports
    /// </summary>
    public async Task<IActionResult> Reports(DateTime? from, DateTime? to)
    {
        try
        {
            from ??= DateTime.UtcNow.AddMonths(-1);
            to ??= DateTime.UtcNow;

            var examAttempts = await _context.QuizAttempts
                .Where(a => a.StartedAt >= from && a.StartedAt <= to)
                .GroupBy(a => a.StartedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalAttempts = g.Count(),
                    CompletedAttempts = g.Count(a => a.Status == QuizAttemptStatus.Completed),
                    AverageScore = g.Average(a => (double)a.Score)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.ExamAttempts = examAttempts;
            ViewBag.TotalAttempts = examAttempts.Sum(e => e.TotalAttempts);
            ViewBag.CompletedAttempts = examAttempts.Sum(e => e.CompletedAttempts);
            ViewBag.AverageScore = examAttempts.Any() ? examAttempts.Average(e => e.AverageScore) : 0;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading proctoring reports");
            SetWarningMessage("تعذر تحميل تقارير المراقبة");
            return View();
        }
    }

    #region Private Methods

    private Dictionary<string, string> GetDefaultProctoringSettings()
    {
        return new Dictionary<string, string>
        {
            // General
            { "ProctoringEnabled", "false" },
            { "ProctoringProvider", "Internal" },
            
            // Browser Lockdown
            { "ProctoringLockBrowser", "true" },
            { "ProctoringPreventCopyPaste", "true" },
            { "ProctoringPreventRightClick", "true" },
            { "ProctoringPreventScreenshot", "true" },
            { "ProctoringFullScreenRequired", "true" },
            
            // Webcam
            { "ProctoringWebcamRequired", "false" },
            { "ProctoringRecordVideo", "false" },
            { "ProctoringPhotoInterval", "30" },
            { "ProctoringFaceDetection", "false" },
            { "ProctoringMultipleFaceAlert", "true" },
            
            // Audio
            { "ProctoringAudioRequired", "false" },
            { "ProctoringRecordAudio", "false" },
            { "ProctoringNoiseAlert", "true" },
            
            // Screen
            { "ProctoringScreenShare", "false" },
            { "ProctoringRecordScreen", "false" },
            { "ProctoringTabSwitchAlert", "true" },
            { "ProctoringMaxTabSwitches", "3" },
            
            // AI
            { "ProctoringAiEnabled", "false" },
            { "ProctoringAiSensitivity", "Medium" },
            
            // Violations
            { "ProctoringAutoTerminate", "false" },
            { "ProctoringViolationThreshold", "5" },
            { "ProctoringNotifyInstructor", "true" }
        };
    }

    #endregion
}

#region ViewModels

public class ProctoringSettingsViewModel
{
    // General
    public bool IsEnabled { get; set; }
    public string? Provider { get; set; } = "Internal";
    
    // Browser Lockdown
    public bool LockBrowser { get; set; } = true;
    public bool PreventCopyPaste { get; set; } = true;
    public bool PreventRightClick { get; set; } = true;
    public bool PreventScreenshot { get; set; } = true;
    public bool FullScreenRequired { get; set; } = true;
    
    // Webcam
    public bool WebcamRequired { get; set; }
    public bool RecordVideo { get; set; }
    public int PhotoIntervalSeconds { get; set; } = 30;
    public bool FaceDetectionEnabled { get; set; }
    public bool MultipleFaceAlert { get; set; } = true;
    
    // Audio
    public bool AudioRequired { get; set; }
    public bool RecordAudio { get; set; }
    public bool NoiseAlert { get; set; } = true;
    
    // Screen
    public bool ScreenShareRequired { get; set; }
    public bool RecordScreen { get; set; }
    public bool TabSwitchAlert { get; set; } = true;
    public int MaxTabSwitches { get; set; } = 3;
    
    // AI Detection
    public bool AiDetectionEnabled { get; set; }
    public string? AiSensitivity { get; set; } = "Medium";
    
    // Violation Handling
    public bool AutoTerminateOnViolation { get; set; }
    public int ViolationThreshold { get; set; } = 5;
    public bool NotifyInstructorOnViolation { get; set; } = true;
}

public class ProctoringViolationViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = "";
    public string QuizName { get; set; } = "";
    public string ViolationType { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Severity { get; set; } = "";
    public bool IsReviewed { get; set; }
}

#endregion

