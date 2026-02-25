using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء مناقشة - Create Discussion ViewModel
/// </summary>
public class CreateDiscussionViewModel
{
    [Required]
    public int CourseId { get; set; }

    public int? LessonId { get; set; }

    [Required(ErrorMessage = "عنوان المناقشة مطلوب")]
    [MaxLength(300, ErrorMessage = "العنوان يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى المناقشة مطلوب")]
    [Display(Name = "المحتوى")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// نموذج الرد على المناقشة - Reply to Discussion ViewModel
/// </summary>
public class ReplyDiscussionViewModel
{
    [Required]
    public int DiscussionId { get; set; }

    public int? ParentReplyId { get; set; }

    [Required(ErrorMessage = "محتوى الرد مطلوب")]
    [Display(Name = "الرد")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// نموذج عرض المناقشة - Discussion Display ViewModel
/// </summary>
public class DiscussionDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorImageUrl { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string? LessonName { get; set; }
    public int RepliesCount { get; set; }
    public int ViewCount { get; set; }
    public bool IsResolved { get; set; }
    public bool IsPinned { get; set; }
    /// <summary>
    /// Whether the current user has saved/bookmarked this discussion
    /// </summary>
    public bool IsSaved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReplyAt { get; set; }
}

/// <summary>
/// عنصر دورة للمناقشات (لوحة اختيار الدورة) - Enrolled course item for discussions landing
/// </summary>
public class DiscussionsCourseItemViewModel
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
}

/// <summary>
/// نموذج عرض الرد - Reply Display ViewModel
/// </summary>
public class ReplyDisplayViewModel
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorImageUrl { get; set; }
    public bool IsInstructorReply { get; set; }
    public bool IsAcceptedAnswer { get; set; }
    public int LikesCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReplyDisplayViewModel> ChildReplies { get; set; } = new();
}

