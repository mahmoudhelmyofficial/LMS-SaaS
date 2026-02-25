using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات الوسائط - Media Settings Controller
/// Enterprise-level media and file management configuration
/// </summary>
public class MediaSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MediaSettingsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public MediaSettingsController(
        ApplicationDbContext context,
        ILogger<MediaSettingsController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// الصفحة الرئيسية لإعدادات الوسائط - Media settings main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Media" || s.Key.StartsWith("Media") || 
                           s.Key.StartsWith("Image") || s.Key.StartsWith("File"))
                .ToListAsync();

            // Default settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = GetDefaultMediaSettings();
            }

            // Calculate storage statistics
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (Directory.Exists(uploadsPath))
            {
                var directoryInfo = new DirectoryInfo(uploadsPath);
                var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                ViewBag.TotalFiles = files.Length;
                ViewBag.TotalSize = FormatFileSize(files.Sum(f => f.Length));
                ViewBag.ImageCount = files.Count(f => IsImageFile(f.Extension));
                ViewBag.DocumentCount = files.Count(f => IsDocumentFile(f.Extension));
                ViewBag.VideoCount = files.Count(f => IsVideoFile(f.Extension));
            }
            else
            {
                ViewBag.TotalFiles = 0;
                ViewBag.TotalSize = "0 KB";
                ViewBag.ImageCount = 0;
                ViewBag.DocumentCount = 0;
                ViewBag.VideoCount = 0;
            }

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات الوسائط");
            SetWarningMessage("تعذر تحميل إعدادات الوسائط. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات الوسائط - Save media settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MediaSettingsViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                SetErrorMessage("يرجى تصحيح الأخطاء في النموذج");
                return RedirectToAction(nameof(Index));
            }

            var settings = new Dictionary<string, string>
            {
                { "MediaMaxImageSize", model.MaxImageSizeMB.ToString() },
                { "MediaMaxDocumentSize", model.MaxDocumentSizeMB.ToString() },
                { "MediaMaxVideoSize", model.MaxVideoSizeMB.ToString() },
                { "MediaAllowedImageTypes", model.AllowedImageTypes ?? "jpg,jpeg,png,gif,webp" },
                { "MediaAllowedDocumentTypes", model.AllowedDocumentTypes ?? "pdf,doc,docx,xls,xlsx,ppt,pptx" },
                { "MediaImageQuality", model.ImageCompressionQuality.ToString() },
                { "MediaAutoResize", model.AutoResizeImages.ToString() },
                { "MediaMaxImageWidth", model.MaxImageWidth.ToString() },
                { "MediaMaxImageHeight", model.MaxImageHeight.ToString() },
                { "MediaGenerateThumbnails", model.GenerateThumbnails.ToString() },
                { "MediaThumbnailWidth", model.ThumbnailWidth.ToString() },
                { "MediaThumbnailHeight", model.ThumbnailHeight.ToString() },
                { "MediaWatermarkEnabled", model.WatermarkEnabled.ToString() },
                { "MediaWatermarkText", model.WatermarkText ?? "" }
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
                        Category = "Media",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Media settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات الوسائط بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving media settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تنظيف الملفات المؤقتة - Clean temporary files
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CleanTempFiles()
    {
        try
        {
            var tempPath = Path.Combine(_environment.WebRootPath, "uploads", "temp");
            if (Directory.Exists(tempPath))
            {
                var files = Directory.GetFiles(tempPath);
                var deletedCount = 0;
                var cutoffDate = DateTime.UtcNow.AddHours(-24);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < cutoffDate)
                    {
                        System.IO.File.Delete(file);
                        deletedCount++;
                    }
                }

                _logger.LogInformation("Cleaned {Count} temporary files", deletedCount);
                SetSuccessMessage($"تم حذف {deletedCount} ملف مؤقت");
            }
            else
            {
                SetInfoMessage("لا توجد ملفات مؤقتة للحذف");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning temporary files");
            SetErrorMessage("حدث خطأ أثناء تنظيف الملفات المؤقتة");
        }

        return RedirectToAction(nameof(Index));
    }

    #region Private Methods

    private Dictionary<string, string> GetDefaultMediaSettings()
    {
        return new Dictionary<string, string>
        {
            { "MediaMaxImageSize", "5" },
            { "MediaMaxDocumentSize", "25" },
            { "MediaMaxVideoSize", "500" },
            { "MediaAllowedImageTypes", "jpg,jpeg,png,gif,webp" },
            { "MediaAllowedDocumentTypes", "pdf,doc,docx,xls,xlsx,ppt,pptx" },
            { "MediaImageQuality", "85" },
            { "MediaAutoResize", "true" },
            { "MediaMaxImageWidth", "1920" },
            { "MediaMaxImageHeight", "1080" },
            { "MediaGenerateThumbnails", "true" },
            { "MediaThumbnailWidth", "300" },
            { "MediaThumbnailHeight", "200" },
            { "MediaWatermarkEnabled", "false" },
            { "MediaWatermarkText", "" }
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
        return imageExtensions.Contains(extension.ToLower());
    }

    private static bool IsDocumentFile(string extension)
    {
        var docExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt" };
        return docExtensions.Contains(extension.ToLower());
    }

    private static bool IsVideoFile(string extension)
    {
        var videoExtensions = new[] { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".wmv" };
        return videoExtensions.Contains(extension.ToLower());
    }

    #endregion
}

/// <summary>
/// نموذج إعدادات الوسائط
/// </summary>
public class MediaSettingsViewModel
{
    public int MaxImageSizeMB { get; set; } = 5;
    public int MaxDocumentSizeMB { get; set; } = 25;
    public int MaxVideoSizeMB { get; set; } = 500;
    public string? AllowedImageTypes { get; set; } = "jpg,jpeg,png,gif,webp";
    public string? AllowedDocumentTypes { get; set; } = "pdf,doc,docx,xls,xlsx,ppt,pptx";
    public int ImageCompressionQuality { get; set; } = 85;
    public bool AutoResizeImages { get; set; } = true;
    public int MaxImageWidth { get; set; } = 1920;
    public int MaxImageHeight { get; set; } = 1080;
    public bool GenerateThumbnails { get; set; } = true;
    public int ThumbnailWidth { get; set; } = 300;
    public int ThumbnailHeight { get; set; } = 200;
    public bool WatermarkEnabled { get; set; }
    public string? WatermarkText { get; set; }
}

