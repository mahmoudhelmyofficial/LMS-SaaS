using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج تقدم الدرس - Lesson Progress ViewModel
/// </summary>
public class LessonProgressViewModel
{
    public int LessonId { get; set; }
    public int WatchedSeconds { get; set; }
    public bool Completed { get; set; }
}

/// <summary>
/// نموذج إضافة ملاحظة - Add Note ViewModel
/// </summary>
public class AddNoteViewModel
{
    [Required]
    public int LessonId { get; set; }

    [Required(ErrorMessage = "محتوى الملاحظة مطلوب")]
    [MaxLength(2000)]
    [Display(Name = "الملاحظة")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "الوقت")]
    public int? Timestamp { get; set; }
}

/// <summary>
/// نموذج إضافة إشارة مرجعية - Add Bookmark ViewModel
/// </summary>
public class AddBookmarkViewModel
{
    [Required]
    public int LessonId { get; set; }

    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string? Title { get; set; }

    [Display(Name = "الوقت")]
    public int? Timestamp { get; set; }
}

/// <summary>
/// نموذج عرض الدورة المسجل بها - Enrolled Course ViewModel
/// </summary>
public class EnrolledCourseViewModel
{
    public int EnrollmentId { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public decimal ProgressPercentage { get; set; }
    public EnrollmentStatus Status { get; set; }
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

/// <summary>
/// نموذج عرض الدرس - Lesson Display ViewModel
/// </summary>
public class LessonDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LessonType Type { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoProvider { get; set; }
    public string? HtmlContent { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public int WatchedSeconds { get; set; }
    public bool IsPreviewable { get; set; }
}

