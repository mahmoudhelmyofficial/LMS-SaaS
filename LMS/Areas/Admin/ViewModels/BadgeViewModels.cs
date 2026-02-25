using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

public class BadgeCreateViewModel
{
    [Required(ErrorMessage = "اسم الشارة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم الشارة")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Display(Name = "صورة الشارة")]
    public string? IconUrl { get; set; }

    [Required]
    [Display(Name = "النوع")]
    public BadgeRarity Rarity { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [Display(Name = "النقاط المطلوبة")]
    public int RequiredPoints { get; set; }

    [Display(Name = "نشطة")]
    public bool IsActive { get; set; } = true;
}

public class BadgeEditViewModel : BadgeCreateViewModel
{
    public int Id { get; set; }
}

