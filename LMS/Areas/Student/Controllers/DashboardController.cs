using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using LMS.Areas.Student.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// لوحة تحكم الطالب - Student Dashboard with Personalized Feed
/// </summary>
public class DashboardController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly ILogger<DashboardController> _logger;
    private readonly IMemoryCache _cache;

    public DashboardController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IRecommendationService recommendationService,
        ILearningAnalyticsService analyticsService,
        ILogger<DashboardController> logger,
        IMemoryCache cache)
    {
        _context = context;
        _currentUserService = currentUserService;
        _recommendationService = recommendationService;
        _analyticsService = analyticsService;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// لوحة التحكم الرئيسية - Main dashboard with personalized activity feed
    /// Optimized with caching for improved performance
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var cacheKey = $"dashboard_{userId}";

        try
        {
            // Try to get cached dashboard data (cache for 5 minutes)
            if (!_cache.TryGetValue(cacheKey, out StudentDashboardViewModel? dashboard))
            {
                _logger.LogInformation("Building dashboard for user {UserId} - cache miss", userId);

                // Initialize empty dashboard that we'll populate
                dashboard = new StudentDashboardViewModel();

                // Get learning stats with error handling
                try
                {
                    var statsCacheKey = $"stats_{userId}";
                    dashboard.Stats = await _cache.GetOrCreateAsync(statsCacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                        return await _analyticsService.GetStudentLearningStatsAsync(userId);
                    }) ?? new StudentLearningStats();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load learning stats for user {UserId}", userId);
                    dashboard.Stats = new StudentLearningStats();
                }

                // Get active enrollments with error handling
                try
                {
                    dashboard.ActiveEnrollments = await _context.Enrollments
                        .AsNoTracking()
                        .Include(e => e.Course)
                            .ThenInclude(c => c!.Instructor)
                        .Include(e => e.LessonProgress)
                        .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
                        .OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt)
                        .Take(6)
                        .AsSplitQuery()
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load active enrollments for user {UserId}", userId);
                    dashboard.ActiveEnrollments = new List<Domain.Entities.Learning.Enrollment>();
                }

                // Get at-risk alerts with error handling
                try
                {
                    dashboard.AtRiskAlerts = await _analyticsService.GetAtRiskAlertsAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load at-risk alerts for user {UserId}", userId);
                    dashboard.AtRiskAlerts = new List<AtRiskAlert>();
                }

                // Get study recommendations with error handling
                try
                {
                    var recommendationsCacheKey = $"study_recommendations_{userId}";
                    dashboard.StudyRecommendations = await _cache.GetOrCreateAsync(recommendationsCacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                        return await _analyticsService.GetStudyRecommendationsAsync(userId);
                    }) ?? new StudyRecommendations();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load study recommendations for user {UserId}", userId);
                    dashboard.StudyRecommendations = new StudyRecommendations();
                }

                // Get personalized activity feed with error handling
                try
                {
                    dashboard.ActivityFeed = await GetPersonalizedActivityFeedAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load activity feed for user {UserId}", userId);
                    dashboard.ActivityFeed = new List<ActivityFeedItem>();
                }

                // Get upcoming deadlines with error handling
                try
                {
                    dashboard.UpcomingDeadlines = await GetUpcomingDeadlinesAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load deadlines for user {UserId}", userId);
                    dashboard.UpcomingDeadlines = new List<DeadlineItem>();
                }

                // Get recent achievements with error handling
                try
                {
                    var achievementsCacheKey = $"achievements_{userId}";
                    dashboard.RecentAchievements = await _cache.GetOrCreateAsync(achievementsCacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                        return await _context.UserBadges
                            .AsNoTracking()
                            .Include(ub => ub.Badge)
                            .Where(ub => ub.UserId == userId)
                            .OrderByDescending(ub => ub.AwardedAt)
                            .Take(5)
                            .ToListAsync();
                    }) ?? new List<Domain.Entities.Gamification.UserBadge>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load achievements for user {UserId}", userId);
                    dashboard.RecentAchievements = new List<Domain.Entities.Gamification.UserBadge>();
                }

                // Get course recommendations with error handling
                try
                {
                    var courseRecsCacheKey = $"course_recs_{userId}";
                    dashboard.Recommendations = await _cache.GetOrCreateAsync(courseRecsCacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                        return await _recommendationService.GetCourseRecommendationsAsync(userId, 4);
                    }) ?? new List<CourseRecommendation>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load course recommendations for user {UserId}", userId);
                    dashboard.Recommendations = new List<CourseRecommendation>();
                }

                // Get notifications with error handling
                try
                {
                    dashboard.UnreadNotifications = await _context.Notifications
                        .AsNoTracking()
                        .Where(n => n.UserId == userId && !n.IsRead)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(5)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load notifications for user {UserId}", userId);
                    dashboard.UnreadNotifications = new List<Domain.Entities.Notifications.Notification>();
                }

                // Cache the complete dashboard for 5 minutes
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetPriority(CacheItemPriority.Normal);

                _cache.Set(cacheKey, dashboard, cacheEntryOptions);
            }
            else
            {
                _logger.LogInformation("Dashboard loaded from cache for user {UserId}", userId);
            }

            return View(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error loading dashboard for user {UserId}", userId);
            // Don't set error message for cleaner UX - just return empty dashboard
            return View(new StudentDashboardViewModel());
        }
    }

    /// <summary>
    /// الإجراءات السريعة - Quick actions
    /// </summary>
    public async Task<IActionResult> QuickActions()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Continue learning - last accessed course
        var lastAccessed = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.LastAccessedAt)
            .FirstOrDefaultAsync();

        // Next quiz to take
        var nextQuiz = await GetNextQuizAsync(userId);

        // Next assignment due
        var nextAssignment = await GetNextAssignmentAsync(userId);

        // Pending reviews
        var completedCoursesWithoutReview = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && 
                       e.Status == EnrollmentStatus.Completed &&
                       !_context.Reviews.Any(r => r.StudentId == userId && r.CourseId == e.CourseId))
            .Take(3)
            .ToListAsync();

        var quickActions = new QuickActionsViewModel
        {
            ContinueLearning = lastAccessed,
            NextQuiz = nextQuiz,
            NextAssignment = nextAssignment,
            CoursesToReview = completedCoursesWithoutReview
        };

        return View(quickActions);
    }

    /// <summary>
    /// التقويم - Calendar with deadlines and schedule
    /// </summary>
    public async Task<IActionResult> Calendar(int year = 0, int month = 0)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (year == 0) year = DateTime.UtcNow.Year;
        if (month == 0) month = DateTime.UtcNow.Month;

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get deadlines
        var deadlines = await GetDeadlinesForPeriodAsync(userId, startDate, endDate);

        // Get scheduled study sessions (if implemented)
        var studySessions = await GetStudySessionsForPeriodAsync(userId, startDate, endDate);

        // Get live classes
        var liveClasses = await _context.LiveClassEnrollments
            .Include(lce => lce.LiveClass)
                .ThenInclude(lc => lc.Course)
            .Where(lce => lce.StudentId == userId &&
                         lce.LiveClass.ScheduledStartTime >= startDate &&
                         lce.LiveClass.ScheduledStartTime <= endDate)
            .Select(lce => new DashboardCalendarEvent
            {
                Title = lce.LiveClass.Title,
                Date = lce.LiveClass.ScheduledStartTime,
                Type = "LiveClass",
                Description = lce.LiveClass.Course.Title
            })
            .ToListAsync();

        var calendar = new CalendarViewModel
        {
            Year = year,
            Month = month,
            Deadlines = deadlines,
            StudySessions = studySessions,
            LiveClasses = liveClasses
        };

        return View(calendar);
    }

    /// <summary>
    /// الإنجازات - Achievements showcase
    /// </summary>
    public async Task<IActionResult> Achievements()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var user = await _context.Users.FindAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in database", userId);
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        StudentLearningStats stats;
        try
        {
            stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get learning stats for user {UserId}", userId);
            stats = new StudentLearningStats();
        }

        // Get all badges
        var badges = await _context.UserBadges
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == userId)
            .OrderByDescending(ub => ub.AwardedAt)
            .ToListAsync();

        // Get certificates
        var certificates = await _context.Certificates
            .Include(c => c.Course)
            .Where(c => c.StudentId == userId && !c.IsRevoked)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync();

        // Get milestones
        var milestones = CalculateMilestones(stats);

        var achievements = new AchievementsViewModel
        {
            User = user,
            Stats = stats,
            Badges = badges,
            Certificates = certificates,
            Milestones = milestones
        };

        return View(achievements);
    }

    /// <summary>
    /// إحصائيات الدراسة - Study statistics
    /// </summary>
    public async Task<IActionResult> StudyStats(int days = 30)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        var startDate = DateTime.UtcNow.AddDays(-days);

        // Get daily study time
        var dailyStats = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .SelectMany(e => e.LessonProgress)
            .Where(lp => lp.LastWatchedAt >= startDate)
            .GroupBy(lp => lp.LastWatchedAt!.Value.Date)
            .Select(g => new DailyStudyStats
            {
                Date = g.Key,
                MinutesStudied = g.Sum(lp => lp.WatchedSeconds) / 60,
                LessonsCompleted = g.Count(lp => lp.IsCompleted)
            })
            .OrderBy(ds => ds.Date)
            .ToListAsync();

        // Get study time by course
        var byCourse = await _context.Enrollments
            .Include(e => e.Course)
            .Include(e => e.LessonProgress)
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .Select(e => new CourseStudyStats
            {
                CourseName = e.Course.Title,
                TotalMinutes = e.TotalWatchTimeMinutes,
                LessonsCompleted = e.LessonProgress.Count(lp => lp.IsCompleted),
                TotalLessons = e.Course.TotalLessons
            })
            .OrderByDescending(cs => cs.TotalMinutes)
            .ToListAsync();

        // Calculate streaks
        var currentStreak = CalculateCurrentStreak(dailyStats);
        var longestStreak = CalculateLongestStreak(dailyStats);

        var studyStats = new StudyStatsViewModel
        {
            Days = days,
            DailyStats = dailyStats,
            ByCourse = byCourse,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            TotalMinutes = dailyStats.Sum(ds => ds.MinutesStudied),
            AverageMinutesPerDay = dailyStats.Any() ? dailyStats.Average(ds => ds.MinutesStudied) : 0
        };

        return View(studyStats);
    }

    /// <summary>
    /// الخلاصة الأسبوعية - Weekly digest
    /// </summary>
    public async Task<IActionResult> WeeklyDigest()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        var weekStart = DateTime.UtcNow.AddDays(-7);

        var digest = new WeeklyDigestViewModel
        {
            WeekStart = weekStart,
            WeekEnd = DateTime.UtcNow,
            CoursesActive = await _context.Enrollments
                .CountAsync(e => e.StudentId == userId && 
                               e.Status == EnrollmentStatus.Active &&
                               e.LastAccessedAt >= weekStart),
            LessonsCompleted = await _context.LessonProgress
                .CountAsync(lp => lp.Enrollment.StudentId == userId &&
                                 lp.CompletedAt >= weekStart),
            QuizzesTaken = await _context.QuizAttempts
                .CountAsync(qa => qa.StudentId == userId &&
                                 qa.StartedAt >= weekStart),
            MinutesStudied = await _context.Enrollments
                .Where(e => e.StudentId == userId)
                .SelectMany(e => e.LessonProgress)
                .Where(lp => lp.LastWatchedAt >= weekStart)
                .SumAsync(lp => lp.WatchedSeconds / 60),
            BadgesEarned = await _context.UserBadges
                .CountAsync(ub => ub.UserId == userId && ub.AwardedAt >= weekStart),
            PointsEarned = await _context.PointTransactions
                .Where(pt => pt.UserId == userId && pt.CreatedAt >= weekStart)
                .SumAsync(pt => pt.Points)
        };

        return View(digest);
    }

    /// <summary>
    /// البحث الذكي في لوحة التحكم - Smart dashboard search
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(string query)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { results = new List<object>(), error = "Unauthorized" });
        }

        if (string.IsNullOrWhiteSpace(query))
            return Json(new { results = new List<object>() });

        var results = new List<SearchResultItem>();

        // Search in enrolled courses (link to course Learn page by course id)
        var courses = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && e.Course.Title.Contains(query))
            .Take(5)
            .Select(e => new SearchResultItem
            {
                Type = "Course",
                Title = e.Course.Title,
                Url = Url.Action("Learn", "Courses", new { area = "Student", id = e.CourseId })!,
                Icon = "fas fa-book"
            })
            .ToListAsync();

        results.AddRange(courses);

        // Search in lessons
        var lessons = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .Where(l => _context.Enrollments.Any(e => e.StudentId == userId && e.CourseId == l.Module.CourseId) &&
                       l.Title.Contains(query))
            .Take(5)
            .Select(l => new SearchResultItem
            {
                Type = "Lesson",
                Title = l.Title,
                Url = Url.Action("Lesson", "Learning", new { lessonId = l.Id })!,
                Icon = "fas fa-play-circle",
                Subtitle = l.Module.Course.Title
            })
            .ToListAsync();

        results.AddRange(lessons);

        // Search in assignments
        var assignments = await _context.AssignmentSubmissions
            .Include(a => a.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(a => a.StudentId == userId && a.Assignment.Title.Contains(query))
            .Take(3)
            .Select(a => new SearchResultItem
            {
                Type = "Assignment",
                Title = a.Assignment.Title,
                Url = Url.Action("ViewSubmission", "Assignments", new { id = a.Id })!,
                Icon = "fas fa-file-alt",
                Subtitle = a.Assignment.Lesson.Module.Course.Title
            })
            .ToListAsync();

        results.AddRange(assignments);

        return Json(new { results });
    }

    #region Private Helper Methods

    private async Task<List<ActivityFeedItem>> GetPersonalizedActivityFeedAsync(string userId)
    {
        var feed = new List<ActivityFeedItem>();
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

        // Recent lesson completions
        var recentLessons = await _context.LessonProgress
            .Include(lp => lp.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(lp => lp.Enrollment.StudentId == userId && 
                        lp.CompletedAt >= oneWeekAgo && 
                        lp.IsCompleted)
            .OrderByDescending(lp => lp.CompletedAt)
            .Take(5)
            .ToListAsync();

        feed.AddRange(recentLessons.Select(lp => new ActivityFeedItem
        {
            Type = "LessonCompleted",
            Title = "أكملت درس",
            Description = lp.Lesson.Title,
            Subtitle = lp.Lesson.Module.Course.Title,
            Timestamp = lp.CompletedAt!.Value,
            Icon = "fas fa-check-circle text-success"
        }));

        // Recent quiz attempts
        var recentQuizzes = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(qa => qa.StudentId == userId && qa.SubmittedAt >= oneWeekAgo)
            .OrderByDescending(qa => qa.SubmittedAt)
            .Take(3)
            .ToListAsync();

        feed.AddRange(recentQuizzes.Select(qa => new ActivityFeedItem
        {
            Type = "QuizCompleted",
            Title = qa.IsPassed ? "نجحت في اختبار" : "أكملت اختبار",
            Description = qa.Quiz.Title,
            Subtitle = $"النتيجة: {qa.PercentageScore:F0}%",
            Timestamp = qa.SubmittedAt ?? DateTime.UtcNow,
            Icon = qa.IsPassed ? "fas fa-trophy text-warning" : "fas fa-clipboard-check"
        }));

        // Recent badges
        var recentBadges = await _context.UserBadges
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == userId && ub.AwardedAt >= oneWeekAgo)
            .OrderByDescending(ub => ub.AwardedAt)
            .Take(3)
            .ToListAsync();

        feed.AddRange(recentBadges.Select(ub => new ActivityFeedItem
        {
            Type = "BadgeEarned",
            Title = "حصلت على وسام",
            Description = ub.Badge.Name,
            Subtitle = ub.Badge.Description,
            Timestamp = ub.AwardedAt,
            Icon = "fas fa-medal text-primary"
        }));

        return feed.OrderByDescending(f => f.Timestamp).Take(10).ToList();
    }

    private async Task<List<DeadlineItem>> GetUpcomingDeadlinesAsync(string userId)
    {
        var deadlines = new List<DeadlineItem>();
        var twoWeeksFromNow = DateTime.UtcNow.AddDays(14);

        // Assignment deadlines
        var assignments = await _context.AssignmentSubmissions
            .Include(a => a.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(a => a.StudentId == userId &&
                       a.Status != AssignmentStatus.Graded &&
                       a.Assignment.DueDate.HasValue &&
                       a.Assignment.DueDate.Value <= twoWeeksFromNow)
            .ToListAsync();

        deadlines.AddRange(assignments.Select(a => new DeadlineItem
        {
            Type = "Assignment",
            Title = a.Assignment.Title,
            CourseName = a.Assignment.Lesson.Module.Course.Title,
            DueDate = a.Assignment.DueDate!.Value,
            IsOverdue = a.Assignment.DueDate.Value < DateTime.UtcNow,
            Url = Url.Action("ViewSubmission", "Assignments", new { id = a.Id })!
        }));

        // Quiz availability endings
        var quizzes = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(q => _context.Enrollments.Any(e => e.StudentId == userId && e.CourseId == q.Lesson.Module.CourseId) &&
                       q.AvailableUntil.HasValue &&
                       q.AvailableUntil.Value <= twoWeeksFromNow &&
                       q.AvailableUntil.Value > DateTime.UtcNow)
            .ToListAsync();

        deadlines.AddRange(quizzes.Select(q => new DeadlineItem
        {
            Type = "Quiz",
            Title = q.Title,
            CourseName = q.Lesson.Module.Course.Title,
            DueDate = q.AvailableUntil!.Value,
            IsOverdue = false,
            Url = Url.Action("Start", "Quizzes", new { quizId = q.Id })!
        }));

        return deadlines.OrderBy(d => d.DueDate).ToList();
    }

    private async Task<List<DeadlineItem>> GetDeadlinesForPeriodAsync(string userId, DateTime start, DateTime end)
    {
        var deadlines = new List<DeadlineItem>();

        // Assignment deadlines
        var assignments = await _context.AssignmentSubmissions
            .Include(a => a.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(a => a.StudentId == userId &&
                       a.Assignment.DueDate.HasValue &&
                       a.Assignment.DueDate.Value >= start &&
                       a.Assignment.DueDate.Value <= end)
            .ToListAsync();

        deadlines.AddRange(assignments.Select(a => new DeadlineItem
        {
            Type = "Assignment",
            Title = a.Assignment.Title,
            CourseName = a.Assignment.Lesson.Module.Course.Title,
            DueDate = a.Assignment.DueDate!.Value
        }));

        return deadlines;
    }

    private async Task<List<DashboardCalendarEvent>> GetStudySessionsForPeriodAsync(string userId, DateTime start, DateTime end)
    {
        // This would come from a study planner feature (to be implemented)
        // For now, return empty list
        return new List<DashboardCalendarEvent>();
    }

    private async Task<QuizInfo?> GetNextQuizAsync(string userId)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(q => _context.Enrollments.Any(e => e.StudentId == userId && e.CourseId == q.Lesson.Module.CourseId) &&
                       (!q.AvailableUntil.HasValue || q.AvailableUntil.Value > DateTime.UtcNow))
            .OrderBy(q => q.OrderIndex)
            .FirstOrDefaultAsync();

        return quiz != null ? new QuizInfo
        {
            QuizId = quiz.Id,
            Title = quiz.Title,
            CourseName = quiz.Lesson.Module.Course.Title
        } : null;
    }

    private async Task<AssignmentInfo?> GetNextAssignmentAsync(string userId)
    {
        var assignment = await _context.AssignmentSubmissions
            .Include(a => a.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(a => a.StudentId == userId &&
                       a.Status != AssignmentStatus.Graded &&
                       a.Assignment.DueDate.HasValue)
            .OrderBy(a => a.Assignment.DueDate)
            .FirstOrDefaultAsync();

        return assignment != null ? new AssignmentInfo
        {
            AssignmentId = assignment.Id,
            Title = assignment.Assignment.Title,
            CourseName = assignment.Assignment.Lesson.Module.Course.Title,
            DueDate = assignment.Assignment.DueDate!.Value
        } : null;
    }

    private List<MilestoneItem> CalculateMilestones(StudentLearningStats stats)
    {
        var milestones = new List<MilestoneItem>();

        // Course milestones
        if (stats.CompletedCourses >= 1)
            milestones.Add(new MilestoneItem { Title = "أول دورة مكتملة", Icon = "🎓", Achieved = true });
        if (stats.CompletedCourses >= 5)
            milestones.Add(new MilestoneItem { Title = "5 دورات مكتملة", Icon = "⭐", Achieved = true });
        if (stats.CompletedCourses >= 10)
            milestones.Add(new MilestoneItem { Title = "10 دورات مكتملة", Icon = "🏆", Achieved = true });

        // Study time milestones
        if (stats.TotalStudyMinutes >= 600) // 10 hours
            milestones.Add(new MilestoneItem { Title = "10 ساعات دراسة", Icon = "⏱️", Achieved = true });
        if (stats.TotalStudyMinutes >= 3000) // 50 hours
            milestones.Add(new MilestoneItem { Title = "50 ساعة دراسة", Icon = "📚", Achieved = true });

        // Streak milestones
        if (stats.CurrentStreak >= 7)
            milestones.Add(new MilestoneItem { Title = "أسبوع متواصل", Icon = "🔥", Achieved = true });
        if (stats.LongestStreak >= 30)
            milestones.Add(new MilestoneItem { Title = "شهر متواصل", Icon = "💪", Achieved = true });

        return milestones;
    }

    /// <summary>
    /// Calculate current study streak with 24-hour grace period
    /// Grace period: If user hasn't studied today but studied yesterday, streak is preserved
    /// </summary>
    private int CalculateCurrentStreak(List<DailyStudyStats> dailyStats)
    {
        if (!dailyStats.Any()) return 0;

        var streak = 0;
        var today = DateTime.UtcNow.Date;
        var studiedToday = dailyStats.Any(ds => ds.Date == today && ds.MinutesStudied > 0);
        
        // Start checking from today or yesterday based on grace period
        var startOffset = studiedToday ? 0 : 1;
        
        // If didn't study today or yesterday, streak is broken
        if (!studiedToday && !dailyStats.Any(ds => ds.Date == today.AddDays(-1) && ds.MinutesStudied > 0))
        {
            return 0;
        }

        for (int i = startOffset; i < 365; i++)
        {
            var date = today.AddDays(-i);
            if (dailyStats.Any(ds => ds.Date == date && ds.MinutesStudied > 0))
                streak++;
            else
                break;
        }

        return streak;
    }

    private int CalculateLongestStreak(List<DailyStudyStats> dailyStats)
    {
        if (!dailyStats.Any()) return 0;

        var longestStreak = 0;
        var currentStreak = 0;
        var previousDate = DateTime.MinValue;

        foreach (var stat in dailyStats.OrderBy(ds => ds.Date))
        {
            if (previousDate == DateTime.MinValue || stat.Date == previousDate.AddDays(1))
            {
                currentStreak++;
            }
            else
            {
                longestStreak = Math.Max(longestStreak, currentStreak);
                currentStreak = 1;
            }

            previousDate = stat.Date;
        }

        return Math.Max(longestStreak, currentStreak);
    }

    /// <summary>
    /// Invalidate dashboard cache for a specific user
    /// Call this method when user data changes (e.g., completes a lesson, earns a badge)
    /// </summary>
    public void InvalidateDashboardCache(string userId)
    {
        var cacheKeys = new[]
        {
            $"dashboard_{userId}",
            $"stats_{userId}",
            $"study_recommendations_{userId}",
            $"achievements_{userId}",
            $"course_recs_{userId}"
        };

        foreach (var key in cacheKeys)
        {
            _cache.Remove(key);
        }

        _logger.LogInformation("Dashboard cache invalidated for user {UserId}", userId);
    }

    #endregion
}

#region View Models

public class StudentDashboardViewModel
{
    public StudentLearningStats Stats { get; set; } = new();
    public List<Domain.Entities.Learning.Enrollment> ActiveEnrollments { get; set; } = new();
    public List<AtRiskAlert> AtRiskAlerts { get; set; } = new();
    public StudyRecommendations StudyRecommendations { get; set; } = new();
    public List<ActivityFeedItem> ActivityFeed { get; set; } = new();
    public List<DeadlineItem> UpcomingDeadlines { get; set; } = new();
    public List<Domain.Entities.Gamification.UserBadge> RecentAchievements { get; set; } = new();
    public List<CourseRecommendation> Recommendations { get; set; } = new();
    public List<Domain.Entities.Notifications.Notification> UnreadNotifications { get; set; } = new();
}

public class ActivityFeedItem
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class DeadlineItem
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public string? Url { get; set; }
}

