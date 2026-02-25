using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// Question bank view model
/// </summary>
public class QuestionBankViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "اسم بنك الأسئلة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم بنك الأسئلة")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    [Display(Name = "عام (متاح لجميع المدرسين)")]
    public bool IsPublic { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Question bank item view model
/// </summary>
public class QuestionBankItemViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "بنك الأسئلة مطلوب")]
    public int BankId { get; set; }

    [Required(ErrorMessage = "نص السؤال مطلوب")]
    [Display(Name = "نص السؤال")]
    public string QuestionText { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع السؤال مطلوب")]
    [Display(Name = "نوع السؤال")]
    public QuestionType QuestionType { get; set; }

    [Display(Name = "مستوى الصعوبة")]
    [Range(1, 5)]
    public int DifficultyLevel { get; set; } = 3; // 1-5

    [Display(Name = "النقاط")]
    [Range(1, 100)]
    public int Points { get; set; } = 1;

    [Display(Name = "التفسير")]
    public string? Explanation { get; set; }

    [Display(Name = "الوسوم")]
    [MaxLength(500)]
    public string? Tags { get; set; }

    public List<QuestionOptionViewModel>? Options { get; set; }
}

/// <summary>
/// Question option view model
/// </summary>
public class QuestionOptionViewModel
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// Question category view model
/// </summary>
public class QuestionCategoryViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "اسم التصنيف مطلوب")]
    [MaxLength(100)]
    [Display(Name = "اسم التصنيف")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    [MaxLength(500)]
    public string? Description { get; set; }
}

