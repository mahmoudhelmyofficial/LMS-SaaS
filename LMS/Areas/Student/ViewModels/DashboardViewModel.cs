using LMS.Services.Interfaces;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Enhanced Student Dashboard View Model
/// Focus-based UX with personalized daily focus and smart recommendations
/// </summary>
public class EnhancedDashboardViewModel
{
    #region User Info

    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Greeting { get; set; } = string.Empty;

    #endregion

    #region Daily Focus (Top Priority Section)

    public DailyFocusDto DailyFocus { get; set; } = new();
    public NextActionDto NextBestAction { get; set; } = new();

    #endregion

    #region Streak & Gamification

    public StreakInfoViewModel Streak { get; set; } = new();
    public int TotalPoints { get; set; }
    public int CurrentLevel { get; set; }
    public string LevelName { get; set; } = "Beginner";
    public decimal LevelProgress { get; set; }
    public int PointsToNextLevel { get; set; }
    public List<RecentAchievementViewModel> RecentAchievements { get; set; } = new();

    #endregion

    #region Active Learning

    public List<ActiveCourseViewModel> ActiveCourses { get; set; } = new();
    public ContinueLearningViewModel? ContinueLearning { get; set; }

    #endregion

    #region Spaced Repetition

    public int FlashcardsDueToday { get; set; }
    public int TotalFlashcardDecks { get; set; }
    public decimal ReviewRetentionRate { get; set; }
    public string FlashcardsActionUrl { get; set; } = "/Student/Flashcards";

    #endregion

    #region Study Groups

    public int ActiveStudyGroups { get; set; }
    public List<UpcomingSessionViewModel> UpcomingSessions { get; set; } = new();
    public List<StudyGroupNotificationViewModel> GroupNotifications { get; set; } = new();

    #endregion

    #region Calendar & Deadlines

    public List<DeadlineViewModel> UpcomingDeadlines { get; set; } = new();
    public List<CalendarEventViewModel> TodaysEvents { get; set; } = new();

    #endregion

    #region Analytics Summary

    public WeeklyProgressViewModel WeeklyProgress { get; set; } = new();
    public List<SkillProgressViewModel> SkillProgress { get; set; } = new();

    #endregion

    #region Recommendations

    public List<LearningRecommendationDto> Recommendations { get; set; } = new();
    public List<CourseRecommendationViewModel> CourseRecommendations { get; set; } = new();

    #endregion

    #region Quick Actions

    public List<QuickActionViewModel> QuickActions { get; set; } = new();

    #endregion

    #region Notifications

    public int UnreadNotifications { get; set; }
    public List<NotificationViewModel> RecentNotifications { get; set; } = new();

    #endregion
}

#region Supporting View Models

public class StreakInfoViewModel
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool CompletedToday { get; set; }
    public bool AtRisk { get; set; }
    public List<bool> WeekActivity { get; set; } = new(); // Last 7 days
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔥";
}

public class RecentAchievementViewModel
{
    public int BadgeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }
    public bool IsNew { get; set; }
}

public class ActiveCourseViewModel
{
    public int CourseId { get; set; }
    public int EnrollmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public decimal Progress { get; set; }
    public int CompletedLessons { get; set; }
    public int TotalLessons { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public int? NextLessonId { get; set; }
    public string? NextLessonTitle { get; set; }
    public int RemainingMinutes { get; set; }
    public string EstimatedTimeToComplete { get; set; } = string.Empty;
    public bool HasQuizDue { get; set; }
    public bool HasAssignmentDue { get; set; }
}

public class ContinueLearningViewModel
{
    public int LessonId { get; set; }
    public int CourseId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int ResumeAtSeconds { get; set; }
    public string ResumeAtFormatted { get; set; } = string.Empty;
    public decimal LessonProgress { get; set; }
    public int RemainingMinutes { get; set; }
    public string? CurrentChapter { get; set; }
}

public class UpcomingSessionViewModel
{
    public int SessionId { get; set; }
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public string TimeUntil { get; set; } = string.Empty;
    public int ParticipantsCount { get; set; }
    public string? MeetingLink { get; set; }
    public string RsvpStatus { get; set; } = "Pending";
}

public class StudyGroupNotificationViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty; // "Message", "Session", "Goal"
}

// DeadlineViewModel is defined in StudyPlannerViewModels.cs
// CalendarEventViewModel is defined in CalendarViewModels.cs

public class WeeklyProgressViewModel
{
    public int TotalStudyMinutes { get; set; }
    public int LessonsCompleted { get; set; }
    public int QuizzesCompleted { get; set; }
    public int DaysActive { get; set; }
    public List<DailyStudyViewModel> DailyBreakdown { get; set; } = new();
    public int ChangeFromLastWeek { get; set; } // Percentage
    public string TrendDirection { get; set; } = "Stable"; // "Up", "Down", "Stable"
}

public class DailyStudyViewModel
{
    public DayOfWeek Day { get; set; }
    public DateTime Date { get; set; }
    public int StudyMinutes { get; set; }
    public bool IsToday { get; set; }
}

public class SkillProgressViewModel
{
    public string SkillName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal MasteryLevel { get; set; }
    public string Status { get; set; } = "Learning"; // "NotStarted", "Learning", "Proficient", "Mastered"
    public int CoursesCompleted { get; set; }
    public int TotalCourses { get; set; }
}

public class CourseRecommendationViewModel
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int StudentsCount { get; set; }
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class QuickActionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public bool IsHighlighted { get; set; }
}

public class NotificationViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}

#endregion

