using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Calendar event view model
/// </summary>
public class CalendarEventViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نوع الحدث مطلوب")]
    [Display(Name = "نوع الحدث")]
    [MaxLength(50)]
    public string EventType { get; set; } = "Personal"; // Personal, Study, LiveClass, Assignment, Quiz, Deadline

    [Required(ErrorMessage = "وقت البدء مطلوب")]
    [Display(Name = "وقت البدء")]
    public DateTime StartTime { get; set; }

    [Display(Name = "وقت الانتهاء")]
    public DateTime? EndTime { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Display(Name = "الموقع")]
    [MaxLength(200)]
    public string? Location { get; set; }

    [Display(Name = "حدث لطول اليوم")]
    public bool IsAllDay { get; set; }

    [Display(Name = "اللون")]
    [MaxLength(20)]
    public string Color { get; set; } = "#3788d8";

    [Display(Name = "إرسال تذكير")]
    public bool SendReminder { get; set; } = true;

    [Display(Name = "دقائق التذكير قبل الحدث")]
    [Range(0, 1440)]
    public int ReminderMinutesBefore { get; set; } = 30;
}

/// <summary>
/// Calendar month view model
/// </summary>
public class CalendarMonthViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime FirstDayOfMonth { get; set; }
    public DateTime LastDayOfMonth { get; set; }
    public List<CalendarDayViewModel> Days { get; set; } = new();
}

/// <summary>
/// Calendar day view model
/// </summary>
public class CalendarDayViewModel
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public int EventCount { get; set; }
    public List<CalendarEventSummaryViewModel> Events { get; set; } = new();
}

/// <summary>
/// Calendar event summary view model
/// </summary>
public class CalendarEventSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

