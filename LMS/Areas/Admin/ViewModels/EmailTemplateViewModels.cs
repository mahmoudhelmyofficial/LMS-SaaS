using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج قالب البريد الإلكتروني - Email Template ViewModel
/// </summary>
public class EmailTemplateCreateViewModel
{
    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز القالب مطلوب")]
    [MaxLength(100)]
    [Display(Name = "رمز القالب")]
    public string TemplateCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان البريد")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى HTML مطلوب")]
    [Display(Name = "محتوى HTML")]
    public string HtmlBody { get; set; } = string.Empty;

    [Display(Name = "محتوى نصي")]
    public string? PlainTextBody { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم المرسل")]
    public string? FromName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    [Display(Name = "بريد المرسل")]
    public string? FromEmail { get; set; }

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    [Display(Name = "الفئة")]
    public string? Category { get; set; }

    [MaxLength(10)]
    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";

    [Display(Name = "محتوى البريد")]
    public string Body { get => HtmlBody; set => HtmlBody = value; }

    [MaxLength(100)]
    [Display(Name = "قالب التنسيق")]
    public string? LayoutTemplate { get; set; }

    [Display(Name = "الافتراضي")]
    public bool IsDefault { get; set; } = false;

    [Display(Name = "تتبع الفتح")]
    public bool TrackOpens { get; set; } = true;

    [Display(Name = "تتبع النقرات")]
    public bool TrackClicks { get; set; } = true;
}

public class EmailTemplateEditViewModel : EmailTemplateCreateViewModel
{
    public int Id { get; set; }
    public int SentCount { get; set; }
    public DateTime? LastSentAt { get; set; }
    public int OpenCount { get; set; }
    public int ClickCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// متغيرات القالب المتاحة:
/// {{UserName}}, {{Email}}, {{FirstName}}, {{LastName}}, {{CourseName}}, 
/// {{InstructorName}}, {{PlatformName}}, {{SupportEmail}}, {{LoginUrl}}, 
/// {{ResetPasswordUrl}}, {{VerificationCode}}, {{OrderNumber}}, {{Amount}}
/// </summary>
public class EmailTemplateTestViewModel
{
    [Required]
    public int TemplateId { get; set; }

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress]
    [Display(Name = "إرسال إلى")]
    public string TestEmail { get; set; } = string.Empty;

    [Display(Name = "اسم القالب")]
    public string? TemplateName { get; set; }

    [Display(Name = "وصف القالب")]
    public string? TemplateDescription { get; set; }

    [Display(Name = "البريد الإلكتروني للمستلمين")]
    public string? RecipientEmails { get; set; }

    [Display(Name = "استخدام التنسيق الفعلي")]
    public bool UseActualLayout { get; set; } = true;

    [Display(Name = "تضمين التتبع")]
    public bool IncludeTracking { get; set; } = true;

    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "عنوان القالب")]
    public string? TemplateSubject { get; set; }

    [Display(Name = "محتوى القالب")]
    public string? TemplateBody { get; set; }
}

