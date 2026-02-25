using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج تقييم محاولة الاختبار - Quiz Attempt Grade ViewModel
/// </summary>
public class QuizAttemptGradeViewModel
{
    /// <summary>
    /// معرف المحاولة - Attempt ID
    /// </summary>
    public int AttemptId { get; set; }

    /// <summary>
    /// اسم الطالب - Student name (for display)
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// عنوان الاختبار - Quiz title (for display)
    /// </summary>
    public string QuizTitle { get; set; } = string.Empty;

    /// <summary>
    /// إجمالي النقاط - Total points (for display)
    /// </summary>
    public decimal TotalPoints { get; set; }

    /// <summary>
    /// الدرجة الحالية - Current score
    /// </summary>
    public decimal CurrentScore { get; set; }

    /// <summary>
    /// الإجابات التي تحتاج تقييم يدوي - Manual grading answers
    /// </summary>
    public List<ManualGradeAnswerViewModel> ManualAnswers { get; set; } = new();
}

/// <summary>
/// نموذج تقييم إجابة يدوية - Manual Grade Answer ViewModel
/// </summary>
public class ManualGradeAnswerViewModel
{
    /// <summary>
    /// معرف الإجابة - Answer ID
    /// </summary>
    public int AnswerId { get; set; }

    /// <summary>
    /// معرف السؤال - Question ID
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>
    /// نص السؤال - Question text (for display)
    /// </summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// نقاط السؤال - Question points (for display)
    /// </summary>
    public decimal QuestionPoints { get; set; }

    /// <summary>
    /// إجابة الطالب - Student answer (for display)
    /// </summary>
    public string? StudentAnswer { get; set; }

    /// <summary>
    /// المرفق - Attachment URL (for display)
    /// </summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>
    /// النقاط الحالية - Current points
    /// </summary>
    public decimal? CurrentPoints { get; set; }

    /// <summary>
    /// التغذية الراجعة الحالية - Current feedback
    /// </summary>
    public string? CurrentFeedback { get; set; }

    /// <summary>
    /// النقاط الممنوحة - Points awarded
    /// </summary>
    [Required(ErrorMessage = "النقاط مطلوبة")]
    [Range(0, double.MaxValue)]
    [Display(Name = "النقاط الممنوحة")]
    public decimal PointsAwarded { get; set; }

    /// <summary>
    /// التغذية الراجعة - Feedback
    /// </summary>
    [Display(Name = "التغذية الراجعة")]
    public string? Feedback { get; set; }
}

/// <summary>
/// نموذج إحصائيات محاولات الاختبارات - Quiz Attempt Statistics ViewModel
/// </summary>
public class QuizAttemptStatisticsViewModel
{
    /// <summary>
    /// إجمالي المحاولات - Total attempts
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// عدد الناجحين - Passed count
    /// </summary>
    public int PassedCount { get; set; }

    /// <summary>
    /// عدد الراسبين - Failed count
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// متوسط الدرجة - Average score
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// متوسط الوقت - Average time (minutes)
    /// </summary>
    public double AverageTimeMinutes { get; set; }

    /// <summary>
    /// أعلى درجة - Highest score
    /// </summary>
    public decimal HighestScore { get; set; }

    /// <summary>
    /// أقل درجة - Lowest score
    /// </summary>
    public decimal LowestScore { get; set; }
}

