using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء قالب تقرير - Create Report Template ViewModel
/// </summary>
public class ReportTemplateCreateViewModel
{
    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نوع التقرير مطلوب")]
    [Display(Name = "نوع التقرير")]
    public ReportType ReportType { get; set; }

    [Display(Name = "الفلاتر (JSON)")]
    public string? FiltersJson { get; set; }

    [Display(Name = "الأعمدة (JSON)")]
    public string? ColumnsJson { get; set; }

    [Display(Name = "الترتيب (JSON)")]
    public string? SortingJson { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج تعديل قالب تقرير - Edit Report Template ViewModel
/// </summary>
public class ReportTemplateEditViewModel : ReportTemplateCreateViewModel
{
    public int Id { get; set; }
}

