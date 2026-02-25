using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الرسائل المباشرة - Direct Messages Controller
/// </summary>
public class MessagesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IInstructorNotificationService instructorNotificationService,
        ILogger<MessagesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _instructorNotificationService = instructorNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// صندوق الوارد - Inbox
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;

            var messages = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => m.RecipientId == userId && !m.RecipientDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.UnreadCount = await _context.DirectMessages
                .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted);

            return View(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading messages");
            SetErrorMessage("حدث خطأ أثناء تحميل الرسائل");
            // Return empty view instead of redirecting to dashboard
            ViewBag.Page = 1;
            ViewBag.UnreadCount = 0;
            return View(new List<DirectMessage>());
        }
    }

    /// <summary>
    /// الرسائل المرسلة - Sent messages
    /// </summary>
    public async Task<IActionResult> Sent(int page = 1)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var messages = await _context.DirectMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Where(m => m.SenderId == userId && !m.SenderDeleted)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.Page = page;
        return View(messages);
    }

    /// <summary>
    /// عرض الرسالة - View message
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var message = await _context.DirectMessages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == id && 
                (m.SenderId == userId || m.RecipientId == userId));

        if (message == null)
            return NotFound();

        // Mark as read if receiver is viewing
        if (message.RecipientId == userId && !message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return View(message);
    }

    /// <summary>
    /// إرسال رسالة جديدة - Send new message (alias redirects to Compose)
    /// </summary>
    [HttpGet]
    public IActionResult Send(string? receiverId)
    {
        return RedirectToAction(nameof(Compose), new { receiverId });
    }

    /// <summary>
    /// إرسال رسالة جديدة - Send new message POST (alias redirects to Compose POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(ComposeMessageViewModel model)
    {
        return await Compose(model);
    }

    /// <summary>
    /// إرسال رسالة جديدة - Send new message
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Compose(string? receiverId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var model = new ComposeMessageViewModel
            {
                ReceiverId = receiverId ?? string.Empty
            };

            await LoadInstructorsListAsync(userId);

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading compose page for student {StudentId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة إرسال الرسالة.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الرسالة الجديدة - Save new message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeMessageViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Prevent self-messaging
            if (model.ReceiverId == userId)
            {
                SetErrorMessage("لا يمكنك إرسال رسالة لنفسك");
                await LoadInstructorsListAsync(userId);
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                await LoadInstructorsListAsync(userId);
                return View(model);
            }

            var message = new DirectMessage
            {
                SenderId = userId,
                RecipientId = model.ReceiverId!,
                Subject = model.Subject?.Trim() ?? string.Empty,
                Message = model.Body?.Trim() ?? string.Empty,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                ReplyToId = model.ParentMessageId
            };

            _context.DirectMessages.Add(message);
            await _context.SaveChangesAsync();

            // Send notification to instructor
            try
            {
                var isInstructor = await _context.InstructorProfiles.AnyAsync(p => p.UserId == model.ReceiverId);
                if (isInstructor)
                {
                    var senderName = await _context.Users
                        .Where(u => u.Id == userId)
                        .Select(u => ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim())
                        .FirstOrDefaultAsync() ?? "مستخدم";
                    await _instructorNotificationService.NotifyNewMessageAsync(model.ReceiverId!, message.Id, senderName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Instructor notification failed for message {MessageId}", message.Id);
            }

            SetSuccessMessage("تم إرسال الرسالة بنجاح");
            return RedirectToAction(nameof(Sent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message from student {StudentId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء إرسال الرسالة. يرجى المحاولة مرة أخرى.");
        }

        // Reload instructors list on error
        await LoadInstructorsListAsync(_currentUserService.UserId!);
        return View(model);
    }

    /// <summary>
    /// الرد على رسالة - Reply to message
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Reply(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var originalMessage = await _context.DirectMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == id && m.RecipientId == userId);

            if (originalMessage == null)
            {
                SetErrorMessage("الرسالة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            var model = new ComposeMessageViewModel
            {
                ReceiverId = originalMessage.SenderId,
                Subject = (originalMessage.Subject ?? "").StartsWith("Re: ") 
                    ? originalMessage.Subject ?? ""
                    : "Re: " + (originalMessage.Subject ?? ""),
                ParentMessageId = originalMessage.Id
            };

            ViewBag.OriginalMessage = originalMessage;

            // Load instructors list for recipient selection
            await LoadInstructorsListAsync(userId);

            // Also include the original sender if not in the instructors list
            var instructors = ViewBag.Instructors as List<InstructorDropdownItem> ?? new List<InstructorDropdownItem>();
            if (originalMessage.Sender != null && !instructors.Any(i => i.Id == originalMessage.SenderId))
            {
                var senderFullName = ((originalMessage.Sender.FirstName ?? "") + " " + (originalMessage.Sender.LastName ?? "")).Trim();
                if (string.IsNullOrWhiteSpace(senderFullName)) senderFullName = "مدرس";
                instructors.Add(new InstructorDropdownItem
                {
                    Id = originalMessage.Sender.Id,
                    FullName = senderFullName
                });
                ViewBag.Instructors = instructors;
            }

            return View("Compose", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reply for message {MessageId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل الرسالة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حذف رسالة - Delete message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var message = await _context.DirectMessages
            .FirstOrDefaultAsync(m => m.Id == id && 
                (m.SenderId == userId || m.RecipientId == userId));

        if (message == null)
            return NotFound();

        if (message.SenderId == userId)
            message.SenderDeleted = true;

        if (message.RecipientId == userId)
            message.RecipientDeleted = true;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الرسالة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// عدد الرسائل غير المقروءة - Unread messages count (API)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { unreadCount = 0 });
        }
        
        var count = await _context.DirectMessages
            .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted);

        return Json(new { unreadCount = count });
    }

    /// <summary>
    /// تحديد جميع الرسائل كمقروءة - Mark all as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var unreadMessages = await _context.DirectMessages
            .Where(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديد جميع الرسائل كمقروءة");
        return RedirectToAction(nameof(Index));
    }

    #region Private Helpers

    /// <summary>
    /// تحميل قائمة المدرسين - Load instructors list for messaging dropdown
    /// </summary>
    private async Task LoadInstructorsListAsync(string studentId)
    {
        try
        {
            var instructors = await _context.Enrollments
                .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.Course.Instructor)
                .Where(i => i != null)
                .Select(i => new InstructorDropdownItem
                {
                    Id = i!.Id,
                    FullName = ((i.FirstName ?? "") + " " + (i.LastName ?? "")).Trim()
                })
                .Distinct()
                .OrderBy(i => i.FullName)
                .ToListAsync();

            // Fallback for empty names
            foreach (var instructor in instructors)
            {
                if (string.IsNullOrWhiteSpace(instructor.FullName))
                    instructor.FullName = "مدرس";
            }

            ViewBag.Instructors = instructors;
            _logger.LogInformation("Loaded {Count} instructors for student {StudentId}", instructors.Count, studentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructors list for student {StudentId}", studentId);
            ViewBag.Instructors = new List<InstructorDropdownItem>();
        }
    }

    #endregion
}

/// <summary>
/// عنصر قائمة المدرسين - Instructor dropdown item for messaging
/// </summary>
public class InstructorDropdownItem
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
