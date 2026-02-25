using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات التخزين - Storage Settings Controller
/// Enterprise-level cloud storage and file storage configuration
/// </summary>
public class StorageSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StorageSettingsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public StorageSettingsController(
        ApplicationDbContext context,
        ILogger<StorageSettingsController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// الصفحة الرئيسية لإعدادات التخزين - Storage settings main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Storage" || s.Key.StartsWith("Storage") || 
                           s.Key.StartsWith("Cloud") || s.Key.StartsWith("S3") ||
                           s.Key.StartsWith("Azure"))
                .ToListAsync();

            // Default settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = GetDefaultStorageSettings();
            }

            // Calculate storage usage
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            var contentRoot = _environment.ContentRootPath;

            ViewBag.LocalStorageUsed = CalculateDirectorySize(uploadsPath);
            ViewBag.TotalDiskSpace = GetTotalDiskSpace(contentRoot);
            ViewBag.FreeDiskSpace = GetFreeDiskSpace(contentRoot);
            ViewBag.UsedPercentage = CalculateUsedPercentage(contentRoot);

            // Storage providers
            ViewBag.StorageProviders = new List<StorageProviderInfo>
            {
                new() { Name = "Local", DisplayName = "تخزين محلي", IsActive = true, Icon = "feather-hard-drive" },
                new() { Name = "AWS_S3", DisplayName = "Amazon S3", IsActive = false, Icon = "feather-cloud" },
                new() { Name = "Azure_Blob", DisplayName = "Azure Blob Storage", IsActive = false, Icon = "feather-cloud" },
                new() { Name = "Google_Cloud", DisplayName = "Google Cloud Storage", IsActive = false, Icon = "feather-cloud" }
            };

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات التخزين");
            SetWarningMessage("تعذر تحميل إعدادات التخزين. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات التخزين - Save storage settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StorageSettingsViewModel model)
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
                { "StorageProvider", model.Provider ?? "Local" },
                { "StorageLocalPath", model.LocalPath ?? "uploads" },
                { "StorageMaxQuota", model.MaxQuotaGB.ToString() },
                { "StorageWarningThreshold", model.WarningThresholdPercent.ToString() },
                { "StorageAutoCleanup", model.AutoCleanupEnabled.ToString() },
                { "StorageCleanupDays", model.CleanupAfterDays.ToString() },
                
                // AWS S3 Settings
                { "S3BucketName", model.S3BucketName ?? "" },
                { "S3Region", model.S3Region ?? "" },
                { "S3AccessKey", model.S3AccessKey ?? "" },
                { "S3SecretKey", model.S3SecretKey ?? "" },
                
                // Azure Blob Settings
                { "AzureConnectionString", model.AzureConnectionString ?? "" },
                { "AzureContainerName", model.AzureContainerName ?? "" },
                
                // CDN Settings
                { "CdnEnabled", model.CdnEnabled.ToString() },
                { "CdnBaseUrl", model.CdnBaseUrl ?? "" }
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
                        Category = "Storage",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Storage settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات التخزين بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving storage settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// اختبار اتصال التخزين السحابي - Test cloud storage connection
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(string provider)
    {
        try
        {
            // Test connection based on provider
            switch (provider?.ToLower())
            {
                case "aws_s3":
                    // TODO: Implement AWS S3 connection test
                    SetInfoMessage("اختبار اتصال Amazon S3 قيد التطوير");
                    break;
                case "azure_blob":
                    // TODO: Implement Azure Blob connection test
                    SetInfoMessage("اختبار اتصال Azure Blob قيد التطوير");
                    break;
                case "google_cloud":
                    // TODO: Implement Google Cloud connection test
                    SetInfoMessage("اختبار اتصال Google Cloud قيد التطوير");
                    break;
                default:
                    // Test local storage
                    var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                    if (Directory.Exists(uploadsPath))
                    {
                        SetSuccessMessage("التخزين المحلي يعمل بشكل صحيح");
                    }
                    else
                    {
                        Directory.CreateDirectory(uploadsPath);
                        SetSuccessMessage("تم إنشاء مجلد التخزين المحلي بنجاح");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing storage connection for {Provider}", provider);
            SetErrorMessage($"فشل اختبار الاتصال: {ex.Message}");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تحليل استخدام التخزين - Storage usage analysis
    /// </summary>
    public IActionResult Analysis()
    {
        try
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            var analysis = new StorageAnalysis();

            if (Directory.Exists(uploadsPath))
            {
                var directories = Directory.GetDirectories(uploadsPath);
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var size = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                    analysis.FolderSizes.Add(new FolderSizeInfo
                    {
                        FolderName = dirInfo.Name,
                        Size = size,
                        FormattedSize = FormatFileSize(size),
                        FileCount = dirInfo.GetFiles("*", SearchOption.AllDirectories).Length
                    });
                }

                analysis.TotalSize = analysis.FolderSizes.Sum(f => f.Size);
                analysis.FormattedTotalSize = FormatFileSize(analysis.TotalSize);
            }

            return View(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing storage");
            SetWarningMessage("تعذر تحليل التخزين");
            return View(new StorageAnalysis());
        }
    }

    #region Private Methods

    private Dictionary<string, string> GetDefaultStorageSettings()
    {
        return new Dictionary<string, string>
        {
            { "StorageProvider", "Local" },
            { "StorageLocalPath", "uploads" },
            { "StorageMaxQuota", "50" },
            { "StorageWarningThreshold", "80" },
            { "StorageAutoCleanup", "false" },
            { "StorageCleanupDays", "365" },
            { "S3BucketName", "" },
            { "S3Region", "" },
            { "S3AccessKey", "" },
            { "S3SecretKey", "" },
            { "AzureConnectionString", "" },
            { "AzureContainerName", "" },
            { "CdnEnabled", "false" },
            { "CdnBaseUrl", "" }
        };
    }

    private string CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return "0 KB";
        
        try
        {
            var directoryInfo = new DirectoryInfo(path);
            var size = directoryInfo.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return FormatFileSize(size);
        }
        catch
        {
            return "N/A";
        }
    }

    private string GetTotalDiskSpace(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:");
            return FormatFileSize(drive.TotalSize);
        }
        catch
        {
            return "N/A";
        }
    }

    private string GetFreeDiskSpace(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:");
            return FormatFileSize(drive.AvailableFreeSpace);
        }
        catch
        {
            return "N/A";
        }
    }

    private int CalculateUsedPercentage(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:");
            return (int)((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100);
        }
        catch
        {
            return 0;
        }
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

    #endregion
}

#region ViewModels

public class StorageSettingsViewModel
{
    public string? Provider { get; set; } = "Local";
    public string? LocalPath { get; set; } = "uploads";
    public int MaxQuotaGB { get; set; } = 50;
    public int WarningThresholdPercent { get; set; } = 80;
    public bool AutoCleanupEnabled { get; set; }
    public int CleanupAfterDays { get; set; } = 365;
    
    // AWS S3
    public string? S3BucketName { get; set; }
    public string? S3Region { get; set; }
    public string? S3AccessKey { get; set; }
    public string? S3SecretKey { get; set; }
    
    // Azure Blob
    public string? AzureConnectionString { get; set; }
    public string? AzureContainerName { get; set; }
    
    // CDN
    public bool CdnEnabled { get; set; }
    public string? CdnBaseUrl { get; set; }
}

public class StorageProviderInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public string Icon { get; set; } = "feather-cloud";
}

public class StorageAnalysis
{
    public List<FolderSizeInfo> FolderSizes { get; set; } = new();
    public long TotalSize { get; set; }
    public string FormattedTotalSize { get; set; } = "0 KB";
}

public class FolderSizeInfo
{
    public string FolderName { get; set; } = "";
    public long Size { get; set; }
    public string FormattedSize { get; set; } = "";
    public int FileCount { get; set; }
}

#endregion

