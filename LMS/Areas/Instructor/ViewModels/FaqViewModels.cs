using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// FAQ form view model
/// </summary>
public class FaqFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Required(ErrorMessage = "السؤال مطلوب")]
    [Display(Name = "السؤال")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "الإجابة مطلوبة")]
    [Display(Name = "الإجابة")]
    public string Answer { get; set; } = string.Empty;

    [Display(Name = "التصنيف")]
    [MaxLength(100)]
    public string? Category { get; set; }

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// FAQ statistics view model
/// </summary>
public class FaqStatisticsViewModel
{
    public int TotalFaqs { get; set; }
    public int TotalViews { get; set; }
    public int TotalHelpfulVotes { get; set; }
    public double AverageHelpfulness { get; set; }
    public List<FaqTopItemViewModel> MostViewedFaqs { get; set; } = new();
    public List<FaqTopItemViewModel> MostHelpfulFaqs { get; set; } = new();
}

/// <summary>
/// FAQ top item view model
/// </summary>
public class FaqTopItemViewModel
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Count { get; set; }
}

