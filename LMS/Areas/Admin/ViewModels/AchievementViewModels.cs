using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LMS.Areas.Admin.ViewModels;

public class AchievementCreateViewModel
{
    [Required(ErrorMessage = "اسم الإنجاز مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم الإنجاز")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "عنوان الإنجاز مطلوب")]
    [MaxLength(200)]
    [Display(Name = "العنوان")]
    public string Title { get => Name; set => Name = value; }

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Display(Name = "الأيقونة")]
    public string? IconUrl { get; set; }

    [MaxLength(500)]
    [Display(Name = "الأيقونة")]
    public string? Icon { get => IconUrl; set => IconUrl = value; }

    [Required]
    [Range(0, int.MaxValue)]
    [Display(Name = "النقاط المكتسبة")]
    public int Points { get; set; }

    [MaxLength(100)]
    [Display(Name = "نوع الإنجاز")]
    public string AchievementType { get; set; } = "Course"; // Course, Lesson, Quiz, etc.

    [MaxLength(100)]
    [Display(Name = "النوع")]
    public string Type { get => AchievementType; set => AchievementType = value; }

    [MaxLength(50)]
    [Display(Name = "الندرة")]
    public string Rarity { get; set; } = "Common";

    [Display(Name = "القيمة المطلوبة")]
    public int RequiredValue { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "صورة الشارة")]
    public IFormFile? BadgeImageFile { get; set; }

    [Display(Name = "إنجاز سري")]
    public bool IsSecret { get; set; } = false;

    [Display(Name = "قابل للتكرار")]
    public bool IsRepeatable { get; set; } = false;

    [Display(Name = "إظهار الإشعار")]
    public bool ShowNotification { get; set; } = true;
}

public class AchievementEditViewModel : AchievementCreateViewModel
{
    public int Id { get; set; }

    [Display(Name = "عدد مرات الفتح")]
    public int UnlockedCount { get; set; } = 0;

    [Display(Name = "صورة الشارة الحالية")]
    public string? CurrentBadgeUrl { get; set; }
}

