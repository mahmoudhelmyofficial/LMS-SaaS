using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء خطة اشتراك - Create Subscription Plan ViewModel
/// </summary>
public class SubscriptionPlanCreateViewModel
{
    /// <summary>
    /// اسم الخطة - Plan name
    /// </summary>
    [Required(ErrorMessage = "اسم الخطة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم الخطة")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Required(ErrorMessage = "السعر مطلوب")]
    [Range(0, double.MaxValue)]
    [Display(Name = "السعر")]
    public decimal Price { get; set; }

    /// <summary>
    /// فترة الاشتراك بالأيام - Duration in days
    /// </summary>
    [Required(ErrorMessage = "فترة الاشتراك مطلوبة")]
    [Range(1, 3650)]
    [Display(Name = "فترة الاشتراك (أيام)")]
    public int DurationDays { get; set; } = 30;

    /// <summary>
    /// عدد الدورات - Courses limit
    /// </summary>
    [Display(Name = "عدد الدورات (اتركه فارغاً لعدد غير محدود)")]
    public int? CoursesLimit { get; set; }

    /// <summary>
    /// الوصول للمحتوى المميز - Access to premium content
    /// </summary>
    [Display(Name = "الوصول للمحتوى المميز")]
    public bool AccessToPremiumContent { get; set; }

    /// <summary>
    /// الوصول للبث المباشر - Access to live classes
    /// </summary>
    [Display(Name = "الوصول للبث المباشر")]
    public bool AccessToLiveClasses { get; set; }

    /// <summary>
    /// الدعم الفني ذو الأولوية - Priority support
    /// </summary>
    [Display(Name = "الدعم الفني ذو الأولوية")]
    public bool PrioritySupport { get; set; }

    /// <summary>
    /// الشهادات - Certificates included
    /// </summary>
    [Display(Name = "الشهادات مضمنة")]
    public bool CertificatesIncluded { get; set; } = true;

    /// <summary>
    /// فترة تجريبية بالأيام - Trial period days
    /// </summary>
    [Range(0, 365)]
    [Display(Name = "فترة تجريبية (أيام)")]
    public int TrialPeriodDays { get; set; } = 0;

    /// <summary>
    /// هل نشطة - Is active
    /// </summary>
    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// هل مميزة - Is featured
    /// </summary>
    [Display(Name = "مميزة")]
    public bool IsFeatured { get; set; }

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// نموذج تعديل خطة الاشتراك - Edit Subscription Plan ViewModel
/// </summary>
public class SubscriptionPlanEditViewModel : SubscriptionPlanCreateViewModel
{
    /// <summary>
    /// معرف الخطة - Plan ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عدد المشتركين الحاليين - Current subscribers count
    /// </summary>
    [Display(Name = "عدد المشتركين")]
    public int ActiveSubscribersCount { get; set; }
}

/// <summary>
/// نموذج عرض الاشتراك - Subscription Display ViewModel
/// </summary>
public class SubscriptionDisplayViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Currency { get; set; } = "EGP";
    public bool IsAutoRenew { get; set; }
}

