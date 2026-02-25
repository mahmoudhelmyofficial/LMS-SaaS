using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات الدفع - Payment Settings Controller
/// Enterprise-level payment configuration management
/// </summary>
public class PaymentSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentSettingsController> _logger;
    private readonly IPlatformSettingsService _platformSettings;

    public PaymentSettingsController(
        ApplicationDbContext context,
        ILogger<PaymentSettingsController> logger,
        IPlatformSettingsService platformSettings)
    {
        _context = context;
        _logger = logger;
        _platformSettings = platformSettings;
    }

    /// <summary>
    /// الصفحة الرئيسية لإعدادات الدفع - Payment settings main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await LoadPaymentSettingsIndexDataAsync();
            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات الدفع");
            SetWarningMessage("تعذر تحميل إعدادات الدفع. يرجى المحاولة مرة أخرى.");
            ViewBag.TotalPayments = 0;
            ViewBag.TotalRevenue = 0m;
            ViewBag.MonthlyRevenue = 0m;
            ViewBag.PendingPayments = 0;
            ViewBag.EnabledGateways = new List<string>();
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات الدفع العامة - Save general payment settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PaymentSettingsViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                SetErrorMessage("يرجى تصحيح الأخطاء في النموذج");
                var indexData = await LoadPaymentSettingsIndexDataAsync();
                ViewBag.FailedPaymentModel = model;
                return View("Index", indexData);
            }

            var settings = new Dictionary<string, string>
            {
                { "PaymentCurrency", model.DefaultCurrency ?? "EGP" },
                { "PaymentMinAmount", model.MinimumAmount.ToString() },
                { "PaymentMaxAmount", model.MaximumAmount.ToString() },
                { "PaymentEnabled", model.IsEnabled.ToString() },
                { "PaymentAllowInstallments", model.AllowInstallments.ToString() },
                { "PaymentInstallmentMonths", model.InstallmentMonths.ToString() },
                { "PaymentAutoCapture", model.AutoCapture.ToString() },
                { "PaymentRefundPolicy", model.RefundPolicyDays.ToString() },
                { "PaymentRequireVerification", model.RequireVerification.ToString() }
            };

            foreach (var setting in settings)
            {
                var existing = await _context.PlatformSettings
                    .FirstOrDefaultAsync(s => s.Key == setting.Key);

                if (existing != null)
                {
                    existing.Value = setting.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        Category = "Payment",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            _platformSettings.ClearCache();

            _logger.LogInformation("Payment settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات الدفع بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving payment settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            var indexData = await LoadPaymentSettingsIndexDataAsync();
            return View("Index", indexData);
        }
    }

    /// <summary>
    /// إعدادات العملات - Currency settings
    /// </summary>
    public async Task<IActionResult> Currencies()
    {
        try
        {
            var currencies = await _context.Currencies
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return View(currencies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading currencies");
            SetWarningMessage("تعذر تحميل العملات");
            return View(new List<Domain.Entities.Settings.Currency>());
        }
    }

    /// <summary>
    /// تبديل حالة العملة - Toggle currency status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCurrency(int id)
    {
        try
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null)
            {
                return NotFound();
            }

            currency.IsActive = !currency.IsActive;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Currency {CurrencyCode} status changed to {Status}", 
                currency.Code, currency.IsActive);
            SetSuccessMessage($"تم {(currency.IsActive ? "تفعيل" : "تعطيل")} العملة {currency.Code}");

            return RedirectToAction(nameof(Currencies));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling currency status");
            SetErrorMessage("حدث خطأ أثناء تحديث العملة");
            return RedirectToAction(nameof(Currencies));
        }
    }

    /// <summary>
    /// سياسة الاسترداد - Refund policy (redirects to Index where refund days are edited)
    /// </summary>
    public IActionResult RefundPolicy()
    {
        return RedirectToAction(nameof(Index));
    }

    #region Private Methods

    private async Task<List<PlatformSetting>> LoadPaymentSettingsIndexDataAsync()
    {
        var settings = await _context.PlatformSettings
            .Where(s => s.Group == "Payment" || s.Key.StartsWith("Payment"))
            .ToListAsync();
        if (!settings.Any())
            ViewBag.DefaultSettings = GetDefaultPaymentSettings();
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 1);
        ViewBag.TotalPayments = await _context.Payments.CountAsync();
        ViewBag.TotalRevenue = await _context.Payments
            .Where(p => p.Status == Domain.Enums.PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
        ViewBag.MonthlyRevenue = await _context.Payments
            .Where(p => p.Status == Domain.Enums.PaymentStatus.Completed && p.CreatedAt >= thisMonth)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
        ViewBag.PendingPayments = await _context.Payments
            .CountAsync(p => p.Status == Domain.Enums.PaymentStatus.Pending);
        ViewBag.EnabledGateways = await _context.PaymentGatewaySettings
            .Where(g => g.IsActive)
            .Select(g => g.Name)
            .ToListAsync();
        return settings;
    }

    private Dictionary<string, string> GetDefaultPaymentSettings()
    {
        return new Dictionary<string, string>
        {
            { "PaymentCurrency", "EGP" },
            { "PaymentMinAmount", "10" },
            { "PaymentMaxAmount", "100000" },
            { "PaymentEnabled", "true" },
            { "PaymentAllowInstallments", "true" },
            { "PaymentInstallmentMonths", "3,6,12" },
            { "PaymentAutoCapture", "true" },
            { "PaymentRefundPolicy", "14" },
            { "PaymentRequireVerification", "false" }
        };
    }

    #endregion
}

/// <summary>
/// نموذج إعدادات الدفع
/// </summary>
public class PaymentSettingsViewModel
{
    public string? DefaultCurrency { get; set; } = "EGP";
    public decimal MinimumAmount { get; set; } = 10;
    public decimal MaximumAmount { get; set; } = 100000;
    public bool IsEnabled { get; set; } = true;
    public bool AllowInstallments { get; set; } = true;
    public string InstallmentMonths { get; set; } = "3,6,12";
    public bool AutoCapture { get; set; } = true;
    public int RefundPolicyDays { get; set; } = 14;
    public bool RequireVerification { get; set; }
}

