using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// تقدم الطالب - Student Progress Controller
/// </summary>
public class ProgressController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly ILogger<ProgressController> _logger;

    public ProgressController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningAnalyticsService analyticsService,
        ILogger<ProgressController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة التقدم الشاملة - Comprehensive progress dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Get learning stats with fallback
            StudentLearningStats stats;
            try
            {
                stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting learning stats, using defaults");
                stats = new StudentLearningStats();
            }

            // Get all enrollments with progress
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Category)
                .Include(e => e.LessonProgress)
                .Where(e => e.StudentId == userId)
                .OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt)
                .ToListAsync();

            // Calculate weekly progress
            var weeklyProgress = await CalculateWeeklyProgressAsync(userId);

            // Get quiz performance summary
            var quizAttempts = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                .Where(qa => qa.StudentId == userId && qa.Status == Domain.Enums.QuizAttemptStatus.Completed)
                .OrderByDescending(qa => qa.SubmittedAt)
                .Take(20)
                .ToListAsync();

            // Get assignment completion rate
            var assignmentStats = await GetAssignmentStatsAsync(userId);

            var viewModel = new StudentProgressDashboardViewModel
            {
                Stats = stats,
                Enrollments = enrollments,
                WeeklyProgress = weeklyProgress,
                QuizAttempts = quizAttempts,
                TotalQuizzesPassed = quizAttempts.Count(qa => qa.IsPassed),
                AverageQuizScore = quizAttempts.Any() ? quizAttempts.Average(qa => qa.PercentageScore) : 0,
                AssignmentsSubmitted = assignmentStats.Submitted,
                AssignmentsGraded = assignmentStats.Graded,
                AverageAssignmentGrade = assignmentStats.AverageGrade
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading progress dashboard");
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة التقدم");
            return RedirectToAction("Index", "Dashboard");
        }
    }

    /// <summary>
    /// تقدم دورة معينة - Progress for specific course
    /// </summary>
    public async Task<IActionResult> Course(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

        if (enrollment == null)
            return NotFound();

        // Get detailed progress per module
        var moduleProgress = enrollment.Course.Modules
            .OrderBy(m => m.OrderIndex)
            .Select(m =>
            {
                var moduleLessonIds = m.Lessons.Select(l => l.Id).ToList();
                var completedLessons = enrollment.LessonProgress
                    .Where(lp => moduleLessonIds.Contains(lp.LessonId) && lp.IsCompleted)
                    .Count();

                return new ModuleProgressItem
                {
                    ModuleId = m.Id,
                    ModuleName = m.Title,
                    TotalLessons = m.Lessons.Count,
                    CompletedLessons = completedLessons,
                    ProgressPercentage = m.Lessons.Count > 0 
                        ? (decimal)completedLessons / m.Lessons.Count * 100 
                        : 0
                };
            })
            .ToList();

        // Get performance analytics
        var performance = await _analyticsService.GetPerformanceAnalyticsAsync(userId, enrollmentId);

        ViewBag.ModuleProgress = moduleProgress;
        ViewBag.Performance = performance;

        return View(enrollment);
    }

    #region Private Helper Methods

    private async Task<List<DailyProgressItem>> CalculateWeeklyProgressAsync(string userId)
    {
        var weeklyProgress = new List<DailyProgressItem>();
        var today = DateTime.UtcNow.Date;

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var nextDate = date.AddDays(1);

            var lessonsCompleted = await _context.LessonProgress
                .CountAsync(lp => lp.Enrollment.StudentId == userId &&
                                  lp.CompletedAt.HasValue &&
                                  lp.CompletedAt.Value.Date == date);

            var studyMinutes = await _context.LessonProgress
                .Where(lp => lp.Enrollment.StudentId == userId &&
                            lp.LastWatchedAt.HasValue &&
                            lp.LastWatchedAt.Value.Date == date)
                .SumAsync(lp => lp.WatchedSeconds / 60);

            weeklyProgress.Add(new DailyProgressItem
            {
                Date = date,
                DayName = date.ToString("ddd"),
                LessonsCompleted = lessonsCompleted,
                StudyMinutes = studyMinutes
            });
        }

        return weeklyProgress;
    }

    private async Task<AssignmentStatsResult> GetAssignmentStatsAsync(string userId)
    {
        var submissions = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == userId)
            .ToListAsync();

        var graded = submissions.Where(s => s.Status == Domain.Enums.AssignmentStatus.Graded).ToList();

        return new AssignmentStatsResult
        {
            Submitted = submissions.Count,
            Graded = graded.Count,
            AverageGrade = graded.Any() && graded.All(s => s.Grade.HasValue)
                ? graded.Average(s => s.Grade!.Value)
                : 0
        };
    }

    #endregion
}

#region View Models

public class StudentProgressDashboardViewModel
{
    public StudentLearningStats Stats { get; set; } = new();
    public List<Domain.Entities.Learning.Enrollment> Enrollments { get; set; } = new();
    public List<DailyProgressItem> WeeklyProgress { get; set; } = new();
    public List<Domain.Entities.Assessments.QuizAttempt> QuizAttempts { get; set; } = new();
    public int TotalQuizzesPassed { get; set; }
    public decimal AverageQuizScore { get; set; }
    public int AssignmentsSubmitted { get; set; }
    public int AssignmentsGraded { get; set; }
    public decimal AverageAssignmentGrade { get; set; }
}

public class ModuleProgressItem
{
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int OrderIndex { get; set; }
}

public class DailyProgressItem
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int LessonsCompleted { get; set; }
    public int StudyMinutes { get; set; }
}

public class AssignmentStatsResult
{
    public int Submitted { get; set; }
    public int Graded { get; set; }
    public decimal AverageGrade { get; set; }
}

#endregion

