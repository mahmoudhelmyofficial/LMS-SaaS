using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الإشعارات - Notifications Management Controller
/// </summary>
public class NotificationsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public NotificationsController(
        ApplicationDbContext context,
        ILogger<NotificationsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الإشعارات - Notifications list
    /// </summary>
    public async Task<IActionResult> Index(string? type, bool? isRead, int page = 1)
    {
        var query = _context.Notifications
            .Include(n => n.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<NotificationType>(type, out var notificationType))
        {
            query = query.Where(n => n.Type == notificationType);
        }

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("notifications", 50);
        var totalCount = await query.CountAsync();
        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.IsRead = isRead;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(notifications);
    }

    /// <summary>
    /// إرسال إشعار - Send notification
    /// </summary>
    [HttpGet]
    public IActionResult Send()
    {
        return View(new SendNotificationViewModel());
    }

    /// <summary>
    /// إنشاء إشعار (اسم بديل لـ Send) - Create notification (alias for Send)
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View("Send", new SendNotificationViewModel());
    }

    /// <summary>
    /// حفظ الإشعار الجديد - Save new notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SendNotificationViewModel model)
    {
        return await Send(model);
    }

    /// <summary>
    /// تفاصيل الإشعار - Notification details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var notification = await _context.Notifications
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null)
            return NotFound();

        return View(notification);
    }

    /// <summary>
    /// تحديد كمقروء - Mark as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديد الإشعار كمقروء");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تحديد كمقروء (GET للروابط المباشرة) - Mark as read (GET for direct links)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MarkAsRead(int id, bool redirect = true)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (redirect && !string.IsNullOrEmpty(notification.ActionUrl))
            return Redirect(notification.ActionUrl);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تحديد جميع الإشعارات كمقروءة - Mark all notifications as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null)
            return Unauthorized();
        
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();
        
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", unreadNotifications.Count, user.Id);
        
        SetSuccessMessage($"تم تحديد {unreadNotifications.Count} إشعار كمقروء");
        
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer))
            return Redirect(referer);
        
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الإشعار - Delete notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        
        if (notification == null)
        {
            _logger.LogWarning("Attempted to delete non-existent notification {Id}", id);
            return NotFound();
        }
        
        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted notification {Id}", id);
        
        SetSuccessMessage("تم حذف الإشعار بنجاح");
        
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && !referer.Contains($"/Notifications/Details/{id}"))
            return Redirect(referer);
        
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إرسال الإشعار - Process sending notification
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendNotificationViewModel model)
    {
        if (ModelState.IsValid)
        {
            List<string> recipientIds = new();

            // Determine recipients
            switch (model.Recipients)
            {
                case "All":
                    recipientIds = await _context.Users
                        .Where(u => !u.IsDeleted)
                        .Select(u => u.Id)
                        .ToListAsync();
                    break;

                case "Students":
                    recipientIds = await _context.Users
                        .Where(u => !u.IsDeleted && u.Enrollments.Any())
                        .Select(u => u.Id)
                        .ToListAsync();
                    break;

                case "Instructors":
                    recipientIds = await _context.InstructorProfiles
                        .Select(ip => ip.UserId)
                        .ToListAsync();
                    break;

                case "Specific":
                    recipientIds = model.SpecificUserIds;
                    break;
            }

            // Create notifications
            var notifications = recipientIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = model.Title,
                Message = model.Message,
                Type = model.Type,
                ActionUrl = model.ActionUrl,
                ActionText = model.ActionText,
                IconClass = GetIconClass(model.Type),
                IsRead = false
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            SetSuccessMessage($"تم إرسال الإشعار إلى {recipientIds.Count} مستخدم");
            return RedirectToAction(nameof(Send));
        }

        return View(model);
    }

    /// <summary>
    /// قوالب الإشعارات - Notification templates
    /// </summary>
    public async Task<IActionResult> Templates()
    {
        var templates = await _context.NotificationTemplates
            .OrderBy(t => t.EventType)
            .ToListAsync();

        return View(templates);
    }

    /// <summary>
    /// إنشاء قالب - Create template
    /// </summary>
    [HttpGet]
    public IActionResult CreateTemplate()
    {
        return View(new NotificationTemplateViewModel());
    }

    /// <summary>
    /// حفظ القالب - Save template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(NotificationTemplateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var template = new NotificationTemplate
            {
                Name = model.Name,
                Title = model.Title,
                Content = model.Content,
                EventType = model.EventType,
                IsActive = model.IsActive
            };

            _context.NotificationTemplates.Add(template);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء القالب بنجاح");
            return RedirectToAction(nameof(Templates));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل قالب - Edit template
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var viewModel = new NotificationTemplateViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Title = template.Title,
            Content = template.Content,
            EventType = template.EventType,
            IsActive = template.IsActive
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات القالب - Save template edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(int id, NotificationTemplateViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            template.Name = model.Name;
            template.Title = model.Title;
            template.Content = model.Content;
            template.EventType = model.EventType;
            template.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القالب بنجاح");
            return RedirectToAction(nameof(Templates));
        }

        return View(model);
    }

    /// <summary>
    /// حذف قالب - Delete template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        _context.NotificationTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح");
        return RedirectToAction(nameof(Templates));
    }

    private string GetIconClass(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => "fa-check-circle",
            NotificationType.Warning => "fa-exclamation-triangle",
            NotificationType.Error => "fa-times-circle",
            _ => "fa-info-circle"
        };
    }
}

