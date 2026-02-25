using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج عرض الكتاب للطالب - Book display view model for students
/// </summary>
public class BookDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Author { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal FinalPrice => DiscountPrice ?? Price;
    public bool IsFree { get; set; }
    public BookType BookType { get; set; }
    public BookFormat AvailableFormats { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalSales { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorImageUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsPurchased { get; set; }
    public bool IsInCart { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsBestseller { get; set; }
    public bool IsNew { get; set; }
    public decimal? DiscountPercentage { get; set; }
}

/// <summary>
/// نموذج تفاصيل الكتاب للطالب - Book details view model for students
/// </summary>
public class BookDetailsStudentViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? ISBN { get; set; }
    public string Language { get; set; } = "ar";
    public int? PageCount { get; set; }
    public DateTime? PublicationDate { get; set; }
    public string? Publisher { get; set; }
    public string? Edition { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? PreviewPdfUrl { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal FinalPrice => DiscountPrice ?? Price;
    public bool IsFree { get; set; }
    public string Currency { get; set; } = "EGP";
    public decimal? DiscountPercentage { get; set; }
    
    // Type & Format
    public BookType BookType { get; set; }
    public BookFormat AvailableFormats { get; set; }
    public bool HasPhysicalCopy { get; set; }
    public decimal? PhysicalPrice { get; set; }
    public int? PhysicalStock { get; set; }
    
    // Statistics
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalSales { get; set; }
    
    // Instructor
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorImageUrl { get; set; }
    public string? InstructorBio { get; set; }
    public int InstructorBookCount { get; set; }
    public int InstructorCourseCount { get; set; }
    
    // Category
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? SubCategoryName { get; set; }
    
    // Related
    public int? RelatedCourseId { get; set; }
    public string? RelatedCourseName { get; set; }
    public bool IncludedWithCourse { get; set; }
    
    // User state
    public bool IsPurchased { get; set; }
    public bool IsInCart { get; set; }
    public bool IsInWishlist { get; set; }
    
    // Flags
    public bool IsFeatured { get; set; }
    public bool IsBestseller { get; set; }
    public bool IsNew { get; set; }
    
    // Chapters (TOC)
    public List<BookChapterDisplayViewModel> Chapters { get; set; } = new();
    
    // Reviews
    public List<BookReviewDisplayViewModel> Reviews { get; set; } = new();
    
    // Related Books
    public List<BookDisplayViewModel> RelatedBooks { get; set; } = new();
    public List<BookDisplayViewModel> InstructorBooks { get; set; } = new();
}

/// <summary>
/// نموذج عرض فصل الكتاب - Book chapter display view model
/// </summary>
public class BookChapterDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PageNumber { get; set; }
    public int? EndPageNumber { get; set; }
    public int? ReadingTimeMinutes { get; set; }
    public bool IsPreviewable { get; set; }
    public int OrderIndex { get; set; }
    public List<BookChapterDisplayViewModel> SubChapters { get; set; } = new();
}

/// <summary>
/// نموذج عرض مراجعة الكتاب - Book review display view model
/// </summary>
public class BookReviewDisplayViewModel
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentImageUrl { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? InstructorResponse { get; set; }
    public DateTime? InstructorRespondedAt { get; set; }
    public int HelpfulVotes { get; set; }
    public bool IsFeatured { get; set; }
}

/// <summary>
/// نموذج مكتبتي - My library view model
/// </summary>
public class MyLibraryViewModel
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Author { get; set; }
    public DateTime PurchasedAt { get; set; }
    public BookPurchaseType PurchaseType { get; set; }
    public BookFormat PurchasedFormat { get; set; }
    public int DownloadCount { get; set; }
    public int MaxDownloads { get; set; }
    public int RemainingDownloads => MaxDownloads - DownloadCount;
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool CanDownload => !IsExpired && DownloadCount < MaxDownloads;
    public DateTime? LastDownloadedAt { get; set; }
    public bool HasReviewed { get; set; }
    public int? UserRating { get; set; }
    
    // Physical book info (if applicable)
    public PhysicalBookStatus? PhysicalStatus { get; set; }
    public string? TrackingNumber { get; set; }
}

/// <summary>
/// نموذج تحميل الكتاب - Book download view model
/// </summary>
public class BookDownloadViewModel
{
    public int PurchaseId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public BookFormat AvailableFormats { get; set; }
    public int DownloadCount { get; set; }
    public int MaxDownloads { get; set; }
    public int RemainingDownloads => MaxDownloads - DownloadCount;
    public bool CanDownload => RemainingDownloads > 0;
    public DateTime? LastDownloadedAt { get; set; }
    public string? DownloadToken { get; set; }
}

/// <summary>
/// نموذج إضافة مراجعة - Add review view model
/// </summary>
public class AddBookReviewViewModel
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// نموذج تصفح الكتب - Browse books view model
/// </summary>
public class BrowseBooksViewModel
{
    public List<BookDisplayViewModel> Books { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    // Filters
    public int? CategoryId { get; set; }
    public int? SubCategoryId { get; set; }
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsFree { get; set; }
    public BookType? BookType { get; set; }
    public BookFormat? Format { get; set; }
    public string? Language { get; set; }
    public int? MinRating { get; set; }
    public string? InstructorId { get; set; }
    public string SortBy { get; set; } = "PublishedAt";
    public bool SortDescending { get; set; } = true;
    
    // Filter options
    public List<CategoryFilterOption> Categories { get; set; } = new();
    public List<LanguageFilterOption> Languages { get; set; } = new();
}

/// <summary>
/// خيار فلتر التصنيف - Category filter option
/// </summary>
public class CategoryFilterOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public List<CategoryFilterOption> SubCategories { get; set; } = new();
}

/// <summary>
/// خيار فلتر اللغة - Language filter option
/// </summary>
public class LanguageFilterOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BookCount { get; set; }
}

