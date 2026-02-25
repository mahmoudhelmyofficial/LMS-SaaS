using LMS.Areas.Admin.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// لوحة تحكم المدرس - Instructor Dashboard Controller
/// Includes caching for performance optimization
/// </summary>
public class DashboardController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;
    private readonly ICurrencyService _currencyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardController> _logger;

    // Cache configuration
    private static readonly TimeSpan StatsCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ChartCacheExpiration = TimeSpan.FromMinutes(10);
    private const string DashboardStatsCacheKeyPrefix = "InstructorDashboard_Stats_";
    private const string DashboardChartCacheKeyPrefix = "InstructorDashboard_Chart_";

    public DashboardController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService,
        ICurrencyService currencyService,
        IMemoryCache cache,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configService = configService;
        _currencyService = currencyService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Diagnostic endpoint to check user authentication and roles
    /// </summary>
    public IActionResult CheckAuth()
    {
        var userId = _currentUserService.UserId;
        var userName = _currentUserService.UserName;
        var email = _currentUserService.Email;
        var isAuthenticated = _currentUserService.IsAuthenticated;
        var isInstructor = _currentUserService.IsInstructor;
        var isAdmin = _currentUserService.IsAdmin;
        var isStudent = _currentUserService.IsStudent;

        var info = new
        {
            UserId = userId,
            UserName = userName,
            Email = email,
            IsAuthenticated = isAuthenticated,
            IsInstructor = isInstructor,
            IsAdmin = isAdmin,
            IsStudent = isStudent,
            HasUserId = !string.IsNullOrEmpty(userId),
            AllClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        };

        _logger.LogInformation("Auth Check: {@AuthInfo}", info);

        return Json(info);
    }

    /// <summary>
    /// الصفحة الرئيسية للوحة التحكم - Main dashboard page
    /// Implements caching for improved performance
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;

        try
        {
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (instructorProfile == null)
            {
                _logger.LogWarning("Instructor profile not found for user {UserId}, creating one", userId);
                
                // Auto-create instructor profile
                instructorProfile = new Domain.Entities.Users.InstructorProfile
                {
                    UserId = userId!,
                    Bio = string.Empty,
                    Specialization = string.Empty,
                    YearsOfExperience = 0,
                    TotalEarnings = 0,
                    AvailableBalance = 0,
                    PendingBalance = 0,
                    TotalWithdrawn = 0,
                    AverageRating = 0,
                    TotalStudents = 0,
                    IsApproved = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.InstructorProfiles.Add(instructorProfile);
                await _context.SaveChangesAsync();
                
                // Invalidate cache for this user
                InvalidateDashboardCache(userId!);
                
                SetSuccessMessage("تم إنشاء ملفك الشخصي كمدرس. يرجى تحديث معلوماتك");
            }

            // Try to get cached statistics
            var statsCacheKey = $"{DashboardStatsCacheKeyPrefix}{userId}";
            if (!_cache.TryGetValue(statsCacheKey, out InstructorDashboardStatsCache? cachedStats))
            {
                cachedStats = await LoadDashboardStatsAsync(userId!, instructorProfile);
                
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(StatsCacheExpiration)
                    .SetPriority(CacheItemPriority.Normal);
                
                _cache.Set(statsCacheKey, cachedStats, cacheEntryOptions);
                _logger.LogDebug("Dashboard stats cached for instructor {InstructorId}", userId);
            }

            // Get instructor user info (not cached as it's lightweight)
            var user = await _context.Users.FindAsync(userId);
            
            var stats = new InstructorDashboardViewModel
            {
                InstructorName = user != null ? $"{user.FirstName} {user.LastName}" : Constants.Defaults.DefaultInstructorName,
                
                // Apply cached statistics
                TotalCourses = cachedStats!.TotalCourses,
                PublishedCourses = cachedStats.PublishedCourses,
                DraftCourses = cachedStats.DraftCourses,
                PendingReviewCourses = cachedStats.PendingReviewCourses,
                TotalStudents = cachedStats.TotalStudents,
                ActiveStudents = cachedStats.ActiveStudents,
                NewStudentsThisMonth = cachedStats.NewStudentsThisMonth,
                TotalRevenue = instructorProfile.TotalEarnings,
                AvailableBalance = instructorProfile.AvailableBalance,
                PendingBalance = instructorProfile.PendingBalance,
                MonthlyRevenue = cachedStats.MonthlyRevenue,
                LastMonthRevenue = cachedStats.LastMonthRevenue,
                TotalReviews = cachedStats.TotalReviews,
                AverageRating = instructorProfile.AverageRating,
                UnansweredReviews = cachedStats.UnansweredReviews,
                PendingAssignments = cachedStats.PendingAssignments,
                UnreadDiscussions = cachedStats.UnreadDiscussions,
                PendingQuizReviews = cachedStats.PendingQuizReviews,
                ContentUpdatesNeeded = cachedStats.ContentUpdatesNeeded
            };
                
            // Calculate revenue growth percentage
            stats.RevenueGrowthPercent = stats.LastMonthRevenue > 0 
                ? ((stats.MonthlyRevenue - stats.LastMonthRevenue) / stats.LastMonthRevenue) * 100 
                : (stats.MonthlyRevenue > 0 ? 100 : 0);
            
            // Try to get cached chart data
            var chartCacheKey = $"{DashboardChartCacheKeyPrefix}{userId}";
            if (!_cache.TryGetValue(chartCacheKey, out InstructorDashboardChartCache? cachedChartData))
            {
                cachedChartData = await LoadDashboardChartDataAsync(userId!);
                
                var chartCacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(ChartCacheExpiration)
                    .SetPriority(CacheItemPriority.Low);
                
                _cache.Set(chartCacheKey, cachedChartData, chartCacheOptions);
                _logger.LogDebug("Dashboard chart data cached for instructor {InstructorId}", userId);
            }

            stats.ActiveCourses = cachedChartData!.ActiveCourses;
            stats.EnrollmentChartData = cachedChartData.EnrollmentChartData;
            stats.EarningsChartData = cachedChartData.EarningsChartData;

            // Recent enrollments - always fresh (not cached) as it's time-sensitive
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            stats.RecentEnrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= sevenDaysAgo)
                .OrderByDescending(e => e.EnrolledAt)
                .Take(Constants.DisplayLimits.RecentEnrollmentsOnDashboard)
                .ToListAsync();

            // Get default currency for display
            await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);

            // Check for profile completion warnings
            var hasPaymentMethod = !string.IsNullOrEmpty(instructorProfile.PayPalEmail) ||
                                  !string.IsNullOrEmpty(instructorProfile.BankAccountNumber);

            var (isComplete, completionReason) = BusinessRuleHelper.ValidateInstructorProfileCompletion(
                instructorProfile.InstructorBio,
                instructorProfile.Headline,
                hasPaymentMethod,
                instructorProfile.Status);

            if (!isComplete)
            {
                ViewBag.ProfileWarning = completionReason;
                _logger.LogInformation("Instructor {InstructorId} profile incomplete: {Reason}", 
                    userId, completionReason);
            }

            _logger.LogInformation("Dashboard loaded for instructor {InstructorId}. Courses: {Courses}, Students: {Students}", 
                userId, stats.TotalCourses, stats.TotalStudents);

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة التحكم");
            return View(new InstructorDashboardViewModel());
        }
    }

    /// <summary>
    /// تحميل إحصائيات لوحة التحكم - Load dashboard statistics
    /// </summary>
    private async Task<InstructorDashboardStatsCache> LoadDashboardStatsAsync(string userId, Domain.Entities.Users.InstructorProfile instructorProfile)
    {
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

        return new InstructorDashboardStatsCache
        {
            // Course Statistics
            TotalCourses = await _context.Courses.CountAsync(c => c.InstructorId == userId),
            PublishedCourses = await _context.Courses
                .CountAsync(c => c.InstructorId == userId && c.Status == Domain.Enums.CourseStatus.Published),
            DraftCourses = await _context.Courses
                .CountAsync(c => c.InstructorId == userId && c.Status == Domain.Enums.CourseStatus.Draft),
            PendingReviewCourses = await _context.Courses
                .CountAsync(c => c.InstructorId == userId && c.Status == Domain.Enums.CourseStatus.PendingReview),
                
            // Student Statistics
            TotalStudents = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync(),
            ActiveStudents = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId && 
                           e.Status == Domain.Enums.EnrollmentStatus.Active)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync(),
            NewStudentsThisMonth = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= firstDayOfMonth)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync(),
                
            // Revenue Statistics
            MonthlyRevenue = await _context.InstructorEarnings
                .Where(e => e.InstructorId == userId && e.CreatedAt >= firstDayOfMonth)
                .SumAsync(e => e.NetAmount),
            LastMonthRevenue = await _context.InstructorEarnings
                .Where(e => e.InstructorId == userId && 
                           e.CreatedAt >= firstDayOfLastMonth && 
                           e.CreatedAt < firstDayOfMonth)
                .SumAsync(e => e.NetAmount),
                
            // Reviews & Ratings
            TotalReviews = await _context.Reviews
                .CountAsync(r => r.Course.InstructorId == userId && r.IsApproved),
            UnansweredReviews = await _context.Reviews
                .CountAsync(r => r.Course.InstructorId == userId && 
                           r.IsApproved && 
                           string.IsNullOrEmpty(r.InstructorResponse)),
                           
            // Pending Tasks
            PendingAssignments = await _context.AssignmentSubmissions
                .CountAsync(s => s.Assignment.Lesson.Module.Course.InstructorId == userId 
                    && s.Status == Domain.Enums.AssignmentStatus.Submitted),
            UnreadDiscussions = await _context.Discussions
                .CountAsync(d => d.Course.InstructorId == userId && !d.IsResolved),
            PendingQuizReviews = await _context.QuizAttempts
                .CountAsync(qa => qa.Quiz.Lesson.Module.Course.InstructorId == userId 
                    && qa.Status == Domain.Enums.QuizAttemptStatus.Completed && qa.ReviewedAt == null),
            ContentUpdatesNeeded = await _context.Lessons
                .CountAsync(l => l.Module.Course.InstructorId == userId 
                    && l.UpdatedAt < now.AddMonths(-BusinessRuleHelper.ContentUpdateThresholdMonths)),
                    
            CachedAt = now
        };
    }

    /// <summary>
    /// تحميل بيانات الرسوم البيانية - Load chart data
    /// </summary>
    private async Task<InstructorDashboardChartCache> LoadDashboardChartDataAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var chartData = new InstructorDashboardChartCache();

        // Active courses with details
        chartData.ActiveCourses = await _context.Courses
            .Where(c => c.InstructorId == userId && c.Status == Domain.Enums.CourseStatus.Published)
            .OrderByDescending(c => c.TotalStudents)
            .Take(Constants.DisplayLimits.ActiveCoursesOnDashboard)
            .Select(c => new InstructorCourseItem
            {
                CourseId = c.Id,
                Title = c.Title,
                StudentCount = c.TotalStudents,
                Rating = c.AverageRating,
                Status = c.Status
            })
            .ToListAsync();
            
        // Chart data - Enrollments per day (last 7 days)
        var enrollmentsByDay = await _context.Enrollments
            .Where(e => e.Course.InstructorId == userId && e.EnrolledAt >= sevenDaysAgo)
            .GroupBy(e => e.EnrolledAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
            
        var dayNames = await _configService.GetDayNamesAsync("ar");
        for (int i = Constants.DisplayLimits.ChartDataPoints - 1; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var count = enrollmentsByDay.FirstOrDefault(x => x.Date == date)?.Count ?? 0;
            var dayName = dayNames.TryGetValue(date.DayOfWeek, out var name) ? name : date.DayOfWeek.ToString();
            chartData.EnrollmentChartData.Add(new ChartDataPoint
            {
                Label = dayName,
                Value = count
            });
        }
        
        // Chart data - Earnings per week (last 4 weeks)
        var weekCount = Constants.Analytics.EarningsChartWeeks;
        for (int i = weekCount - 1; i >= 0; i--)
        {
            var weekStart = now.Date.AddDays(-7 * (i + 1));
            var weekEnd = now.Date.AddDays(-7 * i);
            var weekEarnings = await _context.InstructorEarnings
                .Where(e => e.InstructorId == userId && 
                           e.CreatedAt >= weekStart && 
                           e.CreatedAt < weekEnd)
                .SumAsync(e => e.NetAmount);
                
            chartData.EarningsChartData.Add(new ChartDataPoint
            {
                Label = $"الأسبوع {weekCount - i}",
                Value = weekEarnings
            });
        }

        chartData.CachedAt = now;
        return chartData;
    }

    /// <summary>
    /// إبطال ذاكرة التخزين المؤقت للوحة التحكم - Invalidate dashboard cache
    /// Call this when instructor data changes significantly
    /// </summary>
    public void InvalidateDashboardCache(string userId)
    {
        _cache.Remove($"{DashboardStatsCacheKeyPrefix}{userId}");
        _cache.Remove($"{DashboardChartCacheKeyPrefix}{userId}");
        _logger.LogDebug("Dashboard cache invalidated for instructor {InstructorId}", userId);
    }

}

