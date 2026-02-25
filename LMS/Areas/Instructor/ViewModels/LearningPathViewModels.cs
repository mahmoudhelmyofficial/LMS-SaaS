using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء مسار تعلم - Create Learning Path ViewModel
/// </summary>
public class LearningPathCreateViewModel
{
    /// <summary>
    /// الاسم - Name
    /// </summary>
    [Required(ErrorMessage = "اسم المسار مطلوب")]
    [MaxLength(300)]
    [Display(Name = "اسم المسار")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// الوصف القصير - Short description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف القصير")]
    public string? ShortDescription { get; set; }

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة الغلاف")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// صورة البانر - Banner URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة البانر")]
    public string? BannerUrl { get; set; }

    /// <summary>
    /// المستوى - Level
    /// </summary>
    [Required]
    [Display(Name = "المستوى")]
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Display(Name = "السعر")]
    public decimal? Price { get; set; }

    /// <summary>
    /// السعر بعد الخصم - Discounted price
    /// </summary>
    [Display(Name = "السعر بعد الخصم")]
    public decimal? DiscountedPrice { get; set; }

    /// <summary>
    /// هل مجاني - Is free
    /// </summary>
    [Display(Name = "مجاني")]
    public bool IsFree { get; set; } = false;

    /// <summary>
    /// هل منشور - Is published
    /// </summary>
    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = false;

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// الدورات - Course IDs
    /// </summary>
    [Required(ErrorMessage = "يرجى اختيار دورة واحدة على الأقل")]
    [Display(Name = "الدورات")]
    public List<int> CourseIds { get; set; } = new();
}

/// <summary>
/// نموذج تعديل مسار تعلم - Edit Learning Path ViewModel
/// </summary>
public class LearningPathEditViewModel : LearningPathCreateViewModel
{
    /// <summary>
    /// معرف المسار - Path ID
    /// </summary>
    public int Id { get; set; }
}

