using Microsoft.AspNetCore.Mvc.Rendering;

namespace LMS.Helpers;

/// <summary>
/// Currency formatting helper for views
/// Provides consistent currency formatting across the application
/// </summary>
public static class CurrencyFormatHelper
{
    /// <summary>
    /// Format amount with dynamic currency symbol
    /// </summary>
    /// <param name="html">HTML helper instance</param>
    /// <param name="amount">Amount to format</param>
    /// <param name="decimals">Number of decimal places (0 for whole numbers, 2 for currency)</param>
    /// <returns>Formatted string with currency symbol</returns>
    public static string FormatCurrency(this IHtmlHelper html, decimal amount, int decimals = 2)
    {
        var symbol = html.ViewContext.ViewBag.DefaultCurrencySymbol as string ?? "ج.م";
        var formatted = decimals == 0 
            ? amount.ToString("N0") 
            : amount.ToString($"N{decimals}");
        
        return $"{formatted} {symbol}";
    }
    
    /// <summary>
    /// Format amount with currency code instead of symbol
    /// </summary>
    /// <param name="html">HTML helper instance</param>
    /// <param name="amount">Amount to format</param>
    /// <param name="decimals">Number of decimal places</param>
    /// <returns>Formatted string with currency code</returns>
    public static string FormatCurrencyWithCode(this IHtmlHelper html, decimal amount, int decimals = 2)
    {
        var code = html.ViewContext.ViewBag.DefaultCurrencyCode as string ?? "EGP";
        var formatted = decimals == 0 
            ? amount.ToString("N0") 
            : amount.ToString($"N{decimals}");
        
        return $"{formatted} {code}";
    }
    
    /// <summary>
    /// Format amount with currency symbol (static method for use outside views)
    /// </summary>
    /// <param name="amount">Amount to format</param>
    /// <param name="symbol">Currency symbol to use</param>
    /// <param name="decimals">Number of decimal places</param>
    /// <returns>Formatted string with currency symbol</returns>
    public static string FormatCurrency(decimal amount, string? symbol = null, int decimals = 2)
    {
        symbol ??= "ج.م";
        var formatted = decimals == 0 
            ? amount.ToString("N0") 
            : amount.ToString($"N{decimals}");
        
        return $"{formatted} {symbol}";
    }
}

