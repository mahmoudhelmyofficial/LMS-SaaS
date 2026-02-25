using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء قيد جغرافي - Create Country Restriction ViewModel
/// </summary>
public class CountryRestrictionCreateViewModel
{
    [Required(ErrorMessage = "رمز الدولة مطلوب")]
    [MaxLength(2)]
    [Display(Name = "رمز الدولة")]
    public string CountryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الدولة مطلوب")]
    [MaxLength(100)]
    [Display(Name = "اسم الدولة")]
    public string CountryName { get; set; } = string.Empty;

    [Display(Name = "محظورة")]
    public bool IsBlocked { get; set; }

    [MaxLength(500)]
    [Display(Name = "السبب")]
    public string? Reason { get; set; }
}

/// <summary>
/// نموذج تعديل قيد جغرافي - Edit Country Restriction ViewModel
/// </summary>
public class CountryRestrictionEditViewModel : CountryRestrictionCreateViewModel
{
    public int Id { get; set; }
}

