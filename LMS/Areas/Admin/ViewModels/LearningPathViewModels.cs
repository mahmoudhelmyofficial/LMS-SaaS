using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء مسار تعليمي - Create Learning Path ViewModel
/// </summary>
public class LearningPathCreateViewModel
{
    /// <summary>
    /// اسم المسار - Path name
    /// </summary>
    [Required(ErrorMessage = "اسم المسار مطلوب")]
    [MaxLength(300)]
    [Display(Name = "اسم المسار")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// العنوان - Title (alias for Name)
    /// </summary>
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get => Name; set => Name = value; }

    /// <summary>
    /// الرابط المختصر - Slug
    /// </summary>
    [MaxLength(300)]
    [Display(Name = "الرابط المختصر")]
    public string? Slug { get; set; }

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(2000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "صورة الغلاف")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// المستوى - Difficulty level
    /// </summary>
    [Required]
    [Display(Name = "المستوى")]
    public string Level { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced

    /// <summary>
    /// المدة المقدرة بالساعات - Estimated duration in hours
    /// </summary>
    [Range(1, 10000)]
    [Display(Name = "المدة المقدرة (ساعات)")]
    public int EstimatedDurationHours { get; set; }

    /// <summary>
    /// المدة المقدرة - Estimated duration (alias)
    /// </summary>
    [Display(Name = "المدة المقدرة")]
    public int? EstimatedDuration { get => EstimatedDurationHours; set => EstimatedDurationHours = value ?? 0; }

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Range(0, double.MaxValue)]
    [Display(Name = "السعر")]
    public decimal Price { get; set; } = 0;

    /// <summary>
    /// سعر الخصم - Discount price
    /// </summary>
    [Display(Name = "سعر الخصم")]
    public decimal? DiscountPrice { get; set; }

    /// <summary>
    /// معرف التصنيف - Category ID
    /// </summary>
    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    /// <summary>
    /// التصنيفات المتاحة - Available categories
    /// </summary>
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableCategories { get; set; } = new();

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// هل مميز - Is featured
    /// </summary>
    [Display(Name = "مميز")]
    public bool IsFeatured { get; set; }

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// معرفات الدورات - Course IDs
    /// </summary>
    public List<int> CourseIds { get; set; } = new();

    /// <summary>
    /// ملف الصورة - Image file
    /// </summary>
    [Display(Name = "ملف الصورة")]
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }

    /// <summary>
    /// المتطلبات - Prerequisites
    /// </summary>
    [Display(Name = "المتطلبات")]
    public string? Prerequisites { get; set; }

    /// <summary>
    /// ما ستتعلمه - What you will learn
    /// </summary>
    [Display(Name = "ما ستتعلمه")]
    public string? WhatYouWillLearn { get; set; }

    /// <summary>
    /// يتطلب إكمال متتابع - Require sequential completion
    /// </summary>
    [Display(Name = "يتطلب إكمال متتابع")]
    public bool RequireSequentialCompletion { get; set; }

    /// <summary>
    /// إصدار شهادة - Issue certificate
    /// </summary>
    [Display(Name = "إصدار شهادة")]
    public bool IssueCertificate { get; set; } = true;

    /// <summary>
    /// الدورات المتاحة - Available courses
    /// </summary>
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableCourses { get; set; } = new();
}

/// <summary>
/// نموذج تعديل المسار - Edit Learning Path ViewModel
/// </summary>
public class LearningPathEditViewModel : LearningPathCreateViewModel
{
    public int Id { get; set; }
    public int EnrollmentsCount { get; set; }
    public int EnrolledStudentsCount { get => EnrollmentsCount; set => EnrollmentsCount = value; }
    public int CompletedCount { get; set; }
    public string? CurrentImageUrl { get; set; }
    public List<int> SelectedCourses { get; set; } = new();
}

/// <summary>
/// نموذج عرض المسار - Learning Path Display ViewModel
/// </summary>
public class LearningPathDisplayViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Level { get; set; } = string.Empty;
    public int EstimatedDurationHours { get; set; }
    public int CoursesCount { get; set; }
    public int EnrollmentsCount { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
}

