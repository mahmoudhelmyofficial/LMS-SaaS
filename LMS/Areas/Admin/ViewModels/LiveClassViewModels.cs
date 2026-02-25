using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إحصائيات الفصول المباشرة - Live class statistics view model
/// </summary>
public class LiveClassStatisticsViewModel
{
    public int TotalLiveClasses { get; set; }
    public int TotalClasses { get => TotalLiveClasses; set => TotalLiveClasses = value; }
    public int UpcomingClasses { get; set; }
    public int OngoingClasses { get; set; }
    public int CompletedClasses { get; set; }
    public int CancelledClasses { get; set; }
    public int TotalAttendees { get; set; }
    public decimal AverageAttendanceRate { get; set; }
    public int TotalRecordings { get; set; }
    public long TotalDurationMinutes { get; set; }
    public List<LiveClassSummary> RecentClasses { get; set; } = new();
    public List<TopInstructor> TopInstructors { get; set; } = new();
    public Dictionary<string, int> ClassesByMonth { get; set; } = new();
    public List<TimelineDataPoint> ClassesTimeline { get; set; } = new();
    public List<AttendanceDataPoint> AttendanceData { get; set; } = new();
}

/// <summary>
/// Timeline data point
/// </summary>
public class TimelineDataPoint
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// Attendance data point
/// </summary>
public class AttendanceDataPoint
{
    public string Date { get; set; } = string.Empty;
    public int Attendees { get; set; }
    public decimal AttendanceRate { get; set; }
}

/// <summary>
/// ملخص الفصل المباشر - Live class summary
/// </summary>
public class LiveClassSummary
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public DateTime StartTime { get => ScheduledAt; set => ScheduledAt = value; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttendeeCount { get; set; }
    public bool HasRecording { get; set; }
}

/// <summary>
/// أفضل مدرس - Top instructor
/// </summary>
public class TopInstructor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int LiveClassCount { get; set; }
    public int ClassCount { get => LiveClassCount; set => LiveClassCount = value; }
    public int TotalAttendees { get; set; }
    public decimal AverageRating { get; set; }
}

