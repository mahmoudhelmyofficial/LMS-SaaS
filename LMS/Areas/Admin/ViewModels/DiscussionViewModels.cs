using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء مناقشة - Create discussion view model
/// </summary>
public class DiscussionCreateViewModel
{
    [Required(ErrorMessage = "عنوان المناقشة مطلوب")]
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى المناقشة مطلوب")]
    [Display(Name = "المحتوى")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Display(Name = "تثبيت المناقشة")]
    public bool IsPinned { get; set; }

    [Display(Name = "قفل المناقشة")]
    public bool IsLocked { get; set; }
}

/// <summary>
/// نموذج تعديل مناقشة - Edit discussion view model
/// </summary>
public class DiscussionEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان المناقشة مطلوب")]
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى المناقشة مطلوب")]
    [Display(Name = "المحتوى")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Display(Name = "تثبيت المناقشة")]
    public bool IsPinned { get; set; }

    [Display(Name = "قفل المناقشة")]
    public bool IsLocked { get; set; }
}

/// <summary>
/// نموذج قائمة المناقشات - Discussion list view model
/// </summary>
public class DiscussionListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? LessonName { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsResolved { get; set; }
    public int ViewCount { get; set; }
    public int ReplyCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReplyAt { get; set; }
}

