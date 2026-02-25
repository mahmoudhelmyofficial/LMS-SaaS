namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج تقرير المستخدمين - Users Report ViewModel
/// </summary>
public class UsersReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    // Statistics
    public int TotalUsers { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public decimal GrowthRate { get; set; }
    
    // Distribution
    public int TotalStudents { get; set; }
    public int TotalInstructors { get; set; }
    public int TotalAdmins { get; set; }
    
    // Chart data
    public List<ChartDataPoint> UserGrowthChart { get; set; } = new();
    
    // Daily details
    public List<DailyUserStats> DailyStats { get; set; } = new();
}

/// <summary>
/// نموذج تقرير التسجيلات - Enrollments Report ViewModel
/// </summary>
public class EnrollmentsReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    // Statistics
    public int TotalEnrollments { get; set; }
    public int PaidEnrollments { get; set; }
    public int FreeEnrollments { get; set; }
    public decimal GrowthRate { get; set; }
    
    // Chart data
    public List<ChartDataPoint> EnrollmentTrend { get; set; } = new();
    public List<ChartDataPoint> PaidEnrollmentTrend { get; set; } = new();
    
    // Daily details
    public List<DailyEnrollmentStats> DailyStats { get; set; } = new();
}

/// <summary>
/// نموذج تقرير أداء الدورات - Course Performance Report ViewModel
/// </summary>
public class CoursePerformanceReportViewModel
{
    // Statistics
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public decimal AverageRating { get; set; }
    public decimal CompletionRate { get; set; }
    
    // Top courses chart data
    public List<CoursePerformanceItem> TopCoursesByEnrollment { get; set; } = new();
    public List<CoursePerformanceItem> TopCoursesByRevenue { get; set; } = new();
    
    // Course details
    public List<CoursePerformanceItem> Courses { get; set; } = new();
}

/// <summary>
/// نموذج الصفحة الرئيسية للتقارير - Reports Index ViewModel
/// </summary>
public class ReportsIndexViewModel
{
    public int RecentReportsCount { get; set; }
    public int ScheduledReportsCount { get; set; }
    public int ExportedCount { get; set; }
    public int TemplatesCount { get; set; }
    
    public List<RecentReportItem> RecentReports { get; set; } = new();
}

/// <summary>
/// عنصر التقرير الأخير
/// </summary>
public class RecentReportItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// نقطة بيانات للرسم البياني - Chart Data Point
/// </summary>
public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal SecondaryValue { get; set; }
}

/// <summary>
/// إحصائيات المستخدمين اليومية - Daily User Stats
/// </summary>
public class DailyUserStats
{
    public DateTime Date { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public decimal ActivityRate { get; set; }
    public int Students { get; set; }
    public int Instructors { get; set; }
}

/// <summary>
/// إحصائيات التسجيلات اليومية - Daily Enrollment Stats
/// </summary>
public class DailyEnrollmentStats
{
    public DateTime Date { get; set; }
    public int TotalEnrollments { get; set; }
    public decimal PaidPercentage { get; set; }
    public decimal FreePercentage { get; set; }
    public decimal CompletionRate { get; set; }
}

/// <summary>
/// عنصر أداء الدورة - Course Performance Item
/// </summary>
public class CoursePerformanceItem
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string IconClass { get; set; } = "feather-book";
    public int Enrollments { get; set; }
    public int Completed { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal Revenue { get; set; }
    public decimal Rating { get; set; }
    public decimal Growth { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج تقارير المبيعات
/// </summary>
public class SalesReportViewModel
{
    public decimal TotalSales { get; set; }
    public decimal TotalSalesGrowth { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransaction { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal RefundRate { get; set; }
    
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    public List<SalesChartPoint> DailySales { get; set; } = new();
    public List<TopSellingCourse> TopCourses { get; set; } = new();
    public List<RecentSale> RecentSales { get; set; } = new();
    public List<CategoryFilterItem> Categories { get; set; } = new();
}

public class SalesChartPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}

public class TopSellingCourse
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RecentSale
{
    public int PaymentId { get; set; }
    public DateTime Date { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CategoryFilterItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

