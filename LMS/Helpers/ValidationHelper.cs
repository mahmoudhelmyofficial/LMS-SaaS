using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace LMS.Helpers;

/// <summary>
/// مساعد التحقق من الصحة والتنقية - Validation and Sanitization Helper
/// Provides comprehensive input validation and XSS/SQL injection prevention
/// </summary>
public static class ValidationHelper
{
    #region Input Sanitization

    /// <summary>
    /// تنقية النص من HTML الخطير - Sanitize HTML input (XSS Prevention)
    /// Removes potentially dangerous HTML tags and attributes
    /// </summary>
    public static string SanitizeHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove script tags and content
        input = Regex.Replace(input, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        
        // Remove style tags and content
        input = Regex.Replace(input, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        
        // Remove iframe, object, embed tags
        input = Regex.Replace(input, @"<(iframe|object|embed|frame|frameset|applet)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        input = Regex.Replace(input, @"<(iframe|object|embed|frame|frameset|applet)[^>]*/?>", "", RegexOptions.IgnoreCase);
        
        // Remove event handlers (onclick, onerror, onload, etc.)
        input = Regex.Replace(input, @"\s*on\w+\s*=\s*(['""]?).*?\1", "", RegexOptions.IgnoreCase);
        
        // Remove javascript: and data: URLs
        input = Regex.Replace(input, @"(href|src|action)\s*=\s*(['""]?)\s*(javascript|data|vbscript):", "$1=$2#", RegexOptions.IgnoreCase);
        
        // Remove meta refresh
        input = Regex.Replace(input, @"<meta[^>]*http-equiv\s*=\s*(['""]?)refresh\1[^>]*>", "", RegexOptions.IgnoreCase);
        
        // Remove base tag
        input = Regex.Replace(input, @"<base[^>]*>", "", RegexOptions.IgnoreCase);
        
        // Remove form tags
        input = Regex.Replace(input, @"<form[^>]*>[\s\S]*?</form>", "", RegexOptions.IgnoreCase);
        input = Regex.Replace(input, @"<form[^>]*/?>", "", RegexOptions.IgnoreCase);

        return input.Trim();
    }

    /// <summary>
    /// تنقية النص العادي - Sanitize plain text (remove all HTML)
    /// </summary>
    public static string SanitizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove all HTML tags
        var text = Regex.Replace(input, @"<[^>]+>", "");
        
        // Decode HTML entities
        text = HttpUtility.HtmlDecode(text);
        
        // Remove null characters
        text = text.Replace("\0", "");
        
        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }

    /// <summary>
    /// تنقية لاستعلامات قاعدة البيانات - Sanitize for SQL (prevent SQL injection)
    /// Note: Always use parameterized queries, this is an additional layer
    /// </summary>
    public static string SanitizeForSql(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove or escape dangerous SQL characters
        var sanitized = input
            .Replace("'", "''")
            .Replace("--", "")
            .Replace(";", "")
            .Replace("/*", "")
            .Replace("*/", "")
            .Replace("xp_", "")
            .Replace("EXEC", "")
            .Replace("EXECUTE", "")
            .Replace("DROP", "")
            .Replace("DELETE", "")
            .Replace("INSERT", "")
            .Replace("UPDATE", "");

        return sanitized;
    }

    /// <summary>
    /// تنقية للعرض الآمن - Sanitize for safe display (HTML encode)
    /// </summary>
    public static string SanitizeForDisplay(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return HttpUtility.HtmlEncode(input);
    }

    /// <summary>
    /// تنقية عنوان URL - Sanitize URL
    /// </summary>
    public static string SanitizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        // Remove javascript: and data: protocols
        if (Regex.IsMatch(url, @"^\s*(javascript|data|vbscript):", RegexOptions.IgnoreCase))
            return "#";

        // URL encode special characters
        return Uri.EscapeUriString(url.Trim());
    }

    /// <summary>
    /// تنقية معرف (slug) - Sanitize slug/identifier
    /// </summary>
    public static string SanitizeSlug(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Convert to lowercase
        var slug = input.ToLowerInvariant();
        
        // Replace spaces with hyphens
        slug = Regex.Replace(slug, @"\s+", "-");
        
        // Remove invalid characters (keep alphanumeric, hyphens, underscores)
        slug = Regex.Replace(slug, @"[^a-z0-9\-_]", "");
        
        // Remove multiple consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");
        
        return slug.Trim('-', '_');
    }

    /// <summary>
    /// تنقية رقم الهاتف - Sanitize phone number
    /// </summary>
    public static string SanitizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return string.Empty;

        // Keep only digits, plus sign at the beginning
        var sanitized = new StringBuilder();
        var hasPlus = phoneNumber.TrimStart().StartsWith("+");
        
        if (hasPlus)
            sanitized.Append('+');

        foreach (var c in phoneNumber)
        {
            if (char.IsDigit(c))
                sanitized.Append(c);
        }

        return sanitized.ToString();
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// التحقق من صحة البريد الإلكتروني - Validate email format
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && !email.Contains("..");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// التحقق من صحة رقم الهاتف - Validate phone number
    /// </summary>
    public static bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Remove common formatting characters
        phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // Check if it's all digits and reasonable length
        return phoneNumber.All(char.IsDigit) && phoneNumber.Length >= 10 && phoneNumber.Length <= 15;
    }

    /// <summary>
    /// التحقق من صحة عنوان URL - Validate URL
    /// </summary>
    public static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// التحقق من صحة نطاق التاريخ - Validate date range
    /// </summary>
    public static bool IsValidDateRange(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
            return true;

        return startDate.Value <= endDate.Value;
    }

    /// <summary>
    /// التحقق من صحة نسبة الخصم - Validate discount percentage
    /// </summary>
    public static bool IsValidPercentage(decimal percentage)
    {
        return percentage >= 0 && percentage <= 100;
    }

    /// <summary>
    /// التحقق من صحة المبلغ المالي - Validate money amount
    /// </summary>
    public static bool IsValidAmount(decimal amount)
    {
        return amount >= 0 && amount <= 9999999.99m;
    }

    /// <summary>
    /// تنقية اسم الملف - Sanitize filename
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        // Remove path information
        fileName = Path.GetFileName(fileName);

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        fileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Remove multiple underscores
        while (fileName.Contains("__"))
        {
            fileName = fileName.Replace("__", "_");
        }

        return fileName.Trim('_');
    }

    /// <summary>
    /// التحقق من امتداد الملف - Validate file extension
    /// </summary>
    public static bool IsValidFileExtension(string fileName, string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName) || allowedExtensions == null || !allowedExtensions.Any())
            return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return allowedExtensions.Contains(extension);
    }

    /// <summary>
    /// التحقق من حجم الملف - Validate file size
    /// </summary>
    public static bool IsValidFileSize(long fileSizeBytes, int maxSizeMB)
    {
        var maxSizeBytes = maxSizeMB * 1024 * 1024;
        return fileSizeBytes > 0 && fileSizeBytes <= maxSizeBytes;
    }

    #endregion
}

