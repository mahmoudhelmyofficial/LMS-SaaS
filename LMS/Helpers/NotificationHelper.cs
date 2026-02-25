using LMS.Domain.Enums;

namespace LMS.Helpers;

/// <summary>
/// مساعد الإشعارات - Notification Display Helper
/// Provides utility methods for notification display formatting
/// </summary>
public static class NotificationHelper
{
    /// <summary>
    /// الحصول على معلومات نوع الإشعار - Get notification type display info (Arabic)
    /// </summary>
    public static (string Icon, string Color, string Label) GetTypeInfo(NotificationType type) => GetTypeInfo(type, "ar");

    /// <summary>
    /// Get notification type display info with culture (ar/en).
    /// </summary>
    public static (string Icon, string Color, string Label) GetTypeInfo(NotificationType type, string culture)
    {
        var isAr = culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ?? true;
        return type switch
        {
            NotificationType.NewEnrollment or NotificationType.CourseEnrollment
                => ("feather-user-plus", "primary", isAr ? "تسجيل" : "Enrollment"),
            NotificationType.NewReview
                => ("feather-star", "warning", isAr ? "تقييم" : "Review"),
            NotificationType.Message or NotificationType.NewMessage
                => ("feather-mail", "info", isAr ? "رسالة" : "Message"),
            NotificationType.Payment or NotificationType.PaymentUpdate or NotificationType.Purchase or NotificationType.Sale
                => ("feather-dollar-sign", "success", isAr ? "مالية" : "Payment"),
            NotificationType.Course or NotificationType.CourseUpdate
                => ("feather-book", "purple", isAr ? "دورة" : "Course"),
            NotificationType.CourseCompleted
                => ("feather-check-circle", "success", isAr ? "إكمال" : "Completed"),
            NotificationType.AssignmentSubmitted
                => ("feather-file-text", "orange", isAr ? "واجب" : "Assignment"),
            NotificationType.AssignmentGraded
                => ("feather-check-square", "success", isAr ? "تقييم" : "Graded"),
            NotificationType.Assessment or NotificationType.QuizCompleted
                => ("feather-help-circle", "cyan", isAr ? "اختبار" : "Quiz"),
            NotificationType.QuizGraded
                => ("feather-check-circle", "success", isAr ? "نتيجة" : "Result"),
            NotificationType.DiscussionReply or NotificationType.CommentReply
                => ("feather-message-circle", "teal", isAr ? "مناقشة" : "Discussion"),
            NotificationType.System
                => ("feather-settings", "secondary", isAr ? "نظام" : "System"),
            NotificationType.Reminder
                => ("feather-clock", "warning", isAr ? "تذكير" : "Reminder"),
            NotificationType.Achievement or NotificationType.BadgeEarned
                => ("feather-award", "warning", isAr ? "إنجاز" : "Achievement"),
            NotificationType.Certificate or NotificationType.CertificateIssued
                => ("feather-award", "success", isAr ? "شهادة" : "Certificate"),
            NotificationType.LiveClass or NotificationType.LiveClassReminder
                => ("feather-video", "danger", isAr ? "بث مباشر" : "Live"),
            NotificationType.NewAnnouncement
                => ("feather-volume-2", "info", isAr ? "إعلان" : "Announcement"),
            NotificationType.Warning
                => ("feather-alert-triangle", "warning", isAr ? "تحذير" : "Warning"),
            NotificationType.Error
                => ("feather-x-circle", "danger", isAr ? "خطأ" : "Error"),
            NotificationType.Success
                => ("feather-check-circle", "success", isAr ? "نجاح" : "Success"),
            NotificationType.Info
                => ("feather-info", "info", isAr ? "معلومات" : "Info"),
            NotificationType.Social
                => ("feather-users", "primary", isAr ? "اجتماعي" : "Social"),
            NotificationType.Promotional
                => ("feather-gift", "primary", isAr ? "عرض" : "Promo"),
            _ => ("feather-bell", "primary", isAr ? "إشعار" : "Notification")
        };
    }

    /// <summary>
    /// الحصول على أيقونة النوع - Get icon for notification type
    /// </summary>
    public static string GetIcon(NotificationType type)
    {
        var (icon, _, _) = GetTypeInfo(type);
        return icon;
    }

    /// <summary>
    /// الحصول على لون النوع - Get color for notification type
    /// </summary>
    public static string GetColor(NotificationType type)
    {
        var (_, color, _) = GetTypeInfo(type);
        return color;
    }

    /// <summary>
    /// الحصول على تسمية النوع - Get label for notification type (uses current culture via overload in views)
    /// </summary>
    public static string GetLabel(NotificationType type)
    {
        var (_, _, label) = GetTypeInfo(type);
        return label;
    }

    /// <summary>
    /// Get label for notification type in given culture (ar/en).
    /// </summary>
    public static string GetLabel(NotificationType type, string culture)
    {
        var (_, _, label) = GetTypeInfo(type, culture);
        return label;
    }

