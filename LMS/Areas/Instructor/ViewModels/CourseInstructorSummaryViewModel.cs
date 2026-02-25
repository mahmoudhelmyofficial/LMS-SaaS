namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// ViewModel لعرض ملخص المدرسين المساعدين للدورات
/// Summary view for courses with co-instructors
/// </summary>
public class CourseInstructorSummaryViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int CourseId { get; set; }

    /// <summary>
    /// عنوان الدورة - Course Title
    /// </summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>
    /// صورة الدورة - Course Thumbnail
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// عدد المدرسين المساعدين - Co-instructor count
    /// </summary>
    public int CoInstructorCount { get; set; }

    /// <summary>
    /// هل الدورة منشورة - Is course published
    /// </summary>
    public bool IsPublished { get; set; }
}

