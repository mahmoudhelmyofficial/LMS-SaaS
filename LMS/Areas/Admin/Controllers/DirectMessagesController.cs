using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الرسائل المباشرة - Direct Messages Management Controller
/// </summary>
public class DirectMessagesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DirectMessagesController> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public DirectMessagesController(
        ApplicationDbContext context,
        ILogger<DirectMessagesController> logger,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// قائمة الرسائل - Messages List
    /// </summary>
    public async Task<IActionResult> Index(string? senderId, string? recipientId, bool? isRead, string? searchTerm)
    {
        var query = _context.DirectMessages
            .Include(dm => dm.Sender)
            .Include(dm => dm.Recipient)
            .AsQueryable();

        if (!string.IsNullOrEmpty(senderId))
        {
            query = query.Where(dm => dm.SenderId == senderId);
        }

        if (!string.IsNullOrEmpty(recipientId))
        {
            query = query.Where(dm => dm.RecipientId == recipientId);
        }

        if (isRead.HasValue)
        {
            query = query.Where(dm => dm.IsRead == isRead.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(dm => dm.Subject!.Contains(searchTerm) || dm.MessageBody.Contains(searchTerm));
        }

        var messages = await query
            .OrderByDescending(dm => dm.SentAt)
            .Take(200)
            .ToListAsync();

        // Get list of users for recipient selection
        var users = await _context.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.FirstName)
            .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName, u.Email })
            .Take(100)
            .ToListAsync();

        ViewBag.Users = users;
        ViewBag.SenderId = senderId;
        ViewBag.RecipientId = recipientId;
        ViewBag.IsRead = isRead;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.CurrentUserId = _currentUserService.UserId;

        return View(messages);
    }

    /// <summary>
    /// إرسال رسالة جديدة - Send new message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string recipientId, string? subject, string message, IFormFile? attachment)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                return Json(new { success = false, message = "يرجى تحديد المستلم" });
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "يرجى كتابة الرسالة" });
            }

            var senderId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(senderId))
            {
                return Json(new { success = false, message = "لم يتم التعرف على المستخدم" });
            }

            // Verify recipient exists
            var recipientExists = await _context.Users.AnyAsync(u => u.Id == recipientId && !u.IsDeleted);
            if (!recipientExists)
            {
                return Json(new { success = false, message = "المستلم غير موجود" });
            }

            string? attachmentUrl = null;
            if (attachment != null && attachment.Length > 0)
            {
                try
                {
                    attachmentUrl = await _fileStorageService.UploadAsync(attachment, "messages");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save attachment for message");
                }
            }

            var directMessage = new Domain.Entities.Social.DirectMessage
            {
                SenderId = senderId,
                RecipientId = recipientId,
                Subject = subject ?? "",
                MessageBody = message,
                AttachmentUrl = attachmentUrl,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.DirectMessages.Add(directMessage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Message sent from {SenderId} to {RecipientId}", senderId, recipientId);

            return Json(new { success = true, message = "تم إرسال الرسالة بنجاح", messageId = directMessage.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return Json(new { success = false, message = "حدث خطأ أثناء إرسال الرسالة" });
        }
    }

    /// <summary>
    /// الحصول على محادثة بين مستخدمين - Get conversation between users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConversation(string recipientId)
    {
        try
        {
            var currentUserId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Json(new { success = false, message = "لم يتم التعرف على المستخدم" });
            }

            var messages = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => 
                    (m.SenderId == currentUserId && m.RecipientId == recipientId) ||
                    (m.SenderId == recipientId && m.RecipientId == currentUserId))
                .OrderBy(m => m.SentAt)
                .Take(100)
                .Select(m => new 
                {
                    m.Id,
                    m.MessageBody,
                    m.Subject,
                    m.SentAt,
                    m.IsRead,
                    m.AttachmentUrl,
                    IsSent = m.SenderId == currentUserId,
                    SenderName = m.Sender != null 
                        ? $"{m.Sender.FirstName ?? ""} {m.Sender.LastName ?? ""}".Trim()
                        : "Unknown User"
                })
                .ToListAsync();

            // Mark messages as read
            var unreadMessages = await _context.DirectMessages
                .Where(m => m.SenderId == recipientId && m.RecipientId == currentUserId && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            return Json(new { success = true, messages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation");
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل المحادثة" });
        }
    }

    /// <summary>
    /// تفاصيل الرسالة - Message Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var message = await _context.DirectMessages
            .Include(dm => dm.Sender)
            .Include(dm => dm.Recipient)
            .FirstOrDefaultAsync(dm => dm.Id == id);

        if (message == null)
            return NotFound();

        return View(message);
    }

    /// <summary>
    /// حذف الرسالة - Delete Message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await _context.DirectMessages.FindAsync(id);
        if (message == null)
            return NotFound();

        _context.DirectMessages.Remove(message);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الرسالة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف رسائل متعددة - Bulk Delete
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(List<int> messageIds)
    {
        if (messageIds == null || !messageIds.Any())
        {
            SetErrorMessage("لم يتم اختيار أي رسائل");
            return RedirectToAction(nameof(Index));
        }

        var messages = await _context.DirectMessages
            .Where(dm => messageIds.Contains(dm.Id))
            .ToListAsync();

        _context.DirectMessages.RemoveRange(messages);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {messages.Count} رسالة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات الرسائل - Messages Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.DirectMessages.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(dm => dm.SentAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(dm => dm.SentAt <= toDate.Value);
        }

        var messages = await query.Include(m => m.Sender).ToListAsync();

        var stats = new
        {
            TotalMessages = messages.Count,
            ReadMessages = messages.Count(m => m.IsRead),
            UnreadMessages = messages.Count(m => !m.IsRead),
            UniqueConversations = messages.Select(m => new { m.SenderId, m.RecipientId })
                .Distinct()
                .Count(),
            AvgResponseTime = CalculateAvgResponseTime(messages)
        };

        // Generate chart data by day of week
        var dayNames = new[] { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
        var chartLabels = new List<string>();
        var sentData = new List<int>();
        var readData = new List<int>();

        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            chartLabels.Add(dayNames[(int)day]);
            sentData.Add(messages.Count(m => m.SentAt.DayOfWeek == day));
            readData.Add(messages.Count(m => m.SentAt.DayOfWeek == day && m.IsRead));
        }

        // Generate hourly activity data
        var hourlyData = new List<int>();
        for (int hour = 0; hour < 24; hour++)
        {
            hourlyData.Add(messages.Count(m => m.SentAt.Hour == hour));
        }

        // Get top users by message count
        var topUsers = messages
            .GroupBy(m => m.SenderId)
            .Select(g => new {
                UserId = g.Key,
                UserName = g.First().Sender != null 
                    ? $"{g.First().Sender.FirstName ?? ""} {g.First().Sender.LastName ?? ""}".Trim()
                    : "مستخدم",
                MessageCount = g.Count(),
                ReadRate = g.Count() > 0 ? (g.Count(m => m.IsRead) * 100 / g.Count()) : 0
            })
            .OrderByDescending(x => x.MessageCount)
            .Take(5)
            .ToList();

        ViewBag.Stats = stats;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.ChartLabels = chartLabels;
        ViewBag.SentData = sentData;
        ViewBag.ReadData = readData;
        ViewBag.HourlyData = hourlyData;
        ViewBag.TopUsers = topUsers;

        return View();
    }

    private double CalculateAvgResponseTime(List<Domain.Entities.Social.DirectMessage> messages)
    {
        // Simplified calculation
        var conversations = messages
            .GroupBy(m => new { SenderId = string.Compare(m.SenderId, m.RecipientId) < 0 ? m.SenderId : m.RecipientId, 
                               RecipientId = string.Compare(m.SenderId, m.RecipientId) < 0 ? m.RecipientId : m.SenderId })
            .ToList();

        var responseTimes = new List<double>();

        foreach (var conv in conversations)
        {
            var orderedMessages = conv.OrderBy(m => m.SentAt).ToList();
            for (int i = 1; i < orderedMessages.Count; i++)
            {
                responseTimes.Add((orderedMessages[i].SentAt - orderedMessages[i - 1].SentAt).TotalHours);
            }
        }

        return responseTimes.Any() ? responseTimes.Average() : 0;
    }
}

