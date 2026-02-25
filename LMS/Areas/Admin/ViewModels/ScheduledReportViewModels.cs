using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء تقرير مجدول - Create Scheduled Report ViewModel
/// </summary>
public class ScheduledReportCreateViewModel
{
    [Required(ErrorMessage = "اسم التقرير مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم التقرير")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "القالب مطلوب")]
    [Display(Name = "قالب التقرير")]
    public int TemplateId { get; set; }

    [Required(ErrorMessage = "الجدولة مطلوبة")]
    [MaxLength(50)]
    [Display(Name = "الجدولة")]
    public string Schedule { get; set; } = "Daily"; // Daily, Weekly, Monthly

    [Required(ErrorMessage = "المستلمون مطلوبون")]
    [Display(Name = "البريد الإلكتروني للمستلمين")]
    public string RecipientEmails { get; set; } = string.Empty;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج تعديل تقرير مجدول - Edit Scheduled Report ViewModel
/// </summary>
public class ScheduledReportEditViewModel : ScheduledReportCreateViewModel
{
    public int Id { get; set; }

    [Display(Name = "آخر تشغيل")]
    public DateTime? LastRunDate { get; set; }

    [Display(Name = "التشغيل القادم")]
    public DateTime? NextRunDate { get; set; }
}

