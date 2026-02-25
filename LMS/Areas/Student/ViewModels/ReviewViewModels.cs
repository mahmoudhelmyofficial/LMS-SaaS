using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء مراجعة - Create Review ViewModel
/// </summary>
public class CreateReviewViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Required]
    public int CourseId { get; set; }

    /// <summary>
    /// التقييم - Rating (1-5)
    /// </summary>
    [Required(ErrorMessage = "التقييم مطلوب")]
    [Range(1, 5, ErrorMessage = "التقييم يجب أن يكون بين 1 و 5")]
    [Display(Name = "التقييم")]
    public int Rating { get; set; }

    /// <summary>
    /// عنوان المراجعة - Review title
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "عنوان المراجعة")]
    public string? Title { get; set; }

    /// <summary>
    /// نص المراجعة - Review comment
    /// </summary>
    [Required(ErrorMessage = "نص المراجعة مطلوب")]
    [MaxLength(2000)]
    [Display(Name = "مراجعتك")]
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// نموذج تعديل المراجعة - Edit Review ViewModel
/// </summary>
public class EditReviewViewModel : CreateReviewViewModel
{
    /// <summary>
    /// معرف المراجعة - Review ID
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// نموذج عرض المراجعة - Review Display ViewModel
/// </summary>
public class ReviewDisplayViewModel
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentImageUrl { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsApproved { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public string? InstructorResponse { get; set; }
    public DateTime? ResponseAt { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public bool IsPinned { get; set; }
}

/// <summary>
/// نموذج الرد على المراجعة - Instructor Response ViewModel
/// </summary>
public class InstructorResponseViewModel
{
    [Required]
    public int ReviewId { get; set; }

    [Required(ErrorMessage = "نص الرد مطلوب")]
    [MaxLength(1000)]
    [Display(Name = "ردك")]
    public string Response { get; set; } = string.Empty;
}

