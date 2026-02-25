using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء قالب رسالة نصية - Create SMS Template ViewModel
/// </summary>
public class SmsTemplateCreateViewModel
{
    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// الاسم - Name (alias for TemplateName)
    /// </summary>
    public string Name { get => TemplateName; set => TemplateName = value; }

    [Required(ErrorMessage = "مفتاح القالب مطلوب")]
    [MaxLength(100)]
    [Display(Name = "مفتاح القالب")]
    public string TemplateKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى الرسالة مطلوب")]
    [MaxLength(500)]
    [Display(Name = "محتوى الرسالة")]
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>
    /// الرسالة - Message (alias for MessageContent)
    /// </summary>
    public string Message { get => MessageContent; set => MessageContent = value; }

    [MaxLength(10)]
    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Display(Name = "التصنيف")]
    public string? Category { get; set; }

    [Display(Name = "افتراضي")]
    public bool IsDefault { get; set; } = false;
}

/// <summary>
/// نموذج تعديل قالب رسالة نصية - Edit SMS Template ViewModel
/// </summary>
public class SmsTemplateEditViewModel : SmsTemplateCreateViewModel
{
    public int Id { get; set; }
}

