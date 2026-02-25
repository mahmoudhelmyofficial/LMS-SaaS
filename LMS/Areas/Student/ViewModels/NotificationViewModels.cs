using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج عرض الإشعار - Notification Display ViewModel
/// </summary>
public class NotificationDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.System;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string? IconClass { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? RelatedEntity { get; set; }
    public int? RelatedEntityId { get; set; }
}

/// <summary>
/// إحصائيات الإشعارات - Notifications Statistics ViewModel
/// </summary>
public class NotificationStatsViewModel
{
    public int TotalUnread { get; set; }
    public int TotalToday { get; set; }
    public int TotalThisWeek { get; set; }
}

/// <summary>
/// إعدادات الإشعارات - Notification Settings ViewModel
/// </summary>
public class NotificationSettingsViewModel
{
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool CourseUpdatesEnabled { get; set; } = true;
    public bool NewAssignmentsEnabled { get; set; } = true;
    public bool GradeReleasedEnabled { get; set; } = true;
    public bool NewMessagesEnabled { get; set; } = true;
    public bool AnnouncementsEnabled { get; set; } = true;
    public bool LiveClassRemindersEnabled { get; set; } = true;
    public bool CertificatesEnabled { get; set; } = true;
    public bool AchievementsEnabled { get; set; } = true;
    public bool SystemUpdatesEnabled { get; set; } = true;
}

