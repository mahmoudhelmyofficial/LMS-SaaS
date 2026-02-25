using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة العملات - Currency Management Controller
/// Enterprise-level currency configuration for multi-regional LMS
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class CurrenciesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<CurrenciesController> _logger;

    public CurrenciesController(
        ApplicationDbContext context,
        ICurrencyService currencyService,
        ILogger<CurrenciesController> logger)
    {
        _context = context;
        _currencyService = currencyService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة العملات - Currency list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var currencies = await _context.Currencies
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Code)
            .ToListAsync();

        // Get exchange rates for display
        var exchangeRates = await _currencyService.GetExchangeRatesAsync("EGP");
        ViewBag.ExchangeRates = exchangeRates;

        return View(currencies);
    }

    /// <summary>
    /// تفاصيل العملة - Currency details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return NotFound();

        return View(currency);
    }

    /// <summary>
    /// إنشاء عملة جديدة - Create currency (GET)
    /// </summary>
    public IActionResult Create()
    {
        return View(new CurrencyCreateViewModel());
    }

    /// <summary>
    /// إنشاء عملة جديدة - Create currency (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CurrencyCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Check for duplicate code
        var exists = await _context.Currencies.AnyAsync(c => c.Code == model.Code.ToUpperInvariant());
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Code), "رمز العملة موجود بالفعل");
            return View(model);
        }

        var currency = new Currency
        {
            Code = model.Code.ToUpperInvariant(),
            Name = model.Name,
            NameAr = model.NameAr,
            Symbol = model.Symbol,
            SymbolAr = model.SymbolAr,
            DecimalPlaces = model.DecimalPlaces,
            CountryCode = model.CountryCode?.ToUpperInvariant(),
            Country = model.Country,
            ExchangeRate = model.ExchangeRate,
            ExchangeRateUpdatedAt = DateTime.UtcNow,
            IsBaseCurrency = model.IsBaseCurrency,
            IsActive = model.IsActive,
            IsPaymentSupported = model.IsPaymentSupported,
            DisplayOrder = model.DisplayOrder,
            NumberFormat = model.NumberFormat,
            SymbolAfterAmount = model.SymbolAfterAmount,
            MinimumAmount = model.MinimumAmount,
            MaximumAmount = model.MaximumAmount,
            CreatedAt = DateTime.UtcNow
        };

        // If setting as base currency, unset others
        if (model.IsBaseCurrency)
        {
            await UnsetOtherBaseCurrencies();
        }

        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created currency {Code} by admin", model.Code);
        TempData["SuccessMessage"] = string.Format(CultureExtensions.T("تم إنشاء العملة {0} بنجاح", "Currency {0} created successfully."), model.Name);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تعديل العملة - Edit currency (GET)
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return NotFound();

        var model = new CurrencyEditViewModel
        {
            Id = currency.Id,
            Code = currency.Code,
            Name = currency.Name,
            NameAr = currency.NameAr,
            Symbol = currency.Symbol,
            SymbolAr = currency.SymbolAr,
            DecimalPlaces = currency.DecimalPlaces,
            CountryCode = currency.CountryCode,
            Country = currency.Country,
            ExchangeRate = currency.ExchangeRate,
            IsBaseCurrency = currency.IsBaseCurrency,
            IsActive = currency.IsActive,
            IsPaymentSupported = currency.IsPaymentSupported,
            DisplayOrder = currency.DisplayOrder,
            NumberFormat = currency.NumberFormat,
            SymbolAfterAmount = currency.SymbolAfterAmount,
            MinimumAmount = currency.MinimumAmount,
            MaximumAmount = currency.MaximumAmount
        };

        return View(model);
    }

    /// <summary>
    /// تعديل العملة - Edit currency (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CurrencyEditViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return NotFound();

        // Check for duplicate code (excluding current)
        var exists = await _context.Currencies
            .AnyAsync(c => c.Code == model.Code.ToUpperInvariant() && c.Id != id);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Code), "رمز العملة موجود بالفعل");
            return View(model);
        }

        // Track if exchange rate changed
        var rateChanged = currency.ExchangeRate != model.ExchangeRate;

        currency.Code = model.Code.ToUpperInvariant();
        currency.Name = model.Name;
        currency.NameAr = model.NameAr;
        currency.Symbol = model.Symbol;
        currency.SymbolAr = model.SymbolAr;
        currency.DecimalPlaces = model.DecimalPlaces;
        currency.CountryCode = model.CountryCode?.ToUpperInvariant();
        currency.Country = model.Country;
        currency.ExchangeRate = model.ExchangeRate;
        currency.IsBaseCurrency = model.IsBaseCurrency;
        currency.IsActive = model.IsActive;
        currency.IsPaymentSupported = model.IsPaymentSupported;
        currency.DisplayOrder = model.DisplayOrder;
        currency.NumberFormat = model.NumberFormat;
        currency.SymbolAfterAmount = model.SymbolAfterAmount;
        currency.MinimumAmount = model.MinimumAmount;
        currency.MaximumAmount = model.MaximumAmount;
        currency.UpdatedAt = DateTime.UtcNow;

        if (rateChanged)
        {
            currency.ExchangeRateUpdatedAt = DateTime.UtcNow;
        }

        // If setting as base currency, unset others
        if (model.IsBaseCurrency)
        {
            await UnsetOtherBaseCurrencies(id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated currency {Code} by admin", model.Code);
        TempData["SuccessMessage"] = string.Format(CultureExtensions.T("تم تحديث العملة {0} بنجاح", "Currency {0} updated successfully."), model.Name);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف العملة - Delete currency
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return NotFound();

        if (currency.IsBaseCurrency)
        {
            TempData["ErrorMessage"] = CultureExtensions.T("لا يمكن حذف العملة الأساسية", "Cannot delete the base currency.");
            return RedirectToAction(nameof(Index));
        }

        // Check if currency is used in any payments
        var hasPayments = await _context.Payments.AnyAsync(p => p.Currency == currency.Code);
        if (hasPayments)
        {
            TempData["ErrorMessage"] = CultureExtensions.T("لا يمكن حذف العملة لأنها مستخدمة في عمليات دفع", "Cannot delete the currency because it is used in payment transactions.");
            return RedirectToAction(nameof(Index));
        }

        _context.Currencies.Remove(currency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted currency {Code} by admin", currency.Code);
        TempData["SuccessMessage"] = string.Format(CultureExtensions.T("تم حذف العملة {0} بنجاح", "Currency {0} deleted successfully."), currency.Name);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تحديث أسعار الصرف - Update exchange rates
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExchangeRates()
    {
        try
        {
            await _currencyService.UpdateExchangeRatesAsync();
            TempData["SuccessMessage"] = CultureExtensions.T("تم تحديث أسعار الصرف بنجاح", "Exchange rates updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update exchange rates");
            TempData["ErrorMessage"] = CultureExtensions.T("حدث خطأ أثناء تحديث أسعار الصرف", "An error occurred while updating exchange rates.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تعيين العملة الأساسية - Set base currency
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBaseCurrency(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return NotFound();

        await UnsetOtherBaseCurrencies();
        
        currency.IsBaseCurrency = true;
        currency.ExchangeRate = 1m;
        currency.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Set {Code} as base currency", currency.Code);
        TempData["SuccessMessage"] = string.Format(CultureExtensions.T("تم تعيين {0} كعملة أساسية", "{0} set as base currency."), currency.Name);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return Json(new { success = false, message = "العملة غير موجودة" });

        if (currency.IsBaseCurrency && currency.IsActive)
        {
            return Json(new { success = false, message = "لا يمكن تعطيل العملة الأساسية" });
        }

        currency.IsActive = !currency.IsActive;
        currency.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, isActive = currency.IsActive });
    }

    /// <summary>
    /// تحديث سعر الصرف لعملة محددة - Update single currency exchange rate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRate(int id, decimal rate)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if (currency == null)
            return Json(new { success = false, message = "العملة غير موجودة" });

        if (rate <= 0)
            return Json(new { success = false, message = "سعر الصرف يجب أن يكون أكبر من صفر" });

        currency.ExchangeRate = rate;
        currency.ExchangeRateUpdatedAt = DateTime.UtcNow;
        currency.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated exchange rate for {Code} to {Rate}", currency.Code, rate);

        return Json(new { success = true, rate = rate });
    }

    #region Private Methods

    private async Task UnsetOtherBaseCurrencies(int? excludeId = null)
    {
        var baseCurrencies = await _context.Currencies
            .Where(c => c.IsBaseCurrency && (excludeId == null || c.Id != excludeId))
            .ToListAsync();

        foreach (var currency in baseCurrencies)
        {
            currency.IsBaseCurrency = false;
            currency.UpdatedAt = DateTime.UtcNow;
        }
    }

    #endregion
}

#region ViewModels

public class CurrencyCreateViewModel
{
    [Required(ErrorMessage = "رمز العملة مطلوب")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "رمز العملة يجب أن يكون 3 أحرف")]
    [Display(Name = "رمز العملة (ISO 4217)")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم العملة مطلوب")]
    [StringLength(100)]
    [Display(Name = "اسم العملة (إنجليزي)")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "اسم العملة (عربي)")]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز العملة المختصر مطلوب")]
    [StringLength(10)]
    [Display(Name = "الرمز (مثل $)")]
    public string Symbol { get; set; } = string.Empty;

    [StringLength(10)]
    [Display(Name = "الرمز بالعربية")]
    public string? SymbolAr { get; set; }

    [Range(0, 4)]
    [Display(Name = "الخانات العشرية")]
    public int DecimalPlaces { get; set; } = 2;

    [StringLength(2)]
    [Display(Name = "رمز الدولة")]
    public string? CountryCode { get; set; }

    [StringLength(100)]
    [Display(Name = "اسم الدولة")]
    public string? Country { get; set; }

    [Required]
    [Range(0.0001, 999999)]
    [Display(Name = "سعر الصرف (مقابل العملة الأساسية)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Display(Name = "العملة الأساسية")]
    public bool IsBaseCurrency { get; set; } = false;

    [Display(Name = "مفعّلة")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "مدعومة للدفع")]
    public bool IsPaymentSupported { get; set; } = true;

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    [StringLength(50)]
    [Display(Name = "تنسيق الأرقام")]
    public string? NumberFormat { get; set; }

    [Display(Name = "الرمز بعد المبلغ")]
    public bool SymbolAfterAmount { get; set; } = false;

    [Display(Name = "الحد الأدنى للمعاملة")]
    public decimal? MinimumAmount { get; set; }

    [Display(Name = "الحد الأقصى للمعاملة")]
    public decimal? MaximumAmount { get; set; }
}

public class CurrencyEditViewModel : CurrencyCreateViewModel
{
    public int Id { get; set; }
}

#endregion

