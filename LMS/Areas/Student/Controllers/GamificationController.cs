using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التحفيز والإنجازات - Gamification Controller
/// </summary>
public class GamificationController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GamificationController> _logger;

    public GamificationController(
        ApplicationDbContext context, 
        ICurrentUserService currentUserService,
        ILogger<GamificationController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة الإنجازات - Gamification Dashboard
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        var userId = _currentUserService.UserId!;

        try
        {
            // Get user data
            var user = await _context.Users.FindAsync(userId);
            
            if (user == null)
                return NotFound();

            // Get earned badges
            var earnedBadges = await _context.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.UserId == userId)
                .OrderByDescending(ub => ub.AwardedAt)
                .ToListAsync();

            // Get all available badges
            var allBadges = await _context.Badges
                .Where(b => b.IsActive)
                .OrderBy(b => b.RequiredPoints)
                .ToListAsync();

            // Calculate badge progress for locked badges
            var earnedBadgeIds = earnedBadges.Select(ub => ub.BadgeId).ToList();
            var lockedBadges = allBadges.Where(b => !earnedBadgeIds.Contains(b.Id)).ToList();

            // Get recent point transactions
            var recentPoints = await _context.PointTransactions
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Get level information
            var currentLevel = user.Level;
            var currentPoints = user.Points;
            var pointsToNextLevel = CalculatePointsForLevel(currentLevel + 1) - currentPoints;

            // Get statistics
            var completedCourses = await _context.Enrollments
                .CountAsync(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Completed);

            var totalStudyHours = await _context.Enrollments
                .Where(e => e.StudentId == userId)
                .SumAsync(e => e.TotalWatchTimeMinutes) / 60;

            var currentStreak = await CalculateCurrentStreakAsync(userId);
            var longestStreak = await CalculateLongestStreakAsync(userId);

            // Prepare view model
            var viewModel = new GamificationDashboardViewModel
            {
                UserName = user.FullName,
                CurrentLevel = currentLevel,
                CurrentPoints = currentPoints,
                PointsToNextLevel = pointsToNextLevel,
                TotalBadges = earnedBadges.Count,
                CompletedCourses = completedCourses,
                TotalStudyHours = totalStudyHours,
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                EarnedBadges = earnedBadges,
                LockedBadges = lockedBadges,
                RecentPointTransactions = recentPoints,
                ProgressToNextLevel = CalculateProgressToNextLevel(currentLevel, currentPoints)
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading gamification dashboard for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة الإنجازات");
            return RedirectToAction("Index", "Dashboard");
        }
    }

    /// <summary>
    /// تاريخ النقاط - Points History
    /// </summary>
    public async Task<IActionResult> PointsHistory()
    {
        var userId = _currentUserService.UserId;

        var transactions = await _context.PointTransactions
            .Where(pt => pt.UserId == userId)
            .OrderByDescending(pt => pt.CreatedAt)
            .Take(100)
            .Select(pt => new PointTransactionDisplayViewModel
            {
                Id = pt.Id,
                Reason = pt.Reason,
                Points = pt.Points,
                CreatedAt = pt.CreatedAt,
                RelatedEntity = pt.RelatedEntity
            })
            .ToListAsync();

        // Calculate summary
        var totalEarned = transactions.Where(t => t.Points > 0).Sum(t => t.Points);
        var totalSpent = transactions.Where(t => t.Points < 0).Sum(t => Math.Abs(t.Points));

        ViewBag.TotalEarned = totalEarned;
        ViewBag.TotalSpent = totalSpent;

        return View(transactions);
    }

    #region Private Helper Methods

    /// <summary>
    /// Calculate points required for a specific level
    /// </summary>
    private int CalculatePointsForLevel(int level)
    {
        // Formula: 100 * level^2
        return 100 * level * level;
    }

    /// <summary>
    /// Calculate progress percentage to next level
    /// </summary>
    private decimal CalculateProgressToNextLevel(int currentLevel, int currentPoints)
    {
        var currentLevelPoints = CalculatePointsForLevel(currentLevel);
        var nextLevelPoints = CalculatePointsForLevel(currentLevel + 1);
        var pointsInLevel = currentPoints - currentLevelPoints;
        var pointsNeeded = nextLevelPoints - currentLevelPoints;

        if (pointsNeeded == 0)
            return 100;

        return Math.Min(100, (decimal)pointsInLevel / pointsNeeded * 100);
    }

    /// <summary>
    /// Calculate current study streak
    /// </summary>
    private async Task<int> CalculateCurrentStreakAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var streak = 0;

        for (int i = 0; i < 365; i++)
        {
            var date = today.AddDays(-i);
            var hasActivity = await _context.LessonProgress
                .AnyAsync(lp => lp.Enrollment.StudentId == userId && 
                               lp.LastWatchedAt.HasValue &&
                               lp.LastWatchedAt.Value.Date == date);

            if (hasActivity)
                streak++;
            else if (i > 0) // Don't break on first day
                break;
        }

        return streak;
    }

    /// <summary>
    /// Calculate longest study streak
    /// </summary>
    private async Task<int> CalculateLongestStreakAsync(string userId)
    {
        var activities = await _context.LessonProgress
            .Where(lp => lp.Enrollment.StudentId == userId && lp.LastWatchedAt.HasValue)
            .Select(lp => lp.LastWatchedAt!.Value.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        if (!activities.Any())
            return 0;

        var longestStreak = 1;
        var currentStreak = 1;

        for (int i = 1; i < activities.Count; i++)
        {
            if (activities[i] == activities[i - 1].AddDays(1))
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }
        }

        return longestStreak;
    }

    #endregion
}

