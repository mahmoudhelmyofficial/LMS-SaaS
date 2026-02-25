using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج طريقة السحب - Withdrawal method view model
/// </summary>
public class WithdrawalMethodViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الطريقة مطلوب")]
    [MaxLength(100, ErrorMessage = "اسم الطريقة يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "اسم الطريقة")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم العرض مطلوب")]
    [MaxLength(100, ErrorMessage = "اسم العرض يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "اسم العرض")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الطريقة مطلوب")]
    [Display(Name = "نوع الطريقة")]
    public WithdrawalMethodType MethodType { get; set; }

    [MaxLength(500, ErrorMessage = "الوصف يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Url(ErrorMessage = "رابط الأيقونة غير صالح")]
    [MaxLength(500, ErrorMessage = "رابط الأيقونة يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "رابط الأيقونة")]
    public string? IconUrl { get; set; }

    [Display(Name = "مفعلة")]
    public bool IsEnabled { get; set; } = true;

    [Required(ErrorMessage = "الحد الأدنى للسحب مطلوب")]
    [Range(0.01, 1000000, ErrorMessage = "الحد الأدنى للسحب يجب أن يكون بين 0.01 و 1000000")]
    [Display(Name = "الحد الأدنى للسحب")]
    public decimal MinAmount { get; set; } = 100;

    [Required(ErrorMessage = "الحد الأقصى للسحب مطلوب")]
    [Range(1, 10000000, ErrorMessage = "الحد الأقصى للسحب يجب أن يكون بين 1 و 10000000")]
    [Display(Name = "الحد الأقصى للسحب")]
    public decimal MaxAmount { get; set; } = 50000;

    [Range(0, 100, ErrorMessage = "نسبة الرسوم يجب أن تكون بين 0 و 100")]
    [Display(Name = "نسبة الرسوم (%)")]
    public decimal FeePercentage { get; set; } = 0;

    [Range(0, 10000, ErrorMessage = "الرسوم الثابتة يجب أن تكون بين 0 و 10000")]
    [Display(Name = "رسوم ثابتة")]
    public decimal FixedFee { get; set; } = 0;

    [Required(ErrorMessage = "العملات المدعومة مطلوبة")]
    [Display(Name = "العملات المدعومة (JSON Array)")]
    public string SupportedCurrencies { get; set; } = "[\"EGP\"]";

    [Required(ErrorMessage = "وقت المعالجة مطلوب")]
    [MaxLength(200, ErrorMessage = "وقت المعالجة يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "وقت المعالجة المتوقع")]
    public string ProcessingTime { get; set; } = "3-5 أيام عمل";

    [Required(ErrorMessage = "الحقول المطلوبة مطلوبة")]
    [Display(Name = "الحقول المطلوبة (JSON Schema)")]
    public string RequiredFields { get; set; } = "[]";

    [MaxLength(2000, ErrorMessage = "التعليمات يجب ألا تتجاوز 2000 حرف")]
    [Display(Name = "تعليمات الاستخدام")]
    public string? Instructions { get; set; }

    [Range(0, 1000, ErrorMessage = "ترتيب العرض يجب أن يكون بين 0 و 1000")]
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    // For display purposes
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج قائمة طرق السحب - Withdrawal methods list view model
/// </summary>
public class WithdrawalMethodListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WithdrawalMethodType MethodType { get; set; }
    public string? IconUrl { get; set; }
    public bool IsEnabled { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public decimal FeePercentage { get; set; }
    public decimal FixedFee { get; set; }
    public int UsageCount { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج إحصائيات طرق السحب - Withdrawal method statistics view model
/// </summary>
public class WithdrawalMethodStatsViewModel
{
    public int MethodId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public int ApprovedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalFees { get; set; }
}

