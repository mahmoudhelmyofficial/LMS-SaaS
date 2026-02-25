using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة الإعلانات - Course Announcements Controller
/// </summary>
public class AnnouncementsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AnnouncementsController> _logger;

    public AnnouncementsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<AnnouncementsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الإعلانات - Announcements List
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? status, string? search, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.CourseAnnouncements
            .Include(a => a.Course)
            .Where(a => a.InstructorId == userId)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(a => a.CourseId == courseId.Value);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.ToLower())
            {
                case "published":
                    query = query.Where(a => a.IsPublished);
                    break;
                case "draft":
                    query = query.Where(a => !a.IsPublished);
                    break;
            }
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.Title.Contains(search) || 
                                     a.Content.Contains(search) ||
                                     (a.Course != null && a.Course.Title.Contains(search)));
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var announcements = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Status = status;
        ViewBag.SearchTerm = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalCount;
        ViewBag.PageSize = pageSize;

        return View(announcements);
    }

    /// <summary>
    /// إنشاء إعلان جديد - Create new announcement
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? courseId)
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        return View(new AnnouncementCreateViewModel { CourseId = courseId ?? 0 });
    }

    /// <summary>
    /// حفظ الإعلان الجديد - Save new announcement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        // Verify course ownership
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

        if (course == null)
        {
            SetErrorMessage("الدورة غير موجودة أو ليس لديك صلاحية عليها");
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .ToListAsync();
            return View(model);
        }

        try
        {
            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateAnnouncement(
                model.Title,
                model.Content,
                null); // No scheduled date in this context

            if (!isValid)
            {
                _logger.LogWarning("Announcement validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync();
                return View(model);
            }

            // Validate expiration date if set
            if (model.ExpiresAt.HasValue && model.ExpiresAt.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.ExpiresAt), "تاريخ انتهاء الصلاحية يجب أن يكون في المستقبل");
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync();
                return View(model);
            }

            // Validate optional URLs only when provided (allow relative paths from media library)
            if (!string.IsNullOrWhiteSpace(model.AttachmentUrl))
            {
                var (urlValid, urlReason) = BusinessRuleHelper.ValidateUrl(model.AttachmentUrl.Trim(), "رابط المرفق", allowRelativePath: true);
                if (!urlValid)
                {
                    ModelState.AddModelError(nameof(model.AttachmentUrl), urlReason ?? "الرجاء إدخال رابط صحيح");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.InstructorId == userId)
                        .ToListAsync();
                    return View(model);
                }
            }
            if (!string.IsNullOrWhiteSpace(model.ExternalLink))
            {
                var (urlValid, urlReason) = BusinessRuleHelper.ValidateUrl(model.ExternalLink.Trim(), "الرابط الخارجي");
                if (!urlValid)
                {
                    ModelState.AddModelError(nameof(model.ExternalLink), urlReason ?? "الرجاء إدخال رابط صحيح");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.InstructorId == userId)
                        .ToListAsync();
                    return View(model);
                }
            }

            CourseAnnouncement? announcement = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                announcement = new CourseAnnouncement
                {
                    CourseId = model.CourseId,
                    InstructorId = userId!,
                    Title = model.Title,
                    Content = model.Content,
                    AnnouncementType = model.AnnouncementType.ToString().ToLower(),
                    Priority = model.Priority.ToString().ToLower(),
                    IsPublished = model.IsPublished,
                    PublishedAt = model.IsPublished ? DateTime.UtcNow : null,
                    IsPinned = model.IsPinned,
                    SendEmail = model.SendEmail,
                    SendPushNotification = model.SendPushNotification,
                    ExpiresAt = model.ExpiresAt,
                    AttachmentUrl = string.IsNullOrWhiteSpace(model.AttachmentUrl) ? null : model.AttachmentUrl.Trim(),
                    ExternalLink = string.IsNullOrWhiteSpace(model.ExternalLink) ? null : model.ExternalLink.Trim()
                };

                _context.CourseAnnouncements.Add(announcement);
                await _context.SaveChangesAsync();

                // Send email notifications to enrolled students if requested
                if (model.SendEmail && model.IsPublished)
                {
                    var enrolledStudents = await _context.Enrollments
                        .Include(e => e.Student)
                        .Where(e => e.CourseId == model.CourseId && 
                                   e.Status == EnrollmentStatus.Active &&
                                   !string.IsNullOrEmpty(e.Student.Email))
                        .ToListAsync();

                    var emailsSent = 0;
                    foreach (var enrollment in enrolledStudents)
                    {
                        try
                        {
                            await _emailService.SendCourseAnnouncementAsync(
                                enrollment.Student.Email!,
                                course.Title,
                                model.Title,
                                model.Content
                            );
                            emailsSent++;
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send announcement email to {Email}", 
                                enrollment.Student.Email);
                            // Continue with other students
                        }
                    }

                    announcement.EmailSent = emailsSent > 0;
                    
                    _logger.LogInformation("Sent announcement email to {SentCount}/{TotalCount} students for course {CourseId}", 
                        emailsSent, enrolledStudents.Count, model.CourseId);
                }

                // Create in-app notifications for enrolled students if requested
                if (model.SendPushNotification && model.IsPublished)
                {
                    var studentIds = await _context.Enrollments
                        .Where(e => e.CourseId == model.CourseId && e.Status == EnrollmentStatus.Active)
                        .Select(e => e.StudentId)
                        .ToListAsync();

                    var notifications = studentIds.Select(studentId => new Notification
                    {
                        UserId = studentId,
                        Title = $"إعلان جديد: {model.Title}",
                        Message = model.Content.Length > 100 ? model.Content.Substring(0, 100) + "..." : model.Content,
                        Type = NotificationType.Course,
                        ActionUrl = $"/Student/Courses/Details/{model.CourseId}",
                        ActionText = "عرض الدورة",
                        Icon = "fas fa-bullhorn",
                        IsRead = false
                    }).ToList();

                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created {Count} in-app notifications for announcement {AnnouncementId}", 
                        notifications.Count, announcement.Id);
                }
            });

            _logger.LogInformation("Announcement {AnnouncementId} created for course {CourseId} by instructor {InstructorId}", 
                announcement!.Id, model.CourseId, userId);

            SetSuccessMessage("تم إنشاء الإعلان بنجاح" + 
                (model.SendEmail ? " وتم إرسال الإشعارات بالبريد الإلكتروني" : "") +
                (model.SendPushNotification ? " وتم إرسال الإشعارات الفورية" : ""));
            
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating announcement for course {CourseId}", model.CourseId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الإعلان. يرجى المحاولة مرة أخرى");
            
            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .ToListAsync();
            
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الإعلان - Edit announcement
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var announcement = await _context.CourseAnnouncements
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == userId);

        if (announcement == null)
            return NotFound();

        ViewBag.CreatedAt = announcement.CreatedAt.ToString("o");
        ViewBag.UpdatedAt = announcement.UpdatedAt?.ToString("o");

        var viewModel = new AnnouncementEditViewModel
        {
            Id = announcement.Id,
            CourseId = announcement.CourseId,
            Title = announcement.Title,
            Content = announcement.Content,
            AnnouncementType = Enum.TryParse<AnnouncementType>(announcement.AnnouncementType, true, out var type) ? type : AnnouncementType.General,
            Priority = Enum.TryParse<AnnouncementPriority>(announcement.Priority, true, out var priority) ? priority : AnnouncementPriority.Normal,
            IsPublished = announcement.IsPublished,
            IsPinned = announcement.IsPinned,
            SendEmail = announcement.SendEmail && !announcement.EmailSent, // Can't resend
            SendPushNotification = announcement.SendPushNotification,
            ExpiresAt = announcement.ExpiresAt,
            AttachmentUrl = announcement.AttachmentUrl,
            ExternalLink = announcement.ExternalLink
        };

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .ToListAsync();

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

        var userId = _currentUserService.UserId;

        var announcement = await _context.CourseAnnouncements
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == userId);

        if (announcement == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Validate optional URLs only when provided (allow relative paths from media library)
            if (!string.IsNullOrWhiteSpace(model.AttachmentUrl))
            {
                var (urlValid, urlReason) = BusinessRuleHelper.ValidateUrl(model.AttachmentUrl.Trim(), "رابط المرفق", allowRelativePath: true);
                if (!urlValid)
                {
                    ModelState.AddModelError(nameof(model.AttachmentUrl), urlReason ?? "الرجاء إدخال رابط صحيح");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.InstructorId == userId)
                        .ToListAsync();
                    return View(model);
                }
            }
            if (!string.IsNullOrWhiteSpace(model.ExternalLink))
            {
                var (urlValid, urlReason) = BusinessRuleHelper.ValidateUrl(model.ExternalLink.Trim(), "الرابط الخارجي");
                if (!urlValid)
                {
                    ModelState.AddModelError(nameof(model.ExternalLink), urlReason ?? "الرجاء إدخال رابط صحيح");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.InstructorId == userId)
                        .ToListAsync();
                    return View(model);
                }
            }

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateAnnouncement(model.Title, model.Content, null);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, validationReason!);
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync();
                return View(model);
            }

            // Validate expiration date if set
            if (model.ExpiresAt.HasValue && model.ExpiresAt.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.ExpiresAt), "تاريخ انتهاء الصلاحية يجب أن يكون في المستقبل");
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync();
                return View(model);
            }

            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == announcement.CourseId);

            try
            {
                bool wasUnpublished = !announcement.IsPublished;

                await _context.ExecuteInTransactionAsync(async () =>
                {
                    announcement.Title = model.Title;
                    announcement.Content = model.Content;
                    announcement.AnnouncementType = model.AnnouncementType.ToString().ToLower();
                    announcement.Priority = model.Priority.ToString().ToLower();
                    announcement.IsPublished = model.IsPublished;
                    announcement.IsPinned = model.IsPinned;
                    announcement.ExpiresAt = model.ExpiresAt;
                    announcement.AttachmentUrl = string.IsNullOrWhiteSpace(model.AttachmentUrl) ? null : model.AttachmentUrl.Trim();
                    announcement.ExternalLink = string.IsNullOrWhiteSpace(model.ExternalLink) ? null : model.ExternalLink.Trim();

                    if (model.IsPublished && wasUnpublished)
                    {
                        announcement.PublishedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();

                    // Send email notifications to enrolled students if transitioning to published
                    if (model.IsPublished && wasUnpublished && model.SendEmail)
                    {
                        var enrolledStudents = await _context.Enrollments
                            .Include(e => e.Student)
                            .Where(e => e.CourseId == model.CourseId &&
                                       e.Status == EnrollmentStatus.Active &&
                                       !string.IsNullOrEmpty(e.Student.Email))
                            .ToListAsync();

                        var emailsSent = 0;
                        foreach (var enrollment in enrolledStudents)
                        {
                            try
                            {
                                await _emailService.SendCourseAnnouncementAsync(
                                    enrollment.Student.Email!,
                                    course?.Title ?? "الدورة",
                                    model.Title,
                                    model.Content
                                );
                                emailsSent++;
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, "Failed to send announcement email to {Email}",
                                    enrollment.Student.Email);
                                // Continue with other students
                            }
                        }

                        announcement.EmailSent = emailsSent > 0;

                        _logger.LogInformation("Sent announcement email to {SentCount}/{TotalCount} students for course {CourseId}",
                            emailsSent, enrolledStudents.Count, model.CourseId);
                    }

                    // Create in-app notifications for enrolled students if transitioning to published
                    if (model.IsPublished && wasUnpublished && model.SendPushNotification)
                    {
                        var studentIds = await _context.Enrollments
                            .Where(e => e.CourseId == model.CourseId && e.Status == EnrollmentStatus.Active)
                            .Select(e => e.StudentId)
                            .ToListAsync();

                        var notifications = studentIds.Select(studentId => new Notification
                        {
                            UserId = studentId,
                            Title = $"إعلان جديد: {model.Title}",
                            Message = model.Content.Length > 100 ? model.Content.Substring(0, 100) + "..." : model.Content,
                            Type = NotificationType.Course,
                            ActionUrl = $"/Student/Courses/Details/{model.CourseId}",
                            ActionText = "عرض الدورة",
                            Icon = "fas fa-bullhorn",
                            IsRead = false
                        }).ToList();

                        _context.Notifications.AddRange(notifications);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Created {Count} in-app notifications for announcement {AnnouncementId}",
                            notifications.Count, announcement.Id);
                    }
                });

                _logger.LogInformation("Announcement {AnnouncementId} updated for course {CourseId} by instructor {InstructorId}",
                    announcement.Id, model.CourseId, userId);
                SetSuccessMessage("تم تحديث الإعلان بنجاح");
                return RedirectToAction(nameof(Index), new { courseId = announcement.CourseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating announcement {AnnouncementId} for course {CourseId}", id, model.CourseId);
                SetErrorMessage("حدث خطأ أثناء تحديث الإعلان. يرجى المحاولة مرة أخرى");

                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync();

                return View(model);
            }
        }

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// تثبيت/إلغاء تثبيت الإعلان - Pin/Unpin announcement (POST only - removed GET version for security)
    /// </summary>

    /// <summary>
    /// تبديل حالة التثبيت - Toggle pin status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var userId = _currentUserService.UserId;

        var announcement = await _context.CourseAnnouncements
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == userId);

        if (announcement == null)
            return NotFound();

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                announcement.IsPinned = !announcement.IsPinned;
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} {Action} announcement {AnnouncementId}.",
                userId, announcement.IsPinned ? "pinned" : "unpinned", id);
            SetSuccessMessage(announcement.IsPinned ? "تم تثبيت الإعلان" : "تم إلغاء تثبيت الإعلان");
            return RedirectToAction(nameof(Index), new { courseId = announcement.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling pin for announcement {AnnouncementId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة التثبيت.");
            return RedirectToAction(nameof(Index), new { courseId = announcement.CourseId });
        }
    }

    /// <summary>
    /// تبديل حالة النشر - Toggle publish status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var userId = _currentUserService.UserId;

        var announcement = await _context.CourseAnnouncements
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == userId);

        if (announcement == null)
            return NotFound();

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                announcement.IsPublished = !announcement.IsPublished;

                if (announcement.IsPublished && announcement.PublishedAt == null)
                {
                    announcement.PublishedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} {Action} announcement {AnnouncementId}.",
                userId, announcement.IsPublished ? "published" : "unpublished", id);
            SetSuccessMessage($"تم {(announcement.IsPublished ? "نشر" : "إلغاء نشر")} الإعلان بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = announcement.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling publish for announcement {AnnouncementId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة النشر.");
            return RedirectToAction(nameof(Index), new { courseId = announcement.CourseId });
        }
    }

    /// <summary>
    /// حذف الإعلان - Delete announcement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var announcement = await _context.CourseAnnouncements
            .FirstOrDefaultAsync(a => a.Id == id && a.InstructorId == userId);

        if (announcement == null)
            return NotFound();

        var courseId = announcement.CourseId;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.CourseAnnouncements.Remove(announcement);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} deleted announcement {AnnouncementId} from course {CourseId}.",
                userId, id, courseId);
            SetSuccessMessage("تم حذف الإعلان بنجاح");
            return RedirectToAction(nameof(Index), new { courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting announcement {AnnouncementId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف الإعلان.");
            return RedirectToAction(nameof(Index), new { courseId });
        }
    }
}

