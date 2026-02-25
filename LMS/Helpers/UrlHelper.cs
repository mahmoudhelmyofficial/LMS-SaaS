namespace LMS.Helpers;

/// <summary>
/// مساعد الروابط - URL helper utilities
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// الحصول على رابط الصورة الرمزية من Gravatar - Get Gravatar URL
    /// </summary>
    public static string GetGravatarUrl(string email, int size = 100)
    {
        if (string.IsNullOrWhiteSpace(email))
            return GetDefaultAvatarUrl(size);

        var emailHash = email.Trim().ToLowerInvariant().ToMd5Hash();
        return $"https://www.gravatar.com/avatar/{emailHash}?s={size}&d=mp";
    }

    /// <summary>
    /// الحصول على رابط الصورة الافتراضية - Get default avatar URL
    /// </summary>
    public static string GetDefaultAvatarUrl(int size = 100)
    {
        return $"https://ui-avatars.com/api/?size={size}&background=random";
    }

    /// <summary>
    /// الحصول على رابط الصورة من الأحرف الأولى - Get avatar from initials
    /// </summary>
    public static string GetInitialsAvatarUrl(string name, int size = 100)
    {
        var encodedName = Uri.EscapeDataString(name ?? "?");
        return $"https://ui-avatars.com/api/?name={encodedName}&size={size}&background=random&color=fff";
    }

    /// <summary>
    /// استخراج معرف YouTube من الرابط - Extract YouTube video ID
    /// </summary>
    public static string? ExtractYouTubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Handle various YouTube URL formats
        var patterns = new[]
        {
            @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// الحصول على صورة مصغرة YouTube - Get YouTube thumbnail
    /// </summary>
    public static string GetYouTubeThumbnail(string videoId, string quality = "maxresdefault")
    {
        return $"https://img.youtube.com/vi/{videoId}/{quality}.jpg";
    }

    /// <summary>
    /// استخراج معرف Vimeo - Extract Vimeo video ID
    /// </summary>
    public static string? ExtractVimeoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(url, @"vimeo\.com\/(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// تحويل الرابط لرابط مضمّن - Convert to embed URL
    /// </summary>
    public static string? GetEmbedUrl(string url)
    {
        var youtubeId = ExtractYouTubeId(url);
        if (!string.IsNullOrEmpty(youtubeId))
            return $"https://www.youtube.com/embed/{youtubeId}";

        var vimeoId = ExtractVimeoId(url);
        if (!string.IsNullOrEmpty(vimeoId))
            return $"https://player.vimeo.com/video/{vimeoId}";

        return null;
    }

    /// <summary>
    /// إنشاء هاش MD5 - Generate MD5 hash
    /// </summary>
    private static string ToMd5Hash(this string value)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

