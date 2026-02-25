using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء دورة جديدة - Create Course ViewModel
/// </summary>
public class CourseCreateViewModel
{
    /// <summary>
    /// عنوان الدورة - Course title
    /// </summary>
    [Required(ErrorMessage = "عنوان الدورة مطلوب")]
    [MaxLength(300, ErrorMessage = "عنوان الدورة يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الدورة")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف مختصر - Short description
    /// </summary>
    [MaxLength(500, ErrorMessage = "الوصف المختصر يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "وصف مختصر")]
    public string? ShortDescription { get; set; }

    /// <summary>
    /// الوصف الكامل - Full description
    /// </summary>
    [Display(Name = "الوصف الكامل")]
    public string? Description { get; set; }

    /// <summary>
    /// التصنيف - Category ID
    /// </summary>
    [Required(ErrorMessage = "التصنيف مطلوب")]
    [Display(Name = "التصنيف")]
    public int CategoryId { get; set; }

    /// <summary>
    /// التصنيف الفرعي - Subcategory ID
    /// </summary>
    [Display(Name = "التصنيف الفرعي")]
    public int? SubCategoryId { get; set; }

    /// <summary>
    /// مستوى الدورة - Course level
    /// </summary>
    [Display(Name = "المستوى")]
    public CourseLevel Level { get; set; } = CourseLevel.AllLevels;

    /// <summary>
    /// لغة الدورة - Course language
    /// </summary>
    [MaxLength(10)]
    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "السعر يجب أن يكون قيمة موجبة")]
    [Display(Name = "السعر")]
    public decimal Price { get; set; } = 0;

    /// <summary>
    /// سعر الخصم - Discount price
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "سعر الخصم يجب أن يكون قيمة موجبة")]
    [Display(Name = "سعر الخصم")]
    public decimal? DiscountPrice { get; set; }

    /// <summary>
    /// هل مجاني - Is free
    /// </summary>
    [Display(Name = "مجاني")]
    public bool IsFree { get; set; } = false;

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة الغلاف")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// فيديو المعاينة - Preview video URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "فيديو المعاينة")]
    public string? PreviewVideoUrl { get; set; }

    /// <summary>
    /// مزود الفيديو - Video provider
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "مزود الفيديو")]
    public string? PreviewVideoProvider { get; set; }

    /// <summary>
    /// مدة الدورة بالساعات - Duration in hours
    /// </summary>
    [Display(Name = "المدة (بالساعات)")]
    public int? DurationHours { get; set; }

    /// <summary>
    /// ملف الصورة المصغرة - Thumbnail file
    /// </summary>
    [Display(Name = "ملف الصورة")]
    public Microsoft.AspNetCore.Http.IFormFile? ThumbnailFile { get; set; }

    /// <summary>
    /// رابط فيديو المقدمة - Intro video URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "رابط فيديو المقدمة")]
    public string? IntroVideoUrl { get; set; }

    /// <summary>
    /// حالة الدورة - Course status
    /// </summary>
    [Display(Name = "الحالة")]
    public CourseStatus? Status { get; set; }

    /// <summary>
    /// عنوان SEO - Meta title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان SEO")]
    public string? MetaTitle { get; set; }

    /// <summary>
    /// وصف SEO - Meta description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "وصف SEO")]
    public string? MetaDescription { get; set; }

    /// <summary>
    /// كلمات SEO المفتاحية - Meta keywords
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الكلمات المفتاحية")]
    public string? MetaKeywords { get; set; }

    /// <summary>
    /// عامة للجميع - Is public
    /// </summary>
    [Display(Name = "عامة للجميع")]
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// السماح بالمناقشات - Allow discussions
    /// </summary>
    [Display(Name = "السماح بالمناقشات")]
    public bool AllowDiscussions { get; set; } = true;

    /// <summary>
    /// تمكين الشهادة - Enable certificate
    /// </summary>
    [Display(Name = "تمكين الشهادة")]
    public bool EnableCertificate { get; set; } = true;

    /// <summary>
    /// تمكين التنقيط - Enable drip content
    /// </summary>
    [Display(Name = "تمكين التنقيط")]
    public bool EnableDrip { get; set; } = false;
}

/// <summary>
/// نموذج تعديل الدورة - Edit Course ViewModel
/// </summary>
public class CourseEditViewModel : CourseCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// معرف المدرس - Instructor ID
    /// </summary>
    public string? InstructorId { get; set; }

    /// <summary>
    /// رابط الصورة الحالية - Existing thumbnail URL
    /// </summary>
    public string? ExistingThumbnailUrl { get; set; }

    /// <summary>
    /// يوجد شهادة - Has certificate
    /// </summary>
    [Display(Name = "يوجد شهادة")]
    public bool HasCertificate { get; set; } = true;

    /// <summary>
    /// السماح بالمراجعات - Allow reviews
    /// </summary>
    [Display(Name = "السماح بالمراجعات")]
    public bool AllowReviews { get; set; } = true;

    /// <summary>
    /// نقاط التعلم - Learning points
    /// </summary>
    public List<string> LearningPoints { get; set; } = new();

    /// <summary>
    /// المتطلبات - Requirements
    /// </summary>
    public List<string> Requirements { get; set; } = new();

    /// <summary>
    /// تاريخ التحديث - Updated at
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// متوسط التقييم - Average rating
    /// </summary>
    public decimal AverageRating { get; set; }
}

/// <summary>
/// نموذج عرض تفاصيل الدورة للمدرس - Course Details ViewModel
/// </summary>
public class CourseDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public CourseStatus Status { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int TotalStudents { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalModules { get; set; }
    public int TotalLessons { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

