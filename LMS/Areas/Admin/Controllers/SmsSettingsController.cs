using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات الرسائل النصية - SMS Settings Controller
/// </summary>
public class SmsSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<SmsSettingsController> _logger;

    public SmsSettingsController(
        ApplicationDbContext context,
        ISmsService smsService,
        ILogger<SmsSettingsController> logger)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
    }

    /// <summary>
    /// عرض وتعديل إعدادات الرسائل النصية - View and Edit SMS Settings
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var setting = await _context.SmsSettings.FirstOrDefaultAsync();

        if (setting == null)
        {
            // Create default settings
            setting = new SmsSetting();
            _context.SmsSettings.Add(setting);
            await _context.SaveChangesAsync();
        }

        // Get SMS consumption statistics for this month
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var smsNotifications = await _context.Notifications
            .Where(n => n.Type == LMS.Domain.Enums.NotificationType.Sms && n.CreatedAt >= startOfMonth)
            .CountAsync();
        
        var dailyLimit = setting.DailyLimitPerUser * 30; // Approximate monthly limit
        ViewBag.SmsConsumption = smsNotifications;
        ViewBag.SmsLimit = dailyLimit > 0 ? dailyLimit : 1000;
        ViewBag.SmsPercentage = dailyLimit > 0 ? (smsNotifications * 100 / dailyLimit) : 0;

        var viewModel = new SmsSettingViewModel
        {
            Id = setting.Id,
            Provider = setting.Provider,
            ApiKey = setting.ApiKey,
            ApiSecret = setting.ApiSecret,
            SenderId = setting.SenderId,
            IsEnabled = setting.IsEnabled,
            TestMode = setting.TestMode,
            TestPhoneNumber = setting.TestPhoneNumber,
            MaxRetryAttempts = setting.MaxRetryAttempts,
            RetryDelaySeconds = setting.RetryDelaySeconds,
            EnableDeliveryReports = setting.EnableDeliveryReports,
            WebhookUrl = setting.WebhookUrl,
            SupportedCountries = setting.SupportedCountries,
            ConfigurationJson = setting.ConfigurationJson
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ إعدادات الرسائل النصية - Save SMS Settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SmsSettingViewModel model)
    {
        if (ModelState.IsValid)
        {
            var setting = await _context.SmsSettings.FindAsync(model.Id);
            if (setting == null)
                return NotFound();

            setting.Provider = model.Provider;
            setting.ApiKey = model.ApiKey;
            setting.ApiSecret = model.ApiSecret;
            setting.SenderId = model.SenderId;
            setting.IsEnabled = model.IsEnabled;
            setting.TestMode = model.TestMode;
            setting.TestPhoneNumber = model.TestPhoneNumber;
            setting.MaxRetryAttempts = model.MaxRetryAttempts;
            setting.RetryDelaySeconds = model.RetryDelaySeconds;
            setting.EnableDeliveryReports = model.EnableDeliveryReports;
            setting.WebhookUrl = model.WebhookUrl;
            setting.SupportedCountries = model.SupportedCountries;
            setting.ConfigurationJson = model.ConfigurationJson;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم حفظ إعدادات الرسائل النصية بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// إرسال رسالة اختبار - Send test SMS
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string phoneNumber, string message)
    {
        if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(message))
        {
            SetErrorMessage("رقم الهاتف والرسالة مطلوبان");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _smsService.SendSmsAsync(phoneNumber, message);
            
            if (result.Success)
            {
                _logger.LogInformation("Test SMS sent successfully to {Phone} via {Provider}, MessageId: {MessageId}", 
                    phoneNumber, result.Provider, result.MessageId);
                SetSuccessMessage($"تم إرسال رسالة اختبار إلى {phoneNumber} بنجاح (معرف الرسالة: {result.MessageId})");
            }
            else
            {
                _logger.LogWarning("Test SMS failed to {Phone}: {Error}", phoneNumber, result.ErrorMessage);
                SetErrorMessage($"فشل إرسال الرسالة: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test SMS to {Phone}", phoneNumber);
            SetErrorMessage($"حدث خطأ أثناء إرسال الرسالة: {ex.Message}");
        }

        return RedirectToAction(nameof(Index));
    }
}

