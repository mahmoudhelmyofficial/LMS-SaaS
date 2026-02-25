using System.Text.RegularExpressions;
using LMS.Areas.Admin.ViewModels;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات الصفحة الرئيسية - Landing page settings controller
/// </summary>
public class LandingPageSettingsController : AdminBaseController
{
    private readonly IPlatformSettingsService _platformSettings;
    private readonly ILogger<LandingPageSettingsController> _logger;

    public LandingPageSettingsController(
        IPlatformSettingsService platformSettings,
        ILogger<LandingPageSettingsController> logger)
    {
        _platformSettings = platformSettings;
        _logger = logger;
    }

    /// <summary>
    /// صفحة إعدادات الصفحة الرئيسية - Landing page settings page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var model = new LandingPageSettingsViewModel
        {
            Enabled = await _platformSettings.GetBoolSettingAsync("LandingPage.Enabled", false),
            LogoUrl = await _platformSettings.GetSettingAsync("LandingPage.LogoUrl", ""),
            PrimaryColor = await _platformSettings.GetSettingAsync("LandingPage.PrimaryColor", "#0d6efd"),
            SecondaryColor = await _platformSettings.GetSettingAsync("LandingPage.SecondaryColor", "#6c757d"),
            FontFamily = await _platformSettings.GetSettingAsync("LandingPage.FontFamily", ""),
            AboutTitleAr = await _platformSettings.GetSettingAsync("LandingPage.AboutTitleAr", "من نحن"),
            AboutTitleEn = await _platformSettings.GetSettingAsync("LandingPage.AboutTitleEn", "About Us"),
            AboutTextAr = await _platformSettings.GetSettingAsync("LandingPage.AboutTextAr", "منصة تعليمية متكاملة تقدم دورات وكتب وجلسات مباشرة."),
            AboutTextEn = await _platformSettings.GetSettingAsync("LandingPage.AboutTextEn", "An integrated learning platform offering courses, books, and live sessions."),
            DefaultLanguage = await _platformSettings.GetSettingAsync("LandingPage.DefaultLanguage", "browser")
        };
        return View(model);
    }

    /// <summary>
    /// حفظ إعدادات الصفحة الرئيسية - Save landing page settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LandingPageSettingsViewModel model)
    {
        if (model == null)
        {
            SetErrorMessage("تم إرسال بيانات غير صالحة", "Invalid data submitted");
            return RedirectToAction(nameof(Index));
        }

        var hexPattern = new Regex(@"^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6})$");
        if (!string.IsNullOrEmpty(model.PrimaryColor) && !hexPattern.IsMatch(model.PrimaryColor.Trim()))
        {
            ModelState.AddModelError(nameof(model.PrimaryColor), CultureExtensions.T("اللون الأساسي يجب أن يكون بصيغة hex (مثال: #0d6efd)", "Primary color must be a valid hex value (e.g. #0d6efd)."));
        }
        if (!string.IsNullOrEmpty(model.SecondaryColor) && !hexPattern.IsMatch(model.SecondaryColor.Trim()))
        {
            ModelState.AddModelError(nameof(model.SecondaryColor), CultureExtensions.T("اللون الثانوي يجب أن يكون بصيغة hex (مثال: #6c757d)", "Secondary color must be a valid hex value (e.g. #6c757d)."));
        }

        var allowedLanguages = new[] { "browser", "ar", "en" };
        var lang = (model.DefaultLanguage ?? "").Trim().ToLowerInvariant();
        if (!allowedLanguages.Contains(lang))
        {
            model.DefaultLanguage = "browser";
        }

        var allowedFonts = new[] { "", "Cairo", "Segoe UI" };
        var font = model.FontFamily?.Trim() ?? "";
        if (!allowedFonts.Contains(font))
        {
            ModelState.AddModelError(nameof(model.FontFamily), CultureExtensions.T("خط الواجهة غير مسموح. اختر من القائمة.", "Font family is not allowed. Choose from the list."));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var settings = new Dictionary<string, string>
        {
            { "LandingPage.Enabled", model.Enabled.ToString().ToLower() },
            { "LandingPage.LogoUrl", model.LogoUrl ?? "" },
            { "LandingPage.PrimaryColor", (model.PrimaryColor ?? "#0d6efd").Trim() },
            { "LandingPage.SecondaryColor", (model.SecondaryColor ?? "#6c757d").Trim() },
            { "LandingPage.FontFamily", (model.FontFamily ?? "").Trim() },
            { "LandingPage.AboutTitleAr", model.AboutTitleAr ?? "" },
            { "LandingPage.AboutTitleEn", model.AboutTitleEn ?? "" },
            { "LandingPage.AboutTextAr", model.AboutTextAr ?? "" },
            { "LandingPage.AboutTextEn", model.AboutTextEn ?? "" },
            { "LandingPage.DefaultLanguage", model.DefaultLanguage ?? "browser" }
        };

        var success = await _platformSettings.UpdateSettingsAsync(settings, "Landing");

        if (success)
        {
            _platformSettings.ClearCache();
            _logger.LogInformation("Landing page settings updated");
            SetSuccessMessage("تم حفظ إعدادات الصفحة الرئيسية بنجاح", "Landing page settings saved successfully");
        }
        else
        {
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات", "An error occurred while saving settings");
        }

        return RedirectToAction(nameof(Index));
    }
}
