using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء إعلان - Create Announcement ViewModel
/// </summary>
public class AnnouncementCreateViewModel
{
    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "المحتوى مطلوب")]
    [Display(Name = "المحتوى")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Display(Name = "النوع")]
    public string Type { get; set; } = "General"; // General, System, Maintenance, Update, Event

    [Required]
    [Display(Name = "الأولوية")]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Critical

    [Required]
    [Display(Name = "المستلمون")]
    public string Target { get; set; } = "All"; // All, Students, Instructors, Admins

    [Display(Name = "تاريخ البدء")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "تثبيت في الأعلى")]
    public bool IsPinned { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "إرسال إشعار")]
    public bool SendNotification { get; set; } = true;

    [Display(Name = "إرسال بريد إلكتروني")]
    public bool SendEmail { get; set; }

    /// <summary>
    /// عرض في الصفحة الرئيسية - Show on public landing page
    /// </summary>
    [Display(Name = "عرض في الصفحة الرئيسية")]
    public bool ShowOnLandingPage { get; set; }
}

public class AnnouncementEditViewModel : AnnouncementCreateViewModel
{
    public int Id { get; set; }
    public int ViewsCount { get; set; }
}

public class AnnouncementDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Target { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPinned { get; set; }
    public bool IsActive { get; set; }
    public int ViewsCount { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public bool ShowOnLandingPage { get; set; }
}

