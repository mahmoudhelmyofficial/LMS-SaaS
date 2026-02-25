using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء اختبار جديد - Create Quiz ViewModel
/// </summary>
public class QuizCreateViewModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    [Required]
    public int LessonId { get; set; }

    /// <summary>
    /// عنوان الاختبار - Quiz title
    /// </summary>
    [Required(ErrorMessage = "عنوان الاختبار مطلوب")]
    [MaxLength(300, ErrorMessage = "عنوان الاختبار يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الاختبار")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف الاختبار - Quiz description
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "وصف الاختبار")]
    public string? Description { get; set; }

    /// <summary>
    /// التعليمات - Instructions
    /// </summary>
    [Display(Name = "تعليمات الاختبار")]
    public string? Instructions { get; set; }

    /// <summary>
    /// درجة النجاح - Passing score percentage
    /// </summary>
    [Range(0, 100, ErrorMessage = "درجة النجاح يجب أن تكون بين 0 و 100")]
    [Display(Name = "درجة النجاح (%)")]
    public int PassingScore { get; set; } = 70;

    /// <summary>
    /// الحد الزمني بالدقائق - Time limit in minutes
    /// </summary>
    [Range(1, 600, ErrorMessage = "الحد الزمني يجب أن يكون بين 1 و 600 دقيقة")]
    [Display(Name = "الحد الزمني (دقائق)")]
    public int? TimeLimitMinutes { get; set; }

    /// <summary>
    /// عدد المحاولات المسموحة - Maximum attempts
    /// </summary>
    [Range(1, 100, ErrorMessage = "عدد المحاولات يجب أن يكون بين 1 و 100")]
    [Display(Name = "عدد المحاولات المسموحة")]
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// إظهار الإجابات الصحيحة - Show correct answers
    /// </summary>
    [Display(Name = "إظهار الإجابات الصحيحة")]
    public bool ShowCorrectAnswers { get; set; } = true;

    /// <summary>
    /// إظهار النتيجة فوراً - Show score immediately
    /// </summary>
    [Display(Name = "إظهار النتيجة فوراً")]
    public bool ShowScoreImmediately { get; set; } = true;

    /// <summary>
    /// خلط الأسئلة - Shuffle questions
    /// </summary>
    [Display(Name = "خلط ترتيب الأسئلة")]
    public bool ShuffleQuestions { get; set; } = false;

    /// <summary>
    /// خلط الخيارات - Shuffle options
    /// </summary>
    [Display(Name = "خلط ترتيب الخيارات")]
    public bool ShuffleOptions { get; set; } = false;

    /// <summary>
    /// السماح بالعودة للسابق - Allow back navigation
    /// </summary>
    [Display(Name = "السماح بالعودة للسؤال السابق")]
    public bool AllowBackNavigation { get; set; } = true;

    /// <summary>
    /// سؤال واحد بالصفحة - One question per page
    /// </summary>
    [Display(Name = "سؤال واحد بالصفحة")]
    public bool OneQuestionPerPage { get; set; } = false;
}

/// <summary>
/// نموذج تعديل الاختبار - Edit Quiz ViewModel
/// </summary>
public class QuizEditViewModel : QuizCreateViewModel
{
    /// <summary>
    /// معرف الاختبار - Quiz ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// خلط الأسئلة - Shuffle questions
    /// </summary>
    [Display(Name = "خلط ترتيب الأسئلة")]
    public new bool ShuffleQuestions { get; set; } = false;

    /// <summary>
    /// خلط الخيارات - Shuffle options
    /// </summary>
    [Display(Name = "خلط ترتيب الخيارات")]
    public new bool ShuffleOptions { get; set; } = false;

    /// <summary>
    /// إظهار الإجابات الصحيحة - Show correct answers
    /// </summary>
    [Display(Name = "إظهار الإجابات الصحيحة")]
    public new bool ShowCorrectAnswers { get; set; } = true;

    /// <summary>
    /// متى يتم إظهار الإجابات - When to show answers
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "وقت إظهار الإجابات")]
    public string ShowAnswersAfter { get; set; } = "Immediately";

    /// <summary>
    /// إظهار النتيجة فوراً - Show score immediately
    /// </summary>
    [Display(Name = "إظهار النتيجة فوراً")]
    public new bool ShowScoreImmediately { get; set; } = true;

    /// <summary>
    /// السماح بالعودة للسابق - Allow back navigation
    /// </summary>
    [Display(Name = "السماح بالعودة للسؤال السابق")]
    public new bool AllowBackNavigation { get; set; } = true;

    /// <summary>
    /// سؤال واحد بالصفحة - One question per page
    /// </summary>
    [Display(Name = "سؤال واحد بالصفحة")]
    public new bool OneQuestionPerPage { get; set; } = false;

    /// <summary>
    /// متاح من - Available from
    /// </summary>
    [Display(Name = "متاح من")]
    public DateTime? AvailableFrom { get; set; }

    /// <summary>
    /// متاح حتى - Available until
    /// </summary>
    [Display(Name = "متاح حتى")]
    public DateTime? AvailableUntil { get; set; }
}

/// <summary>
/// نموذج إنشاء سؤال - Create Question ViewModel
/// </summary>
public class QuestionCreateViewModel
{
    /// <summary>
    /// معرف الاختبار - Quiz ID
    /// </summary>
    [Required]
    public int QuizId { get; set; }

    /// <summary>
    /// نص السؤال - Question text
    /// </summary>
    [Required(ErrorMessage = "نص السؤال مطلوب")]
    [Display(Name = "نص السؤال")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// نوع السؤال - Question type
    /// </summary>
    [Display(Name = "نوع السؤال")]
    public string QuestionType { get; set; } = "MultipleChoice";

    /// <summary>
    /// الدرجة - Points
    /// </summary>
    [Range(1, 100)]
    [Display(Name = "الدرجة")]
    public int Points { get; set; } = 1;

    /// <summary>
    /// الخيارات - Options (JSON or list)
    /// </summary>
    public List<QuizQuestionOptionViewModel> Options { get; set; } = new();
}

/// <summary>
/// نموذج خيار السؤال للاختبار - Quiz Question Option ViewModel
/// </summary>
public class QuizQuestionOptionViewModel
{
    public int? Id { get; set; }
    
    [Required(ErrorMessage = "نص الخيار مطلوب")]
    [Display(Name = "نص الخيار")]
    public string Text { get; set; } = string.Empty;
    
    [Display(Name = "إجابة صحيحة")]
    public bool IsCorrect { get; set; } = false;
}

