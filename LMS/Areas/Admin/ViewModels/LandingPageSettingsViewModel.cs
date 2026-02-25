using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات الصفحة الرئيسية - Landing page settings view model
/// </summary>
public class LandingPageSettingsViewModel
{
    [Display(Name = "تفعيل الصفحة الرئيسية")]
    public bool Enabled { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط الشعار")]
    public string? LogoUrl { get; set; }

    [MaxLength(20)]
    [Display(Name = "اللون الأساسي")]
    public string PrimaryColor { get; set; } = "#0d6efd";

    [MaxLength(20)]
    [Display(Name = "اللون الثانوي")]
    public string SecondaryColor { get; set; } = "#6c757d";

    [MaxLength(100)]
    [Display(Name = "خط الواجهة")]
    public string? FontFamily { get; set; }

    [MaxLength(200)]
    [Display(Name = "عنوان عنّا (عربي)")]
    public string? AboutTitleAr { get; set; }

    [MaxLength(200)]
    [Display(Name = "عنوان عنّا (إنجليزي)")]
    public string? AboutTitleEn { get; set; }

    [Display(Name = "نص عنّا (عربي)")]
    public string? AboutTextAr { get; set; }

    [Display(Name = "نص عنّا (إنجليزي)")]
    public string? AboutTextEn { get; set; }

    [MaxLength(20)]
    [Display(Name = "اللغة الافتراضية للصفحة الرئيسية")]
    public string DefaultLanguage { get; set; } = "browser";
}
