using LMS.Data;
using LMS.Domain.Entities.Users;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// إعدادات الطالب - Student Settings Controller
/// </summary>
public class SettingsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ILogger<SettingsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// صفحة الإعدادات الرئيسية - Main settings page
    /// </summary>
    /// <param name="tab">Optional tab to activate: account, notifications, privacy, security</param>
    public async Task<IActionResult> Index(string? tab = null)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Index)) });
        }

        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        // Get notification preferences
        var notificationPrefs = await _context.NotificationPreferences
            .FirstOrDefaultAsync(np => np.UserId == userId);

        // Get security info
        var has2FA = await _userManager.GetTwoFactorEnabledAsync(user);

        var viewModel = new StudentSettingsViewModel
        {
            // Account settings
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            Language = user.Language ?? "ar",
            TimeZone = user.TimeZone ?? "Africa/Cairo",
            
            // Profile settings
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Profile?.Bio,
            ProfileVisibility = user.Profile?.ProfileVisibility ?? "Private",
            ShowEmailPublicly = user.Profile?.ShowEmailPublicly ?? false,
            ShowProgressPublicly = user.Profile?.ShowProgressPublicly ?? false,
            ShowBadgesPublicly = user.Profile?.ShowBadgesPublicly ?? true,
            ShowCertificatesPublicly = user.Profile?.ShowCertificatesPublicly ?? true,
            AllowMessages = user.Profile?.AllowMessages ?? true,
            
            // Notification settings
            EmailNotifications = notificationPrefs?.EmailNotifications ?? true,
            PushNotifications = notificationPrefs?.PushNotifications ?? true,
            CourseUpdates = notificationPrefs?.CourseUpdates ?? true,
            AssignmentReminders = notificationPrefs?.AssignmentReminders ?? true,
            WeeklyDigest = notificationPrefs?.WeeklyDigest ?? true,
            
            // Security settings
            TwoFactorEnabled = has2FA,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed
        };

        var allowedTabs = new[] { "account", "notifications", "privacy", "security" };
        if (!string.IsNullOrEmpty(tab) && allowedTabs.Contains(tab))
            ViewBag.ActiveTab = tab;

        return View(viewModel);
    }

    /// <summary>
    /// تحديث إعدادات الحساب - Update account settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccount(AccountSettingsViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });
        if (model == null)
        {
            SetErrorMessage("حدث خطأ في البيانات المدخلة");
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        if (string.IsNullOrEmpty(model.Language) || string.IsNullOrEmpty(model.TimeZone))
        {
            SetErrorMessage("حدث خطأ في البيانات المدخلة");
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        var allowedLanguages = new[] { "ar", "en" };
        var allowedTimeZones = new[] { "Africa/Cairo", "Asia/Riyadh", "Asia/Dubai", "Asia/Kuwait", "UTC" };
        if (!allowedLanguages.Contains(model.Language) || !allowedTimeZones.Contains(model.TimeZone))
        {
            SetErrorMessage("قيم غير صالحة");
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.Language = model.Language;
            user.TimeZone = model.TimeZone;

            if (model.PhoneNumber != null && model.PhoneNumber.Length > 50)
                model.PhoneNumber = model.PhoneNumber.Substring(0, 50);
            if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != user.PhoneNumber)
            {
                user.PhoneNumber = model.PhoneNumber;
                user.PhoneNumberConfirmed = false;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated account settings", userId);
            SetSuccessMessage("تم تحديث إعدادات الحساب بنجاح");
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }
    }

    /// <summary>
    /// تحديث إعدادات الإشعارات - Update notification settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotifications(SettingsNotificationPreferencesViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });
        if (model == null)
        {
            SetErrorMessage("حدث خطأ في البيانات المدخلة");
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }

        try
        {
            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (prefs == null)
            {
                prefs = new Domain.Entities.Notifications.NotificationPreference
                {
                    UserId = userId
                };
                _context.NotificationPreferences.Add(prefs);
            }

            prefs.EmailNotifications = model.EmailNotifications;
            prefs.PushNotifications = model.PushNotifications;
            prefs.CourseUpdates = model.CourseUpdates;
            prefs.AssignmentReminders = model.AssignmentReminders;
            prefs.WeeklyDigest = model.WeeklyDigest;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated notification preferences", userId);
            SetSuccessMessage("تم تحديث إعدادات الإشعارات بنجاح");
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }
    }

    /// <summary>
    /// تحديث إعدادات الخصوصية - Update privacy settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacy(PrivacySettingsUpdateViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });
        if (model == null)
        {
            SetErrorMessage("حدث خطأ في البيانات المدخلة");
            return RedirectToAction(nameof(Index), new { tab = "privacy" });
        }

        var allowedVisibility = new[] { "Public", "Private", "StudentsOnly" };
        if (string.IsNullOrEmpty(model.ProfileVisibility) || !allowedVisibility.Contains(model.ProfileVisibility))
            model.ProfileVisibility = "Private";

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            if (user.Profile == null)
            {
                user.Profile = new UserProfile { UserId = userId };
                _context.UserProfiles.Add(user.Profile);
            }

            user.Profile.ProfileVisibility = model.ProfileVisibility;
            user.Profile.ShowEmailPublicly = model.ShowEmailPublicly;
            user.Profile.ShowProgressPublicly = model.ShowProgressPublicly;
            user.Profile.ShowBadgesPublicly = model.ShowBadgesPublicly;
            user.Profile.ShowCertificatesPublicly = model.ShowCertificatesPublicly;
            user.Profile.AllowMessages = model.AllowMessages;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated privacy settings", userId);
            SetSuccessMessage("تم تحديث إعدادات الخصوصية بنجاح");
            return RedirectToAction(nameof(Index), new { tab = "privacy" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating privacy settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return RedirectToAction(nameof(Index), new { tab = "privacy" });
        }
    }

    /// <summary>
    /// حذف الحساب - Delete account
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(string password)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return NotFound();

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            SetErrorMessage("كلمة المرور غير صحيحة");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // Soft delete - mark as inactive
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {UserId} deleted their account", userId);

            // Sign out
            return RedirectToAction("Logout", "Account", new { area = "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء حذف الحساب");
            return RedirectToAction(nameof(Index));
        }
    }
}

