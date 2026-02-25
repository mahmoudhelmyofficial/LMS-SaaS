using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء تصنيف جديد - Create Category ViewModel
/// </summary>
public class CategoryCreateViewModel
{
    /// <summary>
    /// اسم التصنيف - Category name
    /// </summary>
    [Required(ErrorMessage = "اسم التصنيف مطلوب")]
    [MaxLength(200, ErrorMessage = "اسم التصنيف يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "اسم التصنيف")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Category description
    /// </summary>
    [MaxLength(500, ErrorMessage = "الوصف يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// صورة الأيقونة - Icon URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط الأيقونة")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// صورة الغلاف - Cover image URL
    /// </summary>
    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "صورة الغلاف")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// التصنيف الأب - Parent category ID
    /// </summary>
    [Display(Name = "التصنيف الأب")]
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// هل مميز - Is featured
    /// </summary>
    [Display(Name = "مميز")]
    public bool IsFeatured { get; set; } = false;

    /// <summary>
    /// لون التصنيف - Category color
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "اللون")]
    public string? Color { get; set; }
}

/// <summary>
/// نموذج تعديل التصنيف - Edit Category ViewModel
/// </summary>
public class CategoryEditViewModel : CategoryCreateViewModel
{
    /// <summary>
    /// معرف التصنيف - Category ID
    /// </summary>
    public int Id { get; set; }
}

