using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات الفيديو - Video Settings Controller
/// </summary>
public class VideoSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VideoSettingsController> _logger;

    public VideoSettingsController(
        ApplicationDbContext context,
        ILogger<VideoSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// عرض وتعديل إعدادات الفيديو - View and Edit Video Settings
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var setting = await _context.VideoSettings.FirstOrDefaultAsync();

        if (setting == null)
        {
            // Create default settings
            setting = new VideoSetting();
            _context.VideoSettings.Add(setting);
            await _context.SaveChangesAsync();
        }

        var viewModel = new VideoSettingViewModel
        {
            Id = setting.Id,
            DefaultProvider = setting.DefaultProvider,
            AllowedProviders = setting.AllowedProviders,
            MaxFileSizeMB = setting.MaxFileSizeMB,
            AllowedFormats = setting.AllowedFormats,
            DefaultQuality = setting.DefaultQuality,
            EnableAutoQuality = setting.EnableAutoQuality,
            EnableDownload = setting.EnableDownload,
            EnableWatermark = setting.EnableWatermark,
            WatermarkText = setting.WatermarkText,
            WatermarkImageUrl = setting.WatermarkImageUrl,
            WatermarkPosition = setting.WatermarkPosition,
            WatermarkOpacity = setting.WatermarkOpacity,
            EnableEncryption = setting.EnableEncryption,
            EnableDRM = setting.EnableDRM,
            DRMProvider = setting.DRMProvider,
            EnableSubtitles = setting.EnableSubtitles,
            DefaultSubtitleLanguage = setting.DefaultSubtitleLanguage,
            YouTubeApiKey = setting.YouTubeApiKey,
            VimeoAccessToken = setting.VimeoAccessToken,
            BunnyStreamApiKey = setting.BunnyStreamApiKey,
            BunnyStreamLibraryId = setting.BunnyStreamLibraryId,
            CloudflareAccountId = setting.CloudflareAccountId,
            CloudflareApiToken = setting.CloudflareApiToken,
            EnableTranscoding = setting.EnableTranscoding,
            TranscodingQualitiesJson = setting.TranscodingQualitiesJson,
            StorageProvider = setting.StorageProvider,
            StoragePathPrefix = setting.StoragePathPrefix,
            CDNUrl = setting.CDNUrl,
            EnableAdaptiveBitrate = setting.EnableAdaptiveBitrate,
            EnableThumbnailGeneration = setting.EnableThumbnailGeneration,
            ThumbnailIntervalSeconds = setting.ThumbnailIntervalSeconds
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ إعدادات الفيديو - Save Video Settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(VideoSettingViewModel model)
    {
        if (ModelState.IsValid)
        {
            var setting = await _context.VideoSettings.FindAsync(model.Id);
            if (setting == null)
                return NotFound();

            setting.DefaultProvider = model.DefaultProvider;
            setting.AllowedProviders = model.AllowedProviders;
            setting.MaxFileSizeMB = model.MaxFileSizeMB;
            setting.AllowedFormats = model.AllowedFormats;
            setting.DefaultQuality = model.DefaultQuality;
            setting.EnableAutoQuality = model.EnableAutoQuality;
            setting.EnableDownload = model.EnableDownload;
            setting.EnableWatermark = model.EnableWatermark;
            setting.WatermarkText = model.WatermarkText;
            setting.WatermarkImageUrl = model.WatermarkImageUrl;
            setting.WatermarkPosition = model.WatermarkPosition;
            setting.WatermarkOpacity = model.WatermarkOpacity;
            setting.EnableEncryption = model.EnableEncryption;
            setting.EnableDRM = model.EnableDRM;
            setting.DRMProvider = model.DRMProvider;
            setting.EnableSubtitles = model.EnableSubtitles;
            setting.DefaultSubtitleLanguage = model.DefaultSubtitleLanguage;
            setting.YouTubeApiKey = model.YouTubeApiKey;
            setting.VimeoAccessToken = model.VimeoAccessToken;
            setting.BunnyStreamApiKey = model.BunnyStreamApiKey;
            setting.BunnyStreamLibraryId = model.BunnyStreamLibraryId;
            setting.CloudflareAccountId = model.CloudflareAccountId;
            setting.CloudflareApiToken = model.CloudflareApiToken;
            setting.EnableTranscoding = model.EnableTranscoding;
            setting.TranscodingQualitiesJson = model.TranscodingQualitiesJson;
            setting.StorageProvider = model.StorageProvider;
            setting.StoragePathPrefix = model.StoragePathPrefix;
            setting.CDNUrl = model.CDNUrl;
            setting.EnableAdaptiveBitrate = model.EnableAdaptiveBitrate;
            setting.EnableThumbnailGeneration = model.EnableThumbnailGeneration;
            setting.ThumbnailIntervalSeconds = model.ThumbnailIntervalSeconds;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم حفظ إعدادات الفيديو بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }
}

