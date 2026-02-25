using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات الضرائب - Tax settings view model
/// </summary>
public class TaxSettingViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الضريبة مطلوب")]
    [MaxLength(100, ErrorMessage = "اسم الضريبة يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "اسم الضريبة")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نسبة الضريبة مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة الضريبة يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة الضريبة (%)")]
    public decimal TaxRate { get; set; }

    [Display(Name = "نوع الضريبة")]
    public string TaxType { get; set; } = "VAT"; // VAT, Sales, Service, etc.

    [MaxLength(50, ErrorMessage = "رمز الضريبة يجب ألا يتجاوز 50 حرف")]
    [Display(Name = "رمز الضريبة")]
    public string? TaxCode { get; set; }

    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    [Display(Name = "المنطقة/الولاية")]
    public string? Region { get; set; }

    [Display(Name = "مفعلة")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "تطبيق على الأسعار")]
    public bool ApplyToPrice { get; set; } = true;

    [Display(Name = "تاريخ السريان")]
    public DateTime? EffectiveDate { get; set; }

    [MaxLength(500, ErrorMessage = "الوصف يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }
}

/// <summary>
/// نموذج قائمة الإعدادات - Settings list view model
/// </summary>
public class SettingsListViewModel
{
    public bool HasTaxSettings { get; set; }
    public bool HasSmsSettings { get; set; }
    public bool HasVideoSettings { get; set; }
    public bool HasSeoSettings { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// نموذج إعدادات وظائف المنصة - Platform Features Settings ViewModel
/// </summary>
public class PlatformFeaturesViewModel
{
    // Feature Toggles
    public bool EnableRegistration { get; set; } = true;
    public bool EnableInstructorApplication { get; set; } = true;
    public bool EnableReviews { get; set; } = true;
    public bool EnableComments { get; set; } = true;
    public bool EnableDiscussions { get; set; } = true;
    public bool EnableCertificates { get; set; } = true;
    public bool EnableGamification { get; set; } = false;

    // Limits
    public int MaxUploadSizeMB { get; set; } = 50;
    public int MaxStudentsPerCourse { get; set; } = 1000;
    public int MaxVideoLengthMinutes { get; set; } = 180;
}

