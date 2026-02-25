using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء إعلان - Create Announcement ViewModel
/// </summary>
public class AnnouncementCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    /// <summary>
    /// عنوان الإعلان - Announcement title
    /// </summary>
    [Required(ErrorMessage = "عنوان الإعلان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "عنوان الإعلان")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// محتوى الإعلان - Announcement content
    /// </summary>
    [Required(ErrorMessage = "محتوى الإعلان مطلوب")]
    [Display(Name = "محتوى الإعلان")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// نوع الإعلان - Announcement type
    /// </summary>
    [Display(Name = "نوع الإعلان")]
    public AnnouncementType AnnouncementType { get; set; } = AnnouncementType.General;

    /// <summary>
    /// الأولوية - Priority
    /// </summary>
    [Display(Name = "الأولوية")]
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    /// <summary>
    /// هل منشور - Is published
    /// </summary>
    [Display(Name = "نشر الآن")]
    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// هل مثبت - Is pinned
    /// </summary>
    [Display(Name = "تثبيت في الأعلى")]
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// إرسال بريد إلكتروني - Send email
    /// </summary>
    [Display(Name = "إرسال بريد إلكتروني للطلاب")]
    public bool SendEmail { get; set; } = false;

    /// <summary>
    /// إرسال إشعار فوري - Send push notification
    /// </summary>
    [Display(Name = "إرسال إشعار فوري")]
    public bool SendPushNotification { get; set; } = true;

    /// <summary>
    /// تاريخ انتهاء الصلاحية - Expires at
    /// </summary>
    [Display(Name = "تاريخ انتهاء الصلاحية")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// رابط مرفق - Attachment URL (optional)
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط مرفق")]
    public string? AttachmentUrl { get; set; }

    /// <summary>
    /// رابط خارجي - External link (optional)
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط خارجي")]
    public string? ExternalLink { get; set; }
}

/// <summary>
/// نموذج تعديل الإعلان - Edit Announcement ViewModel
/// </summary>
public class AnnouncementEditViewModel : AnnouncementCreateViewModel
{
    /// <summary>
    /// المعرف - Announcement ID
    /// </summary>
    public int Id { get; set; }
}

