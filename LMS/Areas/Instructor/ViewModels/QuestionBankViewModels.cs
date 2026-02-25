using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء بنك أسئلة - Create Question Bank ViewModel
/// </summary>
public class QuestionBankCreateViewModel
{
    /// <summary>
    /// اسم البنك - Bank name
    /// </summary>
    [Required(ErrorMessage = "اسم البنك مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم البنك")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// معرف التصنيف - Category ID
    /// </summary>
    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }
}

/// <summary>
/// نموذج تعديل بنك أسئلة - Edit Question Bank ViewModel
/// </summary>
public class QuestionBankEditViewModel : QuestionBankCreateViewModel
{
    /// <summary>
    /// معرف البنك - Bank ID
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// نموذج إضافة سؤال للبنك - Add Question to Bank ViewModel
/// </summary>
public class QuestionBankQuestionCreateViewModel
{
    /// <summary>
    /// معرف البنك - Question bank ID
    /// </summary>
    [Required]
    public int QuestionBankId { get; set; }

    /// <summary>
    /// نص السؤال - Question text
    /// </summary>
    [Required(ErrorMessage = "نص السؤال مطلوب")]
    [Display(Name = "نص السؤال")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// نوع السؤال - Question type
    /// </summary>
    [Required(ErrorMessage = "نوع السؤال مطلوب")]
    [Display(Name = "نوع السؤال")]
    public QuestionType QuestionType { get; set; }

    /// <summary>
    /// الدرجة - Points
    /// </summary>
    [Required]
    [Range(1, 100)]
    [Display(Name = "الدرجة")]
    public int Points { get; set; } = 1;

    /// <summary>
    /// مستوى الصعوبة - Difficulty level
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Display(Name = "مستوى الصعوبة")]
    public string DifficultyLevel { get; set; } = "Medium"; // Easy, Medium, Hard

    /// <summary>
    /// الشرح - Explanation
    /// </summary>
    [Display(Name = "الشرح")]
    public string? Explanation { get; set; }

    /// <summary>
    /// الوسوم - Tags
    /// </summary>
    [Display(Name = "الوسوم")]
    public string? Tags { get; set; }

    /// <summary>
    /// الخيارات - Options
    /// </summary>
    public List<QuestionBankOptionViewModel> Options { get; set; } = new();
}

/// <summary>
/// نموذج تعديل سؤال في البنك - Edit Question in Bank ViewModel
/// </summary>
public class QuestionBankQuestionEditViewModel : QuestionBankQuestionCreateViewModel
{
    /// <summary>
    /// معرف عنصر البنك - Bank item ID
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// نموذج خيار السؤال لبنك الأسئلة - Question Bank Option ViewModel
/// </summary>
public class QuestionBankOptionViewModel
{
    /// <summary>
    /// نص الخيار - Option text
    /// </summary>
    [Required(ErrorMessage = "نص الخيار مطلوب")]
    [Display(Name = "نص الخيار")]
    public string OptionText { get; set; } = string.Empty;

    /// <summary>
    /// هل صحيح - Is correct
    /// </summary>
    [Display(Name = "إجابة صحيحة")]
    public bool IsCorrect { get; set; }

    /// <summary>
    /// ترتيب العرض - Display order
    /// </summary>
    public int DisplayOrder { get; set; }
}

