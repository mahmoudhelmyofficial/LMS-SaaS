using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء كتاب - Create book view model
/// </summary>
public class BookCreateViewModel
{
    [Required(ErrorMessage = "عنوان الكتاب مطلوب")]
    [MaxLength(300, ErrorMessage = "العنوان يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الكتاب")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "الوصف المختصر يجب ألا يتجاوز 500 حرف")]
    [Display(Name = "وصف مختصر")]
    public string? ShortDescription { get; set; }

    [Display(Name = "الوصف الكامل")]
    public string? Description { get; set; }

    [MaxLength(200, ErrorMessage = "اسم المؤلف يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "اسم المؤلف")]
    public string? Author { get; set; }

    [MaxLength(50)]
    [Display(Name = "رقم ISBN")]
    public string? ISBN { get; set; }

    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";

    [Display(Name = "عدد الصفحات")]
    [Range(1, 10000, ErrorMessage = "عدد الصفحات يجب أن يكون بين 1 و 10000")]
    public int? PageCount { get; set; }

    [Display(Name = "تاريخ النشر")]
    [DataType(DataType.Date)]
    public DateTime? PublicationDate { get; set; }

    [MaxLength(200)]
    [Display(Name = "الناشر")]
    public string? Publisher { get; set; }

    [Required(ErrorMessage = "التصنيف مطلوب")]
    [Display(Name = "التصنيف")]
    public int CategoryId { get; set; }

    [Display(Name = "التصنيف الفرعي")]
    public int? SubCategoryId { get; set; }

    [Display(Name = "صورة الغلاف")]
    public string? CoverImageUrl { get; set; }

    [Required(ErrorMessage = "السعر مطلوب")]
    [Range(0, 99999.99, ErrorMessage = "السعر يجب أن يكون بين 0 و 99999.99")]
    [Display(Name = "السعر")]
    public decimal Price { get; set; }

    [Range(0, 99999.99, ErrorMessage = "سعر الخصم يجب أن يكون بين 0 و 99999.99")]
    [Display(Name = "سعر الخصم")]
    public decimal? DiscountPrice { get; set; }

    [Display(Name = "مجاني")]
    public bool IsFree { get; set; }

    [Display(Name = "نوع الكتاب")]
    public BookType BookType { get; set; } = BookType.EBook;

    [Display(Name = "التنسيقات المتاحة")]
    public BookFormat AvailableFormats { get; set; } = BookFormat.PDF;

    [Display(Name = "متوفر بنسخة ورقية")]
    public bool HasPhysicalCopy { get; set; }

    [Range(0, 99999.99)]
    [Display(Name = "سعر النسخة الورقية")]
    public decimal? PhysicalPrice { get; set; }

    [Range(0, 10000)]
    [Display(Name = "المخزون")]
    public int? PhysicalStock { get; set; }

    [Display(Name = "الدورة المرتبطة")]
    public int? RelatedCourseId { get; set; }

    [Display(Name = "مضمن مع الدورة")]
    public bool IncludedWithCourse { get; set; }

    // Direct file uploads for better UX
    [Display(Name = "رفع ملف PDF مباشرة")]
    public IFormFile? DirectPdfUpload { get; set; }

    [Display(Name = "رفع ملف EPUB مباشرة")]
    public IFormFile? DirectEpubUpload { get; set; }

    [Display(Name = "رفع ملف MOBI مباشرة")]
    public IFormFile? DirectMobiUpload { get; set; }

    [Display(Name = "رفع ملف المعاينة (PDF) مباشرة")]
    public IFormFile? DirectPreviewPdfUpload { get; set; }
}

/// <summary>
/// نموذج تعديل كتاب - Edit book view model
/// </summary>
public class BookEditViewModel : BookCreateViewModel
{
    public int Id { get; set; }

    [MaxLength(50)]
    [Display(Name = "الطبعة")]
    public string? Edition { get; set; }

    [Display(Name = "ملف المعاينة")]
    public string? PreviewPdfUrl { get; set; }

    [Display(Name = "ملف الكتاب (PDF)")]
    public string? FullPdfUrl { get; set; }

    [Display(Name = "ملف EPUB")]
    public string? EpubUrl { get; set; }

    [Display(Name = "ملف MOBI")]
    public string? MobiUrl { get; set; }

    [Display(Name = "ملف صوتي")]
    public string? AudioUrl { get; set; }

    public long FileSizeBytes { get; set; }

    [Display(Name = "تفعيل حماية DRM")]
    public bool EnableDRM { get; set; } = true;

    [Display(Name = "السماح بالطباعة")]
    public bool AllowPrinting { get; set; }

    [Range(1, 100)]
    [Display(Name = "الحد الأقصى للتحميلات")]
    public int? MaxDownloads { get; set; } = 3;

    [Display(Name = "تفعيل العلامة المائية")]
    public bool EnableWatermark { get; set; } = true;

    [MaxLength(200)]
    [Display(Name = "عنوان SEO")]
    public string? MetaTitle { get; set; }

    [MaxLength(500)]
    [Display(Name = "وصف SEO")]
    public string? MetaDescription { get; set; }

    [MaxLength(500)]
    [Display(Name = "كلمات SEO المفتاحية")]
    public string? MetaKeywords { get; set; }

    [Display(Name = "السماح بالمراجعات")]
    public bool AllowReviews { get; set; } = true;

    public BookStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int TotalSales { get; set; }
    public int TotalDownloads { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

/// <summary>
/// نموذج فصل الكتاب - Book chapter view model
/// </summary>
public class BookChapterViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان الفصل مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان الفصل")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "وصف الفصل")]
    public string? Description { get; set; }

    [Display(Name = "رقم الصفحة")]
    [Range(1, 10000)]
    public int? PageNumber { get; set; }

    [Display(Name = "رقم الصفحة الأخيرة")]
    [Range(1, 10000)]
    public int? EndPageNumber { get; set; }

    [Display(Name = "مدة القراءة (دقائق)")]
    [Range(1, 500)]
    public int? ReadingTimeMinutes { get; set; }

    [Display(Name = "قابل للمعاينة")]
    public bool IsPreviewable { get; set; }

    [Display(Name = "الفصل الأب")]
    public int? ParentChapterId { get; set; }

    public int OrderIndex { get; set; }
}

/// <summary>
/// نموذج قائمة الكتب - Book list view model
/// </summary>
public class BookListViewModel
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
    public decimal TotalRevenue { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

/// <summary>
/// نموذج تفاصيل الكتاب - Book details view model
/// </summary>
public class BookDetailsViewModel
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
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public bool IsFree { get; set; }
    public BookType BookType { get; set; }
    public BookFormat AvailableFormats { get; set; }
    public bool HasPhysicalCopy { get; set; }
    public decimal? PhysicalPrice { get; set; }
    public int? PhysicalStock { get; set; }
    public BookStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Statistics
    public int TotalSales { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int ViewCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalEarnings { get; set; }

    // Related
    public string CategoryName { get; set; } = string.Empty;
    public string? SubCategoryName { get; set; }
    public string? RelatedCourseName { get; set; }
    public List<BookChapterViewModel> Chapters { get; set; } = new();

    // Monthly stats
    public int SalesThisMonth { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public int DownloadsThisMonth { get; set; }
}

/// <summary>
/// نموذج رفع ملف الكتاب - Book file upload view model
/// </summary>
public class BookFileUploadViewModel
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    
    [Display(Name = "ملف الكتاب")]
    public IFormFile? BookFile { get; set; }
    
    [Display(Name = "نوع الملف")]
    public BookFormat FileFormat { get; set; } = BookFormat.PDF;
    
    [Display(Name = "ملف المعاينة")]
    public IFormFile? PreviewFile { get; set; }
    
    [Display(Name = "صورة الغلاف")]
    public IFormFile? CoverImage { get; set; }

    // Current files
    public string? CurrentPdfUrl { get; set; }
    public string? CurrentEpubUrl { get; set; }
    public string? CurrentMobiUrl { get; set; }
    public string? CurrentPreviewUrl { get; set; }
    public string? CurrentCoverUrl { get; set; }
}

/// <summary>
/// نموذج إحصائيات الكتاب - Book stats view model
/// </summary>
public class BookStatsViewModel
{
    public int TotalBooks { get; set; }
    public int PublishedBooks { get; set; }
    public int DraftBooks { get; set; }
    public int PendingReviewBooks { get; set; }
    public int TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalEarnings { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int SalesThisMonth { get; set; }
    public decimal RevenueThisMonth { get; set; }
    
    // Chart data
    public List<int> MonthlySales { get; set; } = new();
    public List<decimal> MonthlyRevenue { get; set; } = new();
    public List<string> MonthLabels { get; set; } = new();
}

