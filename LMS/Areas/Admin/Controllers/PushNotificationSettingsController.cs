using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات الإشعارات الفورية - Push Notification Settings Controller
/// </summary>
public class PushNotificationSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PushNotificationSettingsController> _logger;

    public PushNotificationSettingsController(
        ApplicationDbContext context,
        ILogger<PushNotificationSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الإعدادات - Settings List (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Get notification statistics for today
        var today = DateTime.UtcNow.Date;
        var todayNotifications = await _context.Notifications
            .Where(n => n.CreatedAt >= today)
            .ToListAsync();
        
        var notificationsToday = todayNotifications.Count;
        var readNotifications = todayNotifications.Count(n => n.IsRead);
        var clickRate = notificationsToday > 0 ? (readNotifications * 100 / notificationsToday) : 0;
        
        ViewBag.NotificationsToday = notificationsToday;
        ViewBag.ClickRate = clickRate;

        // Load push notification settings
        var settings = await GetPushNotificationSettingsAsync();
        ViewBag.Settings = settings;

        return View();
    }

    /// <summary>
    /// حفظ إعدادات الإشعارات الفورية - Save push notification settings (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PushNotificationSettingsViewModel model)
    {
        try
        {
            var settings = new Dictionary<string, string>
            {
                { "PushNotificationEnabled", model.Enabled.ToString().ToLower() },
                { "PushNotificationServerKey", model.ServerKey ?? "" },
                { "PushNotificationSenderId", model.SenderId ?? "" },
                
                // Web Push (VAPID) - only save if provided (never log private key)
                { "VAPIDPublicKey", model.VAPIDPublicKey ?? "" },
                { "VAPIDPrivateKey", model.VAPIDPrivateKey ?? "" },
                
                // Notification types
                { "PushNotifyNewCourse", model.NotifyNewCourse.ToString().ToLower() },
                { "PushNotifyEnrollment", model.NotifyEnrollment.ToString().ToLower() },
                { "PushNotifyLiveClass", model.NotifyLiveClass.ToString().ToLower() },
                { "PushNotifyAssignment", model.NotifyAssignment.ToString().ToLower() },
                { "PushNotifyCertificate", model.NotifyCertificate.ToString().ToLower() }
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
                        Category = "PushNotifications",
                        CreatedAt = DateTime.UtcNow,
                        IsSensitive = setting.Key == "VAPIDPrivateKey"
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Push notification settings updated successfully");
            SetSuccessMessage(CultureExtensions.T("تم حفظ إعدادات الإشعارات الفورية بنجاح", "Push notification settings saved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving push notification settings");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء حفظ إعدادات الإشعارات الفورية", "An error occurred while saving push notification settings."));
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// توليد مفاتيح VAPID - Generate VAPID keys for Web Push
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateVapidKeys()
    {
        try
        {
            var vapidKeys = VapidHelper.GenerateVapidKeys();
            var publicKey = vapidKeys.PublicKey;
            var privateKey = vapidKeys.PrivateKey;

            foreach (var key in new[] { ("VAPIDPublicKey", publicKey), ("VAPIDPrivateKey", privateKey) })
            {
                var existing = await _context.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key.Item1);
                if (existing != null)
                {
                    existing.Value = key.Item2;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = key.Item1,
                        Value = key.Item2,
                        Category = "PushNotifications",
                        CreatedAt = DateTime.UtcNow,
                        IsSensitive = key.Item1 == "VAPIDPrivateKey"
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("VAPID keys generated and saved (private key not logged)");
            return Json(new { success = true, message = "تم توليد مفاتيح VAPID وحفظها", publicKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating VAPID keys");
            return Json(new { success = false, message = "حدث خطأ أثناء توليد المفاتيح" });
        }
    }

    /// <summary>
    /// إرسال إشعار تجريبي - Send test notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestNotification()
    {
        try
        {
            // Get current user
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "لم يتم العثور على المستخدم" });
            }

            // Create a test notification
            var notification = new Domain.Entities.Notifications.Notification
            {
                UserId = userId,
                Title = "إشعار تجريبي",
                Message = "هذا إشعار تجريبي للتحقق من صحة إعدادات الإشعارات الفورية.",
                Type = Domain.Enums.NotificationType.System,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Test push notification sent to user {UserId}", userId);
            return Json(new { success = true, message = "تم إرسال الإشعار التجريبي بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test notification");
            return Json(new { success = false, message = "حدث خطأ أثناء إرسال الإشعار التجريبي" });
        }
    }

    /// <summary>
    /// تفاصيل الإعدادات - Settings Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var setting = await _context.PushNotificationSettings
            .Include(pns => pns.User)
            .FirstOrDefaultAsync(pns => pns.Id == id);

        if (setting == null)
            return NotFound();

        return View(setting);
    }

    #region Private Methods

    private async Task<PushNotificationSettingsViewModel> GetPushNotificationSettingsAsync()
    {
        var settings = await _context.PlatformSettings
            .Where(s => s.Group == "PushNotifications" || s.Key.StartsWith("PushNotif"))
            .ToListAsync();

        string GetString(string key, string defaultValue = "") =>
            settings.FirstOrDefault(s => s.Key == key)?.Value ?? defaultValue;

        bool GetBool(string key, bool defaultValue = true) =>
            settings.FirstOrDefault(s => s.Key == key)?.Value?.ToLower() == "true" ||
            (settings.All(s => s.Key != key) && defaultValue);

        return new PushNotificationSettingsViewModel
        {
            Enabled = GetBool("PushNotificationEnabled"),
            ServerKey = GetString("PushNotificationServerKey"),
            SenderId = GetString("PushNotificationSenderId"),
            VAPIDPublicKey = GetString("VAPIDPublicKey"),
            VAPIDPrivateKey = GetString("VAPIDPrivateKey"),
            NotifyNewCourse = GetBool("PushNotifyNewCourse"),
            NotifyEnrollment = GetBool("PushNotifyEnrollment"),
            NotifyLiveClass = GetBool("PushNotifyLiveClass"),
            NotifyAssignment = GetBool("PushNotifyAssignment"),
            NotifyCertificate = GetBool("PushNotifyCertificate")
        };
    }

    #endregion
}

/// <summary>
/// نموذج إعدادات الإشعارات الفورية
/// </summary>
public class PushNotificationSettingsViewModel
{
    public bool Enabled { get; set; } = true;
    public string? ServerKey { get; set; }
    public string? SenderId { get; set; }

    /// <summary>Web Push (VAPID) public key - safe to expose to client.</summary>
    public string? VAPIDPublicKey { get; set; }
    /// <summary>Web Push (VAPID) private key - sensitive, never log or expose to client.</summary>
    public string? VAPIDPrivateKey { get; set; }
    
    // Notification types
    public bool NotifyNewCourse { get; set; } = true;
    public bool NotifyEnrollment { get; set; } = true;
    public bool NotifyLiveClass { get; set; } = true;
    public bool NotifyAssignment { get; set; } = true;
    public bool NotifyCertificate { get; set; } = true;
}

