using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// مراقبة قائمة انتظار البريد - Email Queue Monitoring Controller
/// </summary>
public class EmailQueueController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmailQueueController> _logger;
    private readonly ISystemConfigurationService _configService;

    public EmailQueueController(
        ApplicationDbContext context, 
        ILogger<EmailQueueController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// لوحة معلومات البريد - Email dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var stats = await GetEmailStatsAsync();
        return View(stats);
    }

    /// <summary>
    /// قائمة رسائل البريد - Email queue list
    /// </summary>
    public async Task<IActionResult> List(string? status, int? priority, DateTime? from, DateTime? to, string? email, int page = 1)
    {
        var query = _context.EmailQueue.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        if (priority.HasValue)
            query = query.Where(e => e.Priority == priority.Value);

        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value);

        if (!string.IsNullOrEmpty(email))
            query = query.Where(e => e.ToEmail.Contains(email));

        var pageSize = await _configService.GetPaginationSizeAsync("email_queue", 50);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var emails = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailQueueListViewModel
            {
                Id = e.Id,
                ToEmail = e.ToEmail,
                ToName = e.ToName,
                Subject = e.Subject,
                Status = e.Status,
                Priority = e.Priority,
                ScheduledFor = e.ScheduledFor,
                SentAt = e.SentAt,
                RetryCount = e.RetryCount,
                MaxRetries = e.MaxRetries,
                IsOpened = e.IsOpened,
                IsClicked = e.IsClicked,
                CreatedAt = e.CreatedAt,
                FailureReason = e.FailureReason
            })
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Email = email;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        return View(emails);
    }

    /// <summary>
    /// تفاصيل البريد - Email details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var email = await _context.EmailQueue
            .FirstOrDefaultAsync(e => e.Id == id);

        if (email == null)
            return NotFound();

        var model = new EmailQueueDetailsViewModel
        {
            Id = email.Id,
            ToEmail = email.ToEmail,
            ToName = email.ToName,
            Subject = email.Subject,
            Body = email.Body,
            PlainTextBody = email.PlainTextBody,
            FromName = email.FromName,
            FromEmail = email.FromEmail,
            ReplyToEmail = email.ReplyToEmail,
            Cc = email.Cc,
            Bcc = email.Bcc,
            TemplateId = email.TemplateId,
            TemplateName = email.TemplateName,
            Priority = email.Priority,
            ScheduledFor = email.ScheduledFor,
            Status = email.Status,
            SentAt = email.SentAt,
            FailureReason = email.FailureReason,
            RetryCount = email.RetryCount,
            MaxRetries = email.MaxRetries,
            NextRetryAt = email.NextRetryAt,
            MessageId = email.MessageId,
            IsOpened = email.IsOpened,
            OpenedAt = email.OpenedAt,
            IsClicked = email.IsClicked,
            ClickedAt = email.ClickedAt,
            CreatedAt = email.CreatedAt,
            Metadata = email.Metadata
        };

        return View(model);
    }

    /// <summary>
    /// البريد الفاشل - Failed emails
    /// </summary>
    public async Task<IActionResult> Failed(int page = 1)
    {
        var pageSize = await _configService.GetPaginationSizeAsync("email_queue_failed", 50);
        var totalCount = await _context.EmailQueue.CountAsync(e => e.Status == "Failed");
        var emails = await _context.EmailQueue
            .Where(e => e.Status == "Failed")
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailQueueListViewModel
            {
                Id = e.Id,
                ToEmail = e.ToEmail,
                ToName = e.ToName,
                Subject = e.Subject,
                Status = e.Status,
                Priority = e.Priority,
                ScheduledFor = e.ScheduledFor,
                SentAt = e.SentAt,
                RetryCount = e.RetryCount,
                MaxRetries = e.MaxRetries,
                CreatedAt = e.CreatedAt,
                FailureReason = e.FailureReason
            })
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(emails);
    }

    /// <summary>
    /// البريد المعلق - Pending emails
    /// </summary>
    public async Task<IActionResult> Pending(int page = 1)
    {
        var pageSize = await _configService.GetPaginationSizeAsync("email_queue_pending", 50);
        var totalCount = await _context.EmailQueue.CountAsync(e => e.Status == "Pending");
        var emails = await _context.EmailQueue
            .Where(e => e.Status == "Pending")
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.ScheduledFor)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailQueueListViewModel
            {
                Id = e.Id,
                ToEmail = e.ToEmail,
                ToName = e.ToName,
                Subject = e.Subject,
                Status = e.Status,
                Priority = e.Priority,
                ScheduledFor = e.ScheduledFor,
                SentAt = e.SentAt,
                RetryCount = e.RetryCount,
                MaxRetries = e.MaxRetries,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(emails);
    }

    /// <summary>
    /// إعادة إرسال البريد - Retry email
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Retry(int id)
    {
        var email = await _context.EmailQueue.FindAsync(id);

        if (email == null)
            return NotFound();

        var model = new RetryEmailViewModel
        {
            EmailId = id,
            RescheduleFor = DateTime.UtcNow.AddMinutes(5),
            NewEmail = email.ToEmail,
            NewSubject = email.Subject
        };

        return View(model);
    }

    /// <summary>
    /// تنفيذ إعادة الإرسال - Execute retry
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(RetryEmailViewModel model)
    {
        if (ModelState.IsValid)
        {
            var email = await _context.EmailQueue.FindAsync(model.EmailId);

            if (email == null)
                return NotFound();

            // Update email details
            if (!string.IsNullOrEmpty(model.NewEmail))
                email.ToEmail = model.NewEmail;

            if (!string.IsNullOrEmpty(model.NewSubject))
                email.Subject = model.NewSubject;

            // Reset status
            email.Status = "Pending";
            email.ScheduledFor = model.RescheduleFor ?? DateTime.UtcNow;
            email.FailureReason = null;
            email.NextRetryAt = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Email {EmailId} scheduled for retry", model.EmailId);

            SetSuccessMessage("تم جدولة البريد لإعادة الإرسال");
            return RedirectToAction(nameof(Details), new { id = model.EmailId });
        }

        return View(model);
    }

    /// <summary>
    /// إعادة إرسال جماعي - Bulk retry
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRetry(string status, int maxRetries = 3)
    {
        var emails = await _context.EmailQueue
            .Where(e => e.Status == status && e.RetryCount < maxRetries)
            .ToListAsync();

        foreach (var email in emails)
        {
            email.Status = "Pending";
            email.ScheduledFor = DateTime.UtcNow.AddMinutes(5);
            email.FailureReason = null;
            email.NextRetryAt = null;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bulk retry scheduled for {Count} emails", emails.Count);

        SetSuccessMessage($"تم جدولة {emails.Count} بريد لإعادة الإرسال");
        return RedirectToAction(nameof(Failed));
    }

    /// <summary>
    /// حذف البريد - Delete email
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var email = await _context.EmailQueue.FindAsync(id);

        if (email == null)
            return NotFound();

        _context.EmailQueue.Remove(email);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email {EmailId} deleted from queue", id);

        SetSuccessMessage("تم حذف البريد من قائمة الانتظار");
        return RedirectToAction(nameof(List));
    }

    /// <summary>
    /// تنظيف الرسائل القديمة - Clean old emails
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanOld(int daysOld = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

        var oldEmails = await _context.EmailQueue
            .Where(e => (e.Status == "Sent" || e.Status == "Failed") && e.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.EmailQueue.RemoveRange(oldEmails);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned {Count} old emails from queue", oldEmails.Count);

        SetSuccessMessage($"تم حذف {oldEmails.Count} بريد قديم من قائمة الانتظار");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الإحصائيات - Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? from, DateTime? to)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var stats = await GetEmailStatsAsync(from.Value, to.Value);

        ViewBag.From = from;
        ViewBag.To = to;

        return View(stats);
    }

    /// <summary>
    /// تصدير البيانات - Export data
    /// </summary>
    public async Task<IActionResult> Export(string status, DateTime? from, DateTime? to)
    {
        var query = _context.EmailQueue.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value);

        var emails = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        // Generate CSV
        var csv = "ID,To Email,To Name,Subject,Status,Priority,Scheduled For,Sent At,Retry Count,Created At,Failure Reason\n";
        foreach (var email in emails)
        {
            csv += $"{email.Id},{email.ToEmail},{email.ToName},{email.Subject},{email.Status},{email.Priority},{email.ScheduledFor:yyyy-MM-dd HH:mm},{email.SentAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"},{email.RetryCount},{email.CreatedAt:yyyy-MM-dd HH:mm},{email.FailureReason ?? "N/A"}\n";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"email-queue-{DateTime.Now:yyyyMMdd}.csv");
    }

    #region Private Helpers

    private async Task<EmailQueueStatsViewModel> GetEmailStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var query = _context.EmailQueue.Where(e => e.CreatedAt >= from.Value && e.CreatedAt <= to.Value);

        var totalEmails = await query.CountAsync();
        var sentEmails = await query.CountAsync(e => e.Status == "Sent");
        var openedEmails = await query.CountAsync(e => e.IsOpened);
        var clickedEmails = await query.CountAsync(e => e.IsClicked);

        var stats = new EmailQueueStatsViewModel
        {
            TotalEmails = totalEmails,
            PendingEmails = await query.CountAsync(e => e.Status == "Pending"),
            ProcessingEmails = await query.CountAsync(e => e.Status == "Processing"),
            SentEmails = sentEmails,
            FailedEmails = await query.CountAsync(e => e.Status == "Failed"),
            OpenedEmails = openedEmails,
            ClickedEmails = clickedEmails,
            OpenRate = sentEmails > 0 ? (decimal)openedEmails / sentEmails * 100 : 0,
            ClickRate = sentEmails > 0 ? (decimal)clickedEmails / sentEmails * 100 : 0,
            EmailsLast24Hours = await _context.EmailQueue.CountAsync(e => e.CreatedAt >= DateTime.UtcNow.AddDays(-1)),
            FailedLast24Hours = await _context.EmailQueue.CountAsync(e => e.Status == "Failed" && e.CreatedAt >= DateTime.UtcNow.AddDays(-1))
        };

        // Emails by day
        stats.EmailsByDay = await query
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new EmailsByDayViewModel
            {
                Date = g.Key,
                TotalCount = g.Count(),
                SentCount = g.Count(e => e.Status == "Sent"),
                FailedCount = g.Count(e => e.Status == "Failed")
            })
            .OrderBy(e => e.Date)
            .ToListAsync();

        // Top failure reasons
        stats.TopFailureReasons = await query
            .Where(e => e.Status == "Failed" && !string.IsNullOrEmpty(e.FailureReason))
            .GroupBy(e => e.FailureReason)
            .Select(g => new TopFailureReasonViewModel
            {
                Reason = g.Key!,
                Count = g.Count()
            })
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToListAsync();

        return stats;
    }

    #endregion
}

