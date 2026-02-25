using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء كوبون - Create Coupon ViewModel
/// </summary>
public class CouponCreateViewModel
{
    /// <summary>
    /// كود الكوبون - Coupon code
    /// </summary>
    [Required(ErrorMessage = "كود الكوبون مطلوب")]
    [MaxLength(50)]
    [Display(Name = "كود الكوبون")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// نوع الخصم - Discount type
    /// </summary>
    [Required(ErrorMessage = "نوع الخصم مطلوب")]
    [Display(Name = "نوع الخصم")]
    public DiscountType DiscountType { get; set; }

    /// <summary>
    /// قيمة الخصم - Discount value
    /// </summary>
    [Required(ErrorMessage = "قيمة الخصم مطلوبة")]
    [Range(0.01, double.MaxValue, ErrorMessage = "قيمة الخصم يجب أن تكون أكبر من صفر")]
    [Display(Name = "قيمة الخصم")]
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// الحد الأقصى للخصم - Maximum discount amount
    /// </summary>
    [Display(Name = "الحد الأقصى للخصم")]
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>
    /// الحد الأدنى للشراء - Minimum purchase amount
    /// </summary>
    [Display(Name = "الحد الأدنى للشراء")]
    public decimal? MinimumPurchaseAmount { get; set; }

    /// <summary>
    /// الدورات المطبقة - Selected course IDs
    /// </summary>
    [Display(Name = "الدورات المطبقة")]
    public List<int>? SelectedCourseIds { get; set; }

    /// <summary>
    /// الحد الأقصى للاستخدام - Maximum uses
    /// </summary>
    [Display(Name = "الحد الأقصى للاستخدام")]
    public int? MaxUses { get; set; }

    /// <summary>
    /// الحد الأقصى لكل مستخدم - Max uses per user
    /// </summary>
    [Required]
    [Range(1, 100)]
    [Display(Name = "الحد الأقصى لكل مستخدم")]
    public int MaxUsesPerUser { get; set; } = 1;

    /// <summary>
    /// تاريخ البدء - Valid from
    /// </summary>
    [Required(ErrorMessage = "تاريخ البدء مطلوب")]
    [Display(Name = "تاريخ البدء")]
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// تاريخ الانتهاء - Valid to
    /// </summary>
    [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
    [Display(Name = "تاريخ الانتهاء")]
    public DateTime ValidTo { get; set; }

    /// <summary>
    /// للمشتريات الأولى فقط - First purchase only
    /// </summary>
    [Display(Name = "للمشتريات الأولى فقط")]
    public bool FirstPurchaseOnly { get; set; }

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج تعديل الكوبون - Edit Coupon ViewModel
/// </summary>
public class CouponEditViewModel : CouponCreateViewModel
{
    /// <summary>
    /// معرف الكوبون - Coupon ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عدد الاستخدامات - Used count
    /// </summary>
    [Display(Name = "عدد الاستخدامات")]
    public int UsedCount { get; set; }
}

/// <summary>
/// نموذج عرض الكوبون - Coupon Display ViewModel
/// </summary>
public class CouponDisplayViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public int UsedCount { get; set; }
    public int? MaxUses { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; }
    public CouponStatus Status { get; set; }
}