// QuickActionsViewModel moved to ViewModels folder
// QuizInfo moved to ViewModels folder
// AssignmentInfo moved to ViewModels folder

public class CalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<DeadlineItem> Deadlines { get; set; } = new();
    public List<DashboardCalendarEvent> StudySessions { get; set; } = new();
    public List<DashboardCalendarEvent> LiveClasses { get; set; } = new();
}

public class DashboardCalendarEvent
{
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// AchievementsViewModel moved to ViewModels folder
// MilestoneItem moved to ViewModels folder

public class StudyStatsViewModel
{
    public int Days { get; set; }
    public List<DailyStudyStats> DailyStats { get; set; } = new();
    public List<CourseStudyStats> ByCourse { get; set; } = new();
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMinutes { get; set; }
    public double AverageMinutesPerDay { get; set; }
}

public class DailyStudyStats
{
    public DateTime Date { get; set; }
    public int MinutesStudied { get; set; }
    public int LessonsCompleted { get; set; }
}

public class CourseStudyStats
{
    public string CourseName { get; set; } = string.Empty;
    public int TotalMinutes { get; set; }
    public int LessonsCompleted { get; set; }
    public int TotalLessons { get; set; }
}

public class WeeklyDigestViewModel
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int CoursesActive { get; set; }
    public int LessonsCompleted { get; set; }
    public int QuizzesTaken { get; set; }
    public int MinutesStudied { get; set; }
    public int BadgesEarned { get; set; }
    public int PointsEarned { get; set; }
}

public class SearchResultItem
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
}

#endregion

