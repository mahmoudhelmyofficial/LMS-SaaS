using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج عرض سجل النشاط - Activity Log Display ViewModel
/// </summary>
public class ActivityLogViewModel
{
    /// <summary>
    /// المعرف - Activity log ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// معرف المستخدم - User ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// اسم المستخدم - User name
    /// </summary>
    [Display(Name = "المستخدم")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني - User email
    /// </summary>
    [Display(Name = "البريد الإلكتروني")]
    public string? UserEmail { get; set; }

    /// <summary>
    /// دور المستخدم - User role
    /// </summary>
    [Display(Name = "الدور")]
    public string? UserRole { get; set; }

    /// <summary>
    /// نوع النشاط - Activity type
    /// </summary>
    [Display(Name = "نوع النشاط")]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// وصف النشاط - Activity description
    /// </summary>
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    #region Entity Reference

    /// <summary>
    /// نوع الكيان - Entity type
    /// </summary>
    [Display(Name = "نوع الكيان")]
    public string? EntityType { get; set; }

    /// <summary>
    /// معرف الكيان - Entity ID
    /// </summary>
    [Display(Name = "معرف الكيان")]
    public int? EntityId { get; set; }

    /// <summary>
    /// اسم الكيان - Entity name
    /// </summary>
    [Display(Name = "اسم الكيان")]
    public string? EntityName { get; set; }

    #endregion

    #region Change Tracking

    /// <summary>
    /// القيمة القديمة - Old value (for updates)
    /// </summary>
    [Display(Name = "القيمة القديمة")]
    public string? OldValue { get; set; }

    /// <summary>
    /// القيمة الجديدة - New value (for updates)
    /// </summary>
    [Display(Name = "القيمة الجديدة")]
    public string? NewValue { get; set; }

    /// <summary>
    /// بيانات إضافية - Additional details (JSON)
    /// </summary>
    public string? Details { get; set; }

    #endregion

    #region Request Info

    /// <summary>
    /// عنوان IP - IP address
    /// </summary>
    [Display(Name = "عنوان IP")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// نوع الجهاز - Device type
    /// </summary>
    [Display(Name = "نوع الجهاز")]
    public string? DeviceType { get; set; }

    /// <summary>
    /// المتصفح - Browser name
    /// </summary>
    [Display(Name = "المتصفح")]
    public string? Browser { get; set; }

    /// <summary>
    /// نظام التشغيل - Operating system
    /// </summary>
    [Display(Name = "نظام التشغيل")]
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// الدولة - Country
    /// </summary>
    [Display(Name = "الدولة")]
    public string? Country { get; set; }

    /// <summary>
    /// المدينة - City
    /// </summary>
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    #endregion

    #region Session Info

    /// <summary>
    /// معرف الجلسة - Session ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// الصفحة المرجعية - Referrer URL
    /// </summary>
    public string? Referrer { get; set; }

    /// <summary>
    /// الرابط الحالي - Current URL
    /// </summary>
    [Display(Name = "الصفحة")]
    public string? Url { get; set; }

    #endregion

    /// <summary>
    /// تاريخ النشاط - Activity timestamp
    /// </summary>
    [Display(Name = "التاريخ")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// المدة بالثواني - Duration in seconds
    /// </summary>
    [Display(Name = "المدة")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// المدة المنسقة - Formatted duration
    /// </summary>
    [Display(Name = "المدة")]
    public string FormattedDuration
    {
        get
        {
            if (!DurationSeconds.HasValue || DurationSeconds.Value == 0)
                return "-";

            var ts = TimeSpan.FromSeconds(DurationSeconds.Value);
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            else if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}:{ts.Seconds:D2}";
            else
                return $"{ts.Seconds}s";
        }
    }
}

/// <summary>
/// نموذج قائمة سجلات النشاط - Activity Logs List ViewModel
/// </summary>
public class ActivityLogListViewModel
{
    /// <summary>
    /// قائمة الأنشطة - Activity logs list
    /// </summary>
    public List<ActivityLogViewModel> Logs { get; set; } = new();

    /// <summary>
    /// إجمالي السجلات - Total logs count
    /// </summary>
    public int TotalLogs { get; set; }

    /// <summary>
    /// الصفحة الحالية - Current page
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// إجمالي الصفحات - Total pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// حجم الصفحة - Page size
    /// </summary>
    public int PageSize { get; set; } = 50;

    #region Filters

    /// <summary>
    /// بحث - Search term
    /// </summary>
    [Display(Name = "بحث")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// نوع النشاط - Activity type filter
    /// </summary>
    [Display(Name = "نوع النشاط")]
    public string? ActivityType { get; set; }

    /// <summary>
    /// معرف المستخدم - User ID filter
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// نوع الكيان - Entity type filter
    /// </summary>
    [Display(Name = "نوع الكيان")]
    public string? EntityType { get; set; }

    /// <summary>
    /// من تاريخ - From date filter
    /// </summary>
    [Display(Name = "من تاريخ")]
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// إلى تاريخ - To date filter
    /// </summary>
    [Display(Name = "إلى تاريخ")]
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// عنوان IP - IP address filter
    /// </summary>
    [Display(Name = "عنوان IP")]
    public string? IpAddress { get; set; }

    #endregion

    /// <summary>
    /// أنواع الأنشطة المتاحة - Available activity types
    /// </summary>
    public List<string> AvailableActivityTypes { get; set; } = new();

    /// <summary>
    /// أنواع الكيانات المتاحة - Available entity types
    /// </summary>
    public List<string> AvailableEntityTypes { get; set; } = new();
}

/// <summary>
/// نموذج إحصائيات سجلات النشاط - Activity Logs Statistics ViewModel
/// </summary>
public class ActivityLogStatisticsViewModel
{
    /// <summary>
    /// إجمالي الأنشطة - Total activities
    /// </summary>
    [Display(Name = "إجمالي الأنشطة")]
    public int TotalActivities { get; set; }

    /// <summary>
    /// المستخدمون النشطون - Active users count
    /// </summary>
    [Display(Name = "المستخدمون النشطون")]
    public int ActiveUsers { get; set; }

    /// <summary>
    /// أكثر نشاط - Most common activity
    /// </summary>
    [Display(Name = "أكثر نشاط")]
    public string? MostCommonActivity { get; set; }

    /// <summary>
    /// عدد أكثر نشاط - Most common activity count
    /// </summary>
    public int MostCommonActivityCount { get; set; }

    /// <summary>
    /// أكثر مستخدم نشاطاً - Most active user
    /// </summary>
    [Display(Name = "أكثر مستخدم نشاطاً")]
    public string? MostActiveUser { get; set; }

    /// <summary>
    /// عدد أنشطة أكثر مستخدم - Most active user activity count
    /// </summary>
    public int MostActiveUserCount { get; set; }

    /// <summary>
    /// الأنشطة حسب النوع - Activities by type
    /// </summary>
    public Dictionary<string, int> ActivitiesByType { get; set; } = new();

    /// <summary>
    /// الأنشطة حسب اليوم - Activities by day (last 7 days)
    /// </summary>
    public Dictionary<DateTime, int> ActivitiesByDay { get; set; } = new();

    /// <summary>
    /// الأنشطة حسب الساعة - Activities by hour (last 24 hours)
    /// </summary>
    public Dictionary<int, int> ActivitiesByHour { get; set; } = new();

    /// <summary>
    /// المتصفحات - Browser usage
    /// </summary>
    public Dictionary<string, int> BrowserUsage { get; set; } = new();

    /// <summary>
    /// أنظمة التشغيل - Operating system usage
    /// </summary>
    public Dictionary<string, int> OsUsage { get; set; } = new();

    /// <summary>
    /// أنواع الأجهزة - Device type usage
    /// </summary>
    public Dictionary<string, int> DeviceTypeUsage { get; set; } = new();

    /// <summary>
    /// الدول - Top countries
    /// </summary>
    public Dictionary<string, int> TopCountries { get; set; } = new();
}

/// <summary>
/// نموذج تفاصيل سجل النشاط - Activity Log Details ViewModel
/// </summary>
public class ActivityLogDetailsViewModel : ActivityLogViewModel
{
    /// <summary>
    /// User Agent الكامل - Full user agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// تفاصيل إضافية محللة - Parsed additional details
    /// </summary>
    public Dictionary<string, object>? ParsedDetails { get; set; }

    /// <summary>
    /// الأنشطة ذات الصلة - Related activities
    /// </summary>
    public List<ActivityLogViewModel> RelatedActivities { get; set; } = new();
}

