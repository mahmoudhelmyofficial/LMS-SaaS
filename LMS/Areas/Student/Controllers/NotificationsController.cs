using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الإشعارات - Notifications Controller
/// </summary>
public class NotificationsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<NotificationsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الإشعارات - Notifications list
    /// </summary>
    public async Task<IActionResult> Index(bool? unreadOnly, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Notifications
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(n => new NotificationDisplayViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                ActionUrl = n.ActionUrl,
                ActionText = n.ActionText,
                IconClass = n.IconClass,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                RelatedEntity = n.RelatedEntity,
                RelatedEntityId = n.RelatedEntityId
            })
            .ToListAsync();

        // Get statistics
        var stats = new NotificationStatsViewModel
        {
            TotalUnread = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead),
            TotalToday = await _context.Notifications
                .CountAsync(n => n.UserId == userId && n.CreatedAt.Date == DateTime.UtcNow.Date),
            TotalThisWeek = await _context.Notifications
                .CountAsync(n => n.UserId == userId && n.CreatedAt >= DateTime.UtcNow.AddDays(-7))
        };

        ViewBag.Stats = stats;
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.Page = page;

        return View(notifications);
    }

    /// <summary>
    /// تحديد كمقروء - Mark as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = _currentUserService.UserId;

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// تحديد الكل كمقروء - Mark all as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _currentUserService.UserId;

        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم تحديد {unreadNotifications.Count} إشعار كمقروء");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الإشعار - Delete notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null)
            return NotFound();

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// حذف كل الإشعارات المقروءة - Delete all read notifications
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllRead()
    {
        var userId = _currentUserService.UserId;

        var readNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && n.IsRead)
            .ToListAsync();

        _context.Notifications.RemoveRange(readNotifications);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {readNotifications.Count} إشعار");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الحصول على عدد الإشعارات غير المقروءة - Get unread count (for navbar)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUserService.UserId;

        var count = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        return Ok(new { count });
    }

    /// <summary>
    /// الحصول على أحدث الإشعارات - Get recent notifications (for dropdown)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecent(int count = 5)
    {
        var userId = _currentUserService.UserId;

        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.ActionUrl,
                n.IconClass,
                n.CreatedAt,
                n.IsRead
            })
            .ToListAsync();

        return Ok(notifications);
    }

    /// <summary>
    /// إعدادات الإشعارات - Notification settings (canonical: Student Settings hub)
    /// </summary>
    [HttpGet]
    public IActionResult Settings()
    {
        return RedirectToAction("Index", "Settings", new { area = "Student", tab = "notifications" });
    }

    /// <summary>
    /// حفظ إعدادات الإشعارات - Save (redirect to canonical Settings hub)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(NotificationSettingsViewModel _)
    {
        return RedirectToAction("Index", "Settings", new { area = "Student", tab = "notifications" });
    }
}

