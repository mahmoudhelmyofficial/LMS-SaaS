using System.ComponentModel.DataAnnotations;
using LMS.Domain.Entities.Security;

namespace LMS.Areas.Admin.ViewModels;

#region Login Logs

/// <summary>
/// Login log filter view model
/// </summary>
public class LoginLogFilterViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool? Success { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
}

#endregion

#region Blocked IP

/// <summary>
/// Blocked IP view model
/// </summary>
public class BlockedIpViewModel
{
    [Required(ErrorMessage = "عنوان IP مطلوب")]
    [Display(Name = "عنوان IP")]
    public string IpAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "السبب مطلوب")]
    [MaxLength(500)]
    [Display(Name = "سبب الحظر")]
    public string Reason { get; set; } = string.Empty;

    [Display(Name = "حظر دائم")]
    public bool IsPermanent { get; set; }

    [Display(Name = "محظور حتى")]
    public DateTime? BlockedUntil { get; set; }

    [Display(Name = "تاريخ انتهاء الصلاحية")]
    public DateTime? ExpiresAt { get => BlockedUntil; set => BlockedUntil = value; }

    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    [MaxLength(1000)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }
}

#endregion

#region Country Restriction

/// <summary>
/// Country restriction view model
/// </summary>
public class CountryRestrictionViewModel
{
    [Required(ErrorMessage = "كود الدولة مطلوب")]
    [MaxLength(10)]
    [Display(Name = "كود الدولة")]
    public string CountryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الدولة مطلوب")]
    [MaxLength(100)]
    [Display(Name = "اسم الدولة")]
    public string CountryName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع القيد مطلوب")]
    [MaxLength(50)]
    [Display(Name = "نوع القيد")]
    public string RestrictionType { get; set; } = "Block"; // Block, AllowOnly

    [MaxLength(500)]
    [Display(Name = "السبب")]
    public string? Reason { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

#endregion

#region Security Dashboard

/// <summary>
/// Security dashboard view model
/// </summary>
public class SecurityDashboardViewModel
{
    public int TotalActiveSessions { get; set; }
    public int FailedLoginsToday { get; set; }
    public int BlockedIpsCount { get; set; }
    public int TwoFactorEnabledUsers { get; set; }
    
    public List<LoginLog> RecentFailedLogins { get; set; } = new();
    public List<BlockedIp> RecentBlockedIps { get; set; } = new();
    public List<SuspiciousActivityViewModel> SuspiciousActivities { get; set; } = new();
}

/// <summary>
/// Suspicious activity view model
/// </summary>
public class SuspiciousActivityViewModel
{
    public string IpAddress { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTime LastAttempt { get; set; }
}

#endregion

#region Two-Factor Authentication

/// <summary>
/// Two-factor settings view model
/// </summary>
public class TwoFactorSettingsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Method { get; set; } = "App"; // App, SMS, Email
}

#endregion

#region Permissions

/// <summary>
/// Permission view model for role-based access control
/// </summary>
public class PermissionViewModel
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

#endregion

#region User Session

/// <summary>
/// User session view model
/// </summary>
public class UserSessionViewModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsActive { get; set; }
}

#endregion

