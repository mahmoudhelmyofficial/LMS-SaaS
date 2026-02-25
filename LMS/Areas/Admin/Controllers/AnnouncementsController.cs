using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الإعلانات - Announcements Management Controller
/// </summary>
public class AnnouncementsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AnnouncementsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public AnnouncementsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<AnnouncementsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الإعلانات - Announcements list
    /// </summary>
    public async Task<IActionResult> Index(bool? active, string? type, int page = 1)
    {
        var query = _context.Announcements
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(a => a.IsActive == active.Value);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(a => a.Type == type);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("announcements", 20);
        var totalCount = await query.CountAsync();
        var announcements = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AnnouncementDisplayViewModel
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                Type = a.Type,
                Priority = a.Priority,
                Target = a.Target,
                CreatedAt = a.CreatedAt,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                IsPinned = a.IsPinned,
                IsActive = a.IsActive,
                ViewsCount = a.ViewsCount,
                CreatedByName = a.CreatedByUser != null ? $"{a.CreatedByUser.FirstName} {a.CreatedByUser.LastName}" : "غير معروف",
                ShowOnLandingPage = a.ShowOnLandingPage
            })
            .ToListAsync();

        ViewBag.Active = active;
        ViewBag.Type = type;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(announcements);
    }

    /// <summary>
    /// تفاصيل الإعلان - Announcement details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var announcement = await _context.Announcements
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (announcement == null)
            return NotFound();

        // Get related announcements (same type or same target, excluding current)
        var relatedAnnouncements = await _context.Announcements
            .Where(a => a.Id != id && a.IsActive && 
                       (a.Type == announcement.Type || a.Target == announcement.Target))
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.RelatedAnnouncements = relatedAnnouncements;

        return View(announcement);
    }

    /// <summary>
    /// إنشاء إعلان جديد - Create new announcement
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new AnnouncementCreateViewModel());
    }

    /// <summary>
    /// حفظ الإعلان الجديد - Save new announcement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var announcement = new SystemAnnouncement
            {
                Title = model.Title,
                Content = model.Content,
                Type = model.Type,
                Priority = model.Priority,
                Target = model.Target,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                IsPinned = model.IsPinned,
                IsActive = model.IsActive,
                ShowOnLandingPage = model.ShowOnLandingPage,
                CreatedById = _currentUserService.UserId
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            // Send notification if requested
            if (model.SendNotification)
            {
                await SendAnnouncementNotifications(announcement);
            }

            SetSuccessMessage("تم إنشاء الإعلان بنجاح");
            return RedirectToAction(nameof(Details), new { id = announcement.Id });
        }

        return View(model);
    }

    /// <summary>
    /// تعديل الإعلان - Edit announcement
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        var viewModel = new AnnouncementEditViewModel
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Content = announcement.Content,
            Type = announcement.Type,
            Priority = announcement.Priority,
            Target = announcement.Target,
            StartDate = announcement.StartDate,
            EndDate = announcement.EndDate,
            IsPinned = announcement.IsPinned,
            IsActive = announcement.IsActive,
            ViewsCount = announcement.ViewsCount,
            ShowOnLandingPage = announcement.ShowOnLandingPage
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الإعلان - Save announcement edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AnnouncementEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            announcement.Title = model.Title;
            announcement.Content = model.Content;
            announcement.Type = model.Type;
            announcement.Priority = model.Priority;
            announcement.Target = model.Target;
            announcement.StartDate = model.StartDate;
            announcement.EndDate = model.EndDate;
            announcement.IsPinned = model.IsPinned;
            announcement.IsActive = model.IsActive;
            announcement.ShowOnLandingPage = model.ShowOnLandingPage;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الإعلان بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// حذف الإعلان - Delete announcement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الإعلان بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل التثبيت - Toggle pin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        announcement.IsPinned = !announcement.IsPinned;
        await _context.SaveChangesAsync();

        SetSuccessMessage(announcement.IsPinned ? "تم تثبيت الإعلان" : "تم إلغاء التثبيت");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// نسخ الإعلان - Duplicate announcement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        var newAnnouncement = new SystemAnnouncement
        {
            Title = $"{announcement.Title} - نسخة",
            Content = announcement.Content,
            Type = announcement.Type,
            Priority = announcement.Priority,
            Target = announcement.Target,
            StartDate = announcement.StartDate,
            EndDate = announcement.EndDate,
            IsPinned = false,
            IsActive = false,
            CreatedById = _currentUserService.UserId
        };

        _context.Announcements.Add(newAnnouncement);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم نسخ الإعلان بنجاح");
        return RedirectToAction(nameof(Edit), new { id = newAnnouncement.Id });
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        try
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                SetErrorMessage("الإعلان غير موجود");
                return RedirectToAction(nameof(Index));
            }

            announcement.IsActive = !announcement.IsActive;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Announcement {AnnouncementId} {Status} by admin", 
                id, announcement.IsActive ? "activated" : "deactivated");

            SetSuccessMessage(announcement.IsActive ? "تم تفعيل الإعلان" : "تم تعطيل الإعلان");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling announcement {AnnouncementId} status", id);
            SetErrorMessage("حدث خطأ أثناء تغيير حالة الإعلان");
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task SendAnnouncementNotifications(SystemAnnouncement announcement)
    {
        try
        {
            List<string> recipientIds = announcement.Target switch
            {
                "Students" => await _context.Users
                    .Where(u => !u.IsDeleted && u.Enrollments.Any())
                    .Select(u => u.Id)
                    .ToListAsync(),
                
                "Instructors" => await _context.InstructorProfiles
                    .Select(ip => ip.UserId)
                    .ToListAsync(),
                
                "Admins" => await _context.UserRoles
                    .Where(ur => ur.RoleId == _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefault())
                    .Select(ur => ur.UserId)
                    .ToListAsync(),
                
                _ => await _context.Users
                    .Where(u => !u.IsDeleted)
                    .Select(u => u.Id)
                    .ToListAsync()
            };

            // Determine notification type based on priority string
            var notificationType = announcement.Priority?.ToLower() switch
            {
                "critical" => Domain.Enums.NotificationType.System,
                "high" => Domain.Enums.NotificationType.System,
                _ => Domain.Enums.NotificationType.Reminder
            };

            var notifications = recipientIds.Select(userId => new Domain.Entities.Notifications.Notification
            {
                UserId = userId,
                Title = announcement.Title,
                Message = !string.IsNullOrEmpty(announcement.Content) && announcement.Content.Length > 200 
                    ? announcement.Content.Substring(0, 200) + "..." 
                    : announcement.Content ?? string.Empty,
                Type = notificationType,
                ActionUrl = $"/Announcements/Details/{announcement.Id}",
                ActionText = "عرض الإعلان",
                IconClass = "fa-bullhorn",
                RelatedEntity = "Announcement",
                RelatedEntityId = announcement.Id,
                IsRead = false
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending announcement notifications for {AnnouncementId}", announcement.Id);
            // Don't throw - notification failure shouldn't break announcement creation
        }
    }
}

