using LMS.Areas.Instructor.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// تحليلات المدرس - Instructor Analytics Controller
/// </summary>
public class AnalyticsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService,
        ICurrencyService currencyService,
        ILogger<AnalyticsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configService = configService;
        _currencyService = currencyService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة التحليلات - Analytics Dashboard (Enhanced with comprehensive metrics)
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, DateTime? fromDate, DateTime? toDate)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, null, _logger);
        
        var userId = _currentUserService.UserId;

        // Default date range: last 30 days (configurable)
        var defaultDateRangeDays = await _configService.GetIntConfigurationAsync("TimePeriods", "last_30_days", Constants.Analytics.DefaultDateRangeDays);
        fromDate ??= DateTime.UtcNow.AddDays(-defaultDateRangeDays);
        toDate ??= DateTime.UtcNow;

        var coursesQuery = _context.Courses.Where(c => c.InstructorId == userId);

        if (courseId.HasValue)
        {
            coursesQuery = coursesQuery.Where(c => c.Id == courseId.Value);
        }

        var courses = await coursesQuery.ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();

        var enrollmentsQuery = _context.Enrollments
            .Include(e => e.LessonProgress)
            .Where(e => courseIds.Contains(e.CourseId));

        if (fromDate.HasValue)
        {
            enrollmentsQuery = enrollmentsQuery.Where(e => e.EnrolledAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            enrollmentsQuery = enrollmentsQuery.Where(e => e.EnrolledAt <= toDate.Value);
        }

        var enrollments = await enrollmentsQuery.ToListAsync();

        // Get instructor profile for earnings
        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);

        var stats = new InstructorAnalyticsViewModel
        {
            // Basic Metrics
            TotalCourses = courses.Count,
            PublishedCourses = courses.Count(c => c.Status == LMS.Domain.Enums.CourseStatus.Published),
            TotalStudents = enrollments.Select(e => e.StudentId).Distinct().Count(),
            TotalEnrollments = enrollments.Count,
            CompletedEnrollments = enrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed),
            ActiveEnrollments = enrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Active),
            
            // Revenue Metrics
            TotalRevenue = enrollments.Sum(e => e.PaidAmount),
            AverageRevenuePerStudent = enrollments.Any() 
                ? enrollments.Sum(e => e.PaidAmount) / enrollments.Select(e => e.StudentId).Distinct().Count() 
                : 0,
            
            // Rating & Reviews
            AverageRating = courses.Any() ? courses.Average(c => c.AverageRating) : 0,
            TotalReviews = courses.Sum(c => c.TotalReviews),
            
            // Engagement Metrics
            CompletionRate = enrollments.Any() 
                ? (decimal)enrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed) / enrollments.Count * 100 
                : 0,
            AverageProgress = enrollments.Any() 
                ? (decimal)enrollments.Average(e => (double)e.ProgressPercentage) 
                : 0,
            
            // Instructor Profile
            AvailableBalance = instructorProfile?.AvailableBalance ?? 0,
            PendingBalance = instructorProfile?.PendingBalance ?? 0,
            TotalEarnings = instructorProfile?.TotalEarnings ?? 0,
            CommissionRate = instructorProfile?.CommissionRate ?? 0,
            
            // Course Breakdown with detailed metrics
            CourseBreakdown = courses.Select(c => new CourseAnalyticsItemViewModel
            {
                CourseId = c.Id,
                CourseTitle = c.Title,
                TotalStudents = c.TotalStudents,
                AverageRating = c.AverageRating,
                TotalReviews = c.TotalReviews,
                Revenue = enrollments.Where(e => e.CourseId == c.Id).Sum(e => e.PaidAmount),
                CompletionRate = enrollments.Where(e => e.CourseId == c.Id).Any()
                    ? (decimal)enrollments.Where(e => e.CourseId == c.Id && e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed).Count() 
                      / enrollments.Where(e => e.CourseId == c.Id).Count() * 100
                    : 0,
                AverageProgress = enrollments.Where(e => e.CourseId == c.Id).Any()
                    ? (decimal)enrollments.Where(e => e.CourseId == c.Id).Average(e => (double)e.ProgressPercentage)
                    : 0
            }).ToList()
        };

        // Enrollment Trend (last 30 days)
        stats.EnrollmentTrend = await GetEnrollmentTrend(courseIds, fromDate.Value, toDate.Value);

        // Revenue Trend
        stats.RevenueTrend = await GetRevenueTrend(courseIds, fromDate.Value, toDate.Value);

        // Top Performing Students (for motivation/recognition)
        stats.TopStudents = await _context.Enrollments
            .Include(e => e.Student)
            .Where(e => courseIds.Contains(e.CourseId))
            .OrderByDescending(e => e.ProgressPercentage)
            .ThenByDescending(e => e.FinalGrade)
            .Take(Constants.DisplayLimits.TopStudentsOnDashboard)
            .Select(e => new TopStudentViewModel
            {
                StudentName = e.Student.FullName,
                StudentImageUrl = e.Student.ProfileImageUrl,
                CourseName = e.Course.Title,
                ProgressPercentage = e.ProgressPercentage,
                FinalGrade = e.FinalGrade
            })
            .ToListAsync();

        // Most Engaged Lessons (students spend most time)
        stats.MostEngagedLessons = await _context.LessonProgress
            .Include(lp => lp.Lesson)
            .Where(lp => courseIds.Contains(lp.Lesson.Module.CourseId))
            .GroupBy(lp => new { lp.LessonId, lp.Lesson.Title })
            .Select(g => new LessonEngagementViewModel
            {
                LessonId = g.Key.LessonId,
                LessonTitle = g.Key.Title,
                TotalWatchTime = g.Sum(lp => lp.WatchedSeconds),
                TotalStudents = g.Select(lp => lp.EnrollmentId).Distinct().Count(),
                AverageWatchTime = g.Average(lp => lp.WatchedSeconds)
            })
            .OrderByDescending(l => l.TotalWatchTime)
            .Take(Constants.DisplayLimits.MostEngagedLessons)
            .ToListAsync();

        ViewBag.Courses = courses;
        ViewBag.CourseId = courseId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        _logger.LogInformation("Analytics loaded for instructor {InstructorId}, {CourseCount} courses, {StudentCount} students", 
            userId, stats.TotalCourses, stats.TotalStudents);

        return View(stats);
    }

    /// <summary>
    /// Get enrollment trend data for chart
    /// </summary>
    private async Task<List<TrendDataPoint>> GetEnrollmentTrend(List<int> courseIds, DateTime fromDate, DateTime toDate)
    {
        return await _context.Enrollments
            .Where(e => courseIds.Contains(e.CourseId) && e.EnrolledAt >= fromDate && e.EnrolledAt <= toDate)
            .GroupBy(e => e.EnrolledAt.Date)
            .Select(g => new TrendDataPoint
            {
                Date = g.Key,
                Value = g.Count()
            })
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Get revenue trend data for chart
    /// </summary>
    private async Task<List<TrendDataPoint>> GetRevenueTrend(List<int> courseIds, DateTime fromDate, DateTime toDate)
    {
        return await _context.Enrollments
            .Where(e => courseIds.Contains(e.CourseId) && e.EnrolledAt >= fromDate && e.EnrolledAt <= toDate)
            .GroupBy(e => e.EnrolledAt.Date)
            .Select(g => new TrendDataPoint
            {
                Date = g.Key,
                Value = g.Sum(e => e.PaidAmount)
            })
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    /// <summary>
    /// نظرة عامة على التحليلات - Analytics Overview
    /// </summary>
    public async Task<IActionResult> Overview()
    {
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

        var totalStudents = await _context.Enrollments
            .Where(e => e.Course.InstructorId == userId)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync();

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);

        var totalRevenue = instructorProfile?.TotalEarnings ?? 0;
        var averageRating = instructorProfile?.AverageRating ?? 0;

        var totalEnrollments = await _context.Enrollments
            .CountAsync(e => e.Course.InstructorId == userId);

        var completedEnrollments = await _context.Enrollments
            .CountAsync(e => e.Course.InstructorId == userId && 
                           e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed);

        var completionRate = totalEnrollments > 0 
            ? (decimal)completedEnrollments / totalEnrollments * 100 
            : 0;

        // Calculate month-over-month changes
        var studentsThisMonth = await _context.Enrollments
            .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= firstDayOfMonth)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync();

        var revenueThisMonth = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId && e.CreatedAt >= firstDayOfMonth)
            .SumAsync(e => e.NetAmount);

        var revenueLastMonth = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId && e.CreatedAt >= firstDayOfLastMonth && e.CreatedAt < firstDayOfMonth)
            .SumAsync(e => e.NetAmount);

        var revenueGrowth = revenueLastMonth > 0 
            ? ((revenueThisMonth - revenueLastMonth) / revenueLastMonth) * 100 
            : (revenueThisMonth > 0 ? 100 : 0);

        var completedThisMonth = await _context.Enrollments
            .CountAsync(e => e.Course.InstructorId == userId && 
                           e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed &&
                           e.CompletedAt >= firstDayOfMonth);

        var totalReviews = await _context.Reviews
            .CountAsync(r => r.Course.InstructorId == userId && r.IsApproved);

        // Basic stats
        ViewBag.TotalStudents = totalStudents;
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.AverageRating = averageRating;
        ViewBag.CompletionRate = completionRate.ToString("F1");
        ViewBag.TotalReviews = totalReviews;

        // Dynamic change values
        ViewBag.NewStudentsThisMonth = studentsThisMonth;
        ViewBag.RevenueGrowth = revenueGrowth;
        ViewBag.CompletedThisMonth = completedThisMonth;

        // Engagement metrics
        ViewBag.TotalViews = await _context.LessonProgress
            .Where(lp => lp.Enrollment.Course.InstructorId == userId)
            .CountAsync();

        ViewBag.CompletedLessons = await _context.LessonProgress
            .Where(lp => lp.Enrollment.Course.InstructorId == userId && lp.IsCompleted)
            .CountAsync();

        ViewBag.TotalDiscussions = await _context.Discussions
            .Where(d => d.Course.InstructorId == userId)
            .CountAsync();

        var avgWatchSeconds = await _context.LessonProgress
            .Where(lp => lp.Enrollment.Course.InstructorId == userId && lp.WatchedSeconds > 0)
            .Select(lp => (double?)lp.WatchedSeconds)
            .AverageAsync() ?? 0;
        
        ViewBag.AverageWatchTime = (int)(avgWatchSeconds / 60); // Convert to minutes

        // Chart data - Revenue and Enrollments per day (last 7 days)
        var dayNames = await _configService.GetDayNamesAsync("ar");
        var chartLabels = new List<string>();
        var revenueChartData = new List<decimal>();
        var enrollmentChartData = new List<int>();

        var enrollmentsByDay = await _context.Enrollments
            .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= sevenDaysAgo)
            .GroupBy(e => e.EnrolledAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(e => e.PaidAmount), Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x);

        for (int i = Constants.DisplayLimits.ChartDataPoints - 1; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var dayName = dayNames.TryGetValue(date.DayOfWeek, out var name) ? name : date.DayOfWeek.ToString();
            chartLabels.Add(dayName);
            
            if (enrollmentsByDay.TryGetValue(date, out var dayData))
            {
                revenueChartData.Add(dayData.Revenue);
                enrollmentChartData.Add(dayData.Count);
            }
            else
            {
                revenueChartData.Add(0);
                enrollmentChartData.Add(0);
            }
        }

        ViewBag.ChartLabels = chartLabels;
        ViewBag.RevenueChartData = revenueChartData;
        ViewBag.EnrollmentChartData = enrollmentChartData;

        // Course status breakdown for doughnut chart
        var courseIds = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        var allEnrollments = await _context.Enrollments
            .Where(e => courseIds.Contains(e.CourseId))
            .ToListAsync();

        ViewBag.CompletedCount = allEnrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed);
        ViewBag.InProgressCount = allEnrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Active && e.ProgressPercentage > 0);
        ViewBag.NotStartedCount = allEnrollments.Count(e => e.ProgressPercentage == 0);

        // Student activity by day (active students per day)
        var studentActivityData = new List<int>();
        for (int i = Constants.DisplayLimits.ChartDataPoints - 1; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var activeCount = await _context.LessonProgress
                .Where(lp => lp.Enrollment.Course.InstructorId == userId && 
                            lp.LastAccessedAt.Date == date)
                .Select(lp => lp.Enrollment.StudentId)
                .Distinct()
                .CountAsync();
            studentActivityData.Add(activeCount);
        }
        ViewBag.StudentActivityData = studentActivityData;

        // Top courses by revenue with growth calculation
        var topCoursesQuery = await _context.Enrollments
            .Where(e => e.Course.InstructorId == userId)
            .GroupBy(e => new { e.CourseId, e.Course.Title })
            .Select(g => new {
                CourseId = g.Key.CourseId,
                Title = g.Key.Title,
                EnrollmentCount = g.Count(),
                Revenue = g.Sum(e => e.PaidAmount),
                Enrollments = g.ToList()
            })
            .OrderByDescending(x => x.Revenue)
            .Take(Constants.DisplayLimits.TopCoursesOnAnalytics)
            .ToListAsync();
        
        // Calculate growth percentage for each course
        var topCourses = topCoursesQuery.Select(c => {
            var enrollmentsThisMonth = c.Enrollments.Count(e => e.EnrolledAt >= firstDayOfMonth);
            var enrollmentsLastMonth = c.Enrollments.Count(e => e.EnrolledAt >= firstDayOfLastMonth && e.EnrolledAt < firstDayOfMonth);
            var growthPercentage = enrollmentsLastMonth > 0 
                ? ((enrollmentsThisMonth - enrollmentsLastMonth) * 100.0 / enrollmentsLastMonth) 
                : (enrollmentsThisMonth > 0 ? 100 : 0);
            
            return new {
                c.Title,
                c.EnrollmentCount,
                c.Revenue,
                GrowthPercentage = Math.Round(growthPercentage, 1)
            };
        }).ToList();
        
        ViewBag.TopCourses = topCourses;

        // Recent activities
        var recentEnrollments = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Where(e => e.Course.InstructorId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Take(Constants.DisplayLimits.RecentActivitiesOnDashboard / 2)
            .Select(e => new {
                Title = $"طالب جديد: {e.Student.FullName}",
                Description = $"سجل في دورة {e.Course.Title}",
                Time = e.EnrolledAt.ToTimeAgo(),
                Icon = "user-plus",
                IconColor = "primary"
            })
            .ToListAsync();

        var recentReviews = await _context.Reviews
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Where(r => r.Course.InstructorId == userId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Take(Constants.DisplayLimits.RecentActivitiesOnDashboard / 2)
            .Select(r => new {
                Title = $"تقييم جديد: {r.Rating} نجوم",
                Description = $"من {r.Student.FullName} على {r.Course.Title}",
                Time = r.CreatedAt.ToTimeAgo(),
                Icon = "star",
                IconColor = "warning"
            })
            .ToListAsync();

        var activities = new List<dynamic>();
        activities.AddRange(recentEnrollments.Select(e => (dynamic)e));
        activities.AddRange(recentReviews.Select(r => (dynamic)r));
        ViewBag.RecentActivities = activities.Take(Constants.DisplayLimits.RecentActivitiesOnDashboard).ToList();

        _logger.LogInformation("Overview analytics loaded for instructor {InstructorId}", userId);

        return View();
    }


    /// <summary>
    /// تحليلات الدورة - Course Analytics
    /// </summary>
    public async Task<IActionResult> CourseDetails(int courseId)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, null, _logger);
        
        var userId = _currentUserService.UserId;

        var course = await _context.Courses
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        var enrollments = await _context.Enrollments
            .Where(e => e.CourseId == courseId)
            .ToListAsync();

        var lessonProgress = await _context.LessonProgress
            .Include(lp => lp.Lesson)
            .Where(lp => lp.Enrollment.CourseId == courseId)
            .ToListAsync();

        ViewBag.Course = course;
        ViewBag.Enrollments = enrollments;
        ViewBag.LessonProgress = lessonProgress;

        return View();
    }

    /// <summary>
    /// تحليلات الأداء - Performance Analytics
    /// </summary>
    public async Task<IActionResult> Performance(int? courseId, DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;

        // Default date range: last 30 days (configurable)
        var defaultDateRangeDays = await _configService.GetIntConfigurationAsync("TimePeriods", "last_30_days", Constants.Analytics.DefaultDateRangeDays);
        fromDate ??= DateTime.UtcNow.AddDays(-defaultDateRangeDays);
        toDate ??= DateTime.UtcNow;

        var coursesQuery = _context.Courses.Where(c => c.InstructorId == userId);

        if (courseId.HasValue)
        {
            coursesQuery = coursesQuery.Where(c => c.Id == courseId.Value);
        }

        var courses = await coursesQuery.ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();

        // Get enrollments in date range
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Include(e => e.Student)
            .Where(e => courseIds.Contains(e.CourseId) && 
                       e.EnrolledAt >= fromDate.Value && 
                       e.EnrolledAt <= toDate.Value)
            .ToListAsync();

        // Get quiz attempts
        var quizAttempts = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
            .Where(qa => courseIds.Contains(qa.Quiz.Lesson.Module.CourseId) && 
                        qa.StartedAt >= fromDate.Value && 
                        qa.StartedAt <= toDate.Value)
            .ToListAsync();

        // Get assignment submissions
        var assignments = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Where(s => courseIds.Contains(s.Assignment.Lesson.Module.CourseId) && 
                       s.SubmittedAt >= fromDate.Value && 
                       s.SubmittedAt <= toDate.Value)
            .ToListAsync();

        // Calculate performance metrics
        ViewBag.TotalEnrollments = enrollments.Count;
        ViewBag.ActiveStudents = enrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Active);
        ViewBag.CompletedEnrollments = enrollments.Count(e => e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed);
        ViewBag.AverageProgress = enrollments.Any() ? enrollments.Average(e => (double)e.ProgressPercentage) : 0;
        ViewBag.AverageQuizScore = quizAttempts.Any() ? quizAttempts.Average(qa => (double)qa.Score) : 0;
        ViewBag.AverageAssignmentGrade = assignments.Where(a => a.Grade.HasValue).Any() 
            ? assignments.Where(a => a.Grade.HasValue).Average(a => (double)a.Grade!.Value) 
            : 0;
        ViewBag.TotalQuizAttempts = quizAttempts.Count;
        ViewBag.TotalAssignmentSubmissions = assignments.Count;

        // Course breakdown
        ViewBag.CoursePerformance = courses.Select(c => new
        {
            CourseId = c.Id,
            CourseTitle = c.Title,
            Enrollments = enrollments.Count(e => e.CourseId == c.Id),
            AverageProgress = enrollments.Where(e => e.CourseId == c.Id).Any() 
                ? enrollments.Where(e => e.CourseId == c.Id).Average(e => (double)e.ProgressPercentage) 
                : 0,
            CompletionRate = enrollments.Where(e => e.CourseId == c.Id).Any()
                ? (enrollments.Count(e => e.CourseId == c.Id && e.Status == LMS.Domain.Enums.EnrollmentStatus.Completed) * 100.0 
                   / enrollments.Count(e => e.CourseId == c.Id))
                : 0
        }).ToList();

        ViewBag.Courses = courses;
        ViewBag.CourseId = courseId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        _logger.LogInformation("Performance analytics loaded for instructor {InstructorId}", userId);

        return View();
    }

    /// <summary>
    /// تحليلات تفاعل الطلاب - Student Engagement Analytics
    /// </summary>
    public async Task<IActionResult> StudentEngagement(int? courseId, DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;

        // Default date range: last 30 days (configurable)
        var defaultDateRangeDays = await _configService.GetIntConfigurationAsync("TimePeriods", "last_30_days", Constants.Analytics.DefaultDateRangeDays);
        fromDate ??= DateTime.UtcNow.AddDays(-defaultDateRangeDays);
        toDate ??= DateTime.UtcNow;

        var coursesQuery = _context.Courses.Where(c => c.InstructorId == userId);

        if (courseId.HasValue)
        {
            coursesQuery = coursesQuery.Where(c => c.Id == courseId.Value);
        }

        var courses = await coursesQuery.ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();

        // Get lesson progress data
        var lessonProgress = await _context.LessonProgress
            .Include(lp => lp.Lesson)
            .Include(lp => lp.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(lp => courseIds.Contains(lp.Lesson.Module.CourseId) && 
                        lp.LastAccessedAt >= fromDate.Value && 
                        lp.LastAccessedAt <= toDate.Value)
            .ToListAsync();

        // Get discussion activity (replies count)
        var discussionReplies = await _context.DiscussionReplies
            .Include(dr => dr.Discussion)
            .Where(dr => courseIds.Contains(dr.Discussion.CourseId) && 
                        dr.CreatedAt >= fromDate.Value && 
                        dr.CreatedAt <= toDate.Value)
            .ToListAsync();

        // Get comments
        var comments = await _context.Comments
            .Where(c => courseIds.Contains(c.CourseId.HasValue ? c.CourseId.Value : 0) && 
                       c.CreatedAt >= fromDate.Value && 
                       c.CreatedAt <= toDate.Value)
            .ToListAsync();

        // Calculate engagement metrics
        ViewBag.TotalWatchTime = lessonProgress.Sum(lp => lp.WatchedSeconds) / 3600.0; // Convert to hours
        ViewBag.AverageWatchTime = lessonProgress.Any() ? lessonProgress.Average(lp => lp.WatchedSeconds) / 60.0 : 0; // Minutes per session
        ViewBag.TotalLessonViews = lessonProgress.Count;
        ViewBag.UniqueActiveStudents = lessonProgress.Select(lp => lp.Enrollment.StudentId).Distinct().Count();
        ViewBag.TotalDiscussionPosts = discussionReplies.Count;
        ViewBag.TotalComments = comments.Count;
        ViewBag.CompletedLessons = lessonProgress.Count(lp => lp.IsCompleted);
        ViewBag.CompletionRate = lessonProgress.Any() 
            ? (lessonProgress.Count(lp => lp.IsCompleted) * 100.0 / lessonProgress.Count) 
            : 0;

        // Most engaged lessons
        ViewBag.MostEngagedLessons = lessonProgress
            .GroupBy(lp => new { lp.LessonId, lp.Lesson.Title })
            .Select(g => new
            {
                LessonId = g.Key.LessonId,
                LessonTitle = g.Key.Title,
                TotalWatchTime = g.Sum(lp => lp.WatchedSeconds) / 3600.0, // Hours
                UniqueStudents = g.Select(lp => lp.EnrollmentId).Distinct().Count(),
                CompletionRate = g.Count(lp => lp.IsCompleted) * 100.0 / g.Count()
            })
            .OrderByDescending(l => l.TotalWatchTime)
            .Take(Constants.DisplayLimits.MostEngagedLessons)
            .ToList();

        // Most active students
        ViewBag.MostActiveStudents = lessonProgress
            .GroupBy(lp => new { lp.Enrollment.StudentId, lp.Enrollment.Student.FirstName, lp.Enrollment.Student.LastName })
            .Select(g => new
            {
                StudentId = g.Key.StudentId,
                StudentName = $"{g.Key.FirstName} {g.Key.LastName}",
                TotalWatchTime = g.Sum(lp => lp.WatchedSeconds) / 3600.0, // Hours
                LessonsCompleted = g.Count(lp => lp.IsCompleted),
                LastActivity = g.Max(lp => lp.LastAccessedAt)
            })
            .OrderByDescending(s => s.TotalWatchTime)
            .Take(Constants.DisplayLimits.TopStudentsOnDashboard)
            .ToList();

        // Daily engagement trend
        ViewBag.EngagementTrend = lessonProgress
            .GroupBy(lp => lp.LastAccessedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                ActiveStudents = g.Select(lp => lp.EnrollmentId).Distinct().Count(),
                TotalWatchTime = g.Sum(lp => lp.WatchedSeconds) / 3600.0,
                LessonsCompleted = g.Count(lp => lp.IsCompleted)
            })
            .OrderBy(t => t.Date)
            .ToList();

        ViewBag.Courses = courses;
        ViewBag.CourseId = courseId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        _logger.LogInformation("Student engagement analytics loaded for instructor {InstructorId}", userId);

        return View();
    }
}

