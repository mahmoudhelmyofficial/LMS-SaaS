using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء عرض فلاش - Create Flash Sale ViewModel
/// </summary>
public class FlashSaleCreateViewModel
{
    [Required(ErrorMessage = "اسم العرض مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم العرض")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "نسبة الخصم مطلوبة")]
    [Range(1, 100)]
    [Display(Name = "نسبة الخصم (%)")]
    public decimal DiscountPercentage { get; set; }

    [Required(ErrorMessage = "تاريخ البدء مطلوب")]
    [Display(Name = "تاريخ البدء")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
    [Display(Name = "تاريخ الانتهاء")]
    public DateTime EndDate { get; set; }

    [Display(Name = "الحد الأقصى للمبيعات")]
    public int? MaxSales { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public List<int> CourseIds { get; set; } = new();

    /// <summary>
    /// الدورات المختارة - Selected courses
    /// </summary>
    public List<int> SelectedCourses { get => CourseIds; set => CourseIds = value; }
}

public class FlashSaleEditViewModel : FlashSaleCreateViewModel
{
    public int Id { get; set; }
    public int SalesCount { get; set; }
    public int CurrentSales { get => SalesCount; set => SalesCount = value; }
}

