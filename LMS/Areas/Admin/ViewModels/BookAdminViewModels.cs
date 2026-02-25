using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج قائمة الكتب للإدارة - Admin book list view model
/// </summary>
public class BookAdminListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Author { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public BookStatus Status { get; set; }
    public BookType BookType { get; set; }
    public int TotalSales { get; set; }
    public int TotalDownloads { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? SubmittedForReviewAt { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
}

/// <summary>
/// نموذج تفاصيل الكتاب للإدارة - Admin book details view model
/// </summary>
public class BookAdminDetailsViewModel
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
    public string? FullPdfUrl { get; set; }
    public string? EpubUrl { get; set; }
    public string? MobiUrl { get; set; }
    public long FileSizeBytes { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public bool IsFree { get; set; }
    public string Currency { get; set; } = "EGP";
    
    // Type & Format
    public BookType BookType { get; set; }
    public BookFormat AvailableFormats { get; set; }
    public bool HasPhysicalCopy { get; set; }
    public decimal? PhysicalPrice { get; set; }
    public int? PhysicalStock { get; set; }
    
    // Status
    public BookStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? SubmittedForReviewAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectedBy { get; set; }
    
    // Protection
    public bool EnableDRM { get; set; }
    public bool AllowPrinting { get; set; }
    public int? MaxDownloads { get; set; }
    public bool EnableWatermark { get; set; }
    
    // Statistics
    public int TotalSales { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int ViewCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PlatformEarnings { get; set; }
    public decimal InstructorEarnings { get; set; }
    
    // Instructor
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorEmail { get; set; }
    public string? InstructorPhone { get; set; }
    public decimal InstructorCommissionRate { get; set; }
    
    // Category
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? SubCategoryName { get; set; }
    
    // Flags
    public bool IsFeatured { get; set; }
    public bool IsBestseller { get; set; }
    public bool AllowReviews { get; set; }
    
    // Lists
    public List<BookChapterAdminViewModel> Chapters { get; set; } = new();
    public List<BookPurchaseAdminViewModel> RecentPurchases { get; set; } = new();
    public List<BookReviewAdminViewModel> RecentReviews { get; set; } = new();
}

/// <summary>
/// نموذج فصل الكتاب للإدارة - Admin book chapter view model
/// </summary>
public class BookChapterAdminViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? EndPageNumber { get; set; }
    public bool IsPreviewable { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// نموذج شراء الكتاب للإدارة - Admin book purchase view model
/// </summary>
public class BookPurchaseAdminViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public DateTime PurchasedAt { get; set; }
    public int DownloadCount { get; set; }
    public bool IsRefunded { get; set; }
    public PhysicalBookStatus? PhysicalStatus { get; set; }
}

/// <summary>
/// نموذج مراجعة الكتاب للإدارة - Admin book review view model
/// </summary>
public class BookReviewAdminViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsApproved { get; set; }
    public bool IsReported { get; set; }
    public int ReportCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج الموافقة/الرفض - Approval action view model
/// </summary>
public class BookApprovalViewModel
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>
/// نموذج إحصائيات الكتب - Book statistics view model
/// </summary>
public class BookStatsAdminViewModel
{
    public int TotalBooks { get; set; }
    public int PublishedBooks { get; set; }
    public int PendingReviewBooks { get; set; }
    public int DraftBooks { get; set; }
    public int RejectedBooks { get; set; }
    public int SuspendedBooks { get; set; }
    
    public int TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PlatformEarnings { get; set; }
    
    public int SalesToday { get; set; }
    public int SalesThisWeek { get; set; }
    public int SalesThisMonth { get; set; }
    
    public decimal RevenueToday { get; set; }
    public decimal RevenueThisWeek { get; set; }
    public decimal RevenueThisMonth { get; set; }
    
    // Top books
    public List<TopBookViewModel> TopSellingBooks { get; set; } = new();
    public List<TopBookViewModel> TopRatedBooks { get; set; } = new();
    
    // Chart data
    public List<int> MonthlySales { get; set; } = new();
    public List<decimal> MonthlyRevenue { get; set; } = new();
    public List<string> MonthLabels { get; set; } = new();
}

/// <summary>
/// نموذج أفضل الكتب - Top book view model
/// </summary>
public class TopBookViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int Sales { get; set; }
    public decimal Revenue { get; set; }
    public decimal Rating { get; set; }
}

/// <summary>
/// نموذج تعديل كتاب من الإدارة - Admin book edit view model
/// </summary>
public class BookAdminEditViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public bool IsBestseller { get; set; }
    public decimal InstructorCommissionRate { get; set; }
    public int CategoryId { get; set; }
    public int? SubCategoryId { get; set; }
}

/// <summary>
/// فلتر قائمة الكتب - Book list filter
/// </summary>
public class BookAdminFilterViewModel
{
    public BookStatus? Status { get; set; }
    public int? CategoryId { get; set; }
    public string? InstructorId { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

