using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء قاعدة جدولة محتوى - Create Content Drip Rule ViewModel
/// </summary>
public class ContentDripCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    /// <summary>
    /// معرف الوحدة - Module ID
    /// </summary>
    [Display(Name = "الوحدة")]
    public int? ModuleId { get; set; }

    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    /// <summary>
    /// نوع الجدولة - Drip type
    /// </summary>
    [Required(ErrorMessage = "نوع الجدولة مطلوب")]
    [Display(Name = "نوع الجدولة")]
    public ContentDripType DripType { get; set; }

    /// <summary>
    /// عدد الأيام - Days after enrollment
    /// </summary>
    [Display(Name = "عدد الأيام بعد التسجيل")]
    public int? DaysAfterEnrollment { get; set; }

    /// <summary>
    /// تاريخ محدد - Specific date
    /// </summary>
    [Display(Name = "تاريخ محدد")]
    public DateTime? SpecificDate { get; set; }

    /// <summary>
    /// يوم الأسبوع - Day of week
    /// </summary>
    [Display(Name = "يوم الأسبوع")]
    [Range(0, 6, ErrorMessage = "يوم الأسبوع يجب أن يكون بين 0 و 6")]
    public int? DayOfWeek { get; set; }

    /// <summary>
    /// ساعة الإتاحة - Release hour
    /// </summary>
    [Required]
    [Range(0, 23, ErrorMessage = "الساعة يجب أن تكون بين 0 و 23")]
    [Display(Name = "ساعة الإتاحة")]
    public int ReleaseHour { get; set; } = 8;

    /// <summary>
    /// المنطقة الزمنية - Timezone
    /// </summary>
    [Required(ErrorMessage = "المنطقة الزمنية مطلوبة")]
    [Display(Name = "المنطقة الزمنية")]
    public string TimeZone { get; set; } = "Egypt Standard Time";

    /// <summary>
    /// إرسال إشعار - Send notification
    /// </summary>
    [Display(Name = "إرسال إشعار عند الإتاحة")]
    public bool SendNotification { get; set; } = true;

    /// <summary>
    /// عنوان الإشعار - Notification title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان الإشعار")]
    public string? NotificationTitle { get; set; }

    /// <summary>
    /// محتوى الإشعار - Notification message
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "محتوى الإشعار")]
    public string? NotificationMessage { get; set; }

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج تعديل قاعدة جدولة محتوى - Edit Content Drip Rule ViewModel
/// </summary>
public class ContentDripEditViewModel : ContentDripCreateViewModel
{
    /// <summary>
    /// معرف القاعدة - Rule ID
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// نموذج إحصائيات جدولة المحتوى - Content Drip Statistics ViewModel
/// </summary>
public class ContentDripStatisticsViewModel
{
    public int TotalRules { get; set; }
    public int ActiveRules { get; set; }
    public int InactiveRules { get; set; }
    public Dictionary<string, int> RulesByType { get; set; } = new();
}

