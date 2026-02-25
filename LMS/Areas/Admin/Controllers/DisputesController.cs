using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة النزاعات والاستردادات المصرفية - Dispute/Chargeback Management
/// Enterprise-level dispute handling for payment gateway compliance
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class DisputesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DisputesController> _logger;

    public DisputesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<DisputesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة النزاعات - Dispute list with filters
    /// </summary>
    public async Task<IActionResult> Index(DisputeFilterViewModel filter)
    {
        var query = _context.PaymentDisputes
            .Include(d => d.Payment)
            .Include(d => d.Customer)
            .AsQueryable();

        // Apply filters
        if (filter.Status.HasValue)
            query = query.Where(d => d.Status == filter.Status.Value);

        if (filter.Type.HasValue)
            query = query.Where(d => d.Type == filter.Type.Value);

        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLower();
            query = query.Where(d => 
                d.ExternalDisputeId!.ToLower().Contains(term) ||
                d.CustomerEmail!.ToLower().Contains(term) ||
                d.CustomerName!.ToLower().Contains(term) ||
                d.OriginalTransactionId!.ToLower().Contains(term));
        }

        if (filter.FromDate.HasValue)
            query = query.Where(d => d.OpenedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(d => d.OpenedAt <= filter.ToDate.Value.AddDays(1));

        if (filter.IsOverdue)
            query = query.Where(d => d.ResponseDueDate != null && 
                DateTime.UtcNow > d.ResponseDueDate && 
                d.Status == DisputeStatus.NeedsResponse);

        // Summary stats
        ViewBag.TotalDisputes = await _context.PaymentDisputes.CountAsync();
        ViewBag.OpenDisputes = await _context.PaymentDisputes
            .CountAsync(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.NeedsResponse);
        ViewBag.OverdueDisputes = await _context.PaymentDisputes
            .CountAsync(d => d.ResponseDueDate != null && DateTime.UtcNow > d.ResponseDueDate && d.Status == DisputeStatus.NeedsResponse);
        ViewBag.WonDisputes = await _context.PaymentDisputes.CountAsync(d => d.Status == DisputeStatus.Won);
        ViewBag.LostDisputes = await _context.PaymentDisputes.CountAsync(d => d.Status == DisputeStatus.Lost);
        ViewBag.TotalDisputedAmount = await _context.PaymentDisputes.SumAsync(d => d.DisputedAmount);
        ViewBag.TotalFees = await _context.PaymentDisputes.SumAsync(d => d.DisputeFee);

        var disputes = await query
            .OrderByDescending(d => d.OpenedAt)
            .Take(100)
            .ToListAsync();

        ViewBag.Filter = filter;
        return View(disputes);
    }

    /// <summary>
    /// تفاصيل النزاع - Dispute details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var dispute = await _context.PaymentDisputes
            .Include(d => d.Payment)
                .ThenInclude(p => p.Course)
            .Include(d => d.Payment)
                .ThenInclude(p => p.Student)
            .Include(d => d.Customer)
            .Include(d => d.ProcessedBy)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dispute == null)
            return NotFound();

        // Get related payments history
        ViewBag.RelatedPayments = await _context.Payments
            .Where(p => p.StudentId == dispute.CustomerId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Get student access/activity info
        if (dispute.Payment.CourseId.HasValue)
        {
            ViewBag.Enrollment = await _context.Enrollments
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.StudentId == dispute.CustomerId && e.CourseId == dispute.Payment.CourseId);
        }

        return View(dispute);
    }

    /// <summary>
    /// تحديث حالة النزاع - Update dispute status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, DisputeStatus newStatus, string? notes)
    {
        var dispute = await _context.PaymentDisputes.FindAsync(id);
        if (dispute == null)
            return NotFound();

        var oldStatus = dispute.Status;
        dispute.Status = newStatus;
        dispute.InternalNotes = string.IsNullOrEmpty(dispute.InternalNotes) 
            ? notes 
            : $"{dispute.InternalNotes}\n\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {notes}";
        dispute.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DisputeStatus.Won || newStatus == DisputeStatus.Lost)
        {
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.Outcome = newStatus.ToString();
            dispute.ProcessedById = _currentUserService.UserId;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} status changed from {Old} to {New} by {Admin}",
            id, oldStatus, newStatus, _currentUserService.UserId);

        TempData["SuccessMessage"] = CultureExtensions.T("تم تحديث حالة النزاع بنجاح", "Dispute status updated successfully.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إرسال الرد/الدليل - Submit evidence response
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitEvidence(DisputeEvidenceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = CultureExtensions.T("يرجى ملء جميع الحقول المطلوبة", "Please fill in all required fields.");
            return RedirectToAction(nameof(Details), new { id = model.DisputeId });
        }

        var dispute = await _context.PaymentDisputes
            .Include(d => d.Payment)
            .FirstOrDefaultAsync(d => d.Id == model.DisputeId);

        if (dispute == null)
            return NotFound();

        // Build evidence record
        var evidence = new
        {
            SubmittedAt = DateTime.UtcNow,
            SubmittedBy = _currentUserService.UserId,
            EvidenceType = model.EvidenceType,
            Description = model.Description,
            FileUrls = model.FileUrls
        };

        dispute.HasEvidence = true;
        dispute.EvidenceType = model.EvidenceType;
        dispute.ResponseNotes = model.Description;
        dispute.EvidenceUrls = model.FileUrls != null 
            ? JsonSerializer.Serialize(model.FileUrls) 
            : null;
        dispute.ServiceDocumentUrl = model.ServiceDocumentUrl;
        dispute.ResponseSubmittedAt = DateTime.UtcNow;
        dispute.Status = DisputeStatus.ResponseSubmitted;
        dispute.ProcessedById = _currentUserService.UserId;
        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Evidence submitted for dispute {DisputeId} by {Admin}", 
            model.DisputeId, _currentUserService.UserId);

        TempData["SuccessMessage"] = CultureExtensions.T("تم إرسال الدليل بنجاح", "Evidence submitted successfully.");
        return RedirectToAction(nameof(Details), new { id = model.DisputeId });
    }

    /// <summary>
    /// قبول النزاع (استرداد للعميل) - Accept dispute and refund
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptDispute(int id, string? notes)
    {
        var dispute = await _context.PaymentDisputes
            .Include(d => d.Payment)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dispute == null)
            return NotFound();

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Update dispute
                dispute.Status = DisputeStatus.Lost;
                dispute.Outcome = "Accepted";
                dispute.DecisionReason = notes ?? "Dispute accepted by admin";
                dispute.ResolvedAt = DateTime.UtcNow;
                dispute.RefundedAmount = dispute.DisputedAmount;
                dispute.ProcessedById = _currentUserService.UserId;
                dispute.UpdatedAt = DateTime.UtcNow;

                // Update payment status
                dispute.Payment.Status = PaymentStatus.Refunded;
                dispute.Payment.RefundedAt = DateTime.UtcNow;
                dispute.Payment.AdminNotes = $"Refunded due to accepted dispute #{dispute.Id}. {notes}";

                // Revoke access if course enrollment
                if (dispute.Payment.EnrollmentId.HasValue)
                {
                    var enrollment = await _context.Enrollments.FindAsync(dispute.Payment.EnrollmentId);
                    if (enrollment != null)
                    {
                        enrollment.Status = EnrollmentStatus.Refunded;
                        enrollment.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Reverse instructor earnings if applicable
                if (dispute.Payment.CourseId.HasValue)
                {
                    var earning = await _context.InstructorEarnings
                        .FirstOrDefaultAsync(e => e.PaymentId == dispute.PaymentId);
                    if (earning != null)
                    {
                        earning.Status = "reversed";
                        earning.RefundedAt = DateTime.UtcNow;
                        earning.Notes = $"Reversed due to dispute #{dispute.Id}";
                    }
                }

                await _context.SaveChangesAsync();
            });

            // Send notification to student
            if (!string.IsNullOrEmpty(dispute.CustomerId))
            {
                await _notificationService.SendNotificationAsync(
                    dispute.CustomerId,
                    new NotificationDto
                    {
                        Title = "تم قبول طلب النزاع",
                        Message = $"تم قبول طلب النزاع الخاص بك وسيتم استرداد المبلغ {dispute.DisputedAmount} {dispute.Currency}",
                        Type = NotificationType.Payment
                    });
            }

            _logger.LogInformation("Dispute {DisputeId} accepted and refunded by {Admin}", 
                id, _currentUserService.UserId);

            TempData["SuccessMessage"] = CultureExtensions.T("تم قبول النزاع وإجراء الاسترداد بنجاح", "Dispute accepted and refund processed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting dispute {DisputeId}", id);
            TempData["ErrorMessage"] = CultureExtensions.T("حدث خطأ أثناء معالجة النزاع", "An error occurred while processing the dispute.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// الطعن في النزاع - Challenge/Fight dispute
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChallengeDispute(int id)
    {
        var dispute = await _context.PaymentDisputes.FindAsync(id);
        if (dispute == null)
            return NotFound();

        dispute.Status = DisputeStatus.UnderReview;
        dispute.FollowUpCount++;
        dispute.LastFollowUpAt = DateTime.UtcNow;
        dispute.ProcessedById = _currentUserService.UserId;
        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} challenged by {Admin}", id, _currentUserService.UserId);

        TempData["SuccessMessage"] = CultureExtensions.T("تم تسجيل الطعن في النزاع", "Appeal submitted for the dispute.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تصدير النزاعات - Export disputes
    /// </summary>
    public async Task<IActionResult> Export(DisputeFilterViewModel filter)
    {
        var query = _context.PaymentDisputes
            .Include(d => d.Payment)
            .Include(d => d.Customer)
            .AsQueryable();

        // Apply same filters as Index
        if (filter.Status.HasValue)
            query = query.Where(d => d.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(d => d.OpenedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(d => d.OpenedAt <= filter.ToDate.Value.AddDays(1));

        var disputes = await query.OrderByDescending(d => d.OpenedAt).ToListAsync();

        // Build CSV
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,External ID,Customer,Email,Amount,Currency,Type,Status,Opened At,Response Due,Resolved At");

        foreach (var d in disputes)
        {
            csv.AppendLine($"{d.Id},{d.ExternalDisputeId},{d.CustomerName},{d.CustomerEmail},{d.DisputedAmount},{d.Currency},{d.Type},{d.Status},{d.OpenedAt:yyyy-MM-dd},{d.ResponseDueDate:yyyy-MM-dd},{d.ResolvedAt:yyyy-MM-dd}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"disputes_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// لوحة النزاعات - Disputes dashboard/analytics
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        // Monthly trend
        var monthlyData = await _context.PaymentDisputes
            .Where(d => d.OpenedAt >= thirtyDaysAgo)
            .GroupBy(d => d.OpenedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count(), Amount = g.Sum(x => x.DisputedAmount) })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // By type breakdown
        var byType = await _context.PaymentDisputes
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        // By gateway breakdown
        var byGateway = await _context.PaymentDisputes
            .GroupBy(d => d.Provider)
            .Select(g => new { Gateway = g.Key, Count = g.Count() })
            .ToListAsync();

        // Win/loss rate
        var resolved = await _context.PaymentDisputes
            .Where(d => d.Status == DisputeStatus.Won || d.Status == DisputeStatus.Lost)
            .ToListAsync();
        var winRate = resolved.Count > 0 
            ? (decimal)resolved.Count(d => d.Status == DisputeStatus.Won) / resolved.Count * 100 
            : 0;

        // Urgent (overdue)
        var overdue = await _context.PaymentDisputes
            .Where(d => d.ResponseDueDate != null && now > d.ResponseDueDate && d.Status == DisputeStatus.NeedsResponse)
            .OrderBy(d => d.ResponseDueDate)
            .Take(10)
            .ToListAsync();

        ViewBag.MonthlyData = monthlyData;
        ViewBag.ByType = byType;
        ViewBag.ByGateway = byGateway;
        ViewBag.WinRate = winRate;
        ViewBag.OverdueDisputes = overdue;
        ViewBag.TotalLost = resolved.Where(d => d.Status == DisputeStatus.Lost).Sum(d => d.DisputedAmount);

        return View();
    }
}

#region ViewModels

public class DisputeFilterViewModel
{
    public DisputeStatus? Status { get; set; }
    public DisputeType? Type { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IsOverdue { get; set; }
}

public class DisputeEvidenceViewModel
{
    [Required]
    public int DisputeId { get; set; }

    [Required(ErrorMessage = "نوع الدليل مطلوب")]
    [Display(Name = "نوع الدليل")]
    public string EvidenceType { get; set; } = string.Empty;

    [Required(ErrorMessage = "وصف الدليل مطلوب")]
    [Display(Name = "وصف الدليل")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "روابط الملفات")]
    public List<string>? FileUrls { get; set; }

    [Display(Name = "رابط إثبات الخدمة")]
    public string? ServiceDocumentUrl { get; set; }
}

#endregion

