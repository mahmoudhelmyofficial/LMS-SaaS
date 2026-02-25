using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Reminder view model
/// </summary>
public class ReminderViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نوع التذكير مطلوب")]
    [Display(Name = "نوع التذكير")]
    [MaxLength(50)]
    public string ReminderType { get; set; } = "General"; // General, Assignment, Quiz, LiveClass, Deadline

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "الدرس")]
    public int? LessonId { get; set; }

    [Display(Name = "التكليف")]
    public int? AssignmentId { get; set; }

    [Required(ErrorMessage = "وقت التذكير مطلوب")]
    [Display(Name = "وقت التذكير")]
    public DateTime RemindAt { get; set; }

    [Display(Name = "إرسال بريد إلكتروني")]
    public bool SendEmail { get; set; } = true;

    [Display(Name = "إرسال إشعار فوري")]
    public bool SendPush { get; set; } = true;
}

/// <summary>
/// Reminder list item view model
/// </summary>
public class ReminderListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ReminderType { get; set; } = string.Empty;
    public string? CourseName { get; set; }
    public DateTime RemindAt { get; set; }
    public DateTime ReminderDate { get => RemindAt; set => RemindAt = value; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsPast { get; set; }
    public int? EnrollmentId { get; set; }
    public LMS.Domain.Entities.Learning.Enrollment? Enrollment { get; set; }
}

