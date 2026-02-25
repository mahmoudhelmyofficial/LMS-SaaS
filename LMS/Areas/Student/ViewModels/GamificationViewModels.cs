using LMS.Domain.Entities.Gamification;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Gamification Dashboard View Model
/// </summary>
public class GamificationDashboardViewModel
{
    public string UserName { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int CurrentPoints { get; set; }
    public int PointsToNextLevel { get; set; }
    public decimal ProgressToNextLevel { get; set; }
    public int TotalBadges { get; set; }
    public int CompletedCourses { get; set; }
    public int TotalStudyHours { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    
    public List<UserBadge> EarnedBadges { get; set; } = new();
    public List<Badge> LockedBadges { get; set; } = new();
    public List<PointTransaction> RecentPointTransactions { get; set; } = new();
}

/// <summary>
/// Point Transaction Display View Model
/// </summary>
public class PointTransactionDisplayViewModel
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedEntity { get; set; }
}

/// <summary>
/// Badge Achievement View Model
/// </summary>
public class BadgeAchievementViewModel
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string? BadgeDescription { get; set; }
    public string? IconUrl { get; set; }
    public DateTime EarnedAt { get; set; }
}

