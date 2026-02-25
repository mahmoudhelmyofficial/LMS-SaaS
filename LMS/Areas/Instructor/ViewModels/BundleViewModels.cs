using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء باقة - Create Bundle ViewModel
/// </summary>
public class BundleCreateViewModel
{
    /// <summary>
    /// اسم الباقة - Bundle name
    /// </summary>
    [Required(ErrorMessage = "اسم الباقة مطلوب")]
    [MaxLength(300)]
    [Display(Name = "اسم الباقة")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// وصف مختصر - Short description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف مختصر")]
    public string? ShortDescription { get; set; }

    /// <summary>
    /// الوصف الكامل - Full description
    /// </summary>
    [Display(Name = "الوصف الكامل")]
    public string? Description { get; set; }

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة الغلاف")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Required(ErrorMessage = "السعر مطلوب")]
    [Range(0, double.MaxValue)]
    [Display(Name = "السعر")]
    public decimal Price { get; set; }

    /// <summary>
    /// الدورات - Course IDs
    /// </summary>
    [Required(ErrorMessage = "يرجى اختيار دورة واحدة على الأقل")]
    [Display(Name = "الدورات")]
    public List<int> CourseIds { get; set; } = new();

    /// <summary>
    /// هل نشطة - Is active
    /// </summary>
    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// تاريخ البدء - Valid from
    /// </summary>
    [Display(Name = "تاريخ البدء")]
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// تاريخ الانتهاء - Valid to
    /// </summary>
    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// الحد الأقصى للمبيعات - Max sales
    /// </summary>
    [Display(Name = "الحد الأقصى للمبيعات")]
    public int? MaxSales { get; set; }
}

/// <summary>
/// نموذج تعديل الباقة - Edit Bundle ViewModel
/// </summary>
public class BundleEditViewModel : BundleCreateViewModel
{
    /// <summary>
    /// معرف الباقة - Bundle ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عدد المبيعات - Sales count
    /// </summary>
    [Display(Name = "عدد المبيعات")]
    public int SalesCount { get; set; }

    /// <summary>
    /// هل مميزة - Is featured
    /// </summary>
    [Display(Name = "مميزة")]
    public bool IsFeatured { get; set; }
}

