using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء إشارة مرجعية - Create Bookmark ViewModel
/// </summary>
public class BookmarkCreateViewModel
{
    /// <summary>
    /// نوع الإشارة - Bookmark type
    /// </summary>
    [Required(ErrorMessage = "نوع الإشارة مطلوب")]
    [Display(Name = "نوع الإشارة")]
    public string BookmarkType { get; set; } = "Lesson"; // Course, Lesson

    /// <summary>
    /// معرف الدورة - Course ID (if bookmarking course)
    /// </summary>
    public int? CourseId { get; set; }

    /// <summary>
    /// معرف الدرس - Lesson ID (if bookmarking lesson)
    /// </summary>
    public int? LessonId { get; set; }

    /// <summary>
    /// ملاحظة - Optional note
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "ملاحظة")]
    public string? Note { get; set; }
}

/// <summary>
/// نموذج تعديل الإشارة المرجعية - Edit Bookmark ViewModel
/// </summary>
public class BookmarkEditViewModel
{
    /// <summary>
    /// المعرف - Bookmark ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// العنوان - Title (for display)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// ملاحظة - Note
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "ملاحظة")]
    public string? Note { get; set; }
}

