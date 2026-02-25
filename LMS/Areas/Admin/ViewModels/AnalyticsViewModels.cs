namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج لوحة التحليلات - Analytics Dashboard ViewModel
/// </summary>
public class AnalyticsDashboardViewModel
{
    // Revenue Analytics
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }
    public decimal RevenueGrowth { get; set; }

    // User Analytics
    public int TotalUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int ActiveUsers { get; set; }
    public decimal UserGrowth { get; set; }

    // Course Analytics
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int PendingCourses { get; set; }
    public decimal AverageCourseRating { get; set; }

    // Enrollment Analytics
    public int TotalEnrollments { get; set; }
    public int EnrollmentsThisMonth { get; set; }
    public int CompletedEnrollments { get; set; }
    public decimal CompletionRate { get; set; }

    // Top Performers
    public List<TopCourseViewModel> TopCourses { get; set; } = new();
    public List<TopInstructorViewModel> TopInstructors { get; set; } = new();
    public List<RevenueByDayViewModel> RevenueChart { get; set; } = new();
}

public class TopCourseViewModel
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int EnrollmentsCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal AverageRating { get; set; }
}

public class TopInstructorViewModel
{
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public int CoursesCount { get; set; }
    public int StudentsCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal AverageRating { get; set; }
}

public class RevenueByDayViewModel
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int OrdersCount { get; set; }
}

/// <summary>
/// نموذج تقرير مفصل - Detailed Report ViewModel
/// </summary>
public class DetailedReportViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public List<ReportDataRow> Data { get; set; } = new();
}

public class ReportDataRow
{
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// نموذج أداء الدورة - Course Performance ViewModel
/// </summary>
public class CoursePerformanceViewModel
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int EnrollmentsCount { get; set; }
    public int CompletedCount { get; set; }
    public double AverageProgress { get; set; }
    public decimal Revenue { get; set; }
    public int ReviewsCount { get; set; }
    public decimal AverageRating { get; set; }
    public double CompletionRate { get; set; }
}

