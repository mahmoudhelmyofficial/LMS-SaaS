using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج تقييم تسليم التكليف - Assignment Submission Grade ViewModel
/// </summary>
public class AssignmentSubmissionGradeViewModel
{
    /// <summary>
    /// معرف التسليم - Submission ID
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// اسم الطالب - Student name (for display)
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// عنوان التكليف - Assignment title (for display)
    /// </summary>
    public string AssignmentTitle { get; set; } = string.Empty;

    /// <summary>
    /// الدرجة القصوى - Max points (for display)
    /// </summary>
    public int MaxPoints { get; set; }

    /// <summary>
    /// تاريخ التسليم - Submitted at (for display)
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// النص المرسل - Text submission (for display)
    /// </summary>
    public string? TextSubmission { get; set; }

    /// <summary>
    /// المرفقات - Attachment URLs (for display)
    /// </summary>
    public string? AttachmentUrls { get; set; }

    /// <summary>
    /// الدرجة الحالية - Current grade
    /// </summary>
    public decimal? CurrentGrade { get; set; }

    /// <summary>
    /// التغذية الراجعة الحالية - Current feedback
    /// </summary>
    public string? CurrentFeedback { get; set; }

    /// <summary>
    /// الحالة الحالية - Current status
    /// </summary>
    public AssignmentStatus CurrentStatus { get; set; }

    /// <summary>
    /// الدرجة - Grade
    /// </summary>
    [Required(ErrorMessage = "الدرجة مطلوبة")]
    [Range(0, double.MaxValue, ErrorMessage = "الدرجة يجب أن تكون صفر أو أكثر")]
    [Display(Name = "الدرجة")]
    public decimal Grade { get; set; }

    /// <summary>
    /// التغذية الراجعة - Feedback
    /// </summary>
    [Display(Name = "التغذية الراجعة")]
    public string? Feedback { get; set; }

    /// <summary>
    /// الحالة - Status
    /// </summary>
    [Required(ErrorMessage = "الحالة مطلوبة")]
    [Display(Name = "الحالة")]
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Graded;
}

/// <summary>
/// نموذج إحصائيات تسليمات التكليفات - Assignment Submission Statistics ViewModel
/// </summary>
public class AssignmentSubmissionStatisticsViewModel
{
    /// <summary>
    /// إجمالي التسليمات - Total submissions
    /// </summary>
    public int TotalSubmissions { get; set; }

    /// <summary>
    /// قيد الانتظار - Pending count
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// تم التقييم - Graded count
    /// </summary>
    public int GradedCount { get; set; }

    /// <summary>
    /// متأخرة - Late submissions count
    /// </summary>
    public int LateCount { get; set; }

    /// <summary>
    /// متوسط الدرجة - Average grade
    /// </summary>
    public decimal AverageGrade { get; set; }

    /// <summary>
    /// متوسط وقت التقييم - Average grading time (hours)
    /// </summary>
    public double AverageGradingTime { get; set; }
}

