namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// ViewModel لعرض ملخص الحضور في البث المباشر
/// Summary view for live class attendance
/// </summary>
public class LiveClassAttendanceSummaryViewModel
{
    /// <summary>
    /// معرف الحصة - Live Class ID
    /// </summary>
    public int LiveClassId { get; set; }

    /// <summary>
    /// عنوان الحصة - Live Class Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// اسم الدورة - Course Name
    /// </summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>
    /// موعد الحصة - Scheduled time
    /// </summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// مدة الحصة - Duration in minutes
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// إجمالي الطلاب - Total students
    /// </summary>
    public int TotalStudents { get; set; }

    /// <summary>
    /// الطلاب الحاضرين - Present students
    /// </summary>
    public int PresentStudents { get; set; }

    /// <summary>
    /// هل انتهت الحصة - Is completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// نسبة الحضور - Attendance rate
    /// </summary>
    public double AttendanceRate => TotalStudents > 0 ? (PresentStudents * 100.0 / TotalStudents) : 0;
}

