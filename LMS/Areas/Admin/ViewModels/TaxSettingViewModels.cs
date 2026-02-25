using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء إعدادات ضريبة - Create Tax Setting ViewModel
/// </summary>
public class TaxSettingCreateViewModel
{
    /// <summary>
    /// اسم الضريبة - Tax name
    /// </summary>
    [Required(ErrorMessage = "اسم الضريبة مطلوب")]
    [MaxLength(100)]
    [Display(Name = "اسم الضريبة")]
    public string TaxName { get; set; } = string.Empty;

    /// <summary>
    /// نسبة الضريبة - Tax rate percentage
    /// </summary>
    [Required(ErrorMessage = "نسبة الضريبة مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة الضريبة يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة الضريبة %")]
    public decimal TaxRate { get; set; }

    /// <summary>
    /// نوع الضريبة - Tax type
    /// </summary>
    [Required(ErrorMessage = "نوع الضريبة مطلوب")]
    [MaxLength(50)]
    [Display(Name = "نوع الضريبة")]
    public string TaxType { get; set; } = "VAT"; // VAT, SalesTax, GST, etc.

    /// <summary>
    /// الدولة - Country
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    /// <summary>
    /// الولاية/المحافظة - State/Province
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "الولاية/المحافظة")]
    public string? State { get; set; }

    /// <summary>
    /// الرمز البريدي - Zip/Postal code
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "الرمز البريدي")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// مفعلة - Is enabled
    /// </summary>
    [Display(Name = "مفعلة")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// افتراضية - Is default
    /// </summary>
    [Display(Name = "افتراضية")]
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// تطبق على المنتجات المادية - Apply to physical products
    /// </summary>
    [Display(Name = "تطبق على المنتجات المادية")]
    public bool ApplyToPhysicalProducts { get; set; } = true;

    /// <summary>
    /// تطبق على المنتجات الرقمية - Apply to digital products
    /// </summary>
    [Display(Name = "تطبق على المنتجات الرقمية")]
    public bool ApplyToDigitalProducts { get; set; } = true;

    /// <summary>
    /// تطبق على الاشتراكات - Apply to subscriptions
    /// </summary>
    [Display(Name = "تطبق على الاشتراكات")]
    public bool ApplyToSubscriptions { get; set; } = true;

    /// <summary>
    /// رقم التسجيل الضريبي - Tax ID number
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "رقم التسجيل الضريبي")]
    public string? TaxIdNumber { get; set; }

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }
}

/// <summary>
/// نموذج تعديل إعدادات الضريبة - Edit Tax Setting ViewModel
/// </summary>
public class TaxSettingEditViewModel : TaxSettingCreateViewModel
{
    /// <summary>
    /// المعرف - ID
    /// </summary>
    public int Id { get; set; }
}

