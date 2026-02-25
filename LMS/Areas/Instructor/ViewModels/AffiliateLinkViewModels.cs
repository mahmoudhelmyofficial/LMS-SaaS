using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء رابط أفلييت - Create Affiliate Link ViewModel
/// </summary>
public class AffiliateLinkCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID (null = all courses)
    /// </summary>
    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    /// <summary>
    /// اسم الحملة - Campaign name
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "اسم الحملة")]
    public string? CampaignName { get; set; }

    /// <summary>
    /// الوصف - Description
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// نسبة العمولة - Commission rate
    /// </summary>
    [Required(ErrorMessage = "نسبة العمولة مطلوبة")]
    [Range(0, 100)]
    [Display(Name = "نسبة العمولة %")]
    public decimal CommissionRate { get; set; } = 10.00m;

    /// <summary>
    /// نوع العمولة - Commission type
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Display(Name = "نوع العمولة")]
    public string CommissionType { get; set; } = "Percentage";

    /// <summary>
    /// العمولة الثابتة - Fixed commission
    /// </summary>
    [Display(Name = "العمولة الثابتة")]
    public decimal? FixedCommission { get; set; }

    /// <summary>
    /// تاريخ البدء - Valid from
    /// </summary>
    [Display(Name = "تاريخ البدء")]
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// تاريخ الانتهاء - Valid to
    /// </summary>
    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// مدة الكوكي بالأيام - Cookie duration in days
    /// </summary>
    [Required]
    [Range(1, 365)]
    [Display(Name = "مدة الكوكي (أيام)")]
    public int CookieDurationDays { get; set; } = 30;

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج تعديل رابط أفلييت - Edit Affiliate Link ViewModel
/// </summary>
public class AffiliateLinkEditViewModel : AffiliateLinkCreateViewModel
{
    /// <summary>
    /// معرف الرابط - Link ID
    /// </summary>
    public int Id { get; set; }
}

