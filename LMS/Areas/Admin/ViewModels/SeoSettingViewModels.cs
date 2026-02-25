using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات SEO - SEO Setting ViewModel
/// </summary>
public class SeoSettingViewModel
{
    public int Id { get; set; }

    /// <summary>
    /// عنوان الموقع - Site title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان الموقع")]
    public string? SiteTitle { get; set; }

    /// <summary>
    /// وصف الموقع - Site description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف الموقع")]
    public string? SiteDescription { get; set; }

    /// <summary>
    /// كلمات مفتاحية - Site keywords
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الكلمات المفتاحية")]
    public string? SiteKeywords { get; set; }

    /// <summary>
    /// عنوان Meta الافتراضي - Default meta title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان Meta الافتراضي")]
    public string? DefaultMetaTitle { get; set; }

    /// <summary>
    /// وصف Meta الافتراضي - Default meta description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف Meta الافتراضي")]
    public string? DefaultMetaDescription { get; set; }

    /// <summary>
    /// كلمات Meta المفتاحية الافتراضية - Default meta keywords
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "كلمات Meta المفتاحية الافتراضية")]
    public string? DefaultMetaKeywords { get; set; }

    /// <summary>
    /// صورة Open Graph - OG Image
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة Open Graph")]
    public string? OgImage { get; set; }

    /// <summary>
    /// نوع بطاقة Twitter - Twitter card type
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "نوع بطاقة Twitter")]
    public string? TwitterCardType { get; set; }

    /// <summary>
    /// حساب Twitter - Twitter site handle
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "حساب Twitter")]
    public string? TwitterSite { get; set; }

    /// <summary>
    /// معرف Google Analytics - Google Analytics ID
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "معرف Google Analytics")]
    public string? GoogleAnalyticsId { get; set; }

    /// <summary>
    /// معرف Google Tag Manager - Google Tag Manager ID
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "معرف Google Tag Manager")]
    public string? GoogleTagManagerId { get; set; }

    /// <summary>
    /// معرف Facebook Pixel - Facebook Pixel ID
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "معرف Facebook Pixel")]
    public string? FacebookPixelId { get; set; }

    /// <summary>
    /// تفعيل Sitemap - Enable sitemap
    /// </summary>
    [Display(Name = "تفعيل Sitemap")]
    public bool EnableSitemap { get; set; } = true;

    /// <summary>
    /// تفعيل robots.txt - Enable robots.txt
    /// </summary>
    [Display(Name = "تفعيل robots.txt")]
    public bool EnableRobotsTxt { get; set; } = true;

    /// <summary>
    /// توجيهات Robots - Robots directive
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "توجيهات Robots")]
    public string? RobotsDirective { get; set; }

    /// <summary>
    /// الرابط الأساسي - Canonical base URL
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "الرابط الأساسي")]
    public string? CanonicalBaseUrl { get; set; }

    /// <summary>
    /// اللغات البديلة - Alternate languages (JSON)
    /// </summary>
    [Display(Name = "اللغات البديلة")]
    public string? AlternateLanguages { get; set; }

    /// <summary>
    /// نوع Schema.org - Schema.org type
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "نوع Schema.org")]
    public string? SchemaOrgType { get; set; }

    /// <summary>
    /// سكريبتات Head مخصصة - Custom head scripts
    /// </summary>
    [Display(Name = "سكريبتات Head مخصصة")]
    public string? CustomHeadScripts { get; set; }

    /// <summary>
    /// سكريبتات Body مخصصة - Custom body scripts
    /// </summary>
    [Display(Name = "سكريبتات Body مخصصة")]
    public string? CustomBodyScripts { get; set; }

    /// <summary>
    /// الكلمات المفتاحية - Keywords (alias)
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الكلمات المفتاحية")]
    public string? Keywords { get; set; }

    /// <summary>
    /// اسم المؤلف - Author name
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "اسم المؤلف")]
    public string? AuthorName { get; set; }

    /// <summary>
    /// رابط الشعار - Logo URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط الشعار")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// رابط الصورة المميزة - Featured image URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط الصورة المميزة")]
    public string? FeaturedImageUrl { get; set; }

    /// <summary>
    /// كود Google Search Console - Google Search Console code
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "كود Google Search Console")]
    public string? GoogleSearchConsoleCode { get; set; }

    /// <summary>
    /// تفعيل Open Graph - Enable Open Graph
    /// </summary>
    [Display(Name = "تفعيل Open Graph")]
    public bool EnableOpenGraph { get; set; } = true;

    /// <summary>
    /// تفعيل بطاقات Twitter - Enable Twitter cards
    /// </summary>
    [Display(Name = "تفعيل بطاقات Twitter")]
    public bool EnableTwitterCards { get; set; } = true;

    /// <summary>
    /// حساب Twitter - Twitter handle
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "حساب Twitter")]
    public string? TwitterHandle { get; set; }

    /// <summary>
    /// تفعيل Schema Markup - Enable schema markup
    /// </summary>
    [Display(Name = "تفعيل Schema Markup")]
    public bool EnableSchemaMarkup { get; set; } = true;

    /// <summary>
    /// محتوى Robots.txt - Robots.txt content
    /// </summary>
    [Display(Name = "محتوى Robots.txt")]
    public string? RobotsTxtContent { get; set; }

    /// <summary>
    /// الرابط الأساسي - Canonical URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الرابط الأساسي")]
    public string? CanonicalUrl { get; set; }

    /// <summary>
    /// تفعيل RSS Feed - Enable RSS feed
    /// </summary>
    [Display(Name = "تفعيل RSS Feed")]
    public bool EnableRssFeed { get; set; } = true;

    /// <summary>
    /// نوع الصفحة - Page type
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "نوع الصفحة")]
    public string? PageType { get; set; }

    /// <summary>
    /// عنوان Meta - Meta title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان Meta")]
    public string? MetaTitle { get; set; }

    /// <summary>
    /// وصف Meta - Meta description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف Meta")]
    public string? MetaDescription { get; set; }

    /// <summary>
    /// كلمات Meta المفتاحية - Meta keywords
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "كلمات Meta المفتاحية")]
    public string? MetaKeywords { get; set; }

    /// <summary>
    /// عنوان Open Graph - OG title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان Open Graph")]
    public string? OgTitle { get; set; }

    /// <summary>
    /// وصف Open Graph - OG description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف Open Graph")]
    public string? OgDescription { get; set; }

    /// <summary>
    /// نوع Schema - Schema type
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "نوع Schema")]
    public string? SchemaType { get; set; }

    /// <summary>
    /// البيانات المنظمة - Structured data (JSON-LD)
    /// </summary>
    [Display(Name = "البيانات المنظمة")]
    public string? StructuredData { get; set; }

    /// <summary>
    /// هل مفعل - Is active
    /// </summary>
    [Display(Name = "مفعل")]
    public bool IsActive { get; set; } = true;
}

