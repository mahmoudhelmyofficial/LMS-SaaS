using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات المنصة - Platform Settings Management Controller
/// </summary>
public class PlatformSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlatformSettingsController> _logger;
    private readonly IPlatformSettingsService _platformSettings;

    public PlatformSettingsController(
        ApplicationDbContext context, 
        ILogger<PlatformSettingsController> logger,
        IPlatformSettingsService platformSettings)
    {
        _context = context;
        _logger = logger;
        _platformSettings = platformSettings;
    }

    /// <summary>
    /// لوحة الإعدادات - Settings dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var model = new PlatformFeaturesViewModel
        {
            EnableRegistration = await _platformSettings.GetBoolSettingAsync("EnableRegistration", true),
            EnableInstructorApplication = await _platformSettings.GetBoolSettingAsync("EnableInstructorApplication", true),
            EnableReviews = await _platformSettings.GetBoolSettingAsync("EnableReviews", true),
            EnableComments = await _platformSettings.GetBoolSettingAsync("EnableComments", true),
            EnableDiscussions = await _platformSettings.GetBoolSettingAsync("EnableDiscussions", true),
            EnableCertificates = await _platformSettings.GetBoolSettingAsync("EnableCertificates", true),
            EnableGamification = await _platformSettings.GetBoolSettingAsync("EnableGamification", false),
            MaxUploadSizeMB = await _platformSettings.GetIntSettingAsync("MaxUploadSizeMB", 50),
            MaxStudentsPerCourse = await _platformSettings.GetIntSettingAsync("MaxStudentsPerCourse", 1000),
            MaxVideoLengthMinutes = await _platformSettings.GetIntSettingAsync("MaxVideoLengthMinutes", 180)
        };

        return View(model);
    }

    /// <summary>
    /// حفظ إعدادات وظائف المنصة - Save platform features settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlatformFeatures(PlatformFeaturesViewModel model)
    {
        var settings = new Dictionary<string, string>
        {
            { "EnableRegistration", model.EnableRegistration.ToString().ToLower() },
            { "EnableInstructorApplication", model.EnableInstructorApplication.ToString().ToLower() },
            { "EnableReviews", model.EnableReviews.ToString().ToLower() },
            { "EnableComments", model.EnableComments.ToString().ToLower() },
            { "EnableDiscussions", model.EnableDiscussions.ToString().ToLower() },
            { "EnableCertificates", model.EnableCertificates.ToString().ToLower() },
            { "EnableGamification", model.EnableGamification.ToString().ToLower() },
            { "MaxUploadSizeMB", model.MaxUploadSizeMB.ToString() },
            { "MaxStudentsPerCourse", model.MaxStudentsPerCourse.ToString() },
            { "MaxVideoLengthMinutes", model.MaxVideoLengthMinutes.ToString() }
        };

        var success = await _platformSettings.UpdateSettingsAsync(settings, "Platform");

        if (success)
        {
            _logger.LogInformation("Platform features settings updated");
            SetSuccessMessage("تم حفظ إعدادات المنصة بنجاح");
        }
        else
        {
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
        }

        return RedirectToAction(nameof(Index));
    }

    #region Tax Settings (redirect to canonical TaxSettingsController)

    /// <summary>
    /// إعدادات الضرائب - Tax settings (redirect to TaxSettings)
    /// </summary>
    public IActionResult TaxSettings()
    {
        return RedirectToAction("Index", "TaxSettings");
    }

    [HttpGet]
    public IActionResult CreateTaxSetting() => RedirectToAction("Create", "TaxSettings");

    [HttpGet]
    public IActionResult EditTaxSetting(int id) => RedirectToAction("Edit", "TaxSettings", new { id });

    #endregion

    #region SMS Settings (redirect to canonical SmsSettingsController)

    /// <summary>
    /// إعدادات الرسائل القصيرة - SMS settings (redirect to SmsSettings)
    /// </summary>
    public IActionResult SmsSettings()
    {
        return RedirectToAction("Index", "SmsSettings");
    }

    [HttpGet]
    public IActionResult CreateSmsSetting() => RedirectToAction("Index", "SmsSettings");

    #endregion

    #region Video Settings (redirect to canonical VideoSettingsController)

    /// <summary>
    /// إعدادات الفيديو - Video settings (redirect to VideoSettings)
    /// </summary>
    public IActionResult VideoSettings()
    {
        return RedirectToAction("Index", "VideoSettings");
    }

    [HttpGet]
    public IActionResult CreateVideoSetting() => RedirectToAction("Index", "VideoSettings");

    #endregion

    #region SEO Settings (redirect to canonical SeoSettingsController)

    /// <summary>
    /// إعدادات SEO - SEO settings (redirect to SeoSettings)
    /// </summary>
    public IActionResult SeoSettings()
    {
        return RedirectToAction("Index", "SeoSettings");
    }

    [HttpGet]
    public IActionResult CreateSeoSetting() => RedirectToAction("Index", "SeoSettings");

    #endregion
}

