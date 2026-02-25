using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// التحليلات المتقدمة - Advanced Analytics Controller
/// </summary>
public class AnalyticsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;
    private readonly IMemoryCache _cache;
    private readonly ISystemConfigurationService _configService;

    public AnalyticsController(
        ApplicationDbContext context, 
        ILogger<AnalyticsController> logger,
        IMemoryCache cache,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _configService = configService;
    }

    /// <summary>
    /// لوحة التحليلات - Analytics dashboard
    /// </summary>
    public async Task<IActionResult> Index(int days = 30)
    {
        try
        {
            var cacheKey = $"analytics_dashboard_{days}";
            
            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out AnalyticsDashboardViewModel? cachedViewModel) && cachedViewModel != null)
            {
                ViewBag.Days = days;
                ViewBag.FromCache = true;
                return View(cachedViewModel);
            }

            var startDate = DateTime.UtcNow.AddDays(-days);
            var lastPeriodStart = DateTime.UtcNow.AddDays(-days * 2);
            var lastPeriodEnd = startDate;

            var viewModel = new AnalyticsDashboardViewModel();

        // Revenue Analytics
        viewModel.TotalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.TotalAmount);

        viewModel.RevenueThisMonth = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= startDate)
            .SumAsync(p => p.TotalAmount);

        viewModel.RevenueLastMonth = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= lastPeriodStart && p.CreatedAt < lastPeriodEnd)
            .SumAsync(p => p.TotalAmount);

        viewModel.RevenueGrowth = viewModel.RevenueLastMonth > 0
            ? ((viewModel.RevenueThisMonth - viewModel.RevenueLastMonth) / viewModel.RevenueLastMonth) * 100
            : 0;

        // User Analytics
        viewModel.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
        viewModel.NewUsersThisMonth = await _context.Users
            .CountAsync(u => !u.IsDeleted && u.CreatedAt >= startDate);
        viewModel.ActiveUsers = await _context.Users
            .CountAsync(u => !u.IsDeleted && u.LastLoginAt >= startDate);

        var usersLastMonth = await _context.Users
            .CountAsync(u => !u.IsDeleted && u.CreatedAt >= lastPeriodStart && u.CreatedAt < lastPeriodEnd);
        viewModel.UserGrowth = usersLastMonth > 0
            ? ((decimal)(viewModel.NewUsersThisMonth - usersLastMonth) / usersLastMonth) * 100
            : 0;

        // Course Analytics
        viewModel.TotalCourses = await _context.Courses.CountAsync();
        viewModel.PublishedCourses = await _context.Courses
            .CountAsync(c => c.Status == CourseStatus.Published);
        viewModel.PendingCourses = await _context.Courses
            .CountAsync(c => c.Status == CourseStatus.PendingReview);
        viewModel.AverageCourseRating = await _context.Courses
            .Where(c => c.TotalReviews > 0)
            .AverageAsync(c => (decimal?)c.AverageRating) ?? 0;

        // Enrollment Analytics
        viewModel.TotalEnrollments = await _context.Enrollments.CountAsync();
        viewModel.EnrollmentsThisMonth = await _context.Enrollments
            .CountAsync(e => e.EnrolledAt >= startDate);
        viewModel.CompletedEnrollments = await _context.Enrollments
            .CountAsync(e => e.Status == EnrollmentStatus.Completed);
        viewModel.CompletionRate = viewModel.TotalEnrollments > 0
            ? ((decimal)viewModel.CompletedEnrollments / viewModel.TotalEnrollments) * 100
            : 0;

        // Top Courses
        var topCoursesLimit = await _configService.GetTopItemsLimitAsync("analytics_top_courses", 10);
        viewModel.TopCourses = await _context.Courses
            .Include(c => c.Enrollments)
            .Select(c => new TopCourseViewModel
            {
                CourseId = c.Id,
                CourseName = c.Title,
                EnrollmentsCount = c.Enrollments.Count,
                Revenue = c.Enrollments.Sum(e => e.PaidAmount),
                AverageRating = c.AverageRating
            })
            .OrderByDescending(c => c.EnrollmentsCount)
            .Take(topCoursesLimit)
            .ToListAsync();

        // Top Instructors
        var topInstructorsLimit = await _configService.GetTopItemsLimitAsync("analytics_top_instructors", 10);
        viewModel.TopInstructors = await _context.InstructorProfiles
            .Include(ip => ip.User)
            .Select(ip => new TopInstructorViewModel
            {
                InstructorId = ip.UserId,
                InstructorName = ip.User != null 
                    ? $"{ip.User.FirstName ?? ""} {ip.User.LastName ?? ""}".Trim()
                    : "Unknown Instructor",
                CoursesCount = _context.Courses.Count(c => c.InstructorId == ip.UserId),
                StudentsCount = _context.Enrollments
                    .Where(e => _context.Courses.Any(c => c.Id == e.CourseId && c.InstructorId == ip.UserId))
                    .Select(e => e.StudentId)
                    .Distinct()
                    .Count(),
                TotalEarnings = ip.TotalEarnings,
                AverageRating = ip.AverageRating
            })
            .OrderByDescending(i => i.TotalEarnings)
            .Take(topInstructorsLimit)
            .ToListAsync();

        // Revenue Chart
        viewModel.RevenueChart = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= startDate)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new RevenueByDayViewModel
            {
                Date = g.Key,
                Amount = g.Sum(p => p.TotalAmount),
                OrdersCount = g.Count()
            })
            .OrderBy(r => r.Date)
            .ToListAsync();

            // Cache the result
            var cacheDuration = await _configService.GetCacheDurationMinutesAsync("analytics", 15);
            _cache.Set(cacheKey, viewModel, TimeSpan.FromMinutes(cacheDuration));

            ViewBag.Days = days;
            ViewBag.FromCache = false;

            _logger.LogInformation("Analytics dashboard loaded for {Days} days", days);

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics dashboard");
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة التحليلات");
            return View(new AnalyticsDashboardViewModel());
        }
    }

    /// <summary>
    /// تقرير المبيعات المفصل - Detailed sales report
    /// </summary>
    public async Task<IActionResult> SalesReport(DateTime? from, DateTime? to)
    {
        from ??= DateTime.UtcNow.AddMonths(-1);
        to ??= DateTime.UtcNow;

        var payments = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .Where(p => p.Status == PaymentStatus.Completed)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.TotalRevenue = payments.Sum(p => p.TotalAmount);
        ViewBag.TotalOrders = payments.Count;
        ViewBag.AverageOrderValue = payments.Any() ? payments.Average(p => p.TotalAmount) : 0;

        return View(payments);
    }

    /// <summary>
    /// تقرير التسجيلات - Enrollments report
    /// </summary>
    public async Task<IActionResult> EnrollmentsReport(DateTime? from, DateTime? to)
    {
        from ??= DateTime.UtcNow.AddMonths(-1);
        to ??= DateTime.UtcNow;

        var enrollments = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Where(e => e.EnrolledAt >= from && e.EnrolledAt <= to)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.TotalEnrollments = enrollments.Count;
        ViewBag.FreeEnrollments = enrollments.Count(e => e.IsFree);
        ViewBag.PaidEnrollments = enrollments.Count(e => !e.IsFree);
        ViewBag.CompletedEnrollments = enrollments.Count(e => e.Status == EnrollmentStatus.Completed);

        return View(enrollments);
    }

    /// <summary>
    /// تقرير أداء الدورات - Course performance report
    /// </summary>
    public async Task<IActionResult> CoursePerformance(int page = 1, string sortBy = "enrollments")
    {
        try
        {
            var pageSize = await _configService.GetPaginationSizeAsync("default", 20);

            // Load courses with instructor data
            var coursesData = await _context.Courses
                .Include(c => c.Instructor)
                .ToListAsync();

            // Build view models with null-safe access
            var courseStats = new List<CoursePerformanceViewModel>();
            
            foreach (var c in coursesData)
            {
                var enrollmentsCount = await _context.Enrollments.CountAsync(e => e.CourseId == c.Id);
                var completedCount = await _context.Enrollments.CountAsync(e => e.CourseId == c.Id && e.Status == EnrollmentStatus.Completed);
                var avgProgress = enrollmentsCount > 0 
                    ? await _context.Enrollments.Where(e => e.CourseId == c.Id).AverageAsync(e => (double?)e.ProgressPercentage) ?? 0
                    : 0;
                var revenue = await _context.Enrollments.Where(e => e.CourseId == c.Id).SumAsync(e => (decimal?)e.PaidAmount) ?? 0;
                var completionRate = enrollmentsCount > 0 ? (completedCount * 100.0 / enrollmentsCount) : 0;

                courseStats.Add(new CoursePerformanceViewModel
                {
                    CourseId = c.Id,
                    CourseTitle = c.Title ?? "بدون عنوان",
                    InstructorName = c.Instructor != null 
                        ? $"{c.Instructor.FirstName ?? ""} {c.Instructor.LastName ?? ""}".Trim()
                        : "غير معروف",
                    Status = c.Status.ToString(),
                    CreatedAt = c.CreatedAt,
                    EnrollmentsCount = enrollmentsCount,
                    CompletedCount = completedCount,
                    AverageProgress = avgProgress,
                    Revenue = revenue,
                    ReviewsCount = c.TotalReviews,
                    AverageRating = c.AverageRating,
                    CompletionRate = completionRate
                });
            }

            // Sort based on parameter
            var sortedCourses = sortBy.ToLower() switch
            {
                "revenue" => courseStats.OrderByDescending(x => x.Revenue).ToList(),
                "rating" => courseStats.OrderByDescending(x => x.AverageRating).ToList(),
                "completion" => courseStats.OrderByDescending(x => x.CompletionRate).ToList(),
                "reviews" => courseStats.OrderByDescending(x => x.ReviewsCount).ToList(),
                _ => courseStats.OrderByDescending(x => x.EnrollmentsCount).ToList()
            };

            var paginatedCourses = sortedCourses
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.SortBy = sortBy;
            ViewBag.TotalPages = (int)Math.Ceiling(courseStats.Count / (double)pageSize);
            ViewBag.TotalCount = courseStats.Count;

            return View(paginatedCourses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course performance report");
            SetErrorMessage("حدث خطأ أثناء تحميل التقرير");
            return View(new List<CoursePerformanceViewModel>());
        }
    }

    /// <summary>
    /// تقرير نشاط المستخدمين - User activity report
    /// </summary>
    public async Task<IActionResult> UserActivity(int days = 7)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        var activeUsersLimit = await _configService.GetPaginationSizeAsync("large_lists", 100);
        var activeUsers = await _context.Users
            .Where(u => !u.IsDeleted && u.LastLoginAt >= startDate)
            .OrderByDescending(u => u.LastLoginAt)
            .Take(activeUsersLimit)
            .ToListAsync();

        ViewBag.Days = days;
        ViewBag.TotalActive = activeUsers.Count;

        return View(activeUsers);
    }

    /// <summary>
    /// تقرير الاستردادات - Refunds report
    /// </summary>
    public async Task<IActionResult> RefundsReport()
    {
        var refunds = await _context.Refunds
            .Include(r => r.Payment)
                .ThenInclude(p => p.Student)
            .Include(r => r.Payment)
                .ThenInclude(p => p.Course)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        ViewBag.TotalRefunds = refunds.Count;
        ViewBag.ApprovedRefunds = refunds.Count(r => r.Status == RefundStatus.Approved);
        ViewBag.PendingRefunds = refunds.Count(r => r.Status == RefundStatus.Pending);
        ViewBag.RefundedAmount = refunds.Where(r => r.Status == RefundStatus.Approved).Sum(r => r.RefundAmount);

        return View(refunds);
    }

    /// <summary>
    /// تصدير البيانات - Export data
    /// </summary>
    public async Task<IActionResult> ExportSales(DateTime? from, DateTime? to)
    {
        try
        {
            from ??= DateTime.UtcNow.AddMonths(-1);
            to ??= DateTime.UtcNow;

            var payments = await _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                .Where(p => p.Status == PaymentStatus.Completed)
                .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Generate CSV
            var csv = "Order ID,Transaction ID,Date,Student Name,Student Email,Course,Original Amount,Discount,Tax,Total Amount,Currency,Payment Method\n";
            foreach (var payment in payments)
            {
                csv += $"{payment.Id}," +
                       $"\"{payment.TransactionId}\"," +
                       $"{payment.CreatedAt:yyyy-MM-dd HH:mm}," +
                       $"\"{payment.Student?.FirstName} {payment.Student?.LastName}\"," +
                       $"{payment.Student?.Email}," +
                       $"\"{payment.Course?.Title ?? "N/A"}\"," +
                       $"{payment.OriginalAmount}," +
                       $"{payment.DiscountAmount}," +
                       $"{payment.TaxAmount}," +
                       $"{payment.TotalAmount}," +
                       $"{payment.Currency}," +
                       $"{payment.PaymentMethod}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            
            _logger.LogInformation("Sales report exported from {From} to {To}. Total: {Count} records", 
                from, to, payments.Count);

            return File(bytes, "text/csv", $"sales-report-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales data");
            SetErrorMessage("حدث خطأ أثناء تصدير البيانات");
            return RedirectToAction(nameof(SalesReport));
        }
    }

    /// <summary>
    /// تقرير المدرسين - Instructors performance report
    /// </summary>
    public async Task<IActionResult> InstructorsReport(int page = 1)
    {
        try
        {
            var pageSize = await _configService.GetPaginationSizeAsync("default", 20);

            var instructors = await _context.InstructorProfiles
                .Include(ip => ip.User)
                .Select(ip => new
                {
                    InstructorId = ip.UserId,
                    Name = $"{ip.User.FirstName} {ip.User.LastName}",
                    Email = ip.User.Email,
                    CoursesCount = _context.Courses.Count(c => c.InstructorId == ip.UserId),
                    PublishedCourses = _context.Courses.Count(c => c.InstructorId == ip.UserId && c.Status == CourseStatus.Published),
                    TotalStudents = _context.Enrollments
                        .Count(e => _context.Courses.Any(c => c.Id == e.CourseId && c.InstructorId == ip.UserId)),
                    TotalEarnings = ip.TotalEarnings,
                    AvailableBalance = ip.AvailableBalance,
                    AverageRating = ip.AverageRating,
                    TotalReviews = ip.TotalReviews,
                    JoinedDate = ip.CreatedAt,
                    IsVerified = ip.IsVerified
                })
                .OrderByDescending(i => i.TotalEarnings)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalInstructors = await _context.InstructorProfiles.CountAsync();

            return View(instructors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructors report");
            SetErrorMessage("حدث خطأ أثناء تحميل تقرير المدرسين");
            return View(new List<object>());
        }
    }

    /// <summary>
    /// تقرير الطلاب - Students report
    /// </summary>
    public async Task<IActionResult> StudentsReport(int page = 1, string sortBy = "enrollments")
    {
        try
        {
            var pageSize = await _configService.GetPaginationSizeAsync("default", 20);

            var studentsQuery = _context.Users
                .Where(u => !u.IsDeleted && _context.Enrollments.Any(e => e.StudentId == u.Id))
                .Select(u => new
                {
                    StudentId = u.Id,
                    Name = $"{u.FirstName} {u.LastName}",
                    Email = u.Email,
                    EnrollmentsCount = _context.Enrollments.Count(e => e.StudentId == u.Id),
                    CompletedCourses = _context.Enrollments.Count(e => e.StudentId == u.Id && e.Status == EnrollmentStatus.Completed),
                    TotalSpent = _context.Payments
                        .Where(p => p.StudentId == u.Id && p.Status == PaymentStatus.Completed)
                        .Sum(p => (decimal?)p.TotalAmount) ?? 0,
                    AverageProgress = _context.Enrollments
                        .Where(e => e.StudentId == u.Id)
                        .Average(e => (double?)e.ProgressPercentage) ?? 0,
                    ReviewsGiven = _context.Reviews.Count(r => r.StudentId == u.Id),
                    JoinedDate = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt
                });

            var sortedStudents = sortBy.ToLower() switch
            {
                "spent" => studentsQuery.OrderByDescending(s => s.TotalSpent),
                "completed" => studentsQuery.OrderByDescending(s => s.CompletedCourses),
                "progress" => studentsQuery.OrderByDescending(s => s.AverageProgress),
                "recent" => studentsQuery.OrderByDescending(s => s.LastLoginAt),
                _ => studentsQuery.OrderByDescending(s => s.EnrollmentsCount)
            };

            var students = await sortedStudents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.SortBy = sortBy;
            ViewBag.TotalStudents = await _context.Users
                .Where(u => !u.IsDeleted && _context.Enrollments.Any(e => e.StudentId == u.Id))
                .CountAsync();

            return View(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading students report");
            SetErrorMessage("حدث خطأ أثناء تحميل تقرير الطلاب");
            return View(new List<object>());
        }
    }

    /// <summary>
    /// تقرير التصنيفات - Categories performance report
    /// </summary>
    public async Task<IActionResult> CategoriesReport()
    {
        try
        {
            var categories = await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .Select(c => new
                {
                    CategoryId = c.Id,
                    CategoryName = c.Name,
                    CoursesCount = c.Courses.Count,
                    PublishedCourses = c.Courses.Count(co => co.Status == CourseStatus.Published),
                    TotalEnrollments = c.Courses.SelectMany(co => co.Enrollments).Count(),
                    TotalRevenue = c.Courses.SelectMany(co => co.Enrollments).Sum(e => (decimal?)e.PaidAmount) ?? 0,
                    AverageRating = c.Courses.Any() ? c.Courses.Average(co => co.AverageRating) : 0,
                    SubCategoriesCount = c.SubCategories.Count,
                    IsActive = c.IsActive
                })
                .OrderByDescending(c => c.TotalRevenue)
                .ToListAsync();

            return View(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories report");
            SetErrorMessage("حدث خطأ أثناء تحميل تقرير التصنيفات");
            return View(new List<object>());
        }
    }

    /// <summary>
    /// مسح ذاكرة التخزين المؤقت - Clear analytics cache
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCache()
    {
        try
        {
            // Clear all analytics caches
            var cacheKeys = new[] { 7, 14, 30, 60, 90 };
            foreach (var days in cacheKeys)
            {
                _cache.Remove($"analytics_dashboard_{days}");
            }

            _logger.LogInformation("Analytics cache cleared");
            SetSuccessMessage("تم مسح ذاكرة التخزين المؤقت بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            SetErrorMessage("حدث خطأ أثناء مسح الذاكرة المؤقتة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تقرير مقارن - Comparative report
    /// </summary>
    public async Task<IActionResult> ComparativeReport(DateTime? period1Start, DateTime? period1End, 
        DateTime? period2Start, DateTime? period2End)
    {
        try
        {
            period1Start ??= DateTime.UtcNow.AddMonths(-2);
            period1End ??= DateTime.UtcNow.AddMonths(-1);
            period2Start ??= DateTime.UtcNow.AddMonths(-1);
            period2End ??= DateTime.UtcNow;

            var period1Data = await GetPeriodData(period1Start.Value, period1End.Value);
            var period2Data = await GetPeriodData(period2Start.Value, period2End.Value);

            var comparison = new
            {
                Period1 = period1Data,
                Period2 = period2Data,
                RevenueGrowth = period1Data.Revenue > 0 
                    ? ((period2Data.Revenue - period1Data.Revenue) / period1Data.Revenue) * 100 
                    : 0,
                EnrollmentsGrowth = period1Data.Enrollments > 0 
                    ? ((period2Data.Enrollments - period1Data.Enrollments) / (double)period1Data.Enrollments) * 100 
                    : 0,
                UsersGrowth = period1Data.NewUsers > 0 
                    ? ((period2Data.NewUsers - period1Data.NewUsers) / (double)period1Data.NewUsers) * 100 
                    : 0,
                Period1Start = period1Start,
                Period1End = period1End,
                Period2Start = period2Start,
                Period2End = period2End
            };

            return View(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comparative report");
            SetErrorMessage("حدث خطأ أثناء تحميل التقرير المقارن");
            return View();
        }
    }

    #region Helper Methods

    private async Task<dynamic> GetPeriodData(DateTime start, DateTime end)
    {
        return new
        {
            Revenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= start && p.CreatedAt <= end)
                .SumAsync(p => (decimal?)p.TotalAmount) ?? 0,
            Enrollments = await _context.Enrollments
                .CountAsync(e => e.EnrolledAt >= start && e.EnrolledAt <= end),
            NewUsers = await _context.Users
                .CountAsync(u => u.CreatedAt >= start && u.CreatedAt <= end && !u.IsDeleted),
            NewCourses = await _context.Courses
                .CountAsync(c => c.CreatedAt >= start && c.CreatedAt <= end),
            Refunds = await _context.Refunds
                .CountAsync(r => r.RequestedAt >= start && r.RequestedAt <= end),
            RefundedAmount = await _context.Refunds
                .Where(r => r.Status == RefundStatus.Approved && r.RequestedAt >= start && r.RequestedAt <= end)
                .SumAsync(r => (decimal?)r.RefundAmount) ?? 0
        };
    }

    #endregion
}

