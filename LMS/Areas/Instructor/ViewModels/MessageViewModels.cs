using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// Compose message view model
/// </summary>
public class MessageComposeViewModel
{
    [Required(ErrorMessage = "المستلم مطلوب")]
    [Display(Name = "المستلم")]
    public string ReceiverId { get; set; } = string.Empty;

    [Required(ErrorMessage = "الموضوع مطلوب")]
    [MaxLength(200)]
    [Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "نص الرسالة مطلوب")]
    [Display(Name = "الرسالة")]
    public string Body { get; set; } = string.Empty;

    public int? ParentMessageId { get; set; }
}

/// <summary>
/// Bulk message view model
/// </summary>
public class BulkMessageViewModel
{
    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Required(ErrorMessage = "الموضوع مطلوب")]
    [MaxLength(200)]
    [Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "نص الرسالة مطلوب")]
    [Display(Name = "الرسالة")]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Message statistics view model
/// </summary>
public class MessageStatisticsViewModel
{
    public int TotalSent { get; set; }
    public int TotalReceived { get; set; }
    public int UnreadReceived { get; set; }
    public int RepliesCount { get; set; }
    public double AverageResponseTime { get; set; }
}

/// <summary>
/// عنصر قائمة الطلاب للرسائل - Student dropdown item for messaging (strongly-typed for ViewBag)
/// </summary>
public class StudentDropdownItem
{
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// عنصر قائمة الدورات مع العدد - Course with student count for bulk messaging (strongly-typed for ViewBag)
/// </summary>
public class CourseWithCountDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
}