/// <summary>
/// نموذج ذاكرة التخزين المؤقت للإحصائيات - Dashboard Stats Cache Model
/// </summary>
public class InstructorDashboardStatsCache
{
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int DraftCourses { get; set; }
    public int PendingReviewCourses { get; set; }
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int NewStudentsThisMonth { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public int TotalReviews { get; set; }
    public int UnansweredReviews { get; set; }
    public int PendingAssignments { get; set; }
    public int UnreadDiscussions { get; set; }
    public int PendingQuizReviews { get; set; }
    public int ContentUpdatesNeeded { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// نموذج ذاكرة التخزين المؤقت للرسوم البيانية - Dashboard Chart Cache Model
/// </summary>
public class InstructorDashboardChartCache
{
    public List<InstructorCourseItem> ActiveCourses { get; set; } = new();
    public List<ChartDataPoint> EnrollmentChartData { get; set; } = new();
    public List<ChartDataPoint> EarningsChartData { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// نموذج عرض لوحة تحكم المدرس - Instructor Dashboard ViewModel
/// </summary>
public class InstructorDashboardViewModel
{
    // Course Statistics
    public int TotalCourses { get; set; }
    public int PublishedCourses { get; set; }
    public int DraftCourses { get; set; }
    public int PendingReviewCourses { get; set; }
    
    // Student Statistics
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int NewStudentsThisMonth { get; set; }
    
    // Revenue Statistics
    public decimal TotalRevenue { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal RevenueGrowthPercent { get; set; }
    
    // Reviews & Ratings
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int UnansweredReviews { get; set; }
    
    // Pending Tasks
    public int PendingAssignments { get; set; }
    public int UnreadDiscussions { get; set; }
    public int PendingQuizReviews { get; set; }
    public int ContentUpdatesNeeded { get; set; }
    
    // Instructor Info
    public string InstructorName { get; set; } = string.Empty;
    
    // Active Courses with details
    public List<InstructorCourseItem> ActiveCourses { get; set; } = new();
    
    // Recent Activity
    public List<Domain.Entities.Learning.Enrollment> RecentEnrollments { get; set; } = new();
    
    // Chart Data
    public List<ChartDataPoint> EnrollmentChartData { get; set; } = new();
    public List<ChartDataPoint> EarningsChartData { get; set; } = new();
}

public class InstructorCourseItem
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public decimal Rating { get; set; }
    public Domain.Enums.CourseStatus Status { get; set; }
}

// ChartDataPoint is defined in LMS.Areas.Admin.ViewModels.ReportViewModels

