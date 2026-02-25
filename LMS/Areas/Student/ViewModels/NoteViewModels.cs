using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء ملاحظة - Create Student Note ViewModel
/// </summary>
public class StudentNoteCreateViewModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    public int LessonId { get; set; }

    /// <summary>
    /// عنوان الدرس - Lesson title (for display)
    /// </summary>
    public string? LessonTitle { get; set; }

    /// <summary>
    /// عنوان الدورة - Course title (for display)
    /// </summary>
    public string? CourseTitle { get; set; }

    /// <summary>
    /// محتوى الملاحظة - Note content
    /// </summary>
    [Required(ErrorMessage = "محتوى الملاحظة مطلوب")]
    [Display(Name = "الملاحظة")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// التوقيت في الفيديو - Video timestamp (seconds)
    /// </summary>
    [Display(Name = "التوقيت في الفيديو (ثواني)")]
    public int? VideoTimestamp { get; set; }

    /// <summary>
    /// لون التمييز - Highlight color
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "لون التمييز")]
    public string? HighlightColor { get; set; }

    /// <summary>
    /// هل مثبتة - Is pinned
    /// </summary>
    [Display(Name = "تثبيت الملاحظة")]
    public bool IsPinned { get; set; } = false;
}

/// <summary>
/// نموذج تعديل الملاحظة - Edit Student Note ViewModel
/// </summary>
public class StudentNoteEditViewModel : StudentNoteCreateViewModel
{
    /// <summary>
    /// معرف الملاحظة - Note ID
    /// </summary>
    public int Id { get; set; }
}

