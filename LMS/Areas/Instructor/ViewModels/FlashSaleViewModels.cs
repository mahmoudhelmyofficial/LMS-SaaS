using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء عرض سريع - Create Flash Sale ViewModel
/// </summary>
public class FlashSaleCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    /// <summary>
    /// اسم العرض - Sale name
    /// </summary>
    [Required(ErrorMessage = "اسم العرض مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم العرض")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// سعر العرض - Discount price
    /// </summary>
    [Required(ErrorMessage = "سعر العرض مطلوب")]
    [Range(0.01, double.MaxValue)]
    [Display(Name = "سعر العرض")]
    public decimal DiscountPrice { get; set; }

    /// <summary>
    /// تاريخ البدء - Start date
    /// </summary>
    [Required(ErrorMessage = "تاريخ البدء مطلوب")]
    [Display(Name = "تاريخ البدء")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// تاريخ الانتهاء - End date
    /// </summary>
    [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
    [Display(Name = "تاريخ الانتهاء")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// الحد الأقصى للكمية - Max quantity
    /// </summary>
    [Display(Name = "الحد الأقصى للكمية")]
    public int? MaxQuantity { get; set; }

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// إظهار العداد التنازلي - Show timer
    /// </summary>
    [Display(Name = "إظهار العداد التنازلي")]
    public bool ShowTimer { get; set; } = true;

    /// <summary>
    /// الأولوية - Priority
    /// </summary>
    [Range(0, 100)]
    [Display(Name = "الأولوية")]
    public int Priority { get; set; } = 0;
}

/// <summary>
/// نموذج تعديل عرض سريع - Edit Flash Sale ViewModel
/// </summary>
public class FlashSaleEditViewModel : FlashSaleCreateViewModel
{
    /// <summary>
    /// معرف العرض - Sale ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// الكمية المباعة - Sold quantity
    /// </summary>
    [Display(Name = "الكمية المباعة")]
    public int SoldQuantity { get; set; }
}

