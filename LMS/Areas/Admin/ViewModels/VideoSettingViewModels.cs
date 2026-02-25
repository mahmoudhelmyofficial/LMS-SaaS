using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات الفيديو - Video Setting ViewModel
/// </summary>
public class VideoSettingViewModel
{
    public int Id { get; set; }

    /// <summary>
    /// مزود الفيديو الافتراضي - Default video provider
    /// </summary>
    [Required(ErrorMessage = "مزود الفيديو مطلوب")]
    [Display(Name = "مزود الفيديو الافتراضي")]
    public VideoProvider DefaultProvider { get; set; }

    /// <summary>
    /// المزودون المسموح بهم - Allowed providers (JSON)
    /// </summary>
    [Display(Name = "المزودون المسموح بهم")]
    public string? AllowedProviders { get; set; }

    /// <summary>
    /// الحد الأقصى لحجم الملف - Max file size in MB
    /// </summary>
    [Range(1, 10000)]
    [Display(Name = "الحد الأقصى لحجم الملف (MB)")]
    public int MaxFileSizeMB { get; set; } = 500;

    /// <summary>
    /// الصيغ المسموحة - Allowed formats (JSON)
    /// </summary>
    [Display(Name = "الصيغ المسموحة")]
    public string? AllowedFormats { get; set; }

