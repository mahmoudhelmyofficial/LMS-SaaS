using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج تعديل ملف المدرس - Instructor Profile Edit ViewModel
/// </summary>
public class InstructorProfileEditViewModel
{
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(100)]
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(100)]
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "نبذة عنك")]
    public string? Bio { get; set; }

    [Display(Name = "السيرة الذاتية للمدرس")]
    public string? InstructorBio { get; set; }

    [MaxLength(200)]
    [Display(Name = "العنوان المهني")]
    public string? Headline { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [MaxLength(100)]
    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    [MaxLength(100)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    [MaxLength(100)]
    [Display(Name = "المنطقة الزمنية")]
    public string TimeZone { get; set; } = "Africa/Cairo";

    [MaxLength(10)]
    [Display(Name = "اللغة المفضلة")]
    public string Language { get; set; } = "ar";
}

/// <summary>
/// نموذج إعدادات الدفع للمدرس - Instructor Payment Settings ViewModel
/// </summary>
public class InstructorPaymentSettingsViewModel
{
    [MaxLength(50)]
    [Display(Name = "طريقة الدفع المفضلة")]
    public string? PayoutMethod { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "الرجاء إدخال بريد إلكتروني صحيح")]
    [Display(Name = "بريد PayPal")]
    public string? PayPalEmail { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم البنك")]
    public string? BankName { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم صاحب الحساب")]
    public string? BankAccountName { get; set; }

    [MaxLength(100)]
    [Display(Name = "رقم الحساب البنكي")]
    public string? BankAccountNumber { get; set; }

    [MaxLength(50)]
    [Display(Name = "رمز IBAN")]
    public string? IBAN { get; set; }

    [MaxLength(20)]
    [Display(Name = "رمز SWIFT")]
    public string? SwiftCode { get; set; }

    [MaxLength(20)]
    [Display(Name = "رقم المحفظة الإلكترونية")]
    public string? MobileWalletNumber { get; set; }

    [MaxLength(50)]
    [Display(Name = "مزود المحفظة")]
    public string? MobileWalletProvider { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "الرجاء إدخال بريد إلكتروني صحيح")]
    [Display(Name = "بريد Wise")]
    public string? WiseEmail { get; set; }

    [MaxLength(200)]
    [Display(Name = "معرف حساب Stripe")]
    public string? StripeAccountId { get; set; }
}

/// <summary>
/// نموذج الإعدادات العامة للمدرس - Instructor General Settings ViewModel
/// </summary>
public class InstructorSettingsViewModel
{
    // Profile Information
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(100)]
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(100)]
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "نبذة عنك")]
    public string? Bio { get; set; }

    [Display(Name = "السيرة الذاتية للمدرس")]
    public string? InstructorBio { get; set; }

    [MaxLength(200)]
    [Display(Name = "العنوان المهني")]
    public string? Headline { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [MaxLength(100)]
    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    [MaxLength(100)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    [MaxLength(100)]
    [Display(Name = "المنطقة الزمنية")]
    public string TimeZone { get; set; } = "Africa/Cairo";

    [MaxLength(10)]
    [Display(Name = "اللغة المفضلة")]
    public string Language { get; set; } = "ar";

    // Payment Settings
    [MaxLength(50)]
    [Display(Name = "طريقة الدفع المفضلة")]
    public string? PayoutMethod { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "الرجاء إدخال بريد إلكتروني صحيح")]
    [Display(Name = "بريد PayPal")]
    public string? PayPalEmail { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم البنك")]
    public string? BankName { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم صاحب الحساب")]
    public string? BankAccountName { get; set; }

    [MaxLength(100)]
    [Display(Name = "رقم الحساب البنكي")]
    public string? BankAccountNumber { get; set; }

    [MaxLength(50)]
    [Display(Name = "رمز IBAN")]
    public string? IBAN { get; set; }

    [MaxLength(20)]
    [Display(Name = "رمز SWIFT")]
    public string? SwiftCode { get; set; }

    // Notification Preferences
    [Display(Name = "إشعارات البريد الإلكتروني")]
    public bool EmailNotifications { get; set; } = true;

    // Read-only stats
    public decimal TotalEarnings { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal CommissionRate { get; set; }
    public bool IsApproved { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalStudents { get; set; }
}

/// <summary>
/// نموذج روابط التواصل - Social Links ViewModel
/// </summary>
public class InstructorSocialLinksViewModel
{
    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "فيسبوك")]
    public string? FacebookUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "تويتر")]
    public string? TwitterUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "لينكدإن")]
    public string? LinkedInUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "يوتيوب")]
    public string? YouTubeUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "انستجرام")]
    public string? InstagramUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "جيتهاب")]
    public string? GitHubUrl { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "تيك توك")]
    public string? TikTokUrl { get; set; }
}

/// <summary>
/// نموذج تفضيلات الإشعارات - Notification Preferences ViewModel
/// </summary>
public class InstructorNotificationPreferencesViewModel
{
    [Display(Name = "إشعار عند تسجيل طالب جديد")]
    public bool EmailOnNewEnrollment { get; set; } = true;

    [Display(Name = "إشعار عند تقييم جديد")]
    public bool EmailOnNewReview { get; set; } = true;

    [Display(Name = "إشعار عند سؤال جديد")]
    public bool EmailOnNewQuestion { get; set; } = true;

    [Display(Name = "إشعار عند رسالة جديدة")]
    public bool EmailOnNewMessage { get; set; } = true;

    [Display(Name = "إشعار عند استلام دفعة")]
    public bool EmailOnPaymentReceived { get; set; } = true;

    [Display(Name = "إشعار عند معالجة السحب")]
    public bool EmailOnWithdrawalProcessed { get; set; } = true;

    [Display(Name = "ملخص أسبوعي")]
    public bool EmailWeeklyDigest { get; set; } = true;

    [Display(Name = "تقرير شهري")]
    public bool EmailMonthlyReport { get; set; } = true;

    [Display(Name = "تحديثات تسويقية")]
    public bool EmailMarketingUpdates { get; set; } = false;

    [Display(Name = "الإشعارات الفورية")]
    public bool PushNotifications { get; set; } = true;

    [Display(Name = "إشعارات داخل التطبيق")]
    public bool InAppNotifications { get; set; } = true;
}

/// <summary>
/// نموذج إعدادات الأمان - Security Settings ViewModel
/// </summary>
public class InstructorSecurityViewModel
{
    public bool TwoFactorEnabled { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public string? LastLoginIp { get; set; }
    public int ActiveSessionsCount { get; set; }
    public List<LoginHistoryItemViewModel> LoginHistory { get; set; } = new();
}

/// <summary>
/// نموذج سجل تسجيل الدخول - Login History Item ViewModel
/// </summary>
public class LoginHistoryItemViewModel
{
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public bool IsSuccessful { get; set; }
}

/// <summary>
/// نموذج تغيير كلمة المرور - Change Password ViewModel
/// </summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل")]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "كلمة المرور غير متطابقة")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "تسجيل الخروج من الأجهزة الأخرى")]
    public bool LogoutOtherSessions { get; set; } = true;
}

