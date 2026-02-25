using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات المصادقة الثنائية - Two Factor Settings Controller
/// </summary>
public class TwoFactorSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TwoFactorSettingsController> _logger;

    public TwoFactorSettingsController(
        ApplicationDbContext context,
        ILogger<TwoFactorSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة إعدادات المصادقة الثنائية - 2FA Settings List
    /// </summary>
    public async Task<IActionResult> Index(bool? isEnabled)
    {
        var query = _context.TwoFactorSettings
            .Include(tfs => tfs.User)
            .AsQueryable();

        if (isEnabled.HasValue)
        {
            query = query.Where(tfs => tfs.IsEnabled == isEnabled.Value);
        }

        var settings = await query
            .OrderBy(tfs => tfs.User != null ? tfs.User.Email ?? "" : "")
            .Take(200)
            .ToListAsync();

        ViewBag.IsEnabled = isEnabled;

        return View(settings);
    }

    /// <summary>
    /// تفاصيل الإعدادات - Settings Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var setting = await _context.TwoFactorSettings
            .Include(tfs => tfs.User)
            .FirstOrDefaultAsync(tfs => tfs.Id == id);

        if (setting == null)
            return NotFound();

        return View(setting);
    }

    /// <summary>
    /// تعطيل المصادقة الثنائية - Disable 2FA
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {
        var setting = await _context.TwoFactorSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        setting.IsEnabled = false;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعطيل المصادقة الثنائية بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إعادة تعيين المصادقة الثنائية - Reset 2FA
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(int id)
    {
        var setting = await _context.TwoFactorSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        setting.Secret = null;
        setting.BackupCodes = null;
        setting.IsEnabled = false;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إعادة تعيين المصادقة الثنائية بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات المصادقة الثنائية - 2FA Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var allSettings = await _context.TwoFactorSettings.ToListAsync();
        var totalUsers = await _context.Users.CountAsync();

        var stats = new
        {
            TotalUsers = totalUsers,
            UsersWithEnabled2FA = allSettings.Count(s => s.IsEnabled),
            UsersWithSMS = allSettings.Count(s => s.IsEnabled && s.Method == "SMS"),
            UsersWithApp = allSettings.Count(s => s.IsEnabled && s.Method == "App"),
            UsersWithEmail = allSettings.Count(s => s.IsEnabled && s.Method == "Email"),
            EnablementRate = totalUsers > 0 ? (allSettings.Count(s => s.IsEnabled) * 100.0 / totalUsers) : 0
        };

        ViewBag.Stats = stats;

        return View();
    }
}

