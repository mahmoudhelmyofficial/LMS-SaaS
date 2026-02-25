namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج عرض المسار التعليمي للطالب - Student Learning Path Display ViewModel
/// </summary>
public class StudentLearningPathViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Level { get; set; } = string.Empty;
    public int EstimatedDurationHours { get; set; }
    public int CoursesCount { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EGP";
    public bool IsEnrolled { get; set; }
    public int CompletedCourses { get; set; }
    public decimal ProgressPercentage { get; set; }
}

/// <summary>
/// نموذج تقدم المسار - Learning Path Progress ViewModel
/// </summary>
public class LearningPathProgressViewModel
{
    public int PathId { get; set; }
    public string PathName { get; set; } = string.Empty;
    public int TotalCourses { get; set; }
    public int CompletedCourses { get; set; }
    public int InProgressCourses { get; set; }
    public decimal OverallProgress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<CourseProgressInPath> Courses { get; set; } = new();
}

public class CourseProgressInPath
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsCompleted { get; set; }
    public decimal ProgressPercentage { get; set; }
    public bool IsEnrolled { get; set; }
}

