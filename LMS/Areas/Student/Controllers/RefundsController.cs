using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// استرداد المدفوعات - Refunds Controller
/// </summary>
public class RefundsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RefundsController> _logger;

    public RefundsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<RefundsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة طلبات الاسترداد - Refund requests list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var refunds = await _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
            .Where(r => r.Payment.StudentId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new RefundDisplayViewModel
            {
                Id = r.Id,
                CourseName = r.Payment.Course != null ? r.Payment.Course.Title : "غير متاح",
                RefundAmount = r.RefundAmount,
                Currency = r.Currency,
                Reason = r.Reason ?? string.Empty,
                Details = r.Details,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                RejectionNotes = r.RejectionNotes
            })
            .ToListAsync();

        return View(refunds);
    }

    /// <summary>
    /// طلب استرداد - Request refund
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Request(int paymentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Enrollment)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.StudentId == userId);

        if (payment == null)
            return NotFound();

        // Only allow refunds for completed payments
        if (payment.Status != PaymentStatus.Completed)
        {
            SetErrorMessage("لا يمكن طلب استرداد لدفعة غير مكتملة");
            return RedirectToAction(nameof(Index));
        }

        // Check if already requested refund
        var existingRefund = await _context.Refunds
            .AnyAsync(r => r.PaymentId == paymentId);

        if (existingRefund)
        {
            SetErrorMessage("تم تقديم طلب استرداد لهذه الدفعة مسبقاً");
            return RedirectToAction(nameof(Index));
        }

        // Check refund eligibility (within 30 days and less than 30% progress)
        var daysSincePurchase = (DateTime.UtcNow - payment.CreatedAt).TotalDays;
        if (daysSincePurchase > 30)
        {
            SetErrorMessage("لا يمكن طلب الاسترداد بعد مرور 30 يوماً على الشراء");
            return RedirectToAction("Index", "Courses", new { area = "Student" });
        }

        if (payment.Enrollment != null && payment.Enrollment.ProgressPercentage > 30)
        {
            SetErrorMessage("لا يمكن طلب الاسترداد بعد إكمال أكثر من 30% من الدورة");
            return RedirectToAction("Index", "Courses", new { area = "Student" });
        }

        var viewModel = new RefundRequestViewModel
        {
            PaymentId = paymentId,
            RefundAmount = payment.TotalAmount,
            Currency = payment.Currency,
            CourseName = payment.Course?.Title ?? "غير متاح"
        };

        return View(viewModel);
    }

    /// <summary>
    /// إرسال طلب الاسترداد - Submit refund request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Request(RefundRequestViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Enrollment)
            .FirstOrDefaultAsync(p => p.Id == model.PaymentId && p.StudentId == userId);

        if (payment == null)
            return NotFound();

        // Only allow refunds for completed payments
        if (payment.Status != PaymentStatus.Completed)
        {
            SetErrorMessage("لا يمكن طلب استرداد لدفعة غير مكتملة");
            return RedirectToAction(nameof(Index));
        }

        // Check if already requested
        var existingRefund = await _context.Refunds
            .AnyAsync(r => r.PaymentId == model.PaymentId);

        if (existingRefund)
        {
            SetErrorMessage("تم تقديم طلب استرداد لهذه الدفعة مسبقاً");
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            var refund = new Refund
            {
                PaymentId = model.PaymentId,
                OriginalTransactionId = payment.TransactionId,
                RefundAmount = payment.TotalAmount,
                Currency = payment.Currency,
                Reason = model.Reason,
                Details = model.Details,
                Status = RefundStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _context.Refunds.Add(refund);

            // Update payment status to indicate refund is pending
            payment.Status = PaymentStatus.Processing;

            // Suspend enrollment access pending refund review
            if (payment.Enrollment != null && payment.Enrollment.Status == EnrollmentStatus.Active)
            {
                payment.Enrollment.Status = EnrollmentStatus.Suspended;
                _logger.LogInformation("Enrollment {EnrollmentId} suspended pending refund for payment {PaymentId}",
                    payment.Enrollment.Id, payment.Id);
            }

            await _context.SaveChangesAsync();

            // Send confirmation notification (refund.Id is now available)
            _context.Notifications.Add(new Domain.Entities.Notifications.Notification
            {
                UserId = userId!,
                Title = "تم استلام طلب الاسترداد",
                Message = $"تم استلام طلب استرداد بقيمة {payment.TotalAmount} {payment.Currency} لدورة \"{payment.Course?.Title ?? "غير محدد"}\". سيتم المراجعة خلال 3-5 أيام عمل.",
                Type = Domain.Enums.NotificationType.Payment,
                ActionUrl = $"/Student/Refunds/Details/{refund.Id}",
                ActionText = "متابعة الطلب",
                IsRead = false
            });

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تقديم طلب الاسترداد بنجاح. سيتم مراجعته خلال 3-5 أيام عمل");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// تفاصيل طلب الاسترداد - Refund details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var refund = await _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
            .Include(r => r.Processor)
            .FirstOrDefaultAsync(r => r.Id == id && r.Payment.StudentId == userId);

        if (refund == null)
            return NotFound();

        return View(refund);
    }
}

