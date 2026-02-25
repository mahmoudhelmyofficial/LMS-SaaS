using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// لوحة تحكم الإدارة - Admin Dashboard Controller
/// </summary>
public class DashboardController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;
    private readonly IMemoryCache _cache;
    private readonly ISystemConfigurationService _configService;

    private const string DashboardCacheKey = "admin_dashboard_stats";

    public DashboardController(
        ApplicationDbContext context, 
        ILogger<DashboardController> logger,
        IMemoryCache cache,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _configService = configService;
    }

    /// <summary>
    /// الصفحة الرئيسية للوحة التحكم - Main dashboard page
    /// </summary>
    /// <param name="from">بداية فترة تحليل الإيرادات (اختياري)</param>
    /// <param name="to">نهاية فترة تحليل الإيرادات (اختياري)</param>
    /// <param name="period">مفتاح الفترة: today, this_week, this_month, last_month, this_year</param>
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? period)
    {
        var stats = new AdminDashboardViewModel();
        var hasErrors = false;
        var errorDetails = new List<string>();

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // Resolve revenue chart date range from period or from/to
        DateTime chartFrom = thisMonthStart;
        DateTime chartTo = now;
        if (!string.IsNullOrEmpty(period))
        {
            switch (period.ToLowerInvariant())
            {
                case "today":
                    chartFrom = todayStart;
                    chartTo = now;
                    break;
                case "this_week":
                    chartFrom = todayStart.AddDays(-(int)todayStart.DayOfWeek);
                    chartTo = now;
                    break;
                case "this_month":
                    chartFrom = thisMonthStart;
                    chartTo = now;
                    break;
                case "last_month":
                    chartFrom = lastMonthStart;
                    chartTo = thisMonthStart.AddTicks(-1);
                    break;
                case "this_year":
                    chartFrom = new DateTime(now.Year, 1, 1);
                    chartTo = now;
                    break;
                default:
                    break;
            }
        }
        else if (from.HasValue && to.HasValue)
        {
            chartFrom = from.Value.Date;
            chartTo = to.Value.Date.AddDays(1).AddTicks(-1);
        }

        ViewBag.ChartFrom = chartFrom;
        ViewBag.ChartTo = chartTo;
        ViewBag.SelectedPeriod = period ?? "this_month";

        try
        {
            // Use cache only when no period/from/to (default view)
            bool useCache = string.IsNullOrEmpty(period) && !from.HasValue && !to.HasValue;
            if (useCache && _cache.TryGetValue(DashboardCacheKey, out AdminDashboardViewModel? cachedStats) && cachedStats != null)
            {
                // Override chart data for selected period even when using cached stats
                ViewBag.FromCache = true;
                return View(cachedStats);
            }

            if (!useCache)
                ViewBag.FromCache = false;

            // Load User Statistics with error handling
            try
            {
                stats.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
                stats.TotalStudents = await _context.Users.CountAsync(u => !u.IsDeleted && 
                    _context.Enrollments.Any(e => e.StudentId == u.Id));
                stats.TotalInstructors = await _context.InstructorProfiles.CountAsync();
                stats.NewUsersThisMonth = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.CreatedAt >= thisMonthStart);
                stats.NewUsersToday = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.CreatedAt >= todayStart);
                stats.ActiveStudents = await _context.Enrollments
                    .Where(e => e.Status == EnrollmentStatus.Active)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading user statistics");
                errorDetails.Add("إحصائيات المستخدمين");
                hasErrors = true;
            }

            // Load Course Statistics with error handling
            try
            {
                stats.TotalCourses = await _context.Courses.CountAsync();
                stats.PublishedCourses = await _context.Courses.CountAsync(c => c.Status == CourseStatus.Published);
                stats.PendingCourses = await _context.Courses.CountAsync(c => c.Status == CourseStatus.PendingReview);
                stats.DraftCourses = await _context.Courses.CountAsync(c => c.Status == CourseStatus.Draft);
                stats.NewCoursesThisMonth = await _context.Courses
                    .CountAsync(c => c.CreatedAt >= thisMonthStart);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading course statistics");
                errorDetails.Add("إحصائيات الدورات");
                hasErrors = true;
            }

            // Load Enrollment Statistics with error handling
            try
            {
                stats.TotalEnrollments = await _context.Enrollments.CountAsync();
                stats.EnrollmentsThisMonth = await _context.Enrollments
                    .CountAsync(e => e.EnrolledAt >= thisMonthStart);
                stats.EnrollmentsToday = await _context.Enrollments
                    .CountAsync(e => e.EnrolledAt >= todayStart);
                stats.CompletedEnrollments = await _context.Enrollments
                    .CountAsync(e => e.Status == EnrollmentStatus.Completed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading enrollment statistics");
                errorDetails.Add("إحصائيات التسجيلات");
                hasErrors = true;
            }

            // Load Revenue Statistics with error handling
            try
            {
                stats.TotalRevenue = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
                stats.RevenueThisMonth = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= thisMonthStart)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
                stats.RevenueLastMonth = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && 
                               p.CreatedAt >= lastMonthStart && p.CreatedAt < thisMonthStart)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
                stats.RevenueToday = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= todayStart)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading revenue statistics");
                errorDetails.Add("إحصائيات الإيرادات");
                hasErrors = true;
            }

            // Load Pending Items with error handling
            try
            {
                stats.PendingInstructorApplications = await _context.InstructorApplications
                    .CountAsync(a => a.Status == InstructorApplicationStatus.Pending);
                stats.PendingSupportTickets = await _context.SupportTickets
                    .CountAsync(t => t.Status == TicketStatus.Open);
                stats.PendingRefunds = await _context.Refunds
                    .CountAsync(r => r.Status == RefundStatus.Pending);
                stats.PendingWithdrawals = await _context.WithdrawalRequests
                    .CountAsync(w => w.Status == WithdrawalStatus.Pending);
                stats.PendingReviews = await _context.Reviews
                    .CountAsync(r => !r.IsApproved && !r.IsRejected);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading pending items");
                errorDetails.Add("العناصر المعلقة");
                hasErrors = true;
            }

            // Load Recent Activity with error handling
            try
            {
                var recentLimit = 5;
                try { recentLimit = await _configService.GetTopItemsLimitAsync("dashboard_recent", 5); } catch { }

                stats.RecentEnrollments = await _context.Enrollments
                    .Include(e => e.Student)
                    .Include(e => e.Course)
                    .OrderByDescending(e => e.EnrolledAt)
                    .Take(recentLimit)
                    .Select(e => new RecentActivityItem
                    {
                        Type = "Enrollment",
                        Description = $"{e.Student.FirstName} {e.Student.LastName} التحق بدورة {e.Course.Title}",
                        Timestamp = e.EnrolledAt
                    })
                    .ToListAsync();

                stats.RecentPayments = await _context.Payments
                    .Include(p => p.Student)
                    .Include(p => p.Course)
                    .Where(p => p.Status == PaymentStatus.Completed)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(recentLimit)
                    .Select(p => new RecentActivityItem
                    {
                        Type = "Payment",
                        Description = $"{p.Student!.FirstName} {p.Student.LastName} دفع {p.TotalAmount} {p.Currency}",
                        Timestamp = p.CreatedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading recent activity");
                errorDetails.Add("النشاطات الأخيرة");
                hasErrors = true;
            }

            // Load Charts Data with error handling (use chartFrom/chartTo for revenue period filter)
            try
            {
                // Fetch payments and group in-memory to avoid SQL translation issues with Date grouping
                var paymentsForChart = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= chartFrom && p.CreatedAt <= chartTo)
                    .Select(p => new { p.CreatedAt, p.TotalAmount })
                    .ToListAsync();

                stats.RevenueChartData = paymentsForChart
                    .GroupBy(p => p.CreatedAt.Date)
                    .Select(g => new ChartDataPoint
                    {
                        Label = g.Key.ToString("dd MMM"),
                        Value = g.Sum(p => p.TotalAmount)
                    })
                    .OrderBy(x => x.Label)
                    .ToList();

                // Fetch enrollments and group in-memory to avoid SQL translation issues with Date grouping
                var enrollmentsForChart = await _context.Enrollments
                    .Where(e => e.EnrolledAt >= chartFrom && e.EnrolledAt <= chartTo)
                    .Select(e => new { e.EnrolledAt })
                    .ToListAsync();

                stats.EnrollmentsChartData = enrollmentsForChart
                    .GroupBy(e => e.EnrolledAt.Date)
                    .Select(g => new ChartDataPoint
                    {
                        Label = g.Key.ToString("dd MMM"),
                        Value = g.Count()
                    })
                    .OrderBy(x => x.Label)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading chart data");
                errorDetails.Add("بيانات الرسوم البيانية");
                hasErrors = true;
            }

            // Load Top Performers with error handling
            try
            {
                var topLimit = 5;
                try { topLimit = await _configService.GetTopItemsLimitAsync("dashboard_top", 5); } catch { }

                stats.TopCourses = await _context.Courses
                    .OrderByDescending(c => c.TotalStudents)
                    .Take(topLimit)
                    .Select(c => new TopItemViewModel
                    {
                        Id = c.Id.ToString(),
                        Name = c.Title,
                        Value = c.TotalStudents,
                        SecondaryValue = c.AverageRating
                    })
                    .ToListAsync();

                stats.TopInstructors = await _context.InstructorProfiles
                    .Include(ip => ip.User)
                    .OrderByDescending(ip => ip.TotalEarnings)
                    .Take(topLimit)
                    .Select(ip => new TopItemViewModel
                    {
                        Id = ip.UserId,
                        Name = $"{ip.User.FirstName} {ip.User.LastName}",
                        Value = (int)ip.TotalEarnings,
                        SecondaryValue = ip.AverageRating
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading top performers");
                errorDetails.Add("أفضل المؤديين");
                hasErrors = true;
            }

            // Calculate growth rates
            stats.RevenueGrowth = stats.RevenueLastMonth > 0
                ? ((stats.RevenueThisMonth - stats.RevenueLastMonth) / stats.RevenueLastMonth) * 100
                : 0;

            stats.CompletionRate = stats.TotalEnrollments > 0
                ? ((decimal)stats.CompletedEnrollments / stats.TotalEnrollments) * 100
                : 0;

            // Cache the result only if no errors
            if (!hasErrors)
            {
                var cacheDuration = 5;
                try { cacheDuration = await _configService.GetCacheDurationMinutesAsync("dashboard", 5); } catch { }
                _cache.Set(DashboardCacheKey, stats, TimeSpan.FromMinutes(cacheDuration));
            }

            ViewBag.FromCache = false;
            
            if (hasErrors)
            {
                SetWarningMessage(string.Format(CultureExtensions.T("تم تحميل بعض البيانات بنجاح. لم يتم تحميل: {0}", "Some data loaded successfully. Failed to load: {0}"), string.Join(CultureExtensions.T("، ", ", "), errorDetails)));
            }
            else
            {
                _logger.LogInformation("Dashboard statistics loaded successfully");
            }

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error loading dashboard: {Message}", ex.Message);
            
            // Show appropriate message based on error type
            if (ex.Message.Contains("Cannot open database") || ex.Message.Contains("does not exist"))
            {
                SetWarningMessage(CultureExtensions.T("لا يمكن الاتصال بقاعدة البيانات. يرجى التأكد من تشغيل الخادم.", "Cannot connect to the database. Please ensure the server is running."));
            }
            else
            {
                SetWarningMessage(CultureExtensions.T("تم تحميل لوحة التحكم بدون بيانات. يرجى التحقق من الاتصال بقاعدة البيانات.", "Dashboard loaded with no data. Please check database connection."));
            }
            
            return View(stats);
        }
    }

    /// <summary>
    /// تحديث الإحصائيات - Refresh statistics
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshStats()
    {
        _cache.Remove(DashboardCacheKey);
        _logger.LogInformation("Dashboard cache cleared");
        SetSuccessMessage(CultureExtensions.T("تم تحديث الإحصائيات", "Statistics updated."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الإشعارات السريعة - Quick notifications
    /// </summary>
    public async Task<IActionResult> QuickNotifications()
    {
        try
        {
            var notifications = new
            {
                PendingApplications = await _context.InstructorApplications
                    .Where(a => a.Status == InstructorApplicationStatus.Pending)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(await _configService.GetTopItemsLimitAsync("dashboard_recent", 5))
                    .Select(a => new { a.Id, a.CreatedAt, ApplicantName = $"{a.FirstName} {a.LastName}" })
                    .ToListAsync(),

                PendingTickets = await _context.SupportTickets
                    .Where(t => t.Status == TicketStatus.Open)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(await _configService.GetTopItemsLimitAsync("dashboard_recent", 5))
                    .Select(t => new { t.Id, t.Subject, t.CreatedAt })
                    .ToListAsync(),

                PendingRefunds = await _context.Refunds
                    .Include(r => r.Payment)
                        .ThenInclude(p => p.Student)
                    .Where(r => r.Status == RefundStatus.Pending)
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(await _configService.GetTopItemsLimitAsync("dashboard_recent", 5))
                    .Select(r => new { 
                        r.Id, 
                        r.RequestedAt, 
                        StudentName = r.Payment.Student != null 
                            ? $"{r.Payment.Student.FirstName} {r.Payment.Student.LastName}" 
                            : "Unknown Student",
                        r.RefundAmount 
                    })
                    .ToListAsync(),

                PendingWithdrawals = await _context.WithdrawalRequests
                    .Include(w => w.Instructor)
                        .ThenInclude(i => i.User)
                    .Where(w => w.Status == WithdrawalStatus.Pending)
                    .OrderByDescending(w => w.CreatedAt)
                    .Take(await _configService.GetTopItemsLimitAsync("dashboard_recent", 5))
                    .Select(w => new { 
                        w.Id, 
                        w.CreatedAt, 
                        InstructorName = w.Instructor != null && w.Instructor.User != null
                            ? $"{w.Instructor.User.FirstName} {w.Instructor.User.LastName}" 
                            : "Unknown Instructor",
                        w.Amount 
                    })
                    .ToListAsync()
            };

            return Json(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quick notifications");
            return Json(new { error = "Failed to load notifications" });
        }
    }
}

/// <summary>
/// نموذج عرض لوحة تحكم الإدارة - Admin Dashboard ViewModel
/// </summary>
public class AdminDashboardViewModel
{
    // User Statistics
    public int TotalUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalInstructors { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersToday { get; set; }
    public int ActiveStudents { get; set; }

    // Course Statistics
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int PendingCourses { get; set; }
    public int DraftCourses { get; set; }
    public int NewCoursesThisMonth { get; set; }

    // Enrollment Statistics
    public int TotalEnrollments { get; set; }
    public int EnrollmentsThisMonth { get; set; }
    public int EnrollmentsToday { get; set; }
    public int CompletedEnrollments { get; set; }
    public decimal CompletionRate { get; set; }

    // Revenue Statistics
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal RevenueGrowth { get; set; }

    // Pending Items
    public int PendingInstructorApplications { get; set; }
    public int PendingSupportTickets { get; set; }
    public int PendingRefunds { get; set; }
    public int PendingWithdrawals { get; set; }
    public int PendingReviews { get; set; }

    // Recent Activity
    public List<RecentActivityItem> RecentEnrollments { get; set; } = new();
    public List<RecentActivityItem> RecentPayments { get; set; } = new();

    // Charts Data
    public List<ChartDataPoint> RevenueChartData { get; set; } = new();
    public List<ChartDataPoint> EnrollmentsChartData { get; set; } = new();

    // Top Performers
    public List<TopItemViewModel> TopCourses { get; set; } = new();
    public List<TopItemViewModel> TopInstructors { get; set; } = new();
}

public class RecentActivityItem
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

// ChartDataPoint is defined in LMS.Areas.Admin.ViewModels.ReportViewModels

public class TopItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public decimal SecondaryValue { get; set; }
}

