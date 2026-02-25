using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LMS.Helpers;

/// <summary>
/// مساعد ViewDataDictionary - ViewDataDictionary Helper
/// Provides safe creation and merging of ViewDataDictionary instances to prevent duplicate key exceptions
/// Enterprise-level helper for consistent ViewData handling across the application
/// </summary>
public static class ViewDataDictionaryHelper
{
    /// <summary>
    /// إنشاء ViewDataDictionary جديد بشكل آمن - Create new ViewDataDictionary safely
    /// Copies parent ViewData and allows safe key overwriting without exceptions
    /// </summary>
    /// <param name="parentViewData">ViewData الأب للنسخ منه - Parent ViewData to copy from (required)</param>
    /// <param name="additionalData">بيانات إضافية للإضافة أو التجاوز - Additional key-value pairs to add/override</param>
    /// <returns>ViewDataDictionary جديد مع البيانات المدمجة - New ViewDataDictionary with merged data</returns>
    /// <exception cref="ArgumentNullException">Thrown when parentViewData is null</exception>
    public static ViewDataDictionary CreateSafe(
        ViewDataDictionary parentViewData,
        Dictionary<string, object?>? additionalData = null)
    {
        if (parentViewData == null)
            throw new ArgumentNullException(nameof(parentViewData), "Parent ViewDataDictionary is required. ViewDataDictionary cannot be created without a parent instance.");

        var viewData = new ViewDataDictionary(parentViewData);

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                // Use indexer to allow overwriting existing keys safely
                viewData[kvp.Key] = kvp.Value;
            }
        }

        return viewData;
    }

    /// <summary>
    /// إنشاء ViewDataDictionary مع زوج واحد من المفاتيح - Create ViewDataDictionary with single key-value pair
    /// Most common use case for partial views
    /// </summary>
    /// <param name="parentViewData">ViewData الأب - Parent ViewData (required)</param>
    /// <param name="key">المفتاح - Key</param>
    /// <param name="value">القيمة - Value</param>
    /// <returns>ViewDataDictionary جديد - New ViewDataDictionary</returns>
    /// <exception cref="ArgumentNullException">Thrown when parentViewData is null</exception>
    public static ViewDataDictionary CreateWith(
        ViewDataDictionary parentViewData,
        string key,
        object? value)
    {
        if (parentViewData == null)
            throw new ArgumentNullException(nameof(parentViewData), "Parent ViewDataDictionary is required. ViewDataDictionary cannot be created without a parent instance.");

        var viewData = new ViewDataDictionary(parentViewData);
        viewData[key] = value;
        return viewData;
    }

    /// <summary>
    /// إنشاء ViewDataDictionary مع عدة أزواج من المفاتيح - Create ViewDataDictionary with multiple key-value pairs
    /// Supports fluent API for multiple values
    /// </summary>
    /// <param name="parentViewData">ViewData الأب - Parent ViewData (required)</param>
    /// <param name="pairs">أزواج المفاتيح والقيم - Key-value pairs</param>
    /// <returns>ViewDataDictionary جديد - New ViewDataDictionary</returns>
    /// <exception cref="ArgumentNullException">Thrown when parentViewData is null</exception>
    public static ViewDataDictionary CreateWith(
        ViewDataDictionary parentViewData,
        params (string Key, object? Value)[] pairs)
    {
        if (parentViewData == null)
            throw new ArgumentNullException(nameof(parentViewData), "Parent ViewDataDictionary is required. ViewDataDictionary cannot be created without a parent instance.");

        var viewData = new ViewDataDictionary(parentViewData);

        foreach (var (key, value) in pairs)
        {
            viewData[key] = value;
        }

        return viewData;
    }
}

