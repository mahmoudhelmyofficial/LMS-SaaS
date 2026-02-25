using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة تفضيلات الإشعارات - Notification Preferences Controller
/// </summary>
public class NotificationPreferencesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationPreferencesController> _logger;

    public NotificationPreferencesController(
        ApplicationDbContext context,
        ILogger<NotificationPreferencesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التفضيلات - Preferences List (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm)
    {
        var query = _context.NotificationPreferences
            .Include(np => np.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(np => (np.User != null && np.User.Email != null && np.User.Email.Contains(searchTerm)) || 
                                     (np.User != null && np.User.FirstName != null && np.User.FirstName.Contains(searchTerm)) ||
                                     (np.User != null && np.User.LastName != null && np.User.LastName.Contains(searchTerm)));
        }

        var preferences = await query
            .OrderBy(np => np.User != null ? np.User.Email ?? "" : "")
            .Take(200)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;

        // Load global notification settings
        var globalSettings = await GetGlobalNotificationSettingsAsync();
        ViewBag.GlobalSettings = globalSettings;

        return View(preferences);
    }

    /// <summary>
    /// حفظ تفضيلات الإشعارات العامة - Save global notification preferences (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(GlobalNotificationPreferencesViewModel model)
    {
        try
        {
            var settings = new Dictionary<string, string>
            {
                // Student notifications
                { "NotifyStudentEnrollmentEmail", model.StudentEnrollmentEmail.ToString().ToLower() },
                { "NotifyStudentEnrollmentSms", model.StudentEnrollmentSms.ToString().ToLower() },
                { "NotifyStudentEnrollmentPush", model.StudentEnrollmentPush.ToString().ToLower() },
                { "NotifyStudentEnrollmentInApp", model.StudentEnrollmentInApp.ToString().ToLower() },
                
                { "NotifyStudentLiveClassEmail", model.StudentLiveClassEmail.ToString().ToLower() },
                { "NotifyStudentLiveClassSms", model.StudentLiveClassSms.ToString().ToLower() },
                { "NotifyStudentLiveClassPush", model.StudentLiveClassPush.ToString().ToLower() },
                { "NotifyStudentLiveClassInApp", model.StudentLiveClassInApp.ToString().ToLower() },
                
                { "NotifyStudentReminderEmail", model.StudentReminderEmail.ToString().ToLower() },
                { "NotifyStudentReminderSms", model.StudentReminderSms.ToString().ToLower() },
                { "NotifyStudentReminderPush", model.StudentReminderPush.ToString().ToLower() },
                { "NotifyStudentReminderInApp", model.StudentReminderInApp.ToString().ToLower() },
                
                { "NotifyStudentCompletionEmail", model.StudentCompletionEmail.ToString().ToLower() },
                { "NotifyStudentCompletionSms", model.StudentCompletionSms.ToString().ToLower() },
                { "NotifyStudentCompletionPush", model.StudentCompletionPush.ToString().ToLower() },
                { "NotifyStudentCompletionInApp", model.StudentCompletionInApp.ToString().ToLower() },
                
                { "NotifyStudentCertificateEmail", model.StudentCertificateEmail.ToString().ToLower() },
                { "NotifyStudentCertificateSms", model.StudentCertificateSms.ToString().ToLower() },
                { "NotifyStudentCertificatePush", model.StudentCertificatePush.ToString().ToLower() },
                { "NotifyStudentCertificateInApp", model.StudentCertificateInApp.ToString().ToLower() },
                
                { "NotifyStudentCommentReplyEmail", model.StudentCommentReplyEmail.ToString().ToLower() },
                { "NotifyStudentCommentReplySms", model.StudentCommentReplySms.ToString().ToLower() },
                { "NotifyStudentCommentReplyPush", model.StudentCommentReplyPush.ToString().ToLower() },
                { "NotifyStudentCommentReplyInApp", model.StudentCommentReplyInApp.ToString().ToLower() },
                
                // Instructor notifications
                { "NotifyInstructorNewStudentEmail", model.InstructorNewStudentEmail.ToString().ToLower() },
                { "NotifyInstructorNewStudentInApp", model.InstructorNewStudentInApp.ToString().ToLower() },
                
                { "NotifyInstructorNewReviewEmail", model.InstructorNewReviewEmail.ToString().ToLower() },
                { "NotifyInstructorNewReviewInApp", model.InstructorNewReviewInApp.ToString().ToLower() },
                
                { "NotifyInstructorQuestionEmail", model.InstructorQuestionEmail.ToString().ToLower() },
                { "NotifyInstructorQuestionInApp", model.InstructorQuestionInApp.ToString().ToLower() },
                
                { "NotifyInstructorPaymentEmail", model.InstructorPaymentEmail.ToString().ToLower() },
                { "NotifyInstructorPaymentInApp", model.InstructorPaymentInApp.ToString().ToLower() },
                
                // Admin notifications
                { "NotifyAdminNewUserEmail", model.AdminNewUserEmail.ToString().ToLower() },
                { "NotifyAdminNewUserInApp", model.AdminNewUserInApp.ToString().ToLower() },
                
                { "NotifyAdminNewCourseEmail", model.AdminNewCourseEmail.ToString().ToLower() },
                { "NotifyAdminNewCourseInApp", model.AdminNewCourseInApp.ToString().ToLower() },
                
                { "NotifyAdminNewPaymentEmail", model.AdminNewPaymentEmail.ToString().ToLower() },
                { "NotifyAdminNewPaymentInApp", model.AdminNewPaymentInApp.ToString().ToLower() },
                
                { "NotifyAdminRefundRequestEmail", model.AdminRefundRequestEmail.ToString().ToLower() },
                { "NotifyAdminRefundRequestInApp", model.AdminRefundRequestInApp.ToString().ToLower() }
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
                        Category = "Notifications",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Global notification preferences updated successfully");
            SetSuccessMessage("تم حفظ تفضيلات الإشعارات بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving notification preferences");
            SetErrorMessage("حدث خطأ أثناء حفظ تفضيلات الإشعارات");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تفاصيل التفضيلات - Preferences Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var preference = await _context.NotificationPreferences
            .Include(np => np.User)
            .FirstOrDefaultAsync(np => np.Id == id);

        if (preference == null)
            return NotFound();

        return View(preference);
    }

    #region Private Methods

    private async Task<GlobalNotificationPreferencesViewModel> GetGlobalNotificationSettingsAsync()
    {
        var settings = await _context.PlatformSettings
            .Where(s => s.Group == "Notifications" || s.Key.StartsWith("Notify"))
            .ToListAsync();

        bool GetBool(string key, bool defaultValue = true) =>
            settings.FirstOrDefault(s => s.Key == key)?.Value?.ToLower() == "true" || 
            (settings.All(s => s.Key != key) && defaultValue);

        return new GlobalNotificationPreferencesViewModel
        {
            // Student notifications
            StudentEnrollmentEmail = GetBool("NotifyStudentEnrollmentEmail"),
            StudentEnrollmentSms = GetBool("NotifyStudentEnrollmentSms", false),
            StudentEnrollmentPush = GetBool("NotifyStudentEnrollmentPush"),
            StudentEnrollmentInApp = GetBool("NotifyStudentEnrollmentInApp"),
            
            StudentLiveClassEmail = GetBool("NotifyStudentLiveClassEmail"),
            StudentLiveClassSms = GetBool("NotifyStudentLiveClassSms"),
            StudentLiveClassPush = GetBool("NotifyStudentLiveClassPush"),
            StudentLiveClassInApp = GetBool("NotifyStudentLiveClassInApp"),
            
            StudentReminderEmail = GetBool("NotifyStudentReminderEmail"),
            StudentReminderSms = GetBool("NotifyStudentReminderSms", false),
            StudentReminderPush = GetBool("NotifyStudentReminderPush"),
            StudentReminderInApp = GetBool("NotifyStudentReminderInApp"),
            
            StudentCompletionEmail = GetBool("NotifyStudentCompletionEmail"),
            StudentCompletionSms = GetBool("NotifyStudentCompletionSms", false),
            StudentCompletionPush = GetBool("NotifyStudentCompletionPush"),
            StudentCompletionInApp = GetBool("NotifyStudentCompletionInApp"),
            
            StudentCertificateEmail = GetBool("NotifyStudentCertificateEmail"),
            StudentCertificateSms = GetBool("NotifyStudentCertificateSms", false),
            StudentCertificatePush = GetBool("NotifyStudentCertificatePush"),
            StudentCertificateInApp = GetBool("NotifyStudentCertificateInApp"),
            
            StudentCommentReplyEmail = GetBool("NotifyStudentCommentReplyEmail"),
            StudentCommentReplySms = GetBool("NotifyStudentCommentReplySms", false),
            StudentCommentReplyPush = GetBool("NotifyStudentCommentReplyPush"),
            StudentCommentReplyInApp = GetBool("NotifyStudentCommentReplyInApp"),
            
            // Instructor notifications
            InstructorNewStudentEmail = GetBool("NotifyInstructorNewStudentEmail"),
            InstructorNewStudentInApp = GetBool("NotifyInstructorNewStudentInApp"),
            
            InstructorNewReviewEmail = GetBool("NotifyInstructorNewReviewEmail"),
            InstructorNewReviewInApp = GetBool("NotifyInstructorNewReviewInApp"),
            
            InstructorQuestionEmail = GetBool("NotifyInstructorQuestionEmail"),
            InstructorQuestionInApp = GetBool("NotifyInstructorQuestionInApp"),
            
            InstructorPaymentEmail = GetBool("NotifyInstructorPaymentEmail"),
            InstructorPaymentInApp = GetBool("NotifyInstructorPaymentInApp"),
            
            // Admin notifications
            AdminNewUserEmail = GetBool("NotifyAdminNewUserEmail"),
            AdminNewUserInApp = GetBool("NotifyAdminNewUserInApp"),
            
            AdminNewCourseEmail = GetBool("NotifyAdminNewCourseEmail"),
            AdminNewCourseInApp = GetBool("NotifyAdminNewCourseInApp"),
            
            AdminNewPaymentEmail = GetBool("NotifyAdminNewPaymentEmail"),
            AdminNewPaymentInApp = GetBool("NotifyAdminNewPaymentInApp"),
            
            AdminRefundRequestEmail = GetBool("NotifyAdminRefundRequestEmail"),
            AdminRefundRequestInApp = GetBool("NotifyAdminRefundRequestInApp")
        };
    }

    #endregion
}

/// <summary>
/// نموذج تفضيلات الإشعارات العامة
/// </summary>
public class GlobalNotificationPreferencesViewModel
{
    // Student notifications
    public bool StudentEnrollmentEmail { get; set; } = true;
    public bool StudentEnrollmentSms { get; set; }
    public bool StudentEnrollmentPush { get; set; } = true;
    public bool StudentEnrollmentInApp { get; set; } = true;
    
    public bool StudentLiveClassEmail { get; set; } = true;
    public bool StudentLiveClassSms { get; set; } = true;
    public bool StudentLiveClassPush { get; set; } = true;
    public bool StudentLiveClassInApp { get; set; } = true;
    
    public bool StudentReminderEmail { get; set; } = true;
    public bool StudentReminderSms { get; set; }
    public bool StudentReminderPush { get; set; } = true;
    public bool StudentReminderInApp { get; set; } = true;
    
    public bool StudentCompletionEmail { get; set; } = true;
    public bool StudentCompletionSms { get; set; }
    public bool StudentCompletionPush { get; set; } = true;
    public bool StudentCompletionInApp { get; set; } = true;
    
    public bool StudentCertificateEmail { get; set; } = true;
    public bool StudentCertificateSms { get; set; }
    public bool StudentCertificatePush { get; set; } = true;
    public bool StudentCertificateInApp { get; set; } = true;
    
    public bool StudentCommentReplyEmail { get; set; } = true;
    public bool StudentCommentReplySms { get; set; }
    public bool StudentCommentReplyPush { get; set; } = true;
    public bool StudentCommentReplyInApp { get; set; } = true;
    
    // Instructor notifications
    public bool InstructorNewStudentEmail { get; set; } = true;
    public bool InstructorNewStudentInApp { get; set; } = true;
    
    public bool InstructorNewReviewEmail { get; set; } = true;
    public bool InstructorNewReviewInApp { get; set; } = true;
    
    public bool InstructorQuestionEmail { get; set; } = true;
    public bool InstructorQuestionInApp { get; set; } = true;
    
    public bool InstructorPaymentEmail { get; set; } = true;
    public bool InstructorPaymentInApp { get; set; } = true;
    
    // Admin notifications
    public bool AdminNewUserEmail { get; set; } = true;
    public bool AdminNewUserInApp { get; set; } = true;
    
    public bool AdminNewCourseEmail { get; set; } = true;
    public bool AdminNewCourseInApp { get; set; } = true;
    
    public bool AdminNewPaymentEmail { get; set; } = true;
    public bool AdminNewPaymentInApp { get; set; } = true;
    
    public bool AdminRefundRequestEmail { get; set; } = true;
    public bool AdminRefundRequestInApp { get; set; } = true;
}

