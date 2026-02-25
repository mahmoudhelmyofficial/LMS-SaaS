using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// Course instructor view model
/// </summary>
public class CourseInstructorViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "الدورة مطلوبة")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "المدرس مطلوب")]
    [Display(Name = "المدرس")]
    public string InstructorId { get; set; } = string.Empty;

    [Required(ErrorMessage = "الدور مطلوب")]
    [Display(Name = "الدور")]
    [MaxLength(50)]
    public string Role { get; set; } = "co-instructor"; // owner, co-instructor, assistant, guest

    [Display(Name = "نسبة الأرباح (%)")]
    [Range(0, 100)]
    public decimal RevenueSharePercentage { get; set; } = 0;

    #region Permissions

    [Display(Name = "يمكنه تعديل المحتوى")]
    public bool CanEditContent { get; set; } = true;

    [Display(Name = "يمكنه إدارة الطلاب")]
    public bool CanManageStudents { get; set; } = true;

    [Display(Name = "يمكنه تصحيح الواجبات")]
    public bool CanGradeAssignments { get; set; } = true;

    [Display(Name = "يمكنه عمل بث مباشر")]
    public bool CanHostLiveClasses { get; set; } = true;

    [Display(Name = "يمكنه الرد على المناقشات")]
    public bool CanReplyDiscussions { get; set; } = true;

    [Display(Name = "يمكنه إرسال إعلانات")]
    public bool CanSendAnnouncements { get; set; } = true;

    [Display(Name = "يمكنه رؤية التحليلات")]
    public bool CanViewAnalytics { get; set; } = true;

    [Display(Name = "يمكنه إدارة السعر")]
    public bool CanManagePricing { get; set; } = false;

    [Display(Name = "يمكنه إدارة الكوبونات")]
    public bool CanManageCoupons { get; set; } = false;

    #endregion

    [Display(Name = "عنوان مخصص")]
    [MaxLength(100)]
    public string? CustomTitle { get; set; }

    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }
}

/// <summary>
/// Course instructor list item view model
/// </summary>
public class CourseInstructorListViewModel
{
    public int Id { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string InstructorEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal RevenueSharePercentage { get; set; }
    public bool IsPrimaryOwner { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? CustomTitle { get; set; }
}

/// <summary>
/// Instructor permissions template view model
/// </summary>
public class InstructorPermissionsTemplateViewModel
{
    public string TemplateName { get; set; } = string.Empty;
    public bool CanEditContent { get; set; }
    public bool CanManageStudents { get; set; }
    public bool CanGradeAssignments { get; set; }
    public bool CanHostLiveClasses { get; set; }
    public bool CanReplyDiscussions { get; set; }
    public bool CanSendAnnouncements { get; set; }
    public bool CanViewAnalytics { get; set; }
    public bool CanManagePricing { get; set; }
    public bool CanManageCoupons { get; set; }
}

