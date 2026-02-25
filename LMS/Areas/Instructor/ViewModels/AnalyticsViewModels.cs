namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج تحليلات المدرس - Instructor Analytics ViewModel (Enhanced)
/// </summary>
public class InstructorAnalyticsViewModel
{
    // Basic Metrics
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int TotalStudents { get; set; }
    public int TotalEnrollments { get; set; }
    public int CompletedEnrollments { get; set; }
    public int ActiveEnrollments { get; set; }

    // Revenue Metrics
    public decimal TotalRevenue { get; set; }
    public decimal AverageRevenuePerStudent { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal CommissionRate { get; set; }

    // Rating & Reviews
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }

    // Engagement Metrics
    public decimal CompletionRate { get; set; }
    public decimal AverageProgress { get; set; }

    // Data Collections
    public List<CourseAnalyticsItemViewModel> CourseBreakdown { get; set; } = new();
    public List<TrendDataPoint> EnrollmentTrend { get; set; } = new();
    public List<TrendDataPoint> RevenueTrend { get; set; } = new();
    public List<TopStudentViewModel> TopStudents { get; set; } = new();
    public List<LessonEngagementViewModel> MostEngagedLessons { get; set; } = new();
}

/// <summary>
/// نموذج تحليلات دورة - Course Analytics Item ViewModel (Enhanced)
/// </summary>
public class CourseAnalyticsItemViewModel
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public decimal Revenue { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal AverageProgress { get; set; }
}

/// <summary>
/// نموذج بيانات الاتجاه - Trend Data Point
/// </summary>
public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// نموذج أفضل طالب - Top Student ViewModel
/// </summary>
public class TopStudentViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string? StudentImageUrl { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public decimal ProgressPercentage { get; set; }
    public decimal? FinalGrade { get; set; }
}

/// <summary>
/// نموذج تفاعل الدرس - Lesson Engagement ViewModel
/// </summary>
public class LessonEngagementViewModel
{
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public int TotalWatchTime { get; set; }
    public int TotalStudents { get; set; }
    public double AverageWatchTime { get; set; }

    public string TotalWatchTimeFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(TotalWatchTime);
            return ts.TotalHours >= 1 
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m" 
                : $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}

