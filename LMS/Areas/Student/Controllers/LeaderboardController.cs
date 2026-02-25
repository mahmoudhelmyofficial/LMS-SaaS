using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

public class LeaderboardController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public LeaderboardController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index(string period = "week")
    {
        var userId = _currentUserService.UserId;
        
        var query = _context.Users
            .Where(u => !u.IsDeleted)
            .Where(u => u.Preferences == null || u.Preferences.ShowInLeaderboard);

        DateTime? startDate = period switch
        {
            "week" => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddMonths(-1),
            _ => null // "all" - no date filter
        };

        List<LeaderboardUserEntry> entries;

        if (startDate.HasValue)
        {
            // For time-based leaderboard, calculate points earned within the period
            entries = await _context.PointTransactions
                .Where(pt => pt.CreatedAt >= startDate.Value && pt.Points > 0)
                .GroupBy(pt => pt.UserId)
                .Select(g => new 
                {
                    UserId = g.Key,
                    PeriodPoints = g.Sum(pt => pt.Points)
                })
                .Join(_context.Users.Where(u => !u.IsDeleted && (u.Preferences == null || u.Preferences.ShowInLeaderboard)),
                    pt => pt.UserId,
                    u => u.Id,
                    (pt, u) => new LeaderboardUserEntry
                    {
                        UserId = u.Id,
                        FullName = u.FirstName + " " + u.LastName,
                        ProfileImageUrl = u.ProfileImageUrl,
                        Points = pt.PeriodPoints,
                        Level = u.Level,
                        LevelName = u.Level >= 4 ? "خبير" : u.Level >= 3 ? "متقدم" : u.Level >= 2 ? "متوسط" : "مبتدئ",
                        BadgesCount = u.Badges.Count,
                        CompletedCourses = u.Enrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed),
                        IsCurrentUser = u.Id == userId
                    })
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.Level)
                .Take(100)
                .ToListAsync();
        }
        else
        {
            // For all-time leaderboard, use total points
            entries = await query
                .OrderByDescending(u => u.Points)
                .ThenByDescending(u => u.Level)
                .Take(100)
                .Select(u => new LeaderboardUserEntry
                {
                    UserId = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    ProfileImageUrl = u.ProfileImageUrl,
                    Points = u.Points,
                    Level = u.Level,
                    LevelName = u.Level >= 4 ? "خبير" : u.Level >= 3 ? "متقدم" : u.Level >= 2 ? "متوسط" : "مبتدئ",
                    BadgesCount = u.Badges.Count,
                    CompletedCourses = u.Enrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed),
                    IsCurrentUser = u.Id == userId
                })
                .ToListAsync();
        }

        // Assign ranks
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Rank = i + 1;
        }

        // Get current user stats
        var currentUser = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Points,
                u.Level,
                BadgesCount = u.Badges.Count,
                CompletedCourses = u.Enrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed)
            })
            .FirstOrDefaultAsync();

        // Find current user's rank
        var userRank = entries.FindIndex(e => e.IsCurrentUser) + 1;
        if (userRank == 0)
        {
            // User not in top 100, calculate actual rank
            var currentUserPoints = currentUser?.Points ?? 0;
            userRank = await _context.Users
                .CountAsync(u => !u.IsDeleted && u.Points > currentUserPoints) + 1;
        }

        var viewModel = new LeaderboardViewModel
        {
            Period = period,
            Entries = entries,
            CurrentUserRank = userRank,
            CurrentUserPoints = currentUser?.Points ?? 0,
            CurrentUserBadges = currentUser?.BadgesCount ?? 0,
            CurrentUserCompletedCourses = currentUser?.CompletedCourses ?? 0
        };

        return View(viewModel);
    }
}

/// <summary>
/// نموذج عرض لوحة المتصدرين - Leaderboard ViewModel
/// </summary>
public class LeaderboardViewModel
{
    public string Period { get; set; } = "week";
    public List<LeaderboardUserEntry> Entries { get; set; } = new();
    public int CurrentUserRank { get; set; }
    public int CurrentUserPoints { get; set; }
    public int CurrentUserBadges { get; set; }
    public int CurrentUserCompletedCourses { get; set; }
}

public class LeaderboardUserEntry
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int Points { get; set; }
    public int Level { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public int BadgesCount { get; set; }
    public int CompletedCourses { get; set; }
    public int Rank { get; set; }
    public bool IsCurrentUser { get; set; }
}

