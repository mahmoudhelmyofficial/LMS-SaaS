using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات العمولة - Commission setting view model
/// </summary>
public class CommissionSettingViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الاسم مطلوب")]
    [MaxLength(200, ErrorMessage = "الاسم يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "الاسم")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الإعداد مطلوب")]
    [Display(Name = "نوع الإعداد")]
    public string Type { get; set; } = "global"; // global, category, course, instructor

    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "المدرس")]
    public string? InstructorId { get; set; }

    [Required(ErrorMessage = "نسبة المنصة مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المنصة يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المنصة (%)")]
    public decimal PlatformRate { get; set; } = 30;

    [Required(ErrorMessage = "نسبة المدرس مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المدرس يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المدرس (%)")]
    public decimal InstructorRate { get; set; } = 70;

    [Required(ErrorMessage = "فترة الانتظار مطلوبة")]
    [Range(0, 365, ErrorMessage = "فترة الانتظار يجب أن تكون بين 0 و 365 يوم")]
    [Display(Name = "فترة الانتظار (بالأيام)")]
    public int HoldPeriodDays { get; set; } = 14;

    [Display(Name = "مفعل")]
    public bool IsActive { get; set; } = true;

    [Range(0, 100, ErrorMessage = "الأولوية يجب أن تكون بين 0 و 100")]
    [Display(Name = "الأولوية")]
    public int Priority { get; set; } = 0;

    [Display(Name = "تاريخ البداية")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? EndDate { get; set; }

    [MaxLength(1000, ErrorMessage = "الملاحظات يجب ألا تتجاوز 1000 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    // For display purposes
    public string? CategoryName { get; set; }
    public string? CourseName { get; set; }
    public string? InstructorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج قائمة إعدادات العمولة - Commission settings list view model
/// </summary>
public class CommissionSettingListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? CourseName { get; set; }
    public string? InstructorName { get; set; }
    public decimal PlatformRate { get; set; }
    public decimal InstructorRate { get; set; }
    public int HoldPeriodDays { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج معاينة العمولة - Commission preview view model
/// </summary>
public class CommissionPreviewViewModel
{
    [Required(ErrorMessage = "مبلغ البيع مطلوب")]
    [Range(0.01, 1000000, ErrorMessage = "مبلغ البيع يجب أن يكون بين 0.01 و 1000000")]
    [Display(Name = "مبلغ البيع")]
    public decimal SaleAmount { get; set; }

    [Display(Name = "معرف الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "معرف المدرس")]
    public string? InstructorId { get; set; }

    // Calculation results
    public decimal PlatformCommission { get; set; }
    public decimal InstructorEarning { get; set; }
    public decimal PlatformRate { get; set; }
    public decimal InstructorRate { get; set; }
    public int HoldPeriodDays { get; set; }
    public DateTime AvailableDate { get; set; }
    public string AppliedSettingName { get; set; } = string.Empty;
    public string AppliedSettingType { get; set; } = string.Empty;
}

/// <summary>
/// نموذج إنشاء إعداد عمولة - Commission setting create view model
/// </summary>
public class CommissionSettingCreateViewModel
{
    [Required(ErrorMessage = "الاسم مطلوب")]
    [MaxLength(200, ErrorMessage = "الاسم يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "الاسم")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الإعداد مطلوب")]
    [Display(Name = "نوع الإعداد")]
    public string Type { get; set; } = "global"; // global, category, course, instructor

    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "المدرس")]
    public string? InstructorId { get; set; }

    [Required(ErrorMessage = "نسبة المنصة مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المنصة يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المنصة (%)")]
    public decimal PlatformRate { get; set; } = 30;

    [Required(ErrorMessage = "نسبة المدرس مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المدرس يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المدرس (%)")]
    public decimal InstructorRate { get; set; } = 70;

    [Required(ErrorMessage = "فترة الانتظار مطلوبة")]
    [Range(0, 365, ErrorMessage = "فترة الانتظار يجب أن تكون بين 0 و 365 يوم")]
    [Display(Name = "فترة الانتظار (بالأيام)")]
    public int HoldPeriodDays { get; set; } = 14;

    [Display(Name = "مفعل")]
    public bool IsActive { get; set; } = true;

    [Range(0, 100, ErrorMessage = "الأولوية يجب أن تكون بين 0 و 100")]
    [Display(Name = "الأولوية")]
    public int Priority { get; set; } = 0;

    [Display(Name = "تاريخ البداية")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? EndDate { get; set; }

    [MaxLength(1000, ErrorMessage = "الملاحظات يجب ألا تتجاوز 1000 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// الحد الأدنى لمبلغ البيع - Minimum sale amount
    /// </summary>
    [Display(Name = "الحد الأدنى لمبلغ البيع")]
    public decimal? MinimumSaleAmount { get; set; }
}

/// <summary>
/// نموذج تعديل إعداد عمولة - Commission setting edit view model
/// </summary>
public class CommissionSettingEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الاسم مطلوب")]
    [MaxLength(200, ErrorMessage = "الاسم يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "الاسم")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الإعداد مطلوب")]
    [Display(Name = "نوع الإعداد")]
    public string Type { get; set; } = "global"; // global, category, course, instructor

    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "المدرس")]
    public string? InstructorId { get; set; }

    [Required(ErrorMessage = "نسبة المنصة مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المنصة يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المنصة (%)")]
    public decimal PlatformRate { get; set; } = 30;

    [Required(ErrorMessage = "نسبة المدرس مطلوبة")]
    [Range(0, 100, ErrorMessage = "نسبة المدرس يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة المدرس (%)")]
    public decimal InstructorRate { get; set; } = 70;

    [Required(ErrorMessage = "فترة الانتظار مطلوبة")]
    [Range(0, 365, ErrorMessage = "فترة الانتظار يجب أن تكون بين 0 و 365 يوم")]
    [Display(Name = "فترة الانتظار (بالأيام)")]
    public int HoldPeriodDays { get; set; } = 14;

    [Display(Name = "مفعل")]
    public bool IsActive { get; set; } = true;

    [Range(0, 100, ErrorMessage = "الأولوية يجب أن تكون بين 0 و 100")]
    [Display(Name = "الأولوية")]
    public int Priority { get; set; } = 0;

    [Display(Name = "تاريخ البداية")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? EndDate { get; set; }

    [MaxLength(1000, ErrorMessage = "الملاحظات يجب ألا تتجاوز 1000 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// الحد الأدنى لمبلغ البيع - Minimum sale amount
    /// </summary>
    [Display(Name = "الحد الأدنى لمبلغ البيع")]
    public decimal? MinimumSaleAmount { get; set; }
}