    /// <summary>
    /// الجودة الافتراضية - Default quality
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "الجودة الافتراضية")]
    public string DefaultQuality { get; set; } = "720p";

    /// <summary>
    /// تفعيل الجودة التلقائية - Enable auto quality
    /// </summary>
    [Display(Name = "تفعيل الجودة التلقائية")]
    public bool EnableAutoQuality { get; set; } = true;

    /// <summary>
    /// السماح بالتحميل - Enable download
    /// </summary>
    [Display(Name = "السماح بالتحميل")]
    public bool EnableDownload { get; set; } = false;

    /// <summary>
    /// تفعيل العلامة المائية - Enable watermark
    /// </summary>
    [Display(Name = "تفعيل العلامة المائية")]
    public bool EnableWatermark { get; set; } = false;

    /// <summary>
    /// نص العلامة المائية - Watermark text
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "نص العلامة المائية")]
    public string? WatermarkText { get; set; }

    /// <summary>
    /// صورة العلامة المائية - Watermark image URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة العلامة المائية")]
    public string? WatermarkImageUrl { get; set; }

    /// <summary>
    /// موضع العلامة المائية - Watermark position
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "موضع العلامة المائية")]
    public string WatermarkPosition { get; set; } = "BottomRight";

    /// <summary>
    /// شفافية العلامة المائية - Watermark opacity
    /// </summary>
    [Range(0, 100)]
    [Display(Name = "شفافية العلامة المائية %")]
    public int WatermarkOpacity { get; set; } = 50;

    /// <summary>
    /// تفعيل التشفير - Enable encryption
    /// </summary>
    [Display(Name = "تفعيل التشفير")]
    public bool EnableEncryption { get; set; } = false;

    /// <summary>
    /// تفعيل DRM - Enable DRM
    /// </summary>
    [Display(Name = "تفعيل DRM")]
    public bool EnableDRM { get; set; } = false;

    /// <summary>
    /// مزود DRM - DRM provider
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "مزود DRM")]
    public string? DRMProvider { get; set; }

    /// <summary>
    /// تفعيل الترجمة - Enable subtitles
    /// </summary>
    [Display(Name = "تفعيل الترجمة")]
    public bool EnableSubtitles { get; set; } = true;

    /// <summary>
    /// لغة الترجمة الافتراضية - Default subtitle language
    /// </summary>
    [MaxLength(10)]
    [Display(Name = "لغة الترجمة الافتراضية")]
    public string DefaultSubtitleLanguage { get; set; } = "ar";

    /// <summary>
    /// مفتاح YouTube API - YouTube API Key
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "مفتاح YouTube API")]
    public string? YouTubeApiKey { get; set; }

    /// <summary>
    /// رمز وصول Vimeo - Vimeo access token
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رمز وصول Vimeo")]
    public string? VimeoAccessToken { get; set; }

    /// <summary>
    /// مفتاح Bunny Stream API - Bunny Stream API Key
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "مفتاح Bunny Stream API")]
    public string? BunnyStreamApiKey { get; set; }

    /// <summary>
    /// معرف مكتبة Bunny Stream - Bunny Stream Library ID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف مكتبة Bunny Stream")]
    public string? BunnyStreamLibraryId { get; set; }

    /// <summary>
    /// معرف حساب Cloudflare - Cloudflare account ID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف حساب Cloudflare")]
    public string? CloudflareAccountId { get; set; }

    /// <summary>
    /// رمز Cloudflare API - Cloudflare API token
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رمز Cloudflare API")]
    public string? CloudflareApiToken { get; set; }

    /// <summary>
    /// تفعيل التحويل - Enable transcoding
    /// </summary>
    [Display(Name = "تفعيل التحويل")]
    public bool EnableTranscoding { get; set; } = true;

    /// <summary>
    /// جودات التحويل - Transcoding qualities (JSON)
    /// </summary>
    [Display(Name = "جودات التحويل")]
    public string? TranscodingQualitiesJson { get; set; }

    /// <summary>
    /// مزود التخزين - Storage provider
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "مزود التخزين")]
    public string StorageProvider { get; set; } = "Local";

    /// <summary>
    /// بادئة المسار - Storage path prefix
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "بادئة المسار")]
    public string? StoragePathPrefix { get; set; }

    /// <summary>
    /// رابط CDN - CDN URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط CDN")]
    public string? CDNUrl { get; set; }

    /// <summary>
    /// تفعيل Adaptive Bitrate - Enable adaptive bitrate
    /// </summary>
    [Display(Name = "تفعيل Adaptive Bitrate")]
    public bool EnableAdaptiveBitrate { get; set; } = true;

    /// <summary>
    /// تفعيل توليد الصور المصغرة - Enable thumbnail generation
    /// </summary>
    [Display(Name = "تفعيل توليد الصور المصغرة")]
    public bool EnableThumbnailGeneration { get; set; } = true;

    /// <summary>
    /// فاصل الصور المصغرة - Thumbnail interval in seconds
    /// </summary>
    [Range(1, 300)]
    [Display(Name = "فاصل الصور المصغرة (ثواني)")]
    public int ThumbnailIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// المزود - Provider
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "المزود")]
    public string? Provider { get; set; }

    /// <summary>
    /// هل مفعل - Is Enabled
    /// </summary>
    [Display(Name = "مفعل")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// مفتاح API - API Key
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "مفتاح API")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// المفتاح السري - API Secret
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "المفتاح السري")]
    public string? ApiSecret { get; set; }

    /// <summary>
    /// معرف المكتبة - Library ID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف المكتبة")]
    public string? LibraryId { get; set; }

    /// <summary>
    /// رابط CDN - CDN URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط CDN")]
    public string? CdnUrl { get; set; }

    /// <summary>
    /// رابط العلامة المائية - Watermark URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط العلامة المائية")]
    public string? WatermarkUrl { get; set; }

    /// <summary>
    /// الحد الأقصى للمدة - Max duration minutes
    /// </summary>
    [Display(Name = "الحد الأقصى للمدة (دقائق)")]
    public int MaxDurationMinutes { get; set; } = 180;

    /// <summary>
    /// الصيغ المدعومة - Supported formats
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "الصيغ المدعومة")]
    public string? SupportedFormats { get; set; }

    /// <summary>
    /// تفعيل الترجمة التلقائية - Enable auto captions
    /// </summary>
    [Display(Name = "تفعيل الترجمة التلقائية")]
    public bool EnableAutoCaptions { get; set; } = false;

    /// <summary>
    /// تفعيل التحليلات - Enable analytics
    /// </summary>
    [Display(Name = "تفعيل التحليلات")]
    public bool EnableAnalytics { get; set; } = true;

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// منطقة التخزين - Storage zone
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "منطقة التخزين")]
    public string? StorageZone { get; set; }

    /// <summary>
    /// الحد الأقصى لحجم التحميل - Max upload size in MB
    /// </summary>
    [Range(1, 10000)]
    [Display(Name = "الحد الأقصى لحجم التحميل (MB)")]
    public int MaxUploadSizeMB { get => MaxFileSizeMB; set => MaxFileSizeMB = value; }

    /// <summary>
    /// التشغيل التلقائي - Auto play
    /// </summary>
    [Display(Name = "التشغيل التلقائي")]
    public bool AutoPlay { get; set; } = false;

    /// <summary>
    /// تفعيل الترجمة - Enable captions
    /// </summary>
    [Display(Name = "تفعيل الترجمة")]
    public bool EnableCaptions { get => EnableSubtitles; set => EnableSubtitles = value; }

    /// <summary>
    /// يتطلب مصادقة - Require authentication
    /// </summary>
    [Display(Name = "يتطلب مصادقة")]
    public bool RequireAuthentication { get; set; } = false;
}

