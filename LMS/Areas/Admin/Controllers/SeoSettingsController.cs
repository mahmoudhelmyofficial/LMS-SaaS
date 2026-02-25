using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات SEO - SEO Settings Controller
/// </summary>
public class SeoSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeoSettingsController> _logger;

    public SeoSettingsController(
        ApplicationDbContext context,
        ILogger<SeoSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// عرض وتعديل إعدادات SEO - View and Edit SEO Settings
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var setting = await _context.SeoSettings.FirstOrDefaultAsync();

        if (setting == null)
        {
            // Create default settings
            setting = new SeoSetting();
            _context.SeoSettings.Add(setting);
            await _context.SaveChangesAsync();
        }

        var viewModel = new SeoSettingViewModel
        {
            Id = setting.Id,
            SiteTitle = setting.SiteTitle,
            SiteDescription = setting.SiteDescription,
            SiteKeywords = setting.SiteKeywords,
            DefaultMetaTitle = setting.DefaultMetaTitle,
            DefaultMetaDescription = setting.DefaultMetaDescription,
            DefaultMetaKeywords = setting.DefaultMetaKeywords,
            OgImage = setting.OgImage,
            TwitterCardType = setting.TwitterCardType,
            TwitterSite = setting.TwitterSite,
            GoogleAnalyticsId = setting.GoogleAnalyticsId,
            GoogleTagManagerId = setting.GoogleTagManagerId,
            FacebookPixelId = setting.FacebookPixelId,
            EnableSitemap = setting.EnableSitemap,
            EnableRobotsTxt = setting.EnableRobotsTxt,
            RobotsDirective = setting.RobotsDirective,
            CanonicalBaseUrl = setting.CanonicalBaseUrl,
            AlternateLanguages = setting.AlternateLanguages,
            SchemaOrgType = setting.SchemaOrgType,
            CustomHeadScripts = setting.CustomHeadScripts,
            CustomBodyScripts = setting.CustomBodyScripts
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ إعدادات SEO - Save SEO Settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SeoSettingViewModel model)
    {
        if (ModelState.IsValid)
        {
            var setting = await _context.SeoSettings.FindAsync(model.Id);
            if (setting == null)
                return NotFound();

            setting.SiteTitle = model.SiteTitle;
            setting.SiteDescription = model.SiteDescription;
            setting.SiteKeywords = model.SiteKeywords;
            setting.DefaultMetaTitle = model.DefaultMetaTitle;
            setting.DefaultMetaDescription = model.DefaultMetaDescription;
            setting.DefaultMetaKeywords = model.DefaultMetaKeywords;
            setting.OgImage = model.OgImage;
            setting.TwitterCardType = model.TwitterCardType;
            setting.TwitterSite = model.TwitterSite;
            setting.GoogleAnalyticsId = model.GoogleAnalyticsId;
            setting.GoogleTagManagerId = model.GoogleTagManagerId;
            setting.FacebookPixelId = model.FacebookPixelId;
            setting.EnableSitemap = model.EnableSitemap;
            setting.EnableRobotsTxt = model.EnableRobotsTxt;
            setting.RobotsDirective = model.RobotsDirective;
            setting.CanonicalBaseUrl = model.CanonicalBaseUrl;
            setting.AlternateLanguages = model.AlternateLanguages;
            setting.SchemaOrgType = model.SchemaOrgType;
            setting.CustomHeadScripts = model.CustomHeadScripts;
            setting.CustomBodyScripts = model.CustomBodyScripts;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم حفظ إعدادات SEO بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// إنشاء إعدادات SEO (توجيه إلى Index) - Create SEO settings (redirect to Index)
    /// </summary>
    [HttpGet]
    public IActionResult CreateSeoSetting() => RedirectToAction(nameof(Index));

    /// <summary>
    /// حفظ إعدادات SEO (للدعم مع النماذج القديمة) - Save SEO settings (for legacy form support)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSeoSetting(SeoSettingViewModel model)
    {
        if (ModelState.IsValid)
        {
            SeoSetting? setting;
            if (model.Id == 0)
            {
                setting = new SeoSetting();
                _context.SeoSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            else
            {
                setting = await _context.SeoSettings.FindAsync(model.Id);
                if (setting == null)
                    return NotFound();
            }

            setting.SiteTitle = model.SiteTitle;
            setting.SiteDescription = model.SiteDescription;
            setting.SiteKeywords = model.SiteKeywords;
            setting.DefaultMetaTitle = model.DefaultMetaTitle;
            setting.DefaultMetaDescription = model.DefaultMetaDescription;
            setting.DefaultMetaKeywords = model.DefaultMetaKeywords;
            setting.OgImage = model.OgImage;
            setting.TwitterCardType = model.TwitterCardType;
            setting.TwitterSite = model.TwitterSite;
            setting.GoogleAnalyticsId = model.GoogleAnalyticsId;
            setting.GoogleTagManagerId = model.GoogleTagManagerId;
            setting.FacebookPixelId = model.FacebookPixelId;
            setting.EnableSitemap = model.EnableSitemap;
            setting.EnableRobotsTxt = model.EnableRobotsTxt;
            setting.RobotsDirective = model.RobotsDirective;
            setting.CanonicalBaseUrl = model.CanonicalBaseUrl;
            setting.AlternateLanguages = model.AlternateLanguages;
            setting.SchemaOrgType = model.SchemaOrgType;
            setting.CustomHeadScripts = model.CustomHeadScripts;
            setting.CustomBodyScripts = model.CustomBodyScripts;

            await _context.SaveChangesAsync();
            SetSuccessMessage("تم حفظ إعدادات SEO بنجاح");
            return RedirectToAction(nameof(Index));
        }
        return View("Index", model);
    }
}

