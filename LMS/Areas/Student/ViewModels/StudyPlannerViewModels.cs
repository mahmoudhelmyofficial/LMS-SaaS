namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج تقويم مخطط الدراسة - Study Planner Calendar ViewModel
/// </summary>
public class StudyPlannerCalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CalendarEventSummaryViewModel> Events { get; set; } = new();
    public List<StudySessionViewModel> StudySessions { get; set; } = new();
    public List<StudySessionViewModel> Sessions { get => StudySessions; set => StudySessions = value; }
    public List<DeadlineViewModel> Deadlines { get; set; } = new();
    public Dictionary<DateTime, int> StudyMinutesPerDay { get; set; } = new();
    public StudentCalendarStats? Stats { get; set; }
    public List<LMS.Domain.Entities.Learning.Enrollment> AvailableCourses { get; set; } = new();
}

public class StudentCalendarStats
{
    public int PlannedSessionsThisWeek { get; set; }
    public int CompletedSessionsThisWeek { get; set; }
    public int TotalMinutesThisWeek { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int PendingSessions { get; set; }
    public int MissedSessions { get; set; }
}

/// <summary>
/// نموذج جلسة الدراسة - Study Session ViewModel
/// </summary>
public class StudySessionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime Date { get => ScheduledDate; set => ScheduledDate = value; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public int Duration { get => DurationMinutes; set => DurationMinutes = value; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// نموذج الموعد النهائي - Deadline ViewModel
/// </summary>
public class DeadlineViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Quiz, Assignment, etc.
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string? Url { get; set; }
}

/// <summary>
/// نموذج التذكيرات - Study Reminders ViewModel
/// </summary>
public class StudyRemindersViewModel
{
    public List<ReminderListItemViewModel> ActiveReminders { get; set; } = new();
    public List<ReminderListItemViewModel> UpcomingReminders { get; set; } = new();
    public List<ReminderListItemViewModel> PastReminders { get; set; } = new();
    public List<ReminderListItemViewModel> OverdueReminders { get; set; } = new();
    public List<ReminderListItemViewModel> CompletedReminders { get; set; } = new();
    public int TotalActive { get => ActiveReminders.Count; }
    public int TotalReminders { get; set; }
    public List<LMS.Domain.Entities.Learning.Enrollment> AvailableCourses { get; set; } = new();
}

