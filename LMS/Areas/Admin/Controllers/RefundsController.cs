using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الاستردادات - Refunds Management Controller
/// </summary>
public class RefundsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RefundsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public RefundsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<RefundsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة طلبات الاسترداد - Refunds list
    /// </summary>
    public async Task<IActionResult> Index(RefundStatus? status, int page = 1)
    {
        var query = _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Student)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
            .Include(r => r.Processor)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("refunds", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var refunds = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminRefundDisplayViewModel
            {
                Id = r.Id,
                StudentName = r.Payment.Student != null 
                    ? $"{r.Payment.Student.FirstName ?? ""} {r.Payment.Student.LastName ?? ""}".Trim()
                    : "Unknown Student",
                StudentEmail = r.Payment.Student != null ? (r.Payment.Student.Email ?? string.Empty) : string.Empty,
                CourseName = r.Payment.Course != null ? r.Payment.Course.Title : "غير متاح",
                RefundAmount = r.RefundAmount,
                Currency = r.Currency,
                Reason = r.Reason ?? string.Empty,
                Status = r.Status,
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                ProcessorName = r.Processor != null ? $"{r.Processor.FirstName} {r.Processor.LastName}" : null
            })
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        return View(refunds);
    }

    /// <summary>
    /// تفاصيل طلب الاسترداد - Refund details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var refund = await _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Student)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
                    .ThenInclude(c => c.Instructor)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Enrollment)
            .Include(r => r.Processor)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (refund == null)
            return NotFound();

        return View(refund);
    }

    /// <summary>
    /// معالجة الاسترداد - Process refund
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Process(int id)
    {
        var refund = await _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Student)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (refund == null)
            return NotFound();

        if (refund.Status != RefundStatus.Pending)
        {
            SetErrorMessage("هذا الطلب تمت معالجته بالفعل", "This request has already been processed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var viewModel = new ProcessRefundViewModel
        {
            RefundId = refund.Id,
            RefundAmount = refund.RefundAmount,
            StudentName = $"{refund.Payment.Student.FirstName} {refund.Payment.Student.LastName}",
            CourseName = refund.Payment.Course?.Title ?? "غير متاح",
            Status = RefundStatus.Approved
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ قرار الاسترداد - Save refund decision
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(ProcessRefundViewModel model)
    {
        try
        {
            var refund = await _context.Refunds
                .Include(r => r.Payment)
                    .ThenInclude(p => p.Student)
                .Include(r => r.Payment)
                    .ThenInclude(p => p.Course)
                        .ThenInclude(c => c.Instructor)
                .Include(r => r.Payment)
                    .ThenInclude(p => p.Enrollment)
                .FirstOrDefaultAsync(r => r.Id == model.RefundId);

            if (refund == null)
            {
                _logger.LogWarning("Refund not found: {RefundId}", model.RefundId);
                return NotFound();
            }

            if (refund.Status != RefundStatus.Pending)
            {
                _logger.LogWarning("Refund {RefundId} already processed. Status: {Status}", 
                    model.RefundId, refund.Status);
                SetErrorMessage("هذا الطلب تمت معالجته بالفعل", "This request has already been processed.");
                return RedirectToAction(nameof(Details), new { id = model.RefundId });
            }

            if (ModelState.IsValid)
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    // Update refund status
                    refund.Status = model.Status;
                    refund.ProcessedAt = DateTime.UtcNow;
                    refund.ProcessedBy = _currentUserService.UserId;

                    if (model.Status == RefundStatus.Rejected)
                    {
                        refund.RejectionNotes = model.Notes;
                    }
                    else if (model.Status == RefundStatus.Approved)
                    {
                        // Update payment status
                        refund.Payment.Status = PaymentStatus.Refunded;
                        refund.Payment.RefundedAt = DateTime.UtcNow;

                        // Update enrollment status
                        if (refund.Payment.Enrollment != null)
                        {
                            refund.Payment.Enrollment.Status = EnrollmentStatus.Cancelled;
                            refund.Payment.Enrollment.RefundedAt = DateTime.UtcNow;

                            // Decrease course student count
                            if (refund.Payment.Course != null)
                            {
                                refund.Payment.Course.TotalStudents = Math.Max(0, refund.Payment.Course.TotalStudents - 1);
                            }
                        }

                        // Adjust instructor earnings
                        var earning = await _context.InstructorEarnings
                            .FirstOrDefaultAsync(e => e.PaymentId == refund.PaymentId);

                        if (earning != null)
                        {
                            var instructorProfile = await _context.InstructorProfiles
                                .FirstOrDefaultAsync(p => p.UserId == refund.Payment.Course!.InstructorId);

                            if (instructorProfile != null)
                            {
                                // Subtract from instructor's earnings
                                instructorProfile.TotalEarnings -= earning.NetAmount;

                                // Deduct from appropriate balance based on earning status
                                if (earning.Status == "available")
                                {
                                    // Earnings were already available, deduct from available balance
                                    instructorProfile.AvailableBalance = Math.Max(0, instructorProfile.AvailableBalance - earning.NetAmount);
                                }
                                else if (earning.Status == "pending")
                                {
                                    // Earnings were still pending, deduct from pending balance
                                    instructorProfile.PendingBalance = Math.Max(0, instructorProfile.PendingBalance - earning.NetAmount);
                                }

                                // Mark earning as refunded
                                earning.Status = "refunded";
                                earning.RefundedAt = DateTime.UtcNow;
                            }
                        }

                        // Create refund transaction record
                        refund.RefundedAt = DateTime.UtcNow;
                        refund.RefundMethod = "Original Payment Method";
                    }

                    await _context.SaveChangesAsync();

                    // Send notification email to student
                    if (refund.Payment.Student?.Email != null)
                    {
                        var subject = model.Status == RefundStatus.Approved 
                            ? "تمت الموافقة على طلب الاسترداد" 
                            : "تم رفض طلب الاسترداد";

                        var body = model.Status == RefundStatus.Approved
                            ? $@"<html><body dir='rtl'>
                                <h2>تمت الموافقة على طلب الاسترداد</h2>
                                <p>عزيزي/عزيزتي {refund.Payment.Student.FirstName},</p>
                                <p>تمت الموافقة على طلب الاسترداد الخاص بك لدورة <strong>{refund.Payment.Course?.Title}</strong>.</p>
                                <p><strong>المبلغ المسترد:</strong> {refund.RefundAmount} {refund.Currency}</p>
                                <p>سيتم إرجاع المبلغ إلى طريقة الدفع الأصلية خلال 5-7 أيام عمل.</p>
                                <br/>
                                <p>فريق منصة LMS</p>
                            </body></html>"
                            : $@"<html><body dir='rtl'>
                                <h2>تم رفض طلب الاسترداد</h2>
                                <p>عزيزي/عزيزتي {refund.Payment.Student.FirstName},</p>
                                <p>للأسف، تم رفض طلب الاسترداد الخاص بك لدورة <strong>{refund.Payment.Course?.Title}</strong>.</p>
                                {(!string.IsNullOrEmpty(model.Notes) ? $"<p><strong>السبب:</strong> {model.Notes}</p>" : "")}
                                <p>إذا كان لديك أي استفسارات، يرجى التواصل مع الدعم الفني.</p>
                                <br/>
                                <p>فريق منصة LMS</p>
                            </body></html>";

                        await _emailService.SendEmailAsync(refund.Payment.Student.Email, subject, body, true);
                    }

                    // Notify instructor if refund approved
                    if (model.Status == RefundStatus.Approved && refund.Payment.Course?.Instructor?.Email != null)
                    {
                        await _emailService.SendEmailAsync(
                            refund.Payment.Course.Instructor.Email,
                            "إشعار باسترداد",
                            $@"<html><body dir='rtl'>
                                <h2>إشعار باسترداد</h2>
                                <p>تم الموافقة على طلب استرداد لدورة <strong>{refund.Payment.Course.Title}</strong>.</p>
                                <p><strong>المبلغ:</strong> {refund.RefundAmount} {refund.Currency}</p>
                                <p>تم خصم المبلغ من أرباحك.</p>
                                <br/>
                                <p>فريق منصة LMS</p>
                            </body></html>",
                            true);
                    }
                });

                _logger.LogInformation("Refund {RefundId} processed as {Status} by admin {AdminId}", 
                    model.RefundId, model.Status, _currentUserService.UserId);

                SetSuccessMessage(model.Status == RefundStatus.Approved 
                    ? "تمت الموافقة على الاسترداد بنجاح. تم إرسال إشعارات للطالب والمدرس." 
                    : "تم رفض طلب الاسترداد وإرسال إشعار للطالب.");

                return RedirectToAction(nameof(Details), new { id = model.RefundId });
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund {RefundId}", model.RefundId);
            SetErrorMessage("حدث خطأ أثناء معالجة الاسترداد", "An error occurred while processing the refund.");
            return View(model);
        }
    }

    /// <summary>
    /// إنشاء طلب استرداد من دفعة - Create refund request from payment
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CreateRefund(int paymentId)
    {
        var payment = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            SetErrorMessage("الدفعة غير موجودة", "Payment not found.");
            return RedirectToAction("Index", "Payments");
        }

        if (payment.Status != PaymentStatus.Completed)
        {
            SetErrorMessage("يمكن استرداد الدفعات المكتملة فقط", "Only completed payments can be refunded.");
            return RedirectToAction("Details", "Payments", new { id = paymentId });
        }

        // Check if refund already exists
        var existingRefund = await _context.Refunds
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId && r.Status != RefundStatus.Rejected);

        if (existingRefund != null)
        {
            SetWarningMessage("يوجد طلب استرداد قائم لهذه الدفعة بالفعل", "A refund request already exists for this payment.");
            return RedirectToAction(nameof(Details), new { id = existingRefund.Id });
        }

        ViewBag.Payment = payment;
        return View();
    }

    /// <summary>
    /// حفظ طلب الاسترداد الجديد - Save new refund request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRefund(int paymentId, string reason, decimal? partialAmount = null)
    {
        try
        {
            var payment = await _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                SetErrorMessage("الدفعة غير موجودة", "Payment not found.");
                return RedirectToAction("Index", "Payments");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                SetErrorMessage("يجب إدخال سبب الاسترداد", "Refund reason is required.");
                ViewBag.Payment = payment;
                return View();
            }

            var refundAmount = partialAmount ?? payment.TotalAmount;
            if (refundAmount > payment.TotalAmount || refundAmount <= 0)
            {
                SetErrorMessage("مبلغ الاسترداد غير صالح", "Invalid refund amount.");
                ViewBag.Payment = payment;
                return View();
            }

            var refund = new Domain.Entities.Payments.Refund
            {
                PaymentId = paymentId,
                RefundAmount = refundAmount,
                Currency = payment.Currency,
                Reason = reason,
                Status = RefundStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserId
            };

            _context.Refunds.Add(refund);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refund request {RefundId} created for payment {PaymentId} by admin {AdminId}", 
                refund.Id, paymentId, _currentUserService.UserId);

            SetSuccessMessage("تم إنشاء طلب الاسترداد بنجاح", "Refund request created successfully.");
            return RedirectToAction(nameof(Process), new { id = refund.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refund for payment {PaymentId}", paymentId);
            SetErrorMessage("حدث خطأ أثناء إنشاء طلب الاسترداد", "An error occurred while creating the refund request.");
            return RedirectToAction("Details", "Payments", new { id = paymentId });
        }
    }

    /// <summary>
    /// إحصائيات الاستردادات - Refund statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var refundsQuery = _context.Refunds.AsQueryable();

            if (fromDate.HasValue)
            {
                refundsQuery = refundsQuery.Where(r => r.RequestedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                refundsQuery = refundsQuery.Where(r => r.RequestedAt <= toDate.Value);
            }

            var stats = new
            {
                TotalRefunds = await refundsQuery.CountAsync(),
                PendingRefunds = await refundsQuery.CountAsync(r => r.Status == RefundStatus.Pending),
                ApprovedRefunds = await refundsQuery.CountAsync(r => r.Status == RefundStatus.Approved),
                RejectedRefunds = await refundsQuery.CountAsync(r => r.Status == RefundStatus.Rejected),
                TotalRefundedAmount = await refundsQuery
                    .Where(r => r.Status == RefundStatus.Approved)
                    .SumAsync(r => r.RefundAmount),
                AverageRefundAmount = await refundsQuery
                    .Where(r => r.Status == RefundStatus.Approved)
                    .AverageAsync(r => (decimal?)r.RefundAmount) ?? 0,
                ApprovalRate = await refundsQuery.CountAsync() > 0
                    ? (await refundsQuery.CountAsync(r => r.Status == RefundStatus.Approved) * 100.0 
                       / await refundsQuery.CountAsync())
                    : 0,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading refund statistics");
            SetErrorMessage("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics.");
            return RedirectToAction(nameof(Index));
        }
    }
}

