using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// وحدة التحكم الأساسية للمدرس - Base controller for Instructor area
/// All instructor controllers should inherit from this
/// </summary>
[Area("Instructor")]
[Authorize(Roles = "Instructor,Admin")]
public abstract class InstructorBaseController : Controller
{
    // Cache configuration for currency
    private const string DefaultCurrencyCacheKey = "instructor_default_currency";
    private static readonly TimeSpan CurrencyCacheExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// رسالة نجاح - Success message for TempData
    /// </summary>
    protected void SetSuccessMessage(string message)
    {
        TempData["SuccessMessage"] = message;
    }

    /// <summary>
    /// رسالة خطأ - Error message for TempData
    /// </summary>
    protected void SetErrorMessage(string message)
    {
        TempData["ErrorMessage"] = message;
    }

    /// <summary>
    /// رسالة تحذير - Warning message for TempData
    /// </summary>
    protected void SetWarningMessage(string message)
    {
        TempData["WarningMessage"] = message;
    }

    /// <summary>
    /// رسالة معلومات - Info message for TempData
    /// </summary>
    protected void SetInfoMessage(string message)
    {
        TempData["InfoMessage"] = message;
    }

    /// <summary>
    /// Helper method to set default currency with caching and fallback support
    /// Enhanced with enterprise-level error handling and performance optimization
    /// </summary>
    protected async Task SetDefaultCurrencyAsync(
        ApplicationDbContext context, 
        ICurrencyService currencyService,
        IMemoryCache? cache = null,
        ILogger? logger = null)
    {
        // Try to get from cache first
        if (cache != null && cache.TryGetValue(DefaultCurrencyCacheKey, out CurrencyInfo? cachedCurrency))
        {
            ViewBag.DefaultCurrencySymbol = cachedCurrency!.SymbolAr ?? cachedCurrency.Symbol;
            ViewBag.DefaultCurrencyCode = cachedCurrency.Code;
            ViewBag.DefaultCurrency = cachedCurrency;
            return;
        }

        try
        {
            // Try to get from Currencies table (primary source)
            var defaultCurrency = await context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsBaseCurrency && c.IsActive);
            
            CurrencyInfo currencyInfo;

            if (defaultCurrency != null)
            {
                currencyInfo = new CurrencyInfo
                {
                    Code = defaultCurrency.Code,
                    Symbol = defaultCurrency.Symbol,
                    SymbolAr = defaultCurrency.SymbolAr ?? defaultCurrency.Symbol,
                    Name = defaultCurrency.Name,
                    NameAr = defaultCurrency.NameAr ?? defaultCurrency.Name,
                    DecimalPlaces = defaultCurrency.DecimalPlaces,
                    IsActive = defaultCurrency.IsActive
                };
            }
            else
            {
                // Fallback to PlatformSettings
                var platformCurrency = await context.PlatformSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == "default_currency");
                
                var currencyCode = platformCurrency?.Value?.Trim().ToUpperInvariant() ?? "EGP";
                
                // Validate currency code
                if (!currencyService.IsCurrencySupported(currencyCode))
                {
                    logger?.LogWarning("Unsupported currency code '{CurrencyCode}' in platform settings, falling back to EGP", currencyCode);
                    currencyCode = "EGP";
                }

                currencyInfo = new CurrencyInfo
                {
                    Code = currencyCode,
                    Symbol = currencyService.GetCurrencySymbol(currencyCode),
                    SymbolAr = currencyService.GetCurrencySymbol(currencyCode),
                    Name = currencyService.GetCurrencyName(currencyCode),
                    NameAr = currencyService.GetCurrencyName(currencyCode, arabic: true),
                    DecimalPlaces = 2,
                    IsActive = true
                };
            }

            // Set ViewBag properties
            ViewBag.DefaultCurrencySymbol = currencyInfo.SymbolAr ?? currencyInfo.Symbol;
            ViewBag.DefaultCurrencyCode = currencyInfo.Code;
            ViewBag.DefaultCurrency = currencyInfo;

            // Cache the result for performance
            if (cache != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CurrencyCacheExpiration,
                    Priority = CacheItemPriority.Normal
                };
                cache.Set(DefaultCurrencyCacheKey, currencyInfo, cacheOptions);
            }
        }
        catch (Exception ex)
        {
            // Log error and use safe fallback
            logger?.LogError(ex, "Error retrieving default currency, using EGP fallback");
            
            var fallbackCurrency = new CurrencyInfo
            {
                Code = "EGP",
                Symbol = currencyService.GetCurrencySymbol("EGP"),
                SymbolAr = currencyService.GetCurrencySymbol("EGP"),
                Name = currencyService.GetCurrencyName("EGP"),
                NameAr = currencyService.GetCurrencyName("EGP", arabic: true),
                DecimalPlaces = 2,
                IsActive = true
            };

            ViewBag.DefaultCurrencySymbol = fallbackCurrency.SymbolAr ?? fallbackCurrency.Symbol;
            ViewBag.DefaultCurrencyCode = fallbackCurrency.Code;
            ViewBag.DefaultCurrency = fallbackCurrency;
        }
    }
}

