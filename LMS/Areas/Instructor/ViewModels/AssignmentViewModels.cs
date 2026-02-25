using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج تقييم التسليم - Grade Submission ViewModel
/// </summary>
public class GradeSubmissionViewModel
{
    /// <summary>
    /// معرف التسليم - Submission ID
    /// </summary>
    public int SubmissionId { get; set; }

    /// <summary>
    /// الدرجة - Grade
    /// </summary>
    [Required(ErrorMessage = "الدرجة مطلوبة")]
    [Range(0, 100, ErrorMessage = "الدرجة يجب أن تكون بين 0 و 100")]
    [Display(Name = "الدرجة")]
    public decimal Grade { get; set; }

    /// <summary>
    /// الملاحظات - Feedback
    /// </summary>
    [MaxLength(2000)]
    [Display(Name = "الملاحظات")]
    public string? Feedback { get; set; }
}

/// <summary>
/// نموذج عرض التسليم - Submission Display ViewModel
/// </summary>
public class SubmissionDisplayViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string AssignmentTitle { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public AssignmentStatus Status { get; set; }
    public decimal? Grade { get; set; }
    public bool IsLate { get; set; }
}

/// <summary>
/// نموذج إنشاء تكليف - Create Assignment ViewModel
/// </summary>
public class AssignmentCreateViewModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    [Required]
    public int LessonId { get; set; }

    /// <summary>
    /// عنوان التكليف - Assignment title
    /// </summary>
    [Required(ErrorMessage = "عنوان التكليف مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان التكليف")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف التكليف - Description
    /// </summary>
    [Display(Name = "وصف التكليف")]
    public string? Description { get; set; }

    /// <summary>
    /// التعليمات - Instructions
    /// </summary>
    [Display(Name = "التعليمات")]
    public string? Instructions { get; set; }

    /// <summary>
    /// تاريخ التسليم - Due date
    /// </summary>
    [Display(Name = "تاريخ التسليم")]
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// الدرجة القصوى - Max grade
    /// </summary>
    [Range(1, 1000)]
    [Display(Name = "الدرجة القصوى")]
    public int MaxGrade { get; set; } = 100;

    /// <summary>
    /// درجة النجاح - Passing score
    /// </summary>
    [Range(0, 1000)]
    [Display(Name = "درجة النجاح")]
    public int? PassingScore { get; set; }

    /// <summary>
    /// السماح بالتسليم المتأخر - Allow late submissions
    /// </summary>
    [Display(Name = "السماح بالتسليم المتأخر")]
    public bool AllowLateSubmissions { get; set; } = true;

    /// <summary>
    /// نسبة خصم التأخير - Late penalty percentage
    /// </summary>
    [Range(0, 100)]
    [Display(Name = "نسبة خصم التأخير (%)")]
    public decimal LatePenaltyPercentage { get; set; } = 10;

    /// <summary>
    /// الحد الأقصى لحجم الملف - Max file size in MB
    /// </summary>
    [Range(1, 100)]
    [Display(Name = "الحد الأقصى لحجم الملف (ميجابايت)")]
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// أنواع الملفات المسموحة - Accepted file types
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "أنواع الملفات المسموحة")]
    public string? AcceptedFileTypes { get; set; } = ".pdf,.doc,.docx,.zip";
}

/// <summary>
/// نموذج تعديل تكليف - Edit Assignment ViewModel
/// </summary>
public class AssignmentEditViewModel : AssignmentCreateViewModel
{
    /// <summary>
    /// معرف التكليف - Assignment ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عدد التسليمات - Submissions count
    /// </summary>
    [Display(Name = "عدد التسليمات")]
    public int SubmissionsCount { get; set; }

    /// <summary>
    /// هل يوجد تسليمات - Has submissions
    /// </summary>
    public bool HasSubmissions => SubmissionsCount > 0;
}

