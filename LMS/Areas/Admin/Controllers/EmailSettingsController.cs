using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات البريد الإلكتروني - Email Settings Controller
/// Enterprise-level email configuration management
/// </summary>
public class EmailSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailSettingsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public EmailSettingsController(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<EmailSettingsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// الصفحة الرئيسية لإعدادات البريد - Email settings main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Email" || s.Key.StartsWith("Email") || s.Key.StartsWith("Smtp"))
                .ToListAsync();

            // Default settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = GetDefaultEmailSettings();
            }

            // Get email queue statistics
            ViewBag.TotalEmails = await _context.EmailLogs.CountAsync();
            ViewBag.SentEmails = await _context.EmailLogs.CountAsync(e => e.IsSent);
            ViewBag.FailedEmails = await _context.EmailLogs.CountAsync(e => !e.IsSent && e.Attempts >= 3);
            ViewBag.PendingEmails = await _context.EmailLogs.CountAsync(e => !e.IsSent && e.Attempts < 3);

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات البريد الإلكتروني");
            SetWarningMessage("تعذر تحميل إعدادات البريد الإلكتروني. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات البريد - Save email settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmailSettingsViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                SetErrorMessage("يرجى تصحيح الأخطاء في النموذج");
                return RedirectToAction(nameof(Index));
            }

            // Get current password if not provided (keep existing)
            var currentPassword = await _context.PlatformSettings
                .FirstOrDefaultAsync(s => s.Key == "SmtpPassword");
            var passwordToSave = !string.IsNullOrWhiteSpace(model.SmtpPassword) 
                ? model.SmtpPassword 
                : currentPassword?.Value ?? "";

            var settings = new Dictionary<string, string>
            {
                { "SmtpHost", model.SmtpHost ?? "" },
                { "SmtpPort", model.SmtpPort.ToString() },
                { "SmtpUsername", model.SmtpUsername ?? "" },
                { "SmtpPassword", passwordToSave },
                { "SmtpEnableSsl", model.SmtpEnableSsl.ToString() },
                { "EmailFromAddress", model.FromAddress ?? "" },
                { "EmailFromName", model.FromName ?? "" },
                { "EmailReplyTo", model.ReplyToAddress ?? "" },
                { "EmailEnabled", model.IsEnabled.ToString() }
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
                        Category = "Email",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Email settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات البريد الإلكتروني بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving email settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// اختبار إعدادات البريد - Test email settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(string testEmail)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(testEmail))
            {
                SetErrorMessage("يرجى إدخال بريد إلكتروني للاختبار");
                return RedirectToAction(nameof(Index));
            }

            await _emailService.SendEmailAsync(
                testEmail,
                "اختبار إعدادات البريد - LMS Platform",
                @"<html><body dir='rtl'>
                    <h2>اختبار البريد الإلكتروني</h2>
                    <p>تم إرسال هذا البريد للتحقق من صحة إعدادات البريد الإلكتروني.</p>
                    <p>إذا تلقيت هذه الرسالة، فإن الإعدادات تعمل بشكل صحيح.</p>
                    <br/>
                    <p>فريق منصة LMS</p>
                </body></html>",
                true);

            _logger.LogInformation("Test email sent successfully to {Email}", testEmail);
            SetSuccessMessage($"تم إرسال بريد الاختبار بنجاح إلى {testEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}", testEmail);
            SetErrorMessage($"فشل إرسال بريد الاختبار: {ex.Message}");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// سجل البريد المرسل - Email logs
    /// </summary>
    public async Task<IActionResult> Logs(bool? sent, int page = 1)
    {
        try
        {
            var query = _context.EmailLogs.AsQueryable();

            if (sent.HasValue)
            {
                query = query.Where(e => e.IsSent == sent.Value);
            }

            var pageSize = await _configService.GetPaginationSizeAsync("email_logs", 50);
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var logs = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.Sent = sent;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading email logs");
            SetWarningMessage("تعذر تحميل سجلات البريد");
            return View(new List<Domain.Entities.Notifications.EmailLog>());
        }
    }

    #region Private Methods

    private Dictionary<string, string> GetDefaultEmailSettings()
    {
        return new Dictionary<string, string>
        {
            { "SmtpHost", "" },
            { "SmtpPort", "587" },
            { "SmtpUsername", "" },
            { "SmtpPassword", "" },
            { "SmtpEnableSsl", "true" },
            { "EmailFromAddress", "" },
            { "EmailFromName", "منصة LMS" },
            { "EmailReplyTo", "" },
            { "EmailEnabled", "false" }
        };
    }

    #endregion
}

/// <summary>
/// نموذج إعدادات البريد الإلكتروني
/// </summary>
public class EmailSettingsViewModel
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? ReplyToAddress { get; set; }
    public bool IsEnabled { get; set; }
}

