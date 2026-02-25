using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات المنصة - Platform Settings Controller
/// </summary>
public class SettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IPlatformSettingsService _platformSettings;

    public SettingsController(
        ApplicationDbContext context,
        ILogger<SettingsController> logger,
        IWebHostEnvironment env,
        IPlatformSettingsService platformSettings)
    {
        _context = context;
        _logger = logger;
        _env = env;
        _platformSettings = platformSettings;
    }

    /// <summary>
    /// الإعدادات العامة - General settings
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var settings = await _context.PlatformSettings.ToListAsync();
        return View(settings);
    }

    /// <summary>
    /// حفظ الإعدادات العامة - Save general settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGeneral(Dictionary<string, string> settings)
    {
        foreach (var setting in settings)
        {
            if (string.IsNullOrEmpty(setting.Key)) continue;
            
            var existing = await _context.PlatformSettings
                .FirstOrDefaultAsync(s => s.Key == setting.Key);

            if (existing != null)
            {
                existing.Value = setting.Value ?? "";
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PlatformSettings.Add(new PlatformSetting
                {
                    Key = setting.Key,
                    Value = setting.Value ?? "",
                    Category = "General",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        SetSuccessMessage("تم حفظ الإعدادات بنجاح");

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إعدادات بوابات الدفع - Payment gateway settings (Redirect to PaymentGatewaySettings)
    /// </summary>
    public IActionResult PaymentGateways()
    {
        return RedirectToAction("Index", "PaymentGatewaySettings");
    }

    /// <summary>
    /// إعدادات البريد الإلكتروني - Email settings (Redirect to EmailSettings)
    /// </summary>
    public IActionResult Email()
    {
        // Redirect to the dedicated EmailSettings controller for full functionality
        return RedirectToAction("Index", "EmailSettings");
    }

    /// <summary>
    /// إعدادات SEO - SEO settings (Redirect to SeoSettings)
    /// </summary>
    public IActionResult Seo()
    {
        return RedirectToAction("Index", "SeoSettings");
    }

    /// <summary>
    /// إعدادات الضرائب - Tax settings (Redirect to TaxSettings)
    /// </summary>
    public IActionResult Tax()
    {
        return RedirectToAction("Index", "TaxSettings");
    }

    /// <summary>
    /// إعدادات العمولات - Commission settings (Redirect to CommissionSettings)
    /// </summary>
    public IActionResult Commissions()
    {
        return RedirectToAction("Index", "CommissionSettings");
    }

    /// <summary>
    /// الإعدادات العامة - General settings (GET)
    /// </summary>
    public async Task<IActionResult> General()
    {
        var settings = await _context.PlatformSettings
            .Where(s => s.Group == "General" || s.Key.Contains("Site") || s.Key.Contains("Admin") ||
                       s.Key.Contains("Support") || s.Key.Contains("Enable") || s.Key.Contains("Maintenance") ||
                       s.Key == "PlatformName" || s.Key == "PlatformLogo" || s.Key == "PlatformLogoDark" || s.Key == "PlatformFavicon")
            .ToListAsync();

        ViewBag.PlatformName = await _platformSettings.GetPlatformNameAsync();
        ViewBag.PlatformLogo = await _platformSettings.GetPlatformLogoAsync();
        ViewBag.PlatformLogoDark = await _platformSettings.GetSettingAsync("PlatformLogoDark");
        ViewBag.PlatformFavicon = await _platformSettings.GetPlatformFaviconAsync();

        return View(settings);
    }

    /// <summary>
    /// حفظ الإعدادات العامة - Save General settings (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> General(
        Dictionary<string, string> settings,
        IFormFile? Logo = null,
        IFormFile? LogoDark = null,
        IFormFile? Favicon = null)
    {
        try
        {
            if (settings != null)
            {
                foreach (var setting in settings)
                {
                    if (string.IsNullOrEmpty(setting.Key)) continue;

                    var existing = await _context.PlatformSettings
                        .FirstOrDefaultAsync(s => s.Key == setting.Key);

                    if (existing != null)
                    {
                        existing.Value = setting.Value ?? "";
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.PlatformSettings.Add(new PlatformSetting
                        {
                            Key = setting.Key,
                            Value = setting.Value ?? "",
                            Category = "General",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Save all form settings first so UpdateSettingAsync sees them (avoids duplicate-key when DB was empty)
                await _context.SaveChangesAsync();

                // Refresh cache for platform name so GetPlatformNameAsync returns new value
                if (settings.TryGetValue("PlatformName", out var platformNameValue) && !string.IsNullOrWhiteSpace(platformNameValue))
                {
                    await _platformSettings.UpdateSettingAsync("PlatformName", platformNameValue.Trim(), "General");
                }
            }

            var webRoot = _env.WebRootPath;
            var brandingDir = !string.IsNullOrEmpty(webRoot)
                ? Path.Combine(webRoot, "uploads", "branding")
                : null;
            var allowedImageExts = new[] { ".png", ".jpg", ".jpeg", ".svg", ".gif" };

            if (Logo != null && Logo.Length > 0 && !string.IsNullOrEmpty(Logo.FileName) && !string.IsNullOrEmpty(brandingDir))
            {
                Directory.CreateDirectory(brandingDir);
                var ext = Path.GetExtension(Logo.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedImageExts.Contains(ext))
                    ext = ".png";
                var logoPath = Path.Combine(brandingDir, "logo" + ext);
                await using (var stream = new FileStream(logoPath, FileMode.Create))
                    await Logo.CopyToAsync(stream);
                await _platformSettings.UpdateSettingAsync("PlatformLogo", "/uploads/branding/logo" + ext, "General");
            }

            if (LogoDark != null && LogoDark.Length > 0 && !string.IsNullOrEmpty(LogoDark.FileName) && !string.IsNullOrEmpty(brandingDir))
            {
                Directory.CreateDirectory(brandingDir);
                var ext = Path.GetExtension(LogoDark.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedImageExts.Contains(ext))
                    ext = ".png";
                var logoDarkPath = Path.Combine(brandingDir, "logo-dark" + ext);
                await using (var stream = new FileStream(logoDarkPath, FileMode.Create))
                    await LogoDark.CopyToAsync(stream);
                await _platformSettings.UpdateSettingAsync("PlatformLogoDark", "/uploads/branding/logo-dark" + ext, "General");
            }

            if (Favicon != null && Favicon.Length > 0 && !string.IsNullOrEmpty(Favicon.FileName) && !string.IsNullOrEmpty(brandingDir))
            {
                var faviconExt = Path.GetExtension(Favicon.FileName).ToLowerInvariant();
                if (faviconExt != ".ico" && faviconExt != ".png")
                    faviconExt = ".ico";
                Directory.CreateDirectory(brandingDir);
                var faviconPath = Path.Combine(brandingDir, "favicon" + faviconExt);
                await using (var stream = new FileStream(faviconPath, FileMode.Create))
                    await Favicon.CopyToAsync(stream);
                await _platformSettings.UpdateSettingAsync("PlatformFavicon", "/uploads/branding/favicon" + faviconExt, "General");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("General settings updated successfully");
            SetSuccessMessage("تم حفظ الإعدادات العامة بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving general settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات العامة");
        }

        return RedirectToAction(nameof(General));
    }

    /// <summary>
    /// إعدادات اللغة والتوطين - Localization settings
    /// </summary>
    public async Task<IActionResult> Localization()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Localization" || s.Key.Contains("Language") || s.Key.Contains("Locale") || s.Key.Contains("Timezone"))
                .ToListAsync();

            // Add default localization settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = new Dictionary<string, string>
                {
                    { "DefaultLanguage", "ar" },
                    { "DefaultTimezone", "Asia/Riyadh" },
                    { "DateFormat", "dd/MM/yyyy" },
                    { "TimeFormat", "HH:mm" },
                    { "CurrencyCode", "SAR" },
                    { "CurrencySymbol", "ر.س" },
                    { "EnableRtl", "true" },
                    { "EnableMultiLanguage", "false" }
                };
            }

            // Available languages
            ViewBag.Languages = new Dictionary<string, string>
            {
                { "ar", "العربية" },
                { "en", "English" },
                { "fr", "Français" },
                { "es", "Español" },
                { "de", "Deutsch" }
            };

            // Available timezones
            ViewBag.Timezones = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => new { Id = tz.Id, Name = tz.DisplayName })
                .ToList();

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات اللغة - Error loading localization settings");
            SetWarningMessage("تعذر تحميل إعدادات اللغة. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات اللغة والتوطين - Save Localization settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLocalization(Dictionary<string, string> settings)
    {
        foreach (var setting in settings)
        {
            if (string.IsNullOrEmpty(setting.Key)) continue;
            
            var existing = await _context.PlatformSettings
                .FirstOrDefaultAsync(s => s.Key == setting.Key);

            if (existing != null)
            {
                existing.Value = setting.Value ?? "";
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PlatformSettings.Add(new PlatformSetting
                {
                    Key = setting.Key,
                    Value = setting.Value ?? "",
                    Category = "Localization",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        SetSuccessMessage("تم حفظ إعدادات اللغة بنجاح");

        return RedirectToAction(nameof(Localization));
    }

    /// <summary>
    /// إعدادات SEO - SEO settings (Alias, redirect to SeoSettings)
    /// </summary>
    [HttpGet]
    public IActionResult SEO()
    {
        return RedirectToAction("Index", "SeoSettings");
    }

    /// <summary>
    /// إعدادات المظهر - Appearance (redirect to General; branding moved to General)
    /// </summary>
    [HttpGet]
    public IActionResult Appearance()
    {
        return RedirectToAction(nameof(General));
    }

    /// <summary>
    /// إعدادات الدفع - Payment settings (Redirect to PaymentSettings)
    /// </summary>
    public IActionResult Payment()
    {
        // Redirect to the dedicated PaymentSettings controller for comprehensive payment management
        return RedirectToAction("Index", "PaymentSettings");
    }

    /// <summary>
    /// إعدادات التكامل - Integration settings (GET)
    /// </summary>
    public async Task<IActionResult> Integrations()
    {
        var settings = await _context.PlatformSettings
            .Where(s => s.Group == "Integration" || s.Key.Contains("Api") || s.Key.Contains("Integration") ||
                       s.Key.Contains("Stripe") || s.Key.Contains("PayPal") || s.Key.Contains("SendGrid") ||
                       s.Key.Contains("Mailgun") || s.Key.Contains("S3") || s.Key.Contains("Spaces") ||
                       s.Key.Contains("Analytics") || s.Key.Contains("Pixel") || s.Key.Contains("Twitter") ||
                       s.Key.Contains("GitHub") || s.Key.Contains("OAuth"))
            .ToListAsync();
        return View(settings);
    }

    /// <summary>
    /// حفظ إعدادات التكامل - Save Integration settings (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Integrations(Dictionary<string, string> settings)
    {
        try
        {
            foreach (var setting in settings)
            {
                if (string.IsNullOrEmpty(setting.Key)) continue;

                var existing = await _context.PlatformSettings
                    .FirstOrDefaultAsync(s => s.Key == setting.Key);

                if (existing != null)
                {
                    existing.Value = setting.Value ?? "";
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value ?? "",
                        Category = "Integration",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Integration settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات التكاملات بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving integration settings");
            SetErrorMessage("حدث خطأ أثناء حفظ إعدادات التكاملات");
        }

        return RedirectToAction(nameof(Integrations));
    }

    /// <summary>
    /// إعدادات الأمان - Security settings (redirect to dedicated SecuritySettings controller)
    /// </summary>
    public IActionResult Security()
    {
        return RedirectToAction("Index", "SecuritySettings");
    }

    /// <summary>
    /// إعدادات الرسائل النصية - SMS settings (Redirect to SmsSettings)
    /// </summary>
    public IActionResult Sms()
    {
        return RedirectToAction("Index", "SmsSettings");
    }

    /// <summary>
    /// إعدادات الفيديو - Video settings (Redirect to VideoSettings)
    /// </summary>
    public IActionResult Video()
    {
        return RedirectToAction("Index", "VideoSettings");
    }
}

