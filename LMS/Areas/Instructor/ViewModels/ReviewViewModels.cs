using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج الرد على المراجعة - Review Response ViewModel
/// </summary>
public class ReviewResponseViewModel
{
    /// <summary>
    /// معرف المراجعة - Review ID
    /// </summary>
    public int ReviewId { get; set; }

    /// <summary>
    /// اسم الطالب - Student name (for display)
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// عنوان الدورة - Course title (for display)
    /// </summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>
    /// التقييم - Rating (for display)
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// تعليق المراجعة - Review comment (for display)
    /// </summary>
    public string? ReviewComment { get; set; }

    /// <summary>
    /// الرد الموجود - Existing response (for editing)
    /// </summary>
    public string? ExistingResponse { get; set; }

    /// <summary>
    /// الرد - Response
    /// </summary>
    [Required(ErrorMessage = "الرد مطلوب")]
    [Display(Name = "ردك على المراجعة")]
    public string Response { get; set; } = string.Empty;
}

/// <summary>
/// نموذج إحصائيات المراجعات - Review Statistics ViewModel
/// </summary>
public class ReviewStatisticsViewModel
{
    /// <summary>
    /// إجمالي المراجعات - Total reviews
    /// </summary>
    public int TotalReviews { get; set; }

    /// <summary>
    /// متوسط التقييم - Average rating
    /// </summary>
    public double AverageRating { get; set; }

    /// <summary>
    /// عدد 5 نجوم - Five star count
    /// </summary>
    public int FiveStarCount { get; set; }

    /// <summary>
    /// عدد 4 نجوم - Four star count
    /// </summary>
    public int FourStarCount { get; set; }

    /// <summary>
    /// عدد 3 نجوم - Three star count
    /// </summary>
    public int ThreeStarCount { get; set; }

    /// <summary>
    /// عدد نجمتان - Two star count
    /// </summary>
    public int TwoStarCount { get; set; }

    /// <summary>
    /// عدد نجمة واحدة - One star count
    /// </summary>
    public int OneStarCount { get; set; }

    /// <summary>
    /// معدل الرد - Response rate (percentage)
    /// </summary>
    public double ResponseRate { get; set; }
}

/// <summary>
/// نموذج الإبلاغ عن مراجعة - Review Report ViewModel
/// </summary>
public class ReviewReportViewModel
{
    /// <summary>
    /// معرف المراجعة - Review ID
    /// </summary>
    public int ReviewId { get; set; }

    /// <summary>
    /// سبب الإبلاغ - Report reason
    /// </summary>
    [Required(ErrorMessage = "سبب الإبلاغ مطلوب")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "سبب الإبلاغ يجب أن يكون بين 10 و 500 حرف")]
    [Display(Name = "سبب الإبلاغ")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// ملاحظات إضافية - Additional notes
    /// </summary>
    [StringLength(1000, ErrorMessage = "الملاحظات يجب ألا تتجاوز 1000 حرف")]
    [Display(Name = "ملاحظات إضافية")]
    public string? AdditionalNotes { get; set; }
}

