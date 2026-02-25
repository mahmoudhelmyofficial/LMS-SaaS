using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

// Note: SalesReportViewModel, SalesChartPoint, TopSellingCourse, RecentSale, 
// CategoryFilterItem, ChartDataPoint, and CoursePerformanceItem are defined in ReportViewModels.cs

/// <summary>
/// Category Chart Item - عنصر مخطط الفئات
/// </summary>
public class CategoryChartItem
{
    public string Name { get; set; } = string.Empty;
    public int CourseCount { get; set; }
}

/// <summary>
/// Report template view model
/// </summary>
public class ReportTemplateViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "اسم القالب مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم القالب")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نوع التقرير مطلوب")]
    [Display(Name = "نوع التقرير")]
    public ReportType ReportType { get; set; }

    [Display(Name = "التكوين (JSON)")]
    public string? Configuration { get; set; }
}

/// <summary>
/// Scheduled report view model
/// </summary>
public class ScheduledReportViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "القالب مطلوب")]
    [Display(Name = "قالب التقرير")]
    public int TemplateId { get; set; }

    [Required(ErrorMessage = "اسم التقرير مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم التقرير")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "التكرار مطلوب")]
    [Display(Name = "التكرار")]
    [MaxLength(20)]
    public string Frequency { get; set; } = "Daily"; // Daily, Weekly, Monthly

    [Display(Name = "يوم الأسبوع")]
    [Range(0, 6)]
    public int? DayOfWeek { get; set; }

    [Display(Name = "يوم الشهر")]
    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    [Required(ErrorMessage = "وقت التشغيل مطلوب")]
    [Display(Name = "وقت التشغيل")]
    public TimeSpan TimeOfDay { get; set; } = new TimeSpan(8, 0, 0);

    [Required(ErrorMessage = "المستلمون مطلوبون")]
    [Display(Name = "المستلمون (بريد إلكتروني مفصول بفاصلة)")]
    public string Recipients { get; set; } = string.Empty;

    [Required(ErrorMessage = "التنسيق مطلوب")]
    [Display(Name = "التنسيق")]
    [MaxLength(10)]
    public string Format { get; set; } = "PDF"; // PDF, Excel, CSV
}

/// <summary>
/// Report export view model
/// </summary>
public class ReportExportViewModel
{
    public int Id { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public ReportType ReportType { get; set; }
    public string Format { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string? GeneratedByUserName { get; set; }
}

/// <summary>
/// Report generation request view model
/// </summary>
public class ReportGenerationViewModel
{
    [Required(ErrorMessage = "نوع التقرير مطلوب")]
    [Display(Name = "نوع التقرير")]
    public ReportType ReportType { get; set; }

    [Display(Name = "من تاريخ")]
    public DateTime? FromDate { get; set; }

    [Display(Name = "إلى تاريخ")]
    public DateTime? ToDate { get; set; }

    [Display(Name = "المستخدم")]
    public string? UserId { get; set; }

    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    [Required(ErrorMessage = "التنسيق مطلوب")]
    [Display(Name = "التنسيق")]
    public string Format { get; set; } = "PDF";

    [Display(Name = "إرسال عبر البريد")]
    public bool SendEmail { get; set; }

    [Display(Name = "البريد الإلكتروني")]
    [EmailAddress]
    public string? Email { get; set; }
}

