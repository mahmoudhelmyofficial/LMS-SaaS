using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج الوسم - Tag view model
/// </summary>
public class TagViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الوسم مطلوب")]
    [MaxLength(50, ErrorMessage = "اسم الوسم يجب ألا يتجاوز 50 حرف")]
    [Display(Name = "اسم الوسم")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "المعرف الفريد يجب ألا يتجاوز 100 حرف")]
    [RegularExpression(@"^[a-z0-9-]*$", ErrorMessage = "المعرف الفريد يجب أن يحتوي على أحرف صغيرة وأرقام وشرطات فقط")]
    [Display(Name = "المعرف الفريد (Slug)")]
    public string? Slug { get; set; }

    [MaxLength(200, ErrorMessage = "الوصف يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(20, ErrorMessage = "اللون يجب ألا يتجاوز 20 حرف")]
    [RegularExpression(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", ErrorMessage = "اللون يجب أن يكون بتنسيق Hex صالح (مثل #FF5733)")]
    [Display(Name = "اللون")]
    public string? Color { get; set; }

    [MaxLength(50, ErrorMessage = "الأيقونة يجب ألا تتجاوز 50 حرف")]
    [Display(Name = "الأيقونة")]
    public string? Icon { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "مميز")]
    public bool IsFeatured { get; set; } = false;

    [Range(0, 1000, ErrorMessage = "ترتيب العرض يجب أن يكون بين 0 و 1000")]
    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    // For display purposes
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// نموذج قائمة الوسوم - Tags list view model
/// </summary>
public class TagListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج إحصائيات الوسم - Tag statistics view model
/// </summary>
public class TagStatsViewModel
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int DraftCourses { get; set; }
    public int TotalEnrollments { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageRating { get; set; }
}

/// <summary>
/// نموذج ربط الدورات بالوسوم - Course tags assignment view model
/// </summary>
public class CourseTagsViewModel
{
    [Required(ErrorMessage = "معرف الدورة مطلوب")]
    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    [Display(Name = "الوسوم")]
    public List<int> SelectedTagIds { get; set; } = new();

    public List<TagViewModel> AvailableTags { get; set; } = new();
    public List<TagViewModel> CurrentTags { get; set; } = new();
}

/// <summary>
/// نموذج البحث عن الوسوم - Tag search view model
/// </summary>
public class TagSearchViewModel
{
    [Display(Name = "الاسم")]
    public string? Name { get; set; }

    [Display(Name = "نشط فقط")]
    public bool? ActiveOnly { get; set; }

    [Display(Name = "مميز فقط")]
    public bool? FeaturedOnly { get; set; }

    [Display(Name = "الحد الأدنى للاستخدام")]
    public int? MinUsageCount { get; set; }
}

/// <summary>
/// نموذج دمج الوسوم - Merge tags view model
/// </summary>
public class MergeTagsViewModel
{
    [Required(ErrorMessage = "الوسم المصدر مطلوب")]
    [Display(Name = "الوسم المصدر (سيتم حذفه)")]
    public int SourceTagId { get; set; }

    [Required(ErrorMessage = "الوسم الهدف مطلوب")]
    [Display(Name = "الوسم الهدف (سيتم الدمج إليه)")]
    public int TargetTagId { get; set; }

    public string? SourceTagName { get; set; }
    public string? TargetTagName { get; set; }
    public int CoursesAffected { get; set; }
}

