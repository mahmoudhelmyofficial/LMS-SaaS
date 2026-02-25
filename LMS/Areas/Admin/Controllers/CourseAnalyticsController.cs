using LMS.Data;
using LMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// تحليلات الدورات - Course Analytics Controller
/// </summary>
public class CourseAnalyticsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CourseAnalyticsController> _logger;

    public CourseAnalyticsController(
        ApplicationDbContext context,
        ILogger<CourseAnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// تحليلات الدورات - Course Analytics
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.CourseAnalytics
            .Include(ca => ca.Course)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(ca => ca.CourseId == courseId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(ca => ca.AnalyticsDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ca => ca.AnalyticsDate <= toDate.Value);
        }

        var analytics = await query
            .OrderByDescending(ca => ca.AnalyticsDate)
            .Take(100)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
        ViewBag.CourseId = courseId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(analytics);
    }

    /// <summary>
    /// تحليلات دورة محددة - Analytics for a specific course
    /// </summary>
    public async Task<IActionResult> Analytics(int id, DateTime? from, DateTime? to)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found: {CourseId}", id);
                return NotFound();
            }

            from ??= DateTime.UtcNow.AddMonths(-1);
            to ??= DateTime.UtcNow;

            // Enrollment statistics
            var totalEnrollments = await _context.Enrollments
                .CountAsync(e => e.CourseId == id);

            var activeEnrollments = await _context.Enrollments
                .CountAsync(e => e.CourseId == id && e.Status == EnrollmentStatus.Active);

            var completedEnrollments = await _context.Enrollments
                .CountAsync(e => e.CourseId == id && e.Status == EnrollmentStatus.Completed);

            var enrollmentsInPeriod = await _context.Enrollments
                .Where(e => e.CourseId == id && e.EnrolledAt >= from && e.EnrolledAt <= to)
                .CountAsync();

            // Revenue statistics
            var totalRevenue = await _context.Enrollments
                .Where(e => e.CourseId == id && e.PaidAmount > 0)
                .SumAsync(e => (decimal?)e.PaidAmount) ?? 0;

            var revenueInPeriod = await _context.Enrollments
                .Where(e => e.CourseId == id && e.PaidAmount > 0 && 
                           e.EnrolledAt >= from && e.EnrolledAt <= to)
                .SumAsync(e => (decimal?)e.PaidAmount) ?? 0;

            // Progress statistics
            var averageProgress = await _context.Enrollments
                .Where(e => e.CourseId == id && e.Status == EnrollmentStatus.Active)
                .AverageAsync(e => (double?)e.ProgressPercentage) ?? 0;

            // Review statistics
            var totalReviews = await _context.Reviews
                .CountAsync(r => r.CourseId == id);

            var averageRating = totalReviews > 0 
                ? await _context.Reviews
                    .Where(r => r.CourseId == id)
                    .AverageAsync(r => r.Rating)
                : 0;

            // Daily enrollment chart data
            var dailyEnrollments = await _context.Enrollments
                .Where(e => e.CourseId == id && e.EnrolledAt >= from && e.EnrolledAt <= to)
                .GroupBy(e => e.EnrolledAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Top students
            var topStudents = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.CourseId == id)
                .OrderByDescending(e => e.ProgressPercentage)
                .Take(10)
                .Select(e => new
                {
                    Student = e.Student,
                    Progress = e.ProgressPercentage,
                    Status = e.Status,
                    EnrolledAt = e.EnrolledAt
                })
                .ToListAsync();

            // Recent reviews
            var recentReviews = await _context.Reviews
                .Include(r => r.Student)
                .Where(r => r.CourseId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.Course = course;
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.TotalEnrollments = totalEnrollments;
            ViewBag.ActiveEnrollments = activeEnrollments;
            ViewBag.CompletedEnrollments = completedEnrollments;
            ViewBag.EnrollmentsInPeriod = enrollmentsInPeriod;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.RevenueInPeriod = revenueInPeriod;
            ViewBag.AverageProgress = averageProgress;
            ViewBag.TotalReviews = totalReviews;
            ViewBag.AverageRating = averageRating;
            ViewBag.DailyEnrollments = dailyEnrollments;
            ViewBag.TopStudents = topStudents;
            ViewBag.RecentReviews = recentReviews;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course analytics {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل التحليلات");
            return RedirectToAction("Details", "Courses", new { id });
        }
    }

    /// <summary>
    /// تفاصيل التحليلات - Analytics Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var analytics = await _context.CourseAnalytics
            .Include(ca => ca.Course)
            .FirstOrDefaultAsync(ca => ca.Id == id);

        if (analytics == null)
            return NotFound();

        return View(analytics);
    }

    /// <summary>
    /// مقارنة الدورات - Compare Courses
    /// </summary>
    public async Task<IActionResult> Compare(List<int> courseIds, DateTime? fromDate, DateTime? toDate)
    {
        if (courseIds == null || !courseIds.Any())
        {
            SetErrorMessage("الرجاء اختيار دورات للمقارنة");
            return RedirectToAction(nameof(Index));
        }

        var query = _context.CourseAnalytics
            .Include(ca => ca.Course)
            .Where(ca => courseIds.Contains(ca.CourseId))
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(ca => ca.AnalyticsDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ca => ca.AnalyticsDate <= toDate.Value);
        }

        var analytics = await query.ToListAsync();

        var comparison = courseIds.Select(courseId => new
        {
            Course = analytics.FirstOrDefault(a => a.CourseId == courseId)?.Course,
            TotalViews = analytics.Where(a => a.CourseId == courseId).Sum(a => a.TotalViews),
            TotalEnrollments = analytics.Where(a => a.CourseId == courseId).Sum(a => a.NewEnrollments),
            AvgCompletionRate = analytics.Where(a => a.CourseId == courseId).Any() 
                ? analytics.Where(a => a.CourseId == courseId).Average(a => a.CompletionRate) 
                : 0
        }).ToList();

        ViewBag.Comparison = comparison;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View();
    }
}

