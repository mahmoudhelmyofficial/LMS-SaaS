namespace LMS.Helpers;

/// <summary>
/// مساعد الملفات - File helper utilities
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// التحقق من صحة امتداد الصورة - Validate image extension
    /// </summary>
    public static bool IsValidImageExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return Common.Constants.Files.AllowedImageExtensions.Contains(extension);
    }

    /// <summary>
    /// التحقق من صحة امتداد الفيديو - Validate video extension
    /// </summary>
    public static bool IsValidVideoExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return Common.Constants.Files.AllowedVideoExtensions.Contains(extension);
    }

    /// <summary>
    /// التحقق من صحة امتداد المستند - Validate document extension
    /// </summary>
    public static bool IsValidDocumentExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return Common.Constants.Files.AllowedDocumentExtensions.Contains(extension);
    }

    /// <summary>
    /// إنشاء اسم فريد للملف - Generate unique file name
    /// </summary>
    public static string GenerateUniqueFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        return uniqueName;
    }

    /// <summary>
    /// الحصول على نوع المحتوى - Get content type from extension
    /// </summary>
    public static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// تنسيق حجم الملف - Format file size for display
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "بايت", "ك.ب", "م.ب", "ج.ب", "ت.ب" };
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// التحقق من حجم الملف - Validate file size
    /// </summary>
    public static bool IsValidFileSize(long bytes, int maxSizeMB)
    {
        return bytes <= maxSizeMB * 1024 * 1024;
    }

    /// <summary>
    /// الحصول على مجلد التحميل - Get upload folder path
    /// </summary>
    public static string GetUploadPath(string baseDirectory, string folder)
    {
        var path = Path.Combine(baseDirectory, folder);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
}

