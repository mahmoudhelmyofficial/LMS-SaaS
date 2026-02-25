using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الاشتراكات - Subscriptions Controller
/// </summary>
public class SubscriptionsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<SubscriptionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// عرض الاشتراكات - View Subscriptions
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscriptions = await _context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CurrentPeriodEnd)
            .ToListAsync();

        return View(subscriptions);
    }

    /// <summary>
    /// تفاصيل الاشتراك - Subscription Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        return View(subscription);
    }

    /// <summary>
    /// الخطط المتاحة - Available Plans
    /// </summary>
    public async Task<IActionResult> Plans()
    {
        var plans = await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return View(plans);
    }

    /// <summary>
    /// الاشتراك في خطة - Subscribe to Plan
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Subscribe(int planId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action("Subscribe", new { planId }) });
        }

        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive)
            return NotFound();

        // Check if user already has an active subscription
        var activeSubscription = await _context.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == "Active");

        if (activeSubscription)
        {
            SetErrorMessage("لديك اشتراك نشط بالفعل");
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new SubscriptionCheckoutViewModel
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Price = plan.Price,
            Currency = "EGP",
            BillingCycle = plan.BillingCycle,
            Features = plan.Features
        };

        return View(viewModel);
    }

    /// <summary>
    /// إلغاء الاشتراك - Cancel Subscription
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        if (subscription.Status != "Active" && subscription.Status != "Trialing")
        {
            SetErrorMessage("لا يمكن إلغاء هذا الاشتراك");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Verify Plan is loaded
        if (subscription.Plan == null)
        {
            _logger.LogError("Subscription {SubscriptionId} has missing Plan navigation property", id);
            SetErrorMessage("حدث خطأ في بيانات الاشتراك");
            return RedirectToAction(nameof(Index));
        }
        
        var viewModel = new SubscriptionCancelViewModel
        {
            Id = subscription.Id,
            PlanName = subscription.Plan.Name,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd
        };

        return View(viewModel);
    }

    /// <summary>
    /// تأكيد إلغاء الاشتراك - Confirm Cancel Subscription
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirm(int id, SubscriptionCancelViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            subscription.CancelAtPeriodEnd = model.CancelAtPeriodEnd;
            
            if (!model.CancelAtPeriodEnd)
            {
                // Cancel immediately
                subscription.Status = "Cancelled";
                subscription.CancelledAt = DateTime.UtcNow;
            }
            
            subscription.CancellationReason = model.CancellationReason;

            await _context.SaveChangesAsync();

            SetSuccessMessage(model.CancelAtPeriodEnd 
                ? "سيتم إلغاء اشتراكك في نهاية الفترة الحالية" 
                : "تم إلغاء اشتراكك بنجاح");
            
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Cancel", model);
    }

    /// <summary>
    /// استئناف الاشتراك - Resume Subscription
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        if (!subscription.CancelAtPeriodEnd)
        {
            SetErrorMessage("الاشتراك غير مجدول للإلغاء");
            return RedirectToAction(nameof(Details), new { id });
        }

        subscription.CancelAtPeriodEnd = false;
        subscription.CancellationReason = null;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم استئناف اشتراكك بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// عرض صفحة تحديث طريقة الدفع - Show Update Payment Method Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UpdatePaymentMethod(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        // Get existing payment methods for the user
        var paymentMethods = await _context.UserPaymentMethods
            .Where(pm => pm.UserId == userId && pm.IsActive)
            .OrderByDescending(pm => pm.IsDefault)
            .ToListAsync();

        ViewBag.PaymentMethods = paymentMethods;
        ViewBag.SubscriptionId = id;
        ViewBag.CurrentPaymentMethodId = subscription.PaymentMethodId;

        return View(subscription);
    }

    /// <summary>
    /// تحديث طريقة الدفع - Update Payment Method (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentMethod(int id, int paymentMethodId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return NotFound();

        // Verify the payment method belongs to the user
        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == paymentMethodId && pm.UserId == userId && pm.IsActive);

        if (paymentMethod == null)
        {
            SetErrorMessage("طريقة الدفع غير صالحة");
            return RedirectToAction(nameof(UpdatePaymentMethod), new { id });
        }

        // Update the subscription's payment method
        subscription.PaymentMethodId = paymentMethodId;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated payment method for subscription {SubscriptionId} to {PaymentMethodId}",
            userId, id, paymentMethodId);

        SetSuccessMessage("تم تحديث طريقة الدفع بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// معالجة الاشتراك - Process subscription payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessSubscription(SubscriptionCheckoutViewModel model, string PaymentMethodId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        try
        {
            // Verify plan exists and is active
            var plan = await _context.SubscriptionPlans.FindAsync(model.PlanId);
            if (plan == null || !plan.IsActive)
            {
                return Json(new { success = false, message = "الخطة غير متاحة" });
            }

            // Check if user already has an active subscription
            var existingSubscription = await _context.Subscriptions
                .AnyAsync(s => s.UserId == userId && s.Status == "Active");

            if (existingSubscription)
            {
                return Json(new { success = false, message = "لديك اشتراك نشط بالفعل" });
            }

            // Calculate billing dates based on plan
            var startDate = DateTime.UtcNow;
            var periodEnd = plan.BillingCycle?.ToLower() switch
            {
                "yearly" or "annual" => startDate.AddYears(1),
                "quarterly" => startDate.AddMonths(3),
                _ => startDate.AddMonths(1) // Monthly by default
            };

            // Create payment record for the subscription
            var payment = new LMS.Domain.Entities.Payments.Payment
            {
                TransactionId = $"SUB_{model.PlanId}_{userId}_{DateTime.UtcNow.Ticks}",
                StudentId = userId!,
                ProductType = LMS.Domain.Enums.ProductType.Subscription,
                PurchaseType = "Subscription",
                SubscriptionPlanId = plan.Id,
                OriginalAmount = plan.Price,
                TotalAmount = plan.Price,
                Currency = plan.Currency ?? "EGP",
                Status = LMS.Domain.Enums.PaymentStatus.Completed,
                Provider = LMS.Domain.Enums.PaymentProvider.Paymob,
                PaymentMethod = PaymentMethodId ?? "card",
                CompletedAt = DateTime.UtcNow,
                Metadata = $"{{\"type\":\"subscription\",\"planId\":{plan.Id},\"billingCycle\":\"{plan.BillingCycle}\"}}"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Create subscription record linked to payment
            var subscription = new LMS.Domain.Entities.Payments.Subscription
            {
                UserId = userId!,
                PlanId = plan.Id,
                Status = "Active",
                StartDate = startDate,
                CurrentPeriodStart = startDate,
                CurrentPeriodEnd = periodEnd,
                NextBillingDate = periodEnd,
                IsAutoRenew = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Create invoice for the subscription
            var invoice = new LMS.Domain.Entities.Payments.Invoice
            {
                InvoiceNumber = $"INV-SUB-{DateTime.UtcNow:yyyyMMdd}-{payment.Id:D6}",
                PaymentId = payment.Id,
                StudentId = userId!,
                IssuedDate = DateTime.UtcNow,
                DueDate = periodEnd,
                Status = "Paid",
                SubTotal = plan.Price,
                TotalAmount = plan.Price,
                Currency = plan.Currency ?? "EGP",
                ItemDescription = $"اشتراك: {plan.Name} ({plan.BillingCycle ?? "شهري"})"
            };
            _context.Invoices.Add(invoice);

            // Send confirmation notification
            _context.Notifications.Add(new LMS.Domain.Entities.Notifications.Notification
            {
                UserId = userId!,
                Title = "مرحباً بك في اشتراكك الجديد! 🎉",
                Message = $"تم تفعيل اشتراكك في خطة \"{plan.Name}\" بنجاح. استمتع بجميع المزايا!",
                Type = LMS.Domain.Enums.NotificationType.Subscription,
                ActionUrl = $"/Student/Subscriptions/Details/{subscription.Id}",
                ActionText = "عرض الاشتراك",
                IsRead = false
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("New subscription {SubscriptionId} with payment {PaymentId} created for user {UserId} on plan {PlanId}",
                subscription.Id, payment.Id, userId, plan.Id);

            return Json(new { success = true, subscriptionId = subscription.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing subscription for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة الاشتراك" });
        }
    }
}

