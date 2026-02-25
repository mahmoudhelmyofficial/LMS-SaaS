using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// إعادة محاولة الدفع - Payment Retry Controller
/// Allows students to retry failed payments
/// </summary>
public class PaymentRetryController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IShoppingCartService _cartService;
    private readonly ILogger<PaymentRetryController> _logger;

    public PaymentRetryController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPaymentGatewayFactory gatewayFactory,
        IShoppingCartService cartService,
        ILogger<PaymentRetryController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _gatewayFactory = gatewayFactory;
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// صفحة الدفعات الفاشلة - Failed payments page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var failedPayments = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Book)
            .Where(p => p.StudentId == userId && 
                       (p.Status == PaymentStatus.Failed || p.Status == PaymentStatus.Expired) &&
                       p.IsRetriable)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(failedPayments);
    }

    /// <summary>
    /// تفاصيل الدفعة الفاشلة - Failed payment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Book)
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

        if (payment == null)
            return NotFound();

        // Get available payment methods
        var user = await _context.Users.FindAsync(userId);
        var countryCode = user?.Country ?? "EG";
        var availableGateways = await _gatewayFactory.GetAvailableMethodsAsync(
            countryCode, payment.TotalAmount, payment.Currency);

        ViewBag.AvailableGateways = availableGateways;
        
        return View(payment);
    }

    /// <summary>
    /// إعادة محاولة الدفع - Retry payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id, PaymentGatewayType gateway)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var payment = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Book)
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

        if (payment == null)
            return Json(new { success = false, message = "الدفعة غير موجودة" });

        if (!payment.IsRetriable)
            return Json(new { success = false, message = "لا يمكن إعادة محاولة هذه الدفعة" });

        try
        {
            var gatewayService = _gatewayFactory.GetGateway(gateway);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var productName = payment.Course?.Title ?? payment.Book?.Title ?? "منتج";

            var request = new CreatePaymentIntentRequest
            {
                Amount = payment.TotalAmount,
                Currency = payment.Currency,
                CustomerId = userId,
                CustomerEmail = payment.Student.Email!,
                CustomerName = payment.Student.FullName,
                OrderId = $"RETRY-{payment.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Description = $"إعادة محاولة: {productName}",
                SuccessUrl = $"{baseUrl}/Student/Checkout/Success?paymentId={payment.Id}",
                CancelUrl = $"{baseUrl}/Student/PaymentRetry/Details/{payment.Id}",
                WebhookUrl = $"{baseUrl}/api/webhooks/{gateway.ToString().ToLower()}",
                Metadata = new Dictionary<string, string>
                {
                    { "payment_id", payment.Id.ToString() },
                    { "retry", "true" },
                    { "original_transaction", payment.TransactionId }
                }
            };

            var result = await gatewayService.CreatePaymentIntentAsync(request);

            if (!result.Success)
            {
                _logger.LogError("Payment retry failed for {PaymentId}: {Error}", id, result.ErrorMessage);
                return Json(new { success = false, message = result.ErrorMessage ?? "فشل في إنشاء عملية الدفع" });
            }

            // Update payment record
            payment.RetryCount = (payment.RetryCount ?? 0) + 1;
            payment.LastRetryAt = DateTime.UtcNow;
            payment.Status = PaymentStatus.Processing;
            payment.TransactionId = result.PaymentIntentId ?? payment.TransactionId;
            payment.Provider = (PaymentProvider)(int)gateway;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment retry initiated for {PaymentId}, attempt #{Attempt}",
                id, payment.RetryCount);

            // Return appropriate response based on gateway
            if (!string.IsNullOrEmpty(result.RedirectUrl))
            {
                return Json(new { success = true, redirectUrl = result.RedirectUrl });
            }
            else if (!string.IsNullOrEmpty(result.ClientSecret))
            {
                return Json(new { 
                    success = true, 
                    clientSecret = result.ClientSecret,
                    paymentIntentId = result.PaymentIntentId
                });
            }
            else
            {
                return Json(new { success = true, message = "تم إنشاء عملية الدفع بنجاح" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying payment {PaymentId}", id);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة طلبك" });
        }
    }

    /// <summary>
    /// إلغاء الدفعة الفاشلة - Cancel failed payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

        if (payment == null)
            return NotFound();

        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إلغاء الدفعة بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

