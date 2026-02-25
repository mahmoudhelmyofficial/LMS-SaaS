using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج قالب الشهادة - Certificate Template ViewModel
/// </summary>
public class CertificateTemplateCreateViewModel
{
    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "HTML المحتوى مطلوب")]
    [Display(Name = "محتوى HTML")]
    public string HtmlContent { get; set; } = string.Empty;

    [Required(ErrorMessage = "CSS الأنماط مطلوبة")]
    [Display(Name = "أنماط CSS")]
    public string CssStyles { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "صورة الخلفية")]
    public string? BackgroundImageUrl { get; set; }

    [Display(Name = "العرض (px)")]
    public int Width { get; set; } = 800;

    [Display(Name = "الارتفاع (px)")]
    public int Height { get; set; } = 600;

    [Display(Name = "الاتجاه")]
    public string Orientation { get; set; } = "Landscape"; // Landscape, Portrait

    [Display(Name = "القالب الافتراضي")]
    public bool IsDefault { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

public class CertificateTemplateEditViewModel : CertificateTemplateCreateViewModel
{
    public int Id { get; set; }
    public int UsageCount { get; set; }
}

/// <summary>
/// متغيرات القالب المتاحة
/// Available template variables:
/// {{StudentName}}, {{CourseName}}, {{InstructorName}}, {{CompletionDate}}, 
/// {{CertificateNumber}}, {{Grade}}, {{Duration}}, {{Platform}}
/// </summary>
public class CertificateTemplatePreviewViewModel
{
    public int TemplateId { get; set; }
    public string StudentName { get; set; } = "محمد أحمد";
    public string CourseName { get; set; } = "دورة تطوير تطبيقات الويب";
    public string InstructorName { get; set; } = "د. أحمد محمد";
    public DateTime CompletionDate { get; set; } = DateTime.Now;
    public string CertificateNumber { get; set; } = "CERT-2025-001234";
    public decimal Grade { get; set; } = 95;
}

