using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء تعليق - Create Comment ViewModel
/// </summary>
public class CommentCreateViewModel
{
    [Required(ErrorMessage = "نوع الكيان مطلوب")]
    public string EntityType { get; set; } = string.Empty; // Lesson, Discussion, etc.

    [Required(ErrorMessage = "معرف الكيان مطلوب")]
    public int EntityId { get; set; }

    [Required(ErrorMessage = "المحتوى مطلوب")]
    [MaxLength(1000)]
    [Display(Name = "التعليق")]
    public string Content { get; set; } = string.Empty;

    public int? ParentCommentId { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>
/// نموذج تعديل تعليق - Edit Comment ViewModel
/// </summary>
public class CommentEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "المحتوى مطلوب")]
    [MaxLength(1000)]
    [Display(Name = "التعليق")]
    public string Content { get; set; } = string.Empty;
}