#region View Models

public class StudentSettingsViewModel
{
    // Account
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Language { get; set; } = "ar";
    public string TimeZone { get; set; } = "Africa/Cairo";
    
    // Profile
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string ProfileVisibility { get; set; } = "Private";
    public bool ShowEmailPublicly { get; set; }
    public bool ShowProgressPublicly { get; set; }
    public bool ShowBadgesPublicly { get; set; } = true;
    public bool ShowCertificatesPublicly { get; set; } = true;
    public bool AllowMessages { get; set; } = true;
    
    // Notifications
    public bool EmailNotifications { get; set; }
    public bool PushNotifications { get; set; }
    public bool CourseUpdates { get; set; }
    public bool AssignmentReminders { get; set; }
    public bool WeeklyDigest { get; set; }
    
    // Security
    public bool TwoFactorEnabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
}

public class AccountSettingsViewModel
{
    public string? PhoneNumber { get; set; }
    public string Language { get; set; } = "ar";
    public string TimeZone { get; set; } = "Africa/Cairo";
}

public class SettingsNotificationPreferencesViewModel
{
    public bool EmailNotifications { get; set; }
    public bool PushNotifications { get; set; }
    public bool CourseUpdates { get; set; }
    public bool AssignmentReminders { get; set; }
    public bool WeeklyDigest { get; set; }
}

public class PrivacySettingsUpdateViewModel
{
    public string ProfileVisibility { get; set; } = "Private";
    public bool ShowEmailPublicly { get; set; }
    public bool ShowProgressPublicly { get; set; }
    public bool ShowBadgesPublicly { get; set; } = true;
    public bool ShowCertificatesPublicly { get; set; } = true;
    public bool AllowMessages { get; set; } = true;
}

#endregion

