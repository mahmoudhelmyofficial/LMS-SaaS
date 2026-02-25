namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Quick Actions View Model
/// </summary>
public class QuickActionsViewModel
{
    public LMS.Domain.Entities.Learning.Enrollment? ContinueLearning { get; set; }
    public QuizInfo? NextQuiz { get; set; }
    public AssignmentInfo? NextAssignment { get; set; }
    public List<LMS.Domain.Entities.Learning.Enrollment> CoursesToReview { get; set; } = new();
}

public class QuizInfo
{
    public int QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
}

public class AssignmentInfo
{
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}

/// <summary>
/// Achievements View Model for Dashboard
/// </summary>
public class AchievementsViewModel
{
    public LMS.Domain.Entities.Users.ApplicationUser User { get; set; } = null!;
    public LMS.Services.Interfaces.StudentLearningStats Stats { get; set; } = new();
    public List<LMS.Domain.Entities.Gamification.UserBadge> Badges { get; set; } = new();
    public List<LMS.Domain.Entities.Certifications.Certificate> Certificates { get; set; } = new();
    public List<MilestoneItem> Milestones { get; set; } = new();
}

public class MilestoneItem
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Achieved { get; set; }
}

/// <summary>
/// Milestone View Model (with additional properties for enhanced views)
/// </summary>
public class MilestoneViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = "🏆";
    public string Icon { get => IconEmoji; set => IconEmoji = value; }
    public bool IsAchieved { get; set; }
    public bool Achieved { get => IsAchieved; set => IsAchieved = value; }
    public int ProgressCurrent { get; set; }
    public int ProgressTotal { get; set; }
    public DateTime? AchievedAt { get; set; }
}

/// <summary>
/// Share Progress View Model (replaces anonymous type for type safety)
/// </summary>
public class ShareProgressViewModel
{
    public string CourseName { get; set; } = string.Empty;
    public decimal Progress { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