    /// <summary>
    /// الحصول على الوقت النسبي - Get relative time display
    /// </summary>
    /// <param name="dateTime">The datetime to format</param>
    /// <param name="culture">Culture for formatting (ar/en)</param>
    /// <returns>Relative time string</returns>
    public static string GetTimeAgo(DateTime dateTime, string culture = "ar")
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (culture == "ar")
        {
            return timeSpan.TotalMinutes switch
            {
                < 1 => "الآن",
                < 2 => "منذ دقيقة",
                < 11 => $"منذ {(int)timeSpan.TotalMinutes} دقائق",
                < 60 => $"منذ {(int)timeSpan.TotalMinutes} دقيقة",
                < 2 * 60 => "منذ ساعة",
                < 24 * 60 => $"منذ {(int)timeSpan.TotalHours} ساعات",
                < 2 * 24 * 60 => "منذ يوم",
                < 7 * 24 * 60 => $"منذ {(int)timeSpan.TotalDays} أيام",
                < 14 * 24 * 60 => "منذ أسبوع",
                < 30 * 24 * 60 => $"منذ {(int)(timeSpan.TotalDays / 7)} أسابيع",
                < 60 * 24 * 60 => "منذ شهر",
                < 365 * 24 * 60 => $"منذ {(int)(timeSpan.TotalDays / 30)} أشهر",
                _ => dateTime.ToString("dd/MM/yyyy")
            };
        }

        return timeSpan.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)timeSpan.TotalMinutes}m ago",
            < 24 * 60 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 * 24 * 60 => $"{(int)timeSpan.TotalDays}d ago",
            _ => dateTime.ToString("dd/MM/yyyy")
        };
    }

    /// <summary>
    /// الحصول على لون الأولوية - Get priority color
    /// </summary>
    /// <param name="priority">Priority level (1-5)</param>
    public static string GetPriorityColor(int priority) => priority switch
    {
        5 => "danger",
        4 => "warning",
        3 => "primary",
        2 => "info",
        _ => "secondary"
    };

    /// <summary>
    /// الحصول على تسمية الأولوية - Get priority label (Arabic)
    /// </summary>
    public static string GetPriorityLabel(int priority) => GetPriorityLabel(priority, "ar");

    /// <summary>
    /// Get priority label in given culture (ar/en).
    /// </summary>
    public static string GetPriorityLabel(int priority, string culture)
    {
        var isAr = culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ?? true;
        return priority switch
        {
            5 => isAr ? "عاجل" : "Urgent",
            4 => isAr ? "مهم" : "Important",
            3 => isAr ? "عادي" : "Normal",
            2 => isAr ? "منخفض" : "Low",
            _ => isAr ? "أرشيف" : "Archive"
        };
    }

    /// <summary>
    /// التحقق من الإشعار المهم - Check if notification is important
    /// </summary>
    public static bool IsImportant(NotificationType type, int priority)
    {
        if (priority >= 4) return true;

        return type switch
        {
            NotificationType.Payment or NotificationType.PaymentUpdate => true,
            NotificationType.AssignmentSubmitted => true,
            NotificationType.LiveClassReminder => true,
            NotificationType.Warning or NotificationType.Error => true,
            NotificationType.CourseCompleted => true,
            _ => false
        };
    }

    /// <summary>
    /// تجميع الإشعارات حسب التاريخ - Group notifications by date (Arabic)
    /// </summary>
    public static string GetDateGroup(DateTime dateTime) => GetDateGroup(dateTime, "ar");

    /// <summary>
    /// Group notifications by date in given culture (ar/en).
    /// </summary>
    public static string GetDateGroup(DateTime dateTime, string culture)
    {
        var today = DateTime.UtcNow.Date;
        var date = dateTime.Date;
        var isAr = culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ?? true;

        if (date == today)
            return isAr ? "اليوم" : "Today";
        if (date == today.AddDays(-1))
            return isAr ? "أمس" : "Yesterday";
        if (date >= today.AddDays(-7))
            return isAr ? "هذا الأسبوع" : "This week";
        if (date >= today.AddDays(-30))
            return isAr ? "هذا الشهر" : "This month";

        return dateTime.ToString("MMMM yyyy");
    }

    /// <summary>
    /// الحصول على قائمة أنواع الإشعارات للتصفية - Get notification types for filter dropdown (Arabic)
    /// </summary>
    public static IEnumerable<(NotificationType Type, string Label)> GetFilterableTypes() => GetFilterableTypes("ar");

    /// <summary>
    /// Get notification types for filter dropdown in given culture (ar/en).
    /// </summary>
    public static IEnumerable<(NotificationType Type, string Label)> GetFilterableTypes(string culture)
    {
        var isAr = culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ?? true;
        if (isAr)
            return new[]
            {
                (NotificationType.NewEnrollment, "التسجيلات"),
                (NotificationType.NewReview, "التقييمات"),
                (NotificationType.Payment, "المالية"),
                (NotificationType.AssignmentSubmitted, "الواجبات"),
                (NotificationType.QuizCompleted, "الاختبارات"),
                (NotificationType.DiscussionReply, "المناقشات"),
                (NotificationType.Message, "الرسائل"),
                (NotificationType.Course, "الدورات"),
                (NotificationType.System, "النظام"),
                (NotificationType.Reminder, "التذكيرات")
            };
        return new[]
        {
            (NotificationType.NewEnrollment, "Enrollments"),
            (NotificationType.NewReview, "Reviews"),
            (NotificationType.Payment, "Payments"),
            (NotificationType.AssignmentSubmitted, "Assignments"),
            (NotificationType.QuizCompleted, "Quizzes"),
            (NotificationType.DiscussionReply, "Discussions"),
            (NotificationType.Message, "Messages"),
            (NotificationType.Course, "Courses"),
            (NotificationType.System, "System"),
            (NotificationType.Reminder, "Reminders")
        };
    }
}


