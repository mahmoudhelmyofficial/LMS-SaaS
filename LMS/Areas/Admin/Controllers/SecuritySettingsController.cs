using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إعدادات الأمان - Security Settings Controller
/// Enterprise-level security configuration management
/// </summary>
public class SecuritySettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SecuritySettingsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public SecuritySettingsController(
        ApplicationDbContext context,
        ILogger<SecuritySettingsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// الصفحة الرئيسية لإعدادات الأمان - Security settings main page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Group == "Security" || s.Key.StartsWith("Security") || 
                           s.Key.StartsWith("Password") || s.Key.StartsWith("Auth") ||
                           s.Key.StartsWith("Session"))
                .ToListAsync();

            // Default settings if none exist
            if (!settings.Any())
            {
                ViewBag.DefaultSettings = GetDefaultSecuritySettings();
            }

            // Security statistics
            var today = DateTime.UtcNow.Date;
            var lastWeek = today.AddDays(-7);

            ViewBag.FailedLoginsToday = await _context.LoginLogs
                .CountAsync(l => !l.IsSuccessful && l.AttemptedAt >= today);
            ViewBag.FailedLoginsWeek = await _context.LoginLogs
                .CountAsync(l => !l.IsSuccessful && l.AttemptedAt >= lastWeek);
            ViewBag.BlockedIpsCount = await _context.BlockedIps.CountAsync(b => b.IsActive);
            ViewBag.TwoFactorEnabledUsers = await _context.Users.CountAsync(u => u.TwoFactorEnabled);
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            ViewBag.ActiveSessionsCount = await _context.UserSessions
                .CountAsync(s => s.IsActive && s.ExpiresAt >= DateTime.UtcNow);

            // Calculate security score
            var twoFactorRate = ViewBag.TotalUsers > 0 ? 
                (ViewBag.TwoFactorEnabledUsers * 100 / ViewBag.TotalUsers) : 0;
            ViewBag.SecurityScore = CalculateSecurityScore(twoFactorRate, ViewBag.BlockedIpsCount > 0);

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل إعدادات الأمان");
            SetWarningMessage("تعذر تحميل إعدادات الأمان. يرجى المحاولة مرة أخرى.");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// حفظ إعدادات الأمان - Save security settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SecuritySettingsViewModel model)
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
                // Password Policy
                { "PasswordMinLength", model.PasswordMinLength.ToString() },
                { "PasswordRequireUppercase", model.PasswordRequireUppercase.ToString() },
                { "PasswordRequireLowercase", model.PasswordRequireLowercase.ToString() },
                { "PasswordRequireDigit", model.PasswordRequireDigit.ToString() },
                { "PasswordRequireSpecialChar", model.PasswordRequireSpecialChar.ToString() },
                { "PasswordExpirationDays", model.PasswordExpirationDays.ToString() },
                { "PasswordHistoryCount", model.PasswordHistoryCount.ToString() },
                
                // Login Security
                { "MaxLoginAttempts", model.MaxLoginAttempts.ToString() },
                { "LockoutDurationMinutes", model.LockoutDurationMinutes.ToString() },
                { "SessionTimeoutMinutes", model.SessionTimeoutMinutes.ToString() },
                { "SingleSessionOnly", model.SingleSessionOnly.ToString() },
                { "RequireEmailVerification", model.RequireEmailVerification.ToString() },
                
                // Two-Factor Authentication
                { "TwoFactorEnabled", model.TwoFactorEnabled.ToString() },
                { "TwoFactorRequired", model.TwoFactorRequired.ToString() },
                { "TwoFactorMethods", model.TwoFactorMethods ?? "Email,Authenticator" },
                
                // IP Security
                { "IpWhitelistEnabled", model.IpWhitelistEnabled.ToString() },
                { "IpWhitelist", model.IpWhitelist ?? "" },
                { "AutoBlockSuspiciousIps", model.AutoBlockSuspiciousIps.ToString() },
                { "SuspiciousAttemptThreshold", model.SuspiciousAttemptThreshold.ToString() },
                
                // CAPTCHA
                { "CaptchaEnabled", model.CaptchaEnabled.ToString() },
                { "CaptchaProvider", model.CaptchaProvider ?? "reCAPTCHA" },
                { "CaptchaSiteKey", model.CaptchaSiteKey ?? "" },
                { "CaptchaSecretKey", model.CaptchaSecretKey ?? "" },
                
                // SSL/HTTPS
                { "ForceHttps", model.ForceHttps.ToString() },
                { "HstsEnabled", model.HstsEnabled.ToString() },
                { "HstsMaxAge", model.HstsMaxAgeDays.ToString() }
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
                        Category = "Security",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Security settings updated successfully");
            SetSuccessMessage("تم حفظ إعدادات الأمان بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving security settings");
            SetErrorMessage("حدث خطأ أثناء حفظ الإعدادات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// سياسة كلمات المرور - Password policy
    /// </summary>
    public async Task<IActionResult> PasswordPolicy()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Key.StartsWith("Password"))
                .ToListAsync();

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading password policy");
            SetWarningMessage("تعذر تحميل سياسة كلمات المرور");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// إعدادات المصادقة الثنائية - Two-factor settings
    /// </summary>
    public async Task<IActionResult> TwoFactorAuth()
    {
        try
        {
            var settings = await _context.PlatformSettings
                .Where(s => s.Key.StartsWith("TwoFactor"))
                .ToListAsync();

            ViewBag.TwoFactorStats = new
            {
                EnabledUsers = await _context.Users.CountAsync(u => u.TwoFactorEnabled),
                TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted),
                EnabledPercentage = await _context.Users.CountAsync(u => !u.IsDeleted) > 0 ?
                    (await _context.Users.CountAsync(u => u.TwoFactorEnabled) * 100 / 
                     await _context.Users.CountAsync(u => !u.IsDeleted)) : 0
            };

            return View(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading two-factor settings");
            SetWarningMessage("تعذر تحميل إعدادات المصادقة الثنائية");
            return View(new List<PlatformSetting>());
        }
    }

    /// <summary>
    /// سجل الأمان - Security audit log
    /// </summary>
    public async Task<IActionResult> AuditLog(DateTime? from, DateTime? to, int page = 1)
    {
        try
        {
            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;

            var query = _context.LoginLogs
                .Include(l => l.User)
                .Where(l => l.AttemptedAt >= from && l.AttemptedAt <= to);

            var pageSize = await _configService.GetPaginationSizeAsync("security_audit_log", 50);
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var logs = await query
                .OrderByDescending(l => l.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit log");
            SetWarningMessage("تعذر تحميل سجل الأمان");
            return View(new List<Domain.Entities.Security.LoginLog>());
        }
    }

    #region Private Methods

    private Dictionary<string, string> GetDefaultSecuritySettings()
    {
        return new Dictionary<string, string>
        {
            // Password Policy
            { "PasswordMinLength", "8" },
            { "PasswordRequireUppercase", "true" },
            { "PasswordRequireLowercase", "true" },
            { "PasswordRequireDigit", "true" },
            { "PasswordRequireSpecialChar", "true" },
            { "PasswordExpirationDays", "90" },
            { "PasswordHistoryCount", "5" },
            
            // Login Security
            { "MaxLoginAttempts", "5" },
            { "LockoutDurationMinutes", "30" },
            { "SessionTimeoutMinutes", "60" },
            { "SingleSessionOnly", "false" },
            { "RequireEmailVerification", "true" },
            
            // Two-Factor
            { "TwoFactorEnabled", "true" },
            { "TwoFactorRequired", "false" },
            { "TwoFactorMethods", "Email,Authenticator" },
            
            // IP Security
            { "IpWhitelistEnabled", "false" },
            { "IpWhitelist", "" },
            { "AutoBlockSuspiciousIps", "true" },
            { "SuspiciousAttemptThreshold", "10" },
            
            // CAPTCHA
            { "CaptchaEnabled", "false" },
            { "CaptchaProvider", "reCAPTCHA" },
            { "CaptchaSiteKey", "" },
            { "CaptchaSecretKey", "" },
            
            // SSL/HTTPS
            { "ForceHttps", "true" },
            { "HstsEnabled", "true" },
            { "HstsMaxAge", "365" }
        };
    }

    private int CalculateSecurityScore(int twoFactorRate, bool hasBlockedIps)
    {
        var score = 50; // Base score
        
        // Two-factor adoption
        if (twoFactorRate >= 80) score += 25;
        else if (twoFactorRate >= 50) score += 15;
        else if (twoFactorRate >= 25) score += 10;
        
        // IP blocking active
        if (hasBlockedIps) score += 10;
        
        // Other security measures (assumed enabled)
        score += 15; // HTTPS, CSRF, etc.
        
        return Math.Min(score, 100);
    }

    #endregion
}

