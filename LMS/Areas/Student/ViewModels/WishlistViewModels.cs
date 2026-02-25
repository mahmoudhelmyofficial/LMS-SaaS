using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج إضافة لقائمة الأمنيات - Add to Wishlist ViewModel
/// </summary>
public class WishlistAddViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID (if course)
    /// </summary>
    public int? CourseId { get; set; }

    /// <summary>
    /// معرف الحزمة - Bundle ID (if bundle)
    /// </summary>
    public int? BundleId { get; set; }

    /// <summary>
    /// معرف مسار التعلم - Learning path ID (if path)
    /// </summary>
    public int? LearningPathId { get; set; }

    /// <summary>
    /// ملاحظات - Optional notes
    /// </summary>
    [MaxLength(500, ErrorMessage = "الملاحظات يجب ألا تتجاوز 500 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// تفعيل إشعار تخفيض السعر - Notify on price drop
    /// </summary>
    [Display(Name = "إشعاري عند انخفاض السعر")]
    public bool NotifyOnPriceDrop { get; set; } = true;
}

/// <summary>
/// نموذج عرض عنصر قائمة الأمنيات - Wishlist Item Display ViewModel
/// </summary>
public class WishlistItemViewModel
{
    /// <summary>
    /// المعرف - Wishlist item ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// نوع العنصر - Item type
    /// </summary>
    [Display(Name = "النوع")]
    public string ItemType { get; set; } = string.Empty; // Course, Bundle, LearningPath

    /// <summary>
    /// معرف العنصر - Item ID
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// عنوان العنصر - Item title
    /// </summary>
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// الوصف المختصر - Short description
    /// </summary>
    public string? ShortDescription { get; set; }

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// اسم المدرس - Instructor name
    /// </summary>
    [Display(Name = "المدرس")]
    public string? InstructorName { get; set; }

    /// <summary>
    /// السعر الحالي - Current price
    /// </summary>
    [Display(Name = "السعر الحالي")]
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// السعر عند الإضافة - Price when added
    /// </summary>
    [Display(Name = "السعر عند الإضافة")]
    public decimal PriceWhenAdded { get; set; }

    /// <summary>
    /// سعر الخصم - Discount price
    /// </summary>
    public decimal? DiscountPrice { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// هل السعر انخفض - Has price dropped
    /// </summary>
    public bool HasPriceDropped => CurrentPrice < PriceWhenAdded;

    /// <summary>
    /// نسبة التوفير - Savings percentage
    /// </summary>
    public decimal SavingsPercentage => PriceWhenAdded > 0 
        ? Math.Round((PriceWhenAdded - CurrentPrice) / PriceWhenAdded * 100, 2) 
        : 0;

    /// <summary>
    /// التقييم - Average rating
    /// </summary>
    [Display(Name = "التقييم")]
    public decimal AverageRating { get; set; }

    /// <summary>
    /// عدد الطلاب - Students count
    /// </summary>
    [Display(Name = "عدد الطلاب")]
    public int StudentsCount { get; set; }

    /// <summary>
    /// المدة - Duration in hours
    /// </summary>
    [Display(Name = "المدة")]
    public int DurationHours { get; set; }

    /// <summary>
    /// المستوى - Course level
    /// </summary>
    [Display(Name = "المستوى")]
    public string? Level { get; set; }

    /// <summary>
    /// ملاحظات - User notes
    /// </summary>
    [Display(Name = "ملاحظاتك")]
    public string? Notes { get; set; }

    /// <summary>
    /// تاريخ الإضافة - Date added
    /// </summary>
    [Display(Name = "تاريخ الإضافة")]
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// إشعار عند تخفيض السعر - Price drop notification enabled
    /// </summary>
    [Display(Name = "إشعار عند تخفيض السعر")]
    public bool NotifyOnPriceDrop { get; set; }

    /// <summary>
    /// هل متاح - Is available for purchase
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// هل مسجل بالفعل - Already enrolled
    /// </summary>
    public bool IsEnrolled { get; set; } = false;
}

/// <summary>
/// نموذج تعديل عنصر قائمة الأمنيات - Edit Wishlist Item ViewModel
/// </summary>
public class WishlistEditViewModel
{
    /// <summary>
    /// المعرف - Wishlist item ID
    /// </summary>
    [Required]
    public int Id { get; set; }

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [MaxLength(500, ErrorMessage = "الملاحظات يجب ألا تتجاوز 500 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// تفعيل إشعار تخفيض السعر - Notify on price drop
    /// </summary>
    [Display(Name = "إشعاري عند انخفاض السعر")]
    public bool NotifyOnPriceDrop { get; set; }
}

/// <summary>
/// نموذج قائمة الأمنيات - Wishlist List ViewModel
/// </summary>
public class WishlistListViewModel
{
    /// <summary>
    /// عناصر قائمة الأمنيات - Wishlist items
    /// </summary>
    public List<WishlistItemViewModel> Items { get; set; } = new();

    /// <summary>
    /// إجمالي العناصر - Total items count
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// إجمالي السعر - Total price of all items
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// إجمالي التوفير - Total savings from price drops
    /// </summary>
    public decimal TotalSavings { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// الصفحة الحالية - Current page
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// إجمالي الصفحات - Total pages
    /// </summary>
    public int TotalPages { get; set; }
}

