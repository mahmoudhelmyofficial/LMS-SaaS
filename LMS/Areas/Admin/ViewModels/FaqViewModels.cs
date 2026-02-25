using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// FAQ create view model
/// </summary>
public class FaqCreateViewModel
{
    [Required(ErrorMessage = "السؤال مطلوب")]
    [Display(Name = "السؤال")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "الإجابة مطلوبة")]
    [Display(Name = "الإجابة")]
    public string Answer { get; set; } = string.Empty;

    [Display(Name = "التصنيف")]
    [MaxLength(100)]
    public string? Category { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "الوسوم")]
    [MaxLength(500)]
    public string? Tags { get; set; }
}

/// <summary>
/// FAQ edit view model
/// </summary>
public class FaqEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "السؤال مطلوب")]
    [Display(Name = "السؤال")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "الإجابة مطلوبة")]
    [Display(Name = "الإجابة")]
    public string Answer { get; set; } = string.Empty;

    [Display(Name = "التصنيف")]
    [MaxLength(100)]
    public string? Category { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; }

    [Display(Name = "منشور")]
    public bool IsPublished { get; set; }

    [Display(Name = "الوسوم")]
    [MaxLength(500)]
    public string? Tags { get; set; }
}

/// <summary>
/// FAQ list view model
/// </summary>
public class FaqListViewModel
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? CourseName { get; set; }
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
}

