using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الإنجازات والشارات - Achievements Controller
/// </summary>
public class AchievementsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AchievementsController> _logger;

    public AchievementsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<AchievementsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة الإنجازات - Achievements dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _currentUserService.UserId!;

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

            var earnedBadgeIds = earnedBadges.Select(ub => ub.BadgeId).ToHashSet();
            var lockedBadges = allBadges.Where(b => !earnedBadgeIds.Contains(b.Id)).ToList();

            // Get recent point transactions
            var recentPoints = await _context.PointTransactions
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Calculate stats
            var completedCourses = await _context.Enrollments
                .CountAsync(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Completed);

            var totalStudyHours = await _context.Enrollments
                .Where(e => e.StudentId == userId)
                .SumAsync(e => e.TotalWatchTimeSeconds) / 3600;

            var viewModel = new AchievementsDashboardViewModel
            {
                UserName = user.FullName,
                CurrentLevel = user.Level,
                CurrentPoints = user.Points,
                PointsToNextLevel = CalculatePointsForLevel(user.Level + 1) - user.Points,
                TotalBadges = earnedBadges.Count,
                CompletedCourses = completedCourses,
                TotalStudyHours = totalStudyHours,
                EarnedBadges = earnedBadges,
                LockedBadges = lockedBadges,
                RecentPointTransactions = recentPoints,
                ProgressToNextLevel = CalculateProgressToNextLevel(user.Level, user.Points)
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading achievements dashboard");
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة الإنجازات");
            // Return empty view model instead of redirecting to dashboard
            var emptyViewModel = new AchievementsDashboardViewModel
            {
                UserName = "مستخدم",
                CurrentLevel = 1,
                CurrentPoints = 0,
                PointsToNextLevel = 100,
                TotalBadges = 0,
                CompletedCourses = 0,
                TotalStudyHours = 0,
                ProgressToNextLevel = 0,
                EarnedBadges = new List<Domain.Entities.Gamification.UserBadge>(),
                LockedBadges = new List<Domain.Entities.Gamification.Badge>(),
                RecentPointTransactions = new List<Domain.Entities.Gamification.PointTransaction>()
            };
            return View(emptyViewModel);
        }
    }

    /// <summary>
    /// لوحة المتصدرين - Leaderboard
    /// </summary>
    public async Task<IActionResult> Leaderboard(string period = "all")
    {
        try
        {
            var userId = _currentUserService.UserId!;

            var query = _context.Users
                .Where(u => u.Points > 0)
                .AsQueryable();

            // Filter by period if needed
            if (period == "month")
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var monthlyPoints = await _context.PointTransactions
                    .Where(pt => pt.CreatedAt >= startOfMonth && pt.Points > 0)
                    .GroupBy(pt => pt.UserId)
                    .Select(g => new { UserId = g.Key, TotalPoints = g.Sum(pt => pt.Points) })
                    .OrderByDescending(x => x.TotalPoints)
                    .Take(100)
                    .ToListAsync();

                var userIds = monthlyPoints.Select(mp => mp.UserId).ToList();
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id);

                var leaderboard = monthlyPoints.Select((mp, index) => new LeaderboardEntryViewModel
                {
                    Rank = index + 1,
                    UserId = mp.UserId,
                    UserName = users.ContainsKey(mp.UserId) ? users[mp.UserId].FullName : "مستخدم",
                    ProfilePictureUrl = users.ContainsKey(mp.UserId) ? users[mp.UserId].ProfilePictureUrl : null,
                    Points = mp.TotalPoints,
                    Level = users.ContainsKey(mp.UserId) ? users[mp.UserId].Level : 1,
                    IsCurrentUser = mp.UserId == userId
                }).ToList();

                ViewBag.Period = period;
                return View(leaderboard);
            }
            else
            {
                var users = await query
                    .OrderByDescending(u => u.Points)
                    .Take(100)
                    .ToListAsync();

                var leaderboard = users.Select((u, index) => new LeaderboardEntryViewModel
                {
                    Rank = index + 1,
                    UserId = u.Id,
                    UserName = u.FirstName + " " + u.LastName,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    Points = u.Points,
                    Level = u.Level,
                    IsCurrentUser = u.Id == userId
                }).ToList();

                ViewBag.Period = period;
                return View(leaderboard);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leaderboard");
            SetErrorMessage("حدث خطأ أثناء تحميل لوحة المتصدرين");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// تاريخ النقاط - Points history
    /// </summary>
    public async Task<IActionResult> Points(int page = 1)
    {
        var userId = _currentUserService.UserId!;

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound();

        var pageSize = 20;
        var transactions = await _context.PointTransactions
            .Where(pt => pt.UserId == userId)
            .OrderByDescending(pt => pt.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalTransactions = await _context.PointTransactions
            .CountAsync(pt => pt.UserId == userId);

        // Calculate summary
        var totalEarned = await _context.PointTransactions
            .Where(pt => pt.UserId == userId && pt.Points > 0)
            .SumAsync(pt => pt.Points);

        var totalSpent = await _context.PointTransactions
            .Where(pt => pt.UserId == userId && pt.Points < 0)
            .SumAsync(pt => Math.Abs(pt.Points));

        var viewModel = new PointsHistoryViewModel
        {
            CurrentPoints = user.Points,
            CurrentLevel = user.Level,
            TotalEarned = totalEarned,
            TotalSpent = totalSpent,
            Transactions = transactions,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalTransactions / (double)pageSize)
        };

        return View(viewModel);
    }

    #region Private Helper Methods

    private int CalculatePointsForLevel(int level)
    {
        return 100 * level * level;
    }

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

    #endregion
}

#region View Models

public class AchievementsDashboardViewModel
{
    public string UserName { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int CurrentPoints { get; set; }
    public int PointsToNextLevel { get; set; }
    public int TotalBadges { get; set; }
    public int CompletedCourses { get; set; }
    public int TotalStudyHours { get; set; }
    public decimal ProgressToNextLevel { get; set; }
    public List<Domain.Entities.Gamification.UserBadge> EarnedBadges { get; set; } = new();
    public List<Domain.Entities.Gamification.Badge> LockedBadges { get; set; } = new();
    public List<Domain.Entities.Gamification.PointTransaction> RecentPointTransactions { get; set; } = new();
}

public class LeaderboardEntryViewModel
{
    public int Rank { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public int Points { get; set; }
    public int Level { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class PointsHistoryViewModel
{
    public int CurrentPoints { get; set; }
    public int CurrentLevel { get; set; }
    public int TotalEarned { get; set; }
    public int TotalSpent { get; set; }
    public List<Domain.Entities.Gamification.PointTransaction> Transactions { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}

#endregion

