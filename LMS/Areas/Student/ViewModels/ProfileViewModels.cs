using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج تعديل الملف الشخصي - Edit Profile ViewModel
/// </summary>
public class ProfileEditViewModel
{
    /// <summary>
    /// الاسم الأول - First name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأول يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// الاسم الأخير - Last name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأخير يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// نبذة مختصرة - Bio
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "نبذة عنك")]
    public string? Bio { get; set; }

    /// <summary>
    /// تاريخ الميلاد - Date of birth
    /// </summary>
    [Display(Name = "تاريخ الميلاد")]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// الدولة - Country
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    /// <summary>
    /// المدينة - City
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    /// <summary>
    /// المنطقة الزمنية - Timezone
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "المنطقة الزمنية")]
    public string TimeZone { get; set; } = "Africa/Cairo";

    /// <summary>
    /// اللغة المفضلة - Preferred language
    /// </summary>
    [MaxLength(10)]
    [Display(Name = "اللغة المفضلة")]
    public string Language { get; set; } = "ar";
    
    /// <summary>
    /// نسبة اكتمال الملف الشخصي - Profile completion percentage
    /// </summary>
    public int ProfileCompletionPercentage { get; set; }
}

/// <summary>
/// نموذج إعدادات المستخدم - User Settings ViewModel
/// </summary>
public class UserSettingsViewModel
{
    /// <summary>
    /// إشعارات البريد الإلكتروني - Email notifications
    /// </summary>
    [Display(Name = "إشعارات البريد الإلكتروني")]
    public bool EmailNotifications { get; set; } = true;

    /// <summary>
    /// الإشعارات الفورية - Push notifications
    /// </summary>
    [Display(Name = "الإشعارات الفورية")]
    public bool PushNotifications { get; set; } = true;

    /// <summary>
    /// رسائل التسويق - Marketing emails
    /// </summary>
    [Display(Name = "رسائل العروض والتحديثات")]
    public bool MarketingEmails { get; set; } = false;
}

/// <summary>
/// نموذج عرض الملف الشخصي - Profile Display ViewModel
/// </summary>
public class ProfileDisplayViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }
    public int Points { get; set; }
    public int Level { get; set; }
    public int BadgesCount { get; set; }
    public int CoursesCount { get; set; }
    public int CertificatesCount { get; set; }
    public DateTime MemberSince { get; set; }
}

/// <summary>
/// نموذج إعدادات الأمان - Security Settings ViewModel
/// </summary>
public class SecuritySettingsViewModel
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public List<LoginAttemptViewModel> RecentLoginAttempts { get; set; } = new();
}

/// <summary>
/// نموذج محاولة تسجيل الدخول - Login Attempt ViewModel
/// </summary>
public class LoginAttemptViewModel
{
    public string IpAddress { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string DeviceInfo { get; set; } = string.Empty;
}

/// <summary>
/// نموذج إعدادات الخصوصية - Privacy Settings ViewModel
/// </summary>
public class PrivacySettingsViewModel
{
    public string ProfileVisibility { get; set; } = "Private"; // Public, Private, FriendsOnly
    public bool ShowEmailPublicly { get; set; }
    public bool ShowProgressPublicly { get; set; }
    public bool ShowBadgesPublicly { get; set; } = true;
    public bool ShowCertificatesPublicly { get; set; } = true;
    public bool AllowMessages { get; set; } = true;
}

/// <summary>
/// نموذج الملف الشخصي العام - Public Profile ViewModel
/// </summary>
public class PublicProfileViewModel
{
    public LMS.Domain.Entities.Users.ApplicationUser User { get; set; } = null!;
    public LMS.Services.Interfaces.StudentLearningStats Stats { get; set; } = null!;
    public List<LMS.Domain.Entities.Certifications.Certificate> Certificates { get; set; } = new();
    public List<LMS.Domain.Entities.Gamification.UserBadge> Badges { get; set; } = new();
}

