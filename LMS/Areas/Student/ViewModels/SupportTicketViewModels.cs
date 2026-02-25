using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إنشاء تذكرة دعم - Create Support Ticket ViewModel
/// </summary>
public class SupportTicketCreateViewModel
{
    /// <summary>
    /// الموضوع - Subject
    /// </summary>
    [Required(ErrorMessage = "موضوع التذكرة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [Required(ErrorMessage = "وصف المشكلة مطلوب")]
    [Display(Name = "وصف المشكلة")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// التصنيف - Category ID
    /// </summary>
    [Display(Name = "التصنيف")]
    public int? CategoryId { get; set; }

    /// <summary>
    /// التصنيف - Category (enum)
    /// </summary>
    [Display(Name = "فئة التذكرة")]
    public LMS.Domain.Enums.TicketCategory? Category { get; set; }

    /// <summary>
    /// معرف الدورة - Course ID (if related to a course)
    /// </summary>
    [Display(Name = "الدورة المرتبطة")]
    public int? CourseId { get; set; }

    /// <summary>
    /// الأولوية - Priority
    /// </summary>
    [Required(ErrorMessage = "الأولوية مطلوبة")]
    [Display(Name = "الأولوية")]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

