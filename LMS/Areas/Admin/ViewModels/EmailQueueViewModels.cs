using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج قائمة قائمة انتظار البريد - Email queue list view model
/// </summary>
public class EmailQueueListViewModel
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public bool IsOpened { get; set; }
    public bool IsClicked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// نموذج تفاصيل البريد - Email details view model
/// </summary>
public class EmailQueueDetailsViewModel
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public string? ReplyToEmail { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public int Priority { get; set; }
    public DateTime ScheduledFor { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? MessageId { get; set; }
    public bool IsOpened { get; set; }
    public DateTime? OpenedAt { get; set; }
    public bool IsClicked { get; set; }
    public DateTime? ClickedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Metadata { get; set; }
}

/// <summary>
/// نموذج إحصائيات البريد - Email statistics view model
/// </summary>
public class EmailQueueStatsViewModel
{
    public int TotalEmails { get; set; }
    public int PendingEmails { get; set; }
    public int ProcessingEmails { get; set; }
    public int SentEmails { get; set; }
    public int FailedEmails { get; set; }
    public int OpenedEmails { get; set; }
    public int ClickedEmails { get; set; }
    public decimal OpenRate { get; set; }
    public decimal ClickRate { get; set; }
    public int EmailsLast24Hours { get; set; }
    public int FailedLast24Hours { get; set; }
    public List<EmailsByDayViewModel> EmailsByDay { get; set; } = new();
    public List<TopFailureReasonViewModel> TopFailureReasons { get; set; } = new();
}

/// <summary>
/// نموذج البريد حسب اليوم - Emails by day view model
/// </summary>
public class EmailsByDayViewModel
{
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// نموذج أسباب الفشل الأكثر شيوعاً - Top failure reasons view model
/// </summary>
public class TopFailureReasonViewModel
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// نموذج إعادة إرسال البريد - Retry email view model
/// </summary>
public class RetryEmailViewModel
{
    public int EmailId { get; set; }

    [Display(Name = "إعادة الجدولة")]
    public DateTime? RescheduleFor { get; set; }

    [Display(Name = "تحديث البريد الإلكتروني")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    public string? NewEmail { get; set; }

    [Display(Name = "تحديث الموضوع")]
    public string? NewSubject { get; set; }
}

/// <summary>
/// نموذج البحث في قائمة الانتظار - Email queue search view model
/// </summary>
public class EmailQueueSearchViewModel
{
    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [Display(Name = "الحالة")]
    public string? Status { get; set; }

    [Display(Name = "من تاريخ")]
    public DateTime? FromDate { get; set; }

    [Display(Name = "إلى تاريخ")]
    public DateTime? ToDate { get; set; }

    [Display(Name = "اسم القالب")]
    public string? TemplateName { get; set; }

    [Display(Name = "الأولوية")]
    public int? Priority { get; set; }

    [Display(Name = "محاولات فاشلة فقط")]
    public bool? FailedOnly { get; set; }
}

