using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

public class LandingPageCreateViewModel
{
    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(300)]
    [Display(Name = "الرابط المختصر")]
    public string? Slug { get; set; }

    [Display(Name = "القالب")]
    public int? TemplateId { get; set; }

    [Display(Name = "الحالة")]
    public LandingPageStatus Status { get; set; } = LandingPageStatus.Draft;

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "المحتوى")]
    public string? Content { get; set; }

    [MaxLength(200)]
    [Display(Name = "عنوان SEO")]
    public string? MetaTitle { get; set; }

    [MaxLength(500)]
    [Display(Name = "الكلمات المفتاحية")]
    public string? MetaKeywords { get; set; }

    [MaxLength(500)]
    [Display(Name = "وصف SEO")]
    public string? MetaDescription { get; set; }

    [Display(Name = "تاريخ النشر")]
    public DateTime? PublishDate { get; set; }

    [Display(Name = "تاريخ الانتهاء")]
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط الصورة")]
    public string? FeaturedImageUrl { get; set; }
}

public class LandingPageEditViewModel : LandingPageCreateViewModel
{
    public int Id { get; set; }
    public int ViewsCount { get; set; }
    public int ConversionsCount { get; set; }
    public int ViewCount { get => ViewsCount; set => ViewsCount = value; }
    public int ConversionCount { get => ConversionsCount; set => ConversionsCount = value; }
}