/// <summary>
/// نموذج إعدادات الأمان
/// </summary>
public class SecuritySettingsViewModel
{
    // Password Policy
    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireSpecialChar { get; set; } = true;
    public int PasswordExpirationDays { get; set; } = 90;
    public int PasswordHistoryCount { get; set; } = 5;
    
    // Login Security
    public int MaxLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 30;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool SingleSessionOnly { get; set; }
    public bool RequireEmailVerification { get; set; } = true;
    
    // Two-Factor
    public bool TwoFactorEnabled { get; set; } = true;
    public bool TwoFactorRequired { get; set; }
    public string? TwoFactorMethods { get; set; } = "Email,Authenticator";
    
    // IP Security
    public bool IpWhitelistEnabled { get; set; }
    public string? IpWhitelist { get; set; }
    public bool AutoBlockSuspiciousIps { get; set; } = true;
    public int SuspiciousAttemptThreshold { get; set; } = 10;
    
    // CAPTCHA
    public bool CaptchaEnabled { get; set; }
    public string? CaptchaProvider { get; set; } = "reCAPTCHA";
    public string? CaptchaSiteKey { get; set; }
    public string? CaptchaSecretKey { get; set; }
    
    // SSL/HTTPS
    public bool ForceHttps { get; set; } = true;
    public bool HstsEnabled { get; set; } = true;
    public int HstsMaxAgeDays { get; set; } = 365;
}

