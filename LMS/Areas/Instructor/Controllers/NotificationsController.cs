using System.Text.Json.Serialization;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// وحدة تحكم إشعارات المدرس - Instructor Notifications Controller
/// Enterprise-level implementation using service layer with caching and filtering
/// </summary>
public class NotificationsController : InstructorBaseController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IPushSubscriptionStore _pushSubscriptionStore;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        IPlatformSettingsService platformSettings,
        IPushSubscriptionStore pushSubscriptionStore,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
        _pushSubscriptionStore = pushSubscriptionStore;
        _logger = logger;
    }

    /// <summary>
    /// عرض جميع الإشعارات مع التصفية - All notifications with filtering
    /// </summary>
    public async Task<IActionResult> Index(
        bool? unreadOnly,
        NotificationType? type,
        int? priority,
        string? search,
        int page = 1)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User ID is null or empty in NotificationsController.Index");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Build filter
            var filter = new NotificationFilterDto
            {
                UnreadOnly = unreadOnly,
                Type = type,
                Priority = priority,
                SearchTerm = search
            };

            // Get paginated notifications using service
            var notifications = await _notificationService.GetFilteredNotificationsAsync(
                userId, filter, page, pageSize: 20);

            // Get statistics for dashboard cards
            var stats = await _notificationService.GetNotificationStatsAsync(userId);

            // Pass data to view
            ViewBag.Stats = stats;
            ViewBag.Filter = filter;
            ViewBag.UnreadOnly = unreadOnly;
            ViewBag.Type = type;
            ViewBag.Priority = priority;
            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = notifications.TotalPages;
            ViewBag.TotalItems = notifications.TotalCount;

            _logger.LogInformation(
                "Instructor {InstructorId} viewed notifications. Total: {Count}, Unread: {Unread}",
                userId, stats.TotalCount, stats.UnreadCount);

            return View(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notifications for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الإشعارات");

            ViewBag.Stats = new NotificationStatsDto();
            ViewBag.Filter = new NotificationFilterDto();
            ViewBag.UnreadOnly = unreadOnly;
            ViewBag.Page = 1;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;

            return View(PaginatedResult<Domain.Entities.Notifications.Notification>.Empty());
        }
    }

    /// <summary>
    /// تحديد الإشعار كمقروء - Mark notification as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, string? returnUrl = null)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        try
        {
            // Verify ownership before marking as read
            var notification = await _notificationService.GetNotificationByIdAsync(id, userId);

            if (notification == null)
            {
                _logger.LogWarning("Notification {NotificationId} not found for user {UserId}", id, userId);
                return NotFound();
            }

            var result = await _notificationService.MarkAsReadAsync(id);

            if (result.IsFailure)
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث الإشعار");
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Notification {NotificationId} marked as read by user {UserId}", id, userId);

            // If there's an action URL, redirect to it
            if (!string.IsNullOrEmpty(notification.ActionUrl))
            {
                return Redirect(notification.ActionUrl);
            }

            // Return to specified URL or index
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الإشعار");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تحديد جميع الإشعارات كمقروءة - Mark all notifications as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        try
        {
            var result = await _notificationService.MarkAllAsReadAsync(userId);

            if (result.IsSuccess)
            {
                var stats = await _notificationService.GetNotificationStatsAsync(userId);
                if (stats.UnreadCount == 0)
                {
                    SetSuccessMessage("تم تحديد جميع الإشعارات كمقروءة");
                }
            }
            else
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث الإشعارات");
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإشعارات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حذف إشعار - Delete notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        try
        {
            // Verify ownership
            var notification = await _notificationService.GetNotificationByIdAsync(id, userId);

            if (notification == null)
            {
                _logger.LogWarning("Notification {NotificationId} not found for user {UserId}", id, userId);
                return NotFound();
            }

            var result = await _notificationService.DeleteNotificationAsync(id);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Notification {NotificationId} deleted by user {UserId}", id, userId);
                SetSuccessMessage("تم حذف الإشعار بنجاح");
            }
            else
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف الإشعار");
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الإشعار");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حذف جميع الإشعارات المقروءة - Delete all read notifications
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllRead()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        try
        {
            var result = await _notificationService.DeleteAllReadAsync(userId);

            if (result.IsSuccess)
            {
                var count = result.Value;
                if (count > 0)
                {
                    _logger.LogInformation(
                        "All read notifications ({Count}) deleted for user {UserId}",
                        count, userId);
                    SetSuccessMessage($"تم حذف {count} إشعار");
                }
                else
                {
                    SetInfoMessage("لا توجد إشعارات مقروءة للحذف");
                }
            }
            else
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف الإشعارات");
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all read notifications for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء حذف الإشعارات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الحصول على عدد الإشعارات غير المقروءة - Get unread count (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new { count = 0 });

        try
        {
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count = unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notification count for user {UserId}", userId);
            return Json(new { count = 0 });
        }
    }

    /// <summary>
    /// الحصول على الإشعارات غير المقروءة - Get recent unread notifications (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentUnread(int count = 5)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new { notifications = Array.Empty<object>() });

        try
        {
            var notifications = await _notificationService.GetUnreadNotificationsAsync(userId, count);

            var result = notifications.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                type = n.Type.ToString(),
                actionUrl = n.ActionUrl,
                createdAt = n.CreatedAt,
                timeAgo = GetTimeAgo(n.CreatedAt),
                icon = GetIconForType(n.Type),
                color = GetColorForType(n.Type)
            });

            return Json(new { notifications = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent unread notifications for user {UserId}", userId);
            return Json(new { notifications = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// الحصول على المفتاح العام VAPID - Get VAPID public key for Web Push subscription
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PushPublicKey()
    {
        var publicKey = await _platformSettings.GetSettingAsync("VAPIDPublicKey");
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return Json(new { error = "VAPID not configured" });
        }
        return Json(new { publicKey });
    }

    /// <summary>
    /// حفظ اشتراك Web Push - Subscribe (save) Web Push subscription
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SubscribePush([FromBody] WebPushSubscribeRequest? request)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, error = "Unauthorized" });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Endpoint) ||
            request.Keys == null || string.IsNullOrWhiteSpace(request.Keys.P256Dh) || string.IsNullOrWhiteSpace(request.Keys.Auth))
        {
            return Json(new { success = false, error = "endpoint and keys (p256dh, auth) are required" });
        }

        if (request.Endpoint.Length > 2000)
        {
            return Json(new { success = false, error = "Invalid endpoint" });
        }

        if (request.Keys.P256Dh.Length > 500 || request.Keys.Auth.Length > 500)
        {
            return Json(new { success = false, error = "Invalid keys length" });
        }

        try
        {
            await _pushSubscriptionStore.SaveOrUpdateAsync(
                userId,
                request.Endpoint.Trim(),
                request.Keys.P256Dh.Trim(),
                request.Keys.Auth.Trim(),
                request.UserAgent?.Trim(),
                request.DeviceLabel?.Trim());
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Web Push subscription for user {UserId}", userId);
            return Json(new { success = false, error = "Failed to save subscription" });
        }
    }

    /// <summary>
    /// الحصول على الإحصائيات - Get notification statistics (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new NotificationStatsDto());

        try
        {
            var stats = await _notificationService.GetNotificationStatsAsync(userId);
            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification stats for user {UserId}", userId);
            return Json(new NotificationStatsDto());
        }
    }

    #region Helper Methods

    private static string GetTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        return timeSpan.TotalMinutes switch
        {
            < 1 => "الآن",
            < 60 => $"منذ {(int)timeSpan.TotalMinutes} دقيقة",
            < 1440 => $"منذ {(int)timeSpan.TotalHours} ساعة",
            < 10080 => $"منذ {(int)timeSpan.TotalDays} يوم",
            _ => dateTime.ToString("dd/MM/yyyy")
        };
    }

    private static string GetIconForType(NotificationType type) => type switch
    {
        NotificationType.NewEnrollment or NotificationType.CourseEnrollment => "feather-user-plus",
        NotificationType.NewReview => "feather-star",
        NotificationType.Message or NotificationType.NewMessage => "feather-mail",
        NotificationType.Payment or NotificationType.PaymentUpdate => "feather-dollar-sign",
        NotificationType.Course or NotificationType.CourseUpdate => "feather-book",
        NotificationType.AssignmentSubmitted or NotificationType.AssignmentGraded => "feather-file-text",
        NotificationType.Assessment or NotificationType.QuizCompleted or NotificationType.QuizGraded => "feather-help-circle",
        NotificationType.DiscussionReply => "feather-message-circle",
        NotificationType.System => "feather-settings",
        NotificationType.Reminder => "feather-clock",
        NotificationType.Achievement or NotificationType.BadgeEarned => "feather-award",
        NotificationType.Certificate or NotificationType.CertificateIssued => "feather-award",
        _ => "feather-bell"
    };

    private static string GetColorForType(NotificationType type) => type switch
    {
        NotificationType.NewEnrollment or NotificationType.CourseEnrollment => "primary",
        NotificationType.NewReview => "warning",
        NotificationType.Message or NotificationType.NewMessage => "info",
        NotificationType.Payment or NotificationType.PaymentUpdate => "success",
        NotificationType.Course or NotificationType.CourseUpdate => "purple",
        NotificationType.AssignmentSubmitted or NotificationType.AssignmentGraded => "orange",
        NotificationType.Assessment or NotificationType.QuizCompleted or NotificationType.QuizGraded => "cyan",
        NotificationType.DiscussionReply => "teal",
        NotificationType.System => "secondary",
        NotificationType.Warning => "warning",
        NotificationType.Error => "danger",
        NotificationType.Success => "success",
        _ => "primary"
    };

    #endregion
}

/// <summary>Request body for Web Push subscribe endpoint.</summary>
public class WebPushSubscribeRequest
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("keys")]
    public WebPushKeysDto Keys { get; set; } = new();

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("deviceLabel")]
    public string? DeviceLabel { get; set; }
}

/// <summary>Keys part of Web Push subscription.</summary>
public class WebPushKeysDto
{
    [JsonPropertyName("p256dh")]
    public string P256Dh { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public string Auth { get; set; } = string.Empty;
}
