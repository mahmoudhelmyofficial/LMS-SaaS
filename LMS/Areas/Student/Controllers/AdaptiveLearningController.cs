using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التعلم التكيفي - Adaptive Learning Controller
/// Provides personalized learning analytics, recommendations, and study plans
/// </summary>
public class AdaptiveLearningController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<AdaptiveLearningController> _logger;

    public AdaptiveLearningController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningAnalyticsService analyticsService,
        IRecommendationService recommendationService,
        ILogger<AdaptiveLearningController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _analyticsService = analyticsService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة التحليلات الشخصية - Personal analytics dashboard
    /// Model: StudentLearningStats
    /// </summary>
    public async Task<IActionResult> MyAnalytics()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل التحليلات");
            return View(new StudentLearningStats());
        }
    }

    /// <summary>
    /// توصيات الدراسة الذكية - Smart study recommendations
    /// Model: StudyRecommendations
    /// </summary>
    public async Task<IActionResult> StudyRecommendations()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var recommendations = await _analyticsService.GetStudyRecommendationsAsync(userId);
            return View(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recommendations for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل التوصيات");
            return View(new StudyRecommendations());
        }
    }

    /// <summary>
    /// تنبيهات المخاطر - At-risk alerts
    /// Model: List&lt;AtRiskAlert&gt;
    /// </summary>
    public async Task<IActionResult> AtRiskAlerts()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var alerts = await _analyticsService.GetAtRiskAlertsAsync(userId);
            return View(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading at-risk alerts for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل التنبيهات");
            return View(new List<AtRiskAlert>());
        }
    }

    /// <summary>
    /// تحليلات الدورة - Course-specific analytics
    /// Uses ViewBag: Course, Enrollment, Performance, Strengths, PeerComparison, EngagementScore
    /// </summary>
    public async Task<IActionResult> CourseAnalytics(int enrollmentId)
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Category)
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

            if (enrollment == null)
            {
                SetErrorMessage("التسجيل غير موجود");
                return RedirectToAction("Index", "Dashboard");
            }

            var performance = await _analyticsService.GetPerformanceAnalyticsAsync(userId, enrollmentId);
            var strengths = await _analyticsService.AnalyzeStrengthsWeaknessesAsync(userId, enrollmentId);
            var peerComparison = await _analyticsService.GetPeerComparisonAsync(userId, enrollmentId);
            var engagementScore = await _analyticsService.CalculateEngagementScoreAsync(userId, enrollmentId);

            ViewBag.Course = enrollment.Course;
            ViewBag.Enrollment = enrollment;
            ViewBag.Performance = performance;
            ViewBag.Strengths = strengths;
            ViewBag.PeerComparison = peerComparison;
            ViewBag.EngagementScore = engagementScore;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course analytics for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage("حدث خطأ أثناء تحميل تحليلات الدورة");
            return RedirectToAction("Index", "Dashboard");
        }
    }

    /// <summary>
    /// الخطوات التالية المقترحة - Suggested next steps
    /// Model: List&lt;CourseRecommendation&gt;
    /// </summary>
    public async Task<IActionResult> NextSteps()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var recommendations = await _recommendationService.GetCourseRecommendationsAsync(userId, 6);
            
            // Set ViewBag data for additional context
            var stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
            ViewBag.Stats = stats;
            
            return View(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading next steps for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الخطوات التالية");
            return View(new List<CourseRecommendation>());
        }
    }

    /// <summary>
    /// خطة الدراسة الذكية - Smart study plan
    /// Model: List&lt;StudyPlanItem&gt;
    /// </summary>
    public async Task<IActionResult> SmartStudyPlan()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var studyPlanItems = await GenerateSmartStudyPlanAsync(userId);
            return View(studyPlanItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading smart study plan for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الخطة الدراسية");
            return View(new List<StudyPlanItem>());
        }
    }

    /// <summary>
    /// الإنجازات - Achievements
    /// Uses ViewBag: Stats, Badges
    /// </summary>
    public async Task<IActionResult> Achievements()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var stats = await _analyticsService.GetStudentLearningStatsAsync(userId);

            var userBadges = await _context.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.UserId == userId)
                .OrderByDescending(ub => ub.AwardedAt)
                .ToListAsync();

            ViewBag.Stats = stats;
            ViewBag.Badges = userBadges;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading achievements for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الإنجازات");
            ViewBag.Stats = new StudentLearningStats();
            ViewBag.Badges = new List<LMS.Domain.Entities.Gamification.UserBadge>();
            return View();
        }
    }

    /// <summary>
    /// توصيات الدورات القادمة - Next course recommendations
    /// Model: List&lt;CourseRecommendation&gt;
    /// </summary>
    public async Task<IActionResult> NextCourseRecommendations()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var recommendations = await _recommendationService.GetCourseRecommendationsAsync(userId, 12);
            return View(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course recommendations for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل توصيات الدورات");
            return View(new List<CourseRecommendation>());
        }
    }

    #region API Endpoints

    /// <summary>
    /// الحصول على التوصيات (API) - Get recommendations API
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecommendations()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "User not found" });

        try
        {
            var recommendations = await _analyticsService.GetStudyRecommendationsAsync(userId);
            return Json(new { success = true, data = recommendations });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
            return Json(new { success = false, message = "Error loading recommendations" });
        }
    }

    /// <summary>
    /// تتبع وقت الدراسة (API) - Track study time API
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrackStudyTime(int enrollmentId, int minutes)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "User not found" });

        try
        {
            await _analyticsService.TrackStudyTimeAsync(userId, enrollmentId, minutes);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking study time for user {UserId}", userId);
            return Json(new { success = false, message = "Error tracking study time" });
        }
    }

    /// <summary>
    /// تجاهل تنبيه - Dismiss alert
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DismissAlert(int alertId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "User not found" });

        _logger.LogInformation("Alert {AlertId} dismissed by user {UserId}", alertId, userId);
        return Json(new { success = true });
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// توليد خطة دراسة ذكية - Generate smart study plan items
    /// </summary>
    private async Task<List<StudyPlanItem>> GenerateSmartStudyPlanAsync(string userId)
    {
        var studyPlanItems = new List<StudyPlanItem>();

        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Include(e => e.LessonProgress)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
            .ToListAsync();

        foreach (var enrollment in enrollments)
        {
            try
            {
                var engagementScore = await _analyticsService.CalculateEngagementScoreAsync(userId, enrollment.Id);
                var prediction = await _analyticsService.PredictCourseCompletionAsync(userId, enrollment.Id);

                var priority = CalculatePriority(enrollment.ProgressPercentage, engagementScore, prediction);
                var remainingLessons = enrollment.TotalLessons - enrollment.CompletedLessonsCount;
                var recommendedMinutes = Math.Max(15, Math.Min(90, remainingLessons * 10));

                studyPlanItems.Add(new StudyPlanItem
                {
                    EnrollmentId = enrollment.Id,
                    CourseName = enrollment.Course?.Title ?? "دورة غير معروفة",
                    ProgressPercentage = enrollment.ProgressPercentage,
                    EngagementScore = engagementScore,
                    CompletionProbability = prediction.CompletionProbability,
                    EstimatedCompletionDate = prediction.EstimatedCompletionDate,
                    RiskLevel = prediction.RiskLevel,
                    Priority = priority,
                    RecommendedDailyMinutes = recommendedMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating study plan item for enrollment {EnrollmentId}", enrollment.Id);
            }
        }

        return studyPlanItems.OrderBy(s => s.Priority).ToList();
    }

    /// <summary>
    /// حساب الأولوية - Calculate priority based on various factors
    /// </summary>
    private static int CalculatePriority(decimal progressPercentage, double engagementScore, CompletionPrediction prediction)
    {
        int priority = 4; // Default: low priority

        if (prediction.RiskLevel == "High")
            priority = 1;
        else if (prediction.RiskLevel == "Medium")
            priority = 2;
        else if (engagementScore < 0.5)
            priority = 2;
        else if (progressPercentage < 30)
            priority = 3;

        return priority;
    }

    #endregion
}
