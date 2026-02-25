using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إرسال إشعار - Send Notification ViewModel
/// </summary>
public class SendNotificationViewModel
{
    /// <summary>
    /// العنوان - Title
    /// </summary>
    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// الرسالة - Message
    /// </summary>
    [Required(ErrorMessage = "الرسالة مطلوبة")]
    [MaxLength(1000)]
    [Display(Name = "الرسالة")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// النوع - Type
    /// </summary>
    [Required]
    [Display(Name = "النوع")]
    public NotificationType Type { get; set; } = NotificationType.System;

    /// <summary>
    /// رابط الإجراء - Action URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط الإجراء")]
    public string? ActionUrl { get; set; }

    /// <summary>
    /// نص الإجراء - Action text
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "نص الإجراء")]
    public string? ActionText { get; set; }

    /// <summary>
    /// المستلمون - Recipients
    /// </summary>
    [Required]
    [Display(Name = "المستلمون")]
    public string Recipients { get; set; } = "All"; // All, Students, Instructors, Specific

    /// <summary>
    /// معرفات المستخدمين المحددين - Specific user IDs
    /// </summary>
    public List<string> SpecificUserIds { get; set; } = new();

    /// <summary>
    /// إرسال بريد إلكتروني - Send email
    /// </summary>
    [Display(Name = "إرسال بريد إلكتروني")]
    public bool SendEmail { get; set; }

    /// <summary>
    /// إرسال SMS - Send SMS
    /// </summary>
    [Display(Name = "إرسال SMS")]
    public bool SendSMS { get; set; }
}

/// <summary>
/// نموذج قالب الإشعار - Notification Template ViewModel
/// </summary>
public class NotificationTemplateViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "المحتوى مطلوب")]
    [Display(Name = "المحتوى")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "نوع الحدث")]
    public string EventType { get; set; } = string.Empty;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

