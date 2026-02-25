namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// قائمة الباقات - Bundle list view model
/// </summary>
public class BundleListViewModel
{
    public List<BundleDisplayViewModel> Bundles { get; set; } = new();
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>
/// عرض الباقة - Bundle display view model
/// </summary>
public class BundleDisplayViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public decimal SavingsPercentage { get; set; }
    public int CoursesCount { get; set; }
    public int TotalDurationHours { get; set; }
    public bool IsFeatured { get; set; }
    public int SalesCount { get; set; }
    public int EnrolledCoursesCount { get; set; }
    public bool IsFullyOwned { get; set; }
    public List<BundleCourseInfo> Courses { get; set; } = new();
}

/// <summary>
/// معلومات دورة في الباقة - Bundle course info
/// </summary>
public class BundleCourseInfo
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? InstructorName { get; set; }
    public bool IsEnrolled { get; set; }
}

/// <summary>
/// تفاصيل الباقة - Bundle details view model
/// </summary>
public class BundleDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public decimal SavingsPercentage { get; set; }
    public decimal SavingsAmount { get; set; }
    public int CoursesCount { get; set; }
    public int TotalDurationHours { get; set; }
    public bool IsFeatured { get; set; }
    public int SalesCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxSales { get; set; }
    public int? RemainingSlots { get; set; }
    public bool IsFullyOwned { get; set; }
    public int OwnedCoursesCount { get; set; }
    public string? FormattedPrice { get; set; }
    public string? FormattedOriginalPrice { get; set; }
    public string? FormattedSavings { get; set; }
    public List<BundleCourseDetailInfo> Courses { get; set; } = new();
}

/// <summary>
/// تفاصيل دورة في الباقة - Bundle course detail info
/// </summary>
public class BundleCourseDetailInfo
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? InstructorName { get; set; }
    public string? InstructorAvatar { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int DurationMinutes { get; set; }
    public int LessonsCount { get; set; }
    public string? Level { get; set; }
    public decimal Rating { get; set; }
    public int TotalStudents { get; set; }
    public bool IsEnrolled { get; set; }
}

/// <summary>
/// عنصر باقة في السلة - Bundle cart item
/// </summary>
public class BundleCartItemInfo
{
    public int BundleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal SavingsAmount { get; set; }
    public int CoursesCount { get; set; }
    public List<string> CourseNames { get; set; } = new();
}

