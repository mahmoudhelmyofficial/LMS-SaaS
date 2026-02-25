using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// Content drip rule view model
/// </summary>
public class ContentDripRuleViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    [Display(Name = "الوحدة")]
    public int? ModuleId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Required(ErrorMessage = "نوع الجدولة مطلوب")]
    [Display(Name = "نوع الجدولة")]
    public ContentDripType DripType { get; set; }

    [Display(Name = "عدد الأيام بعد التسجيل")]
    [Range(0, 365, ErrorMessage = "يجب أن يكون بين 0 و 365")]
    public int? DaysAfterEnrollment { get; set; }

    [Display(Name = "تاريخ محدد")]
    public DateTime? SpecificDate { get; set; }

    [Display(Name = "يوم الأسبوع")]
    [Range(0, 6, ErrorMessage = "يوم غير صحيح")]
    public int? DayOfWeek { get; set; }

    [Display(Name = "ساعة الإتاحة")]
    [Range(0, 23, ErrorMessage = "يجب أن تكون بين 0 و 23")]
    public int ReleaseHour { get; set; } = 8;

    [Display(Name = "المنطقة الزمنية")]
    [MaxLength(100)]
    public string? TimeZone { get; set; } = "Africa/Cairo";

    [Display(Name = "إرسال إشعار")]
    public bool SendNotification { get; set; } = true;

    [Display(Name = "عنوان الإشعار")]
    [MaxLength(200)]
    public string? NotificationTitle { get; set; }

    [Display(Name = "محتوى الإشعار")]
    [MaxLength(500)]
    public string? NotificationMessage { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Content drip statistics view model
/// </summary>
public class ContentDripStatisticsViewModel
{
    public int TotalRules { get; set; }
    public int ActiveRules { get; set; }
    public int ScheduledReleases { get; set; }
    public int ReleasedToday { get; set; }
    public List<UpcomingReleaseViewModel> UpcomingReleases { get; set; } = new();
}

/// <summary>
/// Upcoming release view model
/// </summary>
public class UpcomingReleaseViewModel
{
    public string CourseName { get; set; } = string.Empty;
    public string ContentTitle { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public int AffectedStudents { get; set; }
}

