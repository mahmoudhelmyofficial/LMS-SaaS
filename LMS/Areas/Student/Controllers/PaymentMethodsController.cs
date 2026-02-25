using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// إدارة طرق الدفع المحفوظة - Saved Payment Methods Controller
/// </summary>
public class PaymentMethodsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ILogger<PaymentMethodsController> _logger;

    public PaymentMethodsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPaymentGatewayService paymentGateway,
        ILogger<PaymentMethodsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    /// <summary>
    /// قائمة طرق الدفع المحفوظة - Payment methods list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var paymentMethods = await _context.UserPaymentMethods
            .Where(pm => pm.UserId == userId && !pm.IsDeleted)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync();

        return View(paymentMethods);
    }

    /// <summary>
    /// إضافة طريقة دفع جديدة - Add new payment method
    /// </summary>
    [HttpGet]
    public IActionResult Add()
    {
        return View(new AddPaymentMethodViewModel());
    }

    /// <summary>
    /// حفظ طريقة الدفع الجديدة - Save new payment method
    /// NOTE: In production, this should use Stripe.js or similar client-side tokenization
    /// to avoid sending raw card data to our server (PCI DSS compliance)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddPaymentMethodViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = _currentUserService.UserId!;

        try
        {
            // SECURITY: In production, use Stripe.js to create a payment method
            // The card number should NEVER be sent to our server
            // This is a placeholder implementation for demo purposes

            // Validate card number format (basic Luhn check would be here in production)
            if (string.IsNullOrEmpty(model.CardNumber) || model.CardNumber.Length < 13)
            {
                ModelState.AddModelError("CardNumber", "رقم البطاقة غير صحيح");
                return View(model);
            }

            // Clean card number
            var cleanCardNumber = model.CardNumber.Replace(" ", "").Replace("-", "");

            // Extract only the last 4 digits - NEVER store full card number
            var last4 = cleanCardNumber.Length >= 4 ? cleanCardNumber[^4..] : cleanCardNumber;

            // Detect card brand from first 4-6 digits only (BIN range)
            var cardBrand = DetectCardBrandSecure(cleanCardNumber);

            // If this is set as default, unset all other defaults
            if (model.IsDefault)
            {
                var existingDefaults = await _context.UserPaymentMethods
                    .Where(pm => pm.UserId == userId && pm.IsDefault)
                    .ToListAsync();

                foreach (var pm in existingDefaults)
                {
                    pm.IsDefault = false;
                }
            }

            // In a real implementation, we would:
            // 1. Use Stripe.js to tokenize the card on the client
            // 2. Receive only the token/payment method ID from the client
            // 3. Store only the token, never the actual card data
            
            // For demo: Create a mock token (in production, this comes from Stripe)
            var mockGatewayToken = $"pm_{Guid.NewGuid():N}";

            var paymentMethod = new UserPaymentMethod
            {
                UserId = userId,
                PaymentMethodType = model.PaymentMethodType,
                Last4Digits = last4, // Only store last 4 digits
                CardHolderName = model.CardHolderName,
                ExpiryMonth = model.ExpiryMonth,
                ExpiryYear = model.ExpiryYear,
                CardBrand = cardBrand,
                IsDefault = model.IsDefault,
                NickName = model.NickName,
                PaymentGatewayToken = mockGatewayToken,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserPaymentMethods.Add(paymentMethod);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment method added for user {UserId}, Last4: {Last4}", userId, last4);

            SetSuccessMessage("تم إضافة طريقة الدفع بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding payment method for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء إضافة طريقة الدفع");
            return View(model);
        }
    }

    /// <summary>
    /// تعيين كطريقة افتراضية - Set as default
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

        if (paymentMethod == null)
            return NotFound();

        // Unset all other defaults
        var existingDefaults = await _context.UserPaymentMethods
            .Where(pm => pm.UserId == userId && pm.IsDefault && pm.Id != id)
            .ToListAsync();

        foreach (var pm in existingDefaults)
        {
            pm.IsDefault = false;
        }

        paymentMethod.IsDefault = true;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعيين طريقة الدفع كافتراضية");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف طريقة دفع - Delete payment method
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

        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

        if (paymentMethod == null)
            return NotFound();

        paymentMethod.IsDeleted = true;
        paymentMethod.DeletedAt = DateTime.UtcNow;

        // If this was default, set another as default
        if (paymentMethod.IsDefault)
        {
            var nextMethod = await _context.UserPaymentMethods
                .Where(pm => pm.UserId == userId && !pm.IsDeleted && pm.Id != id)
                .OrderByDescending(pm => pm.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextMethod != null)
            {
                nextMethod.IsDefault = true;
            }
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف طريقة الدفع بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تحديث اللقب - Update nickname
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNickname(int id, string nickname)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

        if (paymentMethod == null)
            return NotFound();

        paymentMethod.NickName = nickname;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// التحقق من صلاحية البطاقة - Verify card validity
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var paymentMethod = await _context.UserPaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

        if (paymentMethod == null)
            return NotFound();

        try
        {
            // Verify with payment gateway (Stripe)
            if (!string.IsNullOrEmpty(paymentMethod.PaymentGatewayToken))
            {
                // Create a small charge ($0.01) and immediately refund to verify card
                var verificationResult = await _paymentGateway.VerifyPaymentMethodAsync(
                    paymentMethod.PaymentGatewayToken
                );

                if (verificationResult.Success)
                {
                    paymentMethod.IsVerified = true;
                    paymentMethod.LastVerifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Payment method {PaymentMethodId} verified for user {UserId}", 
                        id, userId);

                    SetSuccessMessage("تم التحقق من البطاقة بنجاح");
                }
                else
                {
                    _logger.LogWarning("Payment method verification failed for {PaymentMethodId}: {Error}", 
                        id, verificationResult.ErrorMessage);
                    
                    SetErrorMessage($"فشل التحقق من البطاقة: {verificationResult.ErrorMessage}");
                }
            }
            else
            {
                SetErrorMessage("معرف البطاقة غير صالح");
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment method {PaymentMethodId}", id);
            SetErrorMessage("حدث خطأ أثناء التحقق من البطاقة");
            return RedirectToAction(nameof(Index));
        }
    }

    #region Private Helpers

    /// <summary>
    /// Detect card brand securely from BIN range (first 4-6 digits)
    /// This method should be used with cleaned card numbers and 
    /// processes only the minimum necessary digits for brand detection.
    /// </summary>
    private static string DetectCardBrandSecure(string cleanedCardNumber)
    {
        if (string.IsNullOrEmpty(cleanedCardNumber) || cleanedCardNumber.Length < 4)
            return "Unknown";

        // Use only first 2 digits for basic brand detection (minimum needed)
        var prefix1 = cleanedCardNumber[0];
        var prefix2 = cleanedCardNumber.Length >= 2 ? cleanedCardNumber[..2] : cleanedCardNumber;

        // Visa: starts with 4
        if (prefix1 == '4')
            return "Visa";

        // Mastercard: starts with 51-55 or 2221-2720
        if (prefix2.Length >= 2)
        {
            var twoDigit = int.Parse(prefix2);
            if (twoDigit >= 51 && twoDigit <= 55)
                return "Mastercard";
            
            if (cleanedCardNumber.Length >= 4)
            {
                var fourDigit = int.Parse(cleanedCardNumber[..4]);
                if (fourDigit >= 2221 && fourDigit <= 2720)
                    return "Mastercard";
            }
        }

        // American Express: starts with 34 or 37
        if (prefix2 == "34" || prefix2 == "37")
            return "Amex";

        // Mada (Saudi debit): starts with specific BINs
        // Common Mada prefixes: 440647, 440795, 446393, 446404, 446672, etc.
        if (cleanedCardNumber.Length >= 6)
        {
            var sixDigit = cleanedCardNumber[..6];
            var madaPrefixes = new[] { "440647", "440795", "446393", "446404", "446672" };
            if (madaPrefixes.Any(p => sixDigit.StartsWith(p)))
                return "Mada";
        }

        // Meeza (Egyptian): starts with 507803 or similar
        if (cleanedCardNumber.StartsWith("507803") || cleanedCardNumber.StartsWith("507808"))
            return "Meeza";

        return "Card";
    }

    #endregion
}

