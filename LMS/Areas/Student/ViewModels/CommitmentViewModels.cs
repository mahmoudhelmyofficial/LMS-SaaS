namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إحصائيات الالتزام - Commitment Stats ViewModel
/// </summary>
public class CommitmentStatsViewModel
{
    public int CommitmentScore { get; set; }
    public int TotalPlannedSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int MissedSessions { get; set; }
    public int PendingSessions { get; set; }
    public decimal TotalPlannedHours { get; set; }
    public decimal TotalActualHours { get; set; }
    public decimal MissedHours { get; set; }
    public string? BestDay { get; set; }
    public string? WorstDay { get; set; }
    public List<DailyCommitmentViewModel> DailyCommitment { get; set; } = new();
    public List<WeeklyPatternViewModel> WeeklyPattern { get; set; } = new();
    public decimal CompletionRate { get; set; }
    public int CurrentStreak { get; set; }
    public int AverageSessionDuration { get; set; }

    // Aliases for view compatibility
    public int TotalSessionsScheduled { get => TotalPlannedSessions; set => TotalPlannedSessions = value; }
    public int SessionsCompleted { get => CompletedSessions; set => CompletedSessions = value; }
    public int SessionsMissed { get => MissedSessions; set => MissedSessions = value; }
    public int SessionsUpcoming { get => PendingSessions; set => PendingSessions = value; }
    public int LongestStreak { get; set; }
}

public class DailyCommitmentViewModel
{
    public DateTime Date { get; set; }
    public int PlannedSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int MissedSessions { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal CommitmentRate { get => CompletionRate; set => CompletionRate = value; }
}

public class WeeklyPatternViewModel
{
    public string DayOfWeek { get; set; } = string.Empty;
    public int AverageSessions { get; set; }
    public decimal CompletionRate { get; set; }
    public int Completed { get; set; }
    public int Missed { get; set; }
}
