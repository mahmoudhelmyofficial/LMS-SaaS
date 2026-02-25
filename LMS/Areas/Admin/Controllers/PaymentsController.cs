using LMS.Data;
using LMS.Domain.Entities.Financial;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المدفوعات - Payments Management Controller
/// </summary>
public class PaymentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;

    public PaymentsController(
        ApplicationDbContext context, 
        ILogger<PaymentsController> logger,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _configService = configService;
    }

    /// <summary>
    /// قائمة المدفوعات - Payments list
    /// </summary>
    public async Task<IActionResult> Index(PaymentStatus? status, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= to.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("default", 20);
        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Page = page;

        return View(payments);
    }

    /// <summary>
    /// تفاصيل الدفعة - Payment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        return View(payment);
    }

    /// <summary>
    /// عرض الفاتورة - Show Invoice
    /// </summary>
    public async Task<IActionResult> Invoice(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
                .ThenInclude(c => c.Instructor)
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        // If there's an existing invoice, use it; otherwise show payment details as invoice
        return View(payment);
    }

    /// <summary>
    /// طلبات الاسترداد - Refund requests
    /// </summary>
    public async Task<IActionResult> Refunds(RefundStatus? status, int page = 1)
    {
        var query = _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Student)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("default", 20);
        var refunds = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;

        return View(refunds);
    }

    /// <summary>
    /// الموافقة على الاسترداد - Approve refund (Redirects to RefundsController)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRefund(int id)
    {
        // Redirect to RefundsController which has comprehensive logic
        return RedirectToAction("Process", "Refunds", new { id });
    }

    /// <summary>
    /// تأكيد الدفع يدوياً - Manually confirm payment (for offline payments)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, string? notes)
    {
        try
        {
            var payment = await _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                    .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found: {PaymentId}", id);
                return NotFound();
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                SetWarningMessage(CultureExtensions.T("الدفعة مؤكدة بالفعل", "Payment is already confirmed."));
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Update payment status
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                payment.AdminNotes = notes;
                payment.VerifiedBy = _currentUserService.UserId;

                // Create enrollment if doesn't exist
                if (payment.EnrollmentId == null && payment.CourseId.HasValue)
                {
                    var enrollment = new Enrollment
                    {
                        StudentId = payment.StudentId,
                        CourseId = payment.CourseId.Value,
                        Status = EnrollmentStatus.Active,
                        EnrolledAt = DateTime.UtcNow,
                        PaidAmount = payment.TotalAmount,
                        Currency = payment.Currency,
                        IsFree = payment.TotalAmount == 0,
                        TotalLessons = await _context.Lessons
                            .CountAsync(l => l.Module.CourseId == payment.CourseId)
                    };

                    _context.Enrollments.Add(enrollment);
                    await _context.SaveChangesAsync();

                    payment.EnrollmentId = enrollment.Id;

                    // Update course student count
                    if (payment.Course != null)
                    {
                        payment.Course.TotalStudents++;
                    }
                }

                // Create invoice if doesn't exist
                var existingInvoice = await _context.Invoices.AnyAsync(i => i.PaymentId == id);
                if (!existingInvoice)
                {
                    var invoice = new Invoice
                    {
                        InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{payment.Id:D6}",
                        PaymentId = payment.Id,
                        StudentId = payment.StudentId,
                        IssuedDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow,
                        Status = "Paid",
                        SubTotal = payment.OriginalAmount,
                        TaxAmount = payment.TaxAmount,
                        DiscountAmount = payment.DiscountAmount,
                        TotalAmount = payment.TotalAmount,
                        Currency = payment.Currency,
                        Notes = $"Manual confirmation. {notes}"
                    };

                    _context.Invoices.Add(invoice);
                }

                // Calculate instructor earnings if not exists
                if (payment.CourseId.HasValue && payment.Course != null)
                {
                    var existingEarning = await _context.InstructorEarnings
                        .AnyAsync(e => e.PaymentId == payment.Id);

                    if (!existingEarning)
                    {
                        var instructorProfile = await _context.InstructorProfiles
                            .FirstOrDefaultAsync(p => p.UserId == payment.Course.InstructorId);

                        if (instructorProfile != null)
                        {
                            var (platformCommission, instructorAmount) = 
                                BusinessRuleHelper.CalculateCommission(
                                    payment.TotalAmount, 
                                    instructorProfile.CommissionRate);

                            var earning = new InstructorEarning
                            {
                                InstructorId = payment.Course.InstructorId,
                                CourseId = payment.CourseId,
                                PaymentId = payment.Id,
                                EarningType = "sale",
                                GrossAmount = payment.TotalAmount,
                                PlatformCommissionRate = 100 - instructorProfile.CommissionRate,
                                PlatformCommission = platformCommission,
                                InstructorRate = instructorProfile.CommissionRate,
                                NetAmount = instructorAmount,
                                Currency = payment.Currency,
                                Status = "pending",
                                AvailableDate = BusinessRuleHelper.CalculateEarningsAvailabilityDate(DateTime.UtcNow)
                            };

                            instructorProfile.TotalEarnings += instructorAmount;
                            instructorProfile.PendingBalance += instructorAmount;

                            _context.InstructorEarnings.Add(earning);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Send confirmation email
                if (payment.Student?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        payment.Student.Email,
                        "تأكيد استلام الدفعة",
                        $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {payment.Student.FirstName}</h2>
                            <p>تم تأكيد دفعتك بنجاح.</p>
                            <p><strong>رقم العملية:</strong> {payment.TransactionId}</p>
                            <p><strong>المبلغ:</strong> {payment.TotalAmount} {payment.Currency}</p>
                            <p>يمكنك الآن الوصول إلى الدورة.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Payment {PaymentId} manually confirmed by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم تأكيد الدفعة بنجاح وإنشاء التسجيل", "Payment confirmed successfully and enrollment created."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment {PaymentId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تأكيد الدفعة", "An error occurred while confirming the payment."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إلغاء الدفعة - Cancel payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPayment(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetErrorMessage(CultureExtensions.T("يجب إدخال سبب الإلغاء", "Cancellation reason is required."));
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found: {PaymentId}", id);
                return NotFound();
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                SetErrorMessage(CultureExtensions.T("لا يمكن إلغاء دفعة مكتملة. استخدم طلب الاسترداد بدلاً من ذلك", "Cannot cancel a completed payment. Use refund request instead."));
                return RedirectToAction(nameof(Details), new { id });
            }

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = reason;
            payment.AdminNotes = $"Cancelled by admin: {reason}";

            await _context.SaveChangesAsync();

            // Send notification
            if (payment.Student?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    payment.Student.Email,
                    "إلغاء عملية الدفع",
                    $@"<html><body dir='rtl'>
                        <h2>عزيزي/عزيزتي {payment.Student.FirstName}</h2>
                        <p>تم إلغاء عملية الدفع.</p>
                        <p><strong>رقم العملية:</strong> {payment.TransactionId}</p>
                        <p><strong>السبب:</strong> {reason}</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>",
                    true);
            }

            _logger.LogInformation("Payment {PaymentId} cancelled by admin {AdminId}. Reason: {Reason}", 
                id, _currentUserService.UserId, reason);

            SetSuccessMessage(CultureExtensions.T("تم إلغاء الدفعة", "Payment cancelled."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment {PaymentId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إلغاء الدفعة", "An error occurred while cancelling the payment."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// طلبات السحب - Withdrawal requests
    /// </summary>
    public async Task<IActionResult> Withdrawals(WithdrawalStatus? status, int page = 1)
    {
        var query = _context.WithdrawalRequests
            .Include(w => w.Instructor)
                .ThenInclude(i => i.InstructorProfile)
            .Include(w => w.WithdrawalMethod)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("default", 20);
        var withdrawals = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;

        return View(withdrawals);
    }

    /// <summary>
    /// تفاصيل طلب السحب (للمودال) - Get withdrawal details for modal (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWithdrawalDetails(int? id)
    {
        if (!id.HasValue)
            return Json(new { success = false, html = "<div class=\"alert alert-danger\">معرف غير صالح</div>" });

        try
        {
            var withdrawal = await _context.WithdrawalRequests
                .Include(w => w.Instructor)
                    .ThenInclude(i => i.InstructorProfile)
                .Include(w => w.WithdrawalMethod)
                .Include(w => w.ProcessedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id.Value);

            if (withdrawal == null)
                return Json(new { success = false, html = "<div class=\"alert alert-warning\">طلب السحب غير موجود</div>" });

            var profile = withdrawal.Instructor?.InstructorProfile;
            var availableBalance = profile?.AvailableBalance ?? 0;
            var instructorName = withdrawal.Instructor != null
                ? $"{withdrawal.Instructor.FirstName} {withdrawal.Instructor.LastName}"
                : "-";
            var instructorEmail = withdrawal.Instructor?.Email ?? "-";
            var methodName = withdrawal.WithdrawalMethod?.Name ?? "-";
            var methodType = withdrawal.WithdrawalMethod?.Type ?? "";
            var accountDetails = string.IsNullOrWhiteSpace(withdrawal.AccountDetails) || withdrawal.AccountDetails == "{}"
                ? "-"
                : withdrawal.AccountDetails;

            var statusBadge = withdrawal.Status switch
            {
                WithdrawalStatus.Pending => "<span class=\"badge bg-soft-warning text-warning\">قيد الانتظار</span>",
                WithdrawalStatus.Processing => "<span class=\"badge bg-soft-info text-info\">قيد المعالجة</span>",
                WithdrawalStatus.Approved => "<span class=\"badge bg-soft-success text-success\">تمت الموافقة</span>",
                WithdrawalStatus.Completed => "<span class=\"badge bg-soft-success text-success\">مكتمل</span>",
                WithdrawalStatus.Rejected => "<span class=\"badge bg-soft-danger text-danger\">مرفوض</span>",
                WithdrawalStatus.Cancelled => "<span class=\"badge bg-soft-secondary text-secondary\">ملغي</span>",
                _ => withdrawal.Status.ToString()
            };

            var html = $@"
<div class='row g-3'>
    <div class='col-12'><h6 class='border-bottom pb-2'>المدرس</h6></div>
    <div class='col-md-6'><strong>الاسم:</strong> {instructorName}</div>
    <div class='col-md-6'><strong>البريد:</strong> {instructorEmail}</div>
    <div class='col-md-6'><strong>الرصيد المتاح حالياً:</strong> <span class='text-success fw-bold'>{availableBalance:N2} {withdrawal.Currency}</span></div>
    <div class='col-12'><h6 class='border-bottom pb-2 mt-3'>تفاصيل الطلب</h6></div>
    <div class='col-md-4'><strong>المبلغ المطلوب:</strong> {withdrawal.Amount:N2} {withdrawal.Currency}</div>
    <div class='col-md-4'><strong>الرسوم:</strong> {withdrawal.Fee:N2} {withdrawal.Currency}</div>
    <div class='col-md-4'><strong>الصافي:</strong> <span class='text-success fw-bold'>{withdrawal.NetAmount:N2} {withdrawal.Currency}</span></div>
    <div class='col-md-6'><strong>طريقة السحب:</strong> {methodName} <small class='text-muted'>({methodType})</small></div>
    <div class='col-md-6'><strong>الحالة:</strong> {statusBadge}</div>
    <div class='col-12'><strong>تفاصيل الحساب (المدخلة من المدرس):</strong><div class='mt-1 p-2 bg-light rounded' dir='ltr' style='white-space: pre-wrap;'>{System.Net.WebUtility.HtmlEncode(accountDetails)}</div></div>
    <div class='col-12'><strong>ملاحظات المدرس:</strong> {(string.IsNullOrEmpty(withdrawal.InstructorNotes) ? "-" : System.Net.WebUtility.HtmlEncode(withdrawal.InstructorNotes))}</div>
    <div class='col-12'><strong>ملاحظات الإدارة:</strong> {(string.IsNullOrEmpty(withdrawal.AdminNotes) ? "-" : System.Net.WebUtility.HtmlEncode(withdrawal.AdminNotes))}</div>
    <div class='col-12'><strong>تاريخ الطلب:</strong> {withdrawal.CreatedAt:dd/MM/yyyy HH:mm}</div>
    {(withdrawal.ProcessedAt.HasValue ? $"<div class='col-12'><strong>تاريخ المعالجة:</strong> {withdrawal.ProcessedAt:dd/MM/yyyy HH:mm}</div>" : "")}
</div>";

            return Json(new { success = true, html });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading withdrawal details {WithdrawalId}. Message: {Message}, Inner: {Inner}",
                id, ex.Message, ex.InnerException?.Message);
            return Json(new { success = false, html = "<div class=\"alert alert-danger\">حدث خطأ أثناء تحميل التفاصيل</div>" });
        }
    }

    /// <summary>
    /// معالجة طلب السحب - Process withdrawal request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessWithdrawal(int id, WithdrawalStatus status, string? notes)
    {
        try
        {
            var withdrawal = await _context.WithdrawalRequests
                .Include(w => w.Instructor)
                    .ThenInclude(i => i.InstructorProfile)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal == null)
            {
                _logger.LogWarning("Withdrawal request not found: {WithdrawalId}", id);
                return NotFound();
            }

            if (withdrawal.Status != WithdrawalStatus.Pending)
            {
                SetWarningMessage(CultureExtensions.T("الطلب تمت معالجته بالفعل", "Request has already been processed."));
                return RedirectToAction(nameof(Withdrawals));
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                withdrawal.Status = status;
                withdrawal.ProcessedAt = DateTime.UtcNow;
                withdrawal.ProcessedById = _currentUserService.UserId;
                withdrawal.AdminNotes = notes;

                if (status == WithdrawalStatus.Approved)
                {
                    // Do not set CompletedAt here; instructor confirms receipt and then status becomes Completed
                    // Update instructor total withdrawn (available balance already deducted when request was created)
                    if (withdrawal.Instructor.InstructorProfile != null)
                        withdrawal.Instructor.InstructorProfile.TotalWithdrawn += withdrawal.NetAmount;
                }
                else if (status == WithdrawalStatus.Rejected || status == WithdrawalStatus.Cancelled)
                {
                    // Return amount to available balance
                    if (withdrawal.Instructor.InstructorProfile != null)
                        withdrawal.Instructor.InstructorProfile.AvailableBalance += withdrawal.Amount;
                }

                await _context.SaveChangesAsync();

                // Send notification
                if (withdrawal.Instructor?.Email != null)
                {
                    var subject = status == WithdrawalStatus.Approved 
                        ? "تمت الموافقة على طلب السحب" 
                        : "تم رفض طلب السحب";

                    var body = status == WithdrawalStatus.Approved
                        ? $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {withdrawal.Instructor.FirstName}</h2>
                            <p>تمت الموافقة على طلب السحب الخاص بك.</p>
                            <p><strong>المبلغ:</strong> {withdrawal.Amount} {withdrawal.Currency}</p>
                            <p><strong>صافي المبلغ بعد الرسوم:</strong> {withdrawal.NetAmount} {withdrawal.Currency}</p>
                            <p>سيتم تحويل المبلغ إلى حسابك خلال 3-5 أيام عمل.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>"
                        : $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {withdrawal.Instructor.FirstName}</h2>
                            <p>للأسف، تم رفض طلب السحب الخاص بك.</p>
                            <p><strong>المبلغ:</strong> {withdrawal.Amount} {withdrawal.Currency}</p>
                            {(!string.IsNullOrEmpty(notes) ? $"<p><strong>السبب:</strong> {notes}</p>" : "")}
                            <p>تم إعادة المبلغ إلى رصيدك المتاح.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>";

                    await _emailService.SendEmailAsync(
                        withdrawal.Instructor.Email!,
                        subject,
                        body,
                        true);
                }
            });

            _logger.LogInformation("Withdrawal {WithdrawalId} processed as {Status} by admin {AdminId}", 
                id, status, _currentUserService.UserId);

            SetSuccessMessage(status == WithdrawalStatus.Approved 
                ? CultureExtensions.T("تمت الموافقة على طلب السحب وإرسال إشعار", "Withdrawal request approved and notification sent.") 
                : CultureExtensions.T("تم رفض طلب السحب وإرسال إشعار", "Withdrawal request rejected and notification sent."));

            return RedirectToAction(nameof(Withdrawals));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing withdrawal {WithdrawalId}. Message: {Message}, Inner: {Inner}", id, ex.Message, ex.InnerException?.Message);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء معالجة الطلب", "An error occurred while processing the request."));
            return RedirectToAction(nameof(Withdrawals));
        }
    }

    /// <summary>
    /// معالجة طلب السحب (AJAX) - Process withdrawal request from modal (returns JSON)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessWithdrawalAjax([FromBody] ProcessWithdrawalAjaxRequest model)
    {
        if (model == null || model.Id <= 0)
            return Json(new { success = false, message = "معرف الطلب غير صالح" });

        var status = model.Status?.ToLowerInvariant() switch
        {
            "approve" or "approved" => WithdrawalStatus.Approved,
            "reject" or "rejected" => WithdrawalStatus.Rejected,
            "processing" => WithdrawalStatus.Processing,
            _ => (WithdrawalStatus?)null
        };

        if (!status.HasValue)
            return Json(new { success = false, message = "حالة غير مدعومة" });

        try
        {
            var withdrawal = await _context.WithdrawalRequests
                .Include(w => w.Instructor)
                    .ThenInclude(i => i.InstructorProfile)
                .FirstOrDefaultAsync(w => w.Id == model.Id);

            if (withdrawal == null)
                return Json(new { success = false, message = "طلب السحب غير موجود" });

            if (withdrawal.Status != WithdrawalStatus.Pending)
                return Json(new { success = false, message = "الطلب تمت معالجته بالفعل" });

            await _context.ExecuteInTransactionAsync(async () =>
            {
                withdrawal.Status = status.Value;
                withdrawal.ProcessedAt = DateTime.UtcNow;
                withdrawal.ProcessedById = _currentUserService.UserId;
                withdrawal.AdminNotes = model.Notes;

                if (status == WithdrawalStatus.Approved)
                {
                    if (withdrawal.Instructor.InstructorProfile != null)
                        withdrawal.Instructor.InstructorProfile.TotalWithdrawn += withdrawal.NetAmount;
                }
                else if (status == WithdrawalStatus.Rejected || status == WithdrawalStatus.Cancelled)
                {
                    if (withdrawal.Instructor.InstructorProfile != null)
                        withdrawal.Instructor.InstructorProfile.AvailableBalance += withdrawal.Amount;
                }

                await _context.SaveChangesAsync();

                if (withdrawal.Instructor?.Email != null)
                {
                    var subject = status.Value == WithdrawalStatus.Approved ? "تمت الموافقة على طلب السحب" : "تم رفض طلب السحب";
                    var body = status.Value == WithdrawalStatus.Approved
                        ? $@"<html><body dir='rtl'><h2>عزيزي/عزيزتي {withdrawal.Instructor.FirstName}</h2><p>تمت الموافقة على طلب السحب الخاص بك.</p><p><strong>المبلغ:</strong> {withdrawal.Amount} {withdrawal.Currency}</p><p><strong>صافي المبلغ بعد الرسوم:</strong> {withdrawal.NetAmount} {withdrawal.Currency}</p><p>سيتم تحويل المبلغ إلى حسابك خلال 3-5 أيام عمل.</p><br/><p>فريق منصة LMS</p></body></html>"
                        : $@"<html><body dir='rtl'><h2>عزيزي/عزيزتي {withdrawal.Instructor.FirstName}</h2><p>للأسف، تم رفض طلب السحب الخاص بك.</p><p><strong>المبلغ:</strong> {withdrawal.Amount} {withdrawal.Currency}</p>{(!string.IsNullOrEmpty(model.Notes) ? $"<p><strong>السبب:</strong> {model.Notes}</p>" : "")}<p>تم إعادة المبلغ إلى رصيدك المتاح.</p><br/><p>فريق منصة LMS</p></body></html>";
                    await _emailService.SendEmailAsync(withdrawal.Instructor.Email, subject, body, true);
                }
            });

            _logger.LogInformation("Withdrawal {WithdrawalId} processed as {Status} by admin {AdminId} (AJAX)", model.Id, status.Value, _currentUserService.UserId);
            var message = status.Value == WithdrawalStatus.Approved ? "تمت الموافقة على طلب السحب وإرسال إشعار" : "تم رفض طلب السحب وإرسال إشعار";
            return Json(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing withdrawal AJAX {WithdrawalId}. Message: {Message}, Inner: {Inner}", model.Id, ex.Message, ex.InnerException?.Message);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة الطلب" });
        }
    }

    /// <summary>
    /// إحصائيات المدفوعات - Payment statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var paymentsQuery = _context.Payments
                .Where(p => p.CreatedAt >= fromDate && p.CreatedAt <= toDate);

            var stats = new
            {
                TotalPayments = await paymentsQuery.CountAsync(),
                CompletedPayments = await paymentsQuery.CountAsync(p => p.Status == PaymentStatus.Completed),
                PendingPayments = await paymentsQuery.CountAsync(p => p.Status == PaymentStatus.Pending),
                FailedPayments = await paymentsQuery.CountAsync(p => p.Status == PaymentStatus.Failed),
                TotalRevenue = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0,
                AverageTransactionValue = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .AverageAsync(p => (decimal?)p.TotalAmount) ?? 0,
                TotalTax = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .SumAsync(p => (decimal?)p.TaxAmount) ?? 0,
                TotalDiscount = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .SumAsync(p => (decimal?)p.DiscountAmount) ?? 0,
                SuccessRate = await paymentsQuery.CountAsync() > 0
                    ? (await paymentsQuery.CountAsync(p => p.Status == PaymentStatus.Completed) * 100.0 
                       / await paymentsQuery.CountAsync())
                    : 0,
                PaymentsByCurrency = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .GroupBy(p => p.Currency)
                    .Select(g => new { Currency = g.Key, Total = g.Sum(p => p.TotalAmount), Count = g.Count() })
                    .ToListAsync(),
                PaymentsByDay = await paymentsQuery
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .GroupBy(p => p.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(p => p.TotalAmount), Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync(),
                TopCourses = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && 
                               p.CourseId.HasValue &&
                               p.CreatedAt >= fromDate && p.CreatedAt <= toDate)
                    .GroupBy(p => new { p.CourseId, p.Course!.Title })
                    .Select(g => new { 
                        CourseId = g.Key.CourseId, 
                        Title = g.Key.Title, 
                        Revenue = g.Sum(p => p.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Revenue)
                    .Take(await _configService.GetTopItemsLimitAsync("payments_fraud", 10))
                    .ToListAsync(),
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading payment statistics");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير المدفوعات - Export payments to CSV
    /// </summary>
    public async Task<IActionResult> ExportPayments(DateTime? fromDate, DateTime? toDate, PaymentStatus? status)
    {
        try
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var query = _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                .Where(p => p.CreatedAt >= fromDate && p.CreatedAt <= toDate);

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var csv = "Payment ID,Transaction ID,Date,Student Name,Student Email,Course,Status,Original Amount,Discount,Tax,Total Amount,Currency\n";
            
            foreach (var payment in payments)
            {
                csv += $"{payment.Id}," +
                       $"\"{payment.TransactionId}\"," +
                       $"{payment.CreatedAt:yyyy-MM-dd HH:mm}," +
                       $"\"{payment.Student?.FirstName} {payment.Student?.LastName}\"," +
                       $"{payment.Student?.Email}," +
                       $"\"{payment.Course?.Title ?? "N/A"}\"," +
                       $"{payment.Status}," +
                       $"{payment.OriginalAmount}," +
                       $"{payment.DiscountAmount}," +
                       $"{payment.TaxAmount}," +
                       $"{payment.TotalAmount}," +
                       $"{payment.Currency}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"payments-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv";

            _logger.LogInformation("Payment export generated by admin {AdminId}. Count: {Count}", 
                _currentUserService.UserId, payments.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting payments");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تصدير البيانات", "An error occurred while exporting data."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// كشف الاحتيال - Fraud detection review
    /// </summary>
    public async Task<IActionResult> FraudDetection(int page = 1)
    {
        try
        {
            // Get thresholds first
            var fraudTimeWindow = (int)await _configService.GetThresholdAsync("fraud", "time_window_minutes", 10);
            var fraudPaymentCount = await _configService.GetIntConfigurationAsync("Thresholds", "fraud_payment_count", 2);
            var pageSize = await _configService.GetPaginationSizeAsync("default", 20);
            
            // Find suspicious payments based on various criteria
            var suspiciousPayments = await _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                .Where(p => 
                    // Multiple failed attempts from same user
                    _context.Payments.Count(p2 => p2.StudentId == p.StudentId && 
                                                  p2.Status == PaymentStatus.Failed &&
                                                  p2.CreatedAt >= DateTime.UtcNow.AddHours(-24)) > 3
                    ||
                    // High value transactions
                    p.TotalAmount > 10000
                    ||
                    // Multiple transactions in short time
                    _context.Payments.Count(p2 => p2.StudentId == p.StudentId &&
                                                  p2.CreatedAt >= DateTime.UtcNow.AddMinutes(-fraudTimeWindow)) > fraudPaymentCount
                )
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    Payment = p,
                    FailedAttempts = _context.Payments.Count(p2 => p2.StudentId == p.StudentId && 
                                                                    p2.Status == PaymentStatus.Failed &&
                                                                    p2.CreatedAt >= DateTime.UtcNow.AddHours(-24)),
                    RecentTransactions = _context.Payments.Count(p2 => p2.StudentId == p.StudentId &&
                                                                       p2.CreatedAt >= DateTime.UtcNow.AddMinutes(-fraudTimeWindow)),
                    IsHighValue = p.TotalAmount > 10000
                })
                .ToListAsync();

            ViewBag.Page = page;
            return View(suspiciousPayments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading fraud detection data");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل البيانات", "An error occurred while loading data."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Request model for ProcessWithdrawalAjax
    /// </summary>
    public class ProcessWithdrawalAjaxRequest
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }
}


