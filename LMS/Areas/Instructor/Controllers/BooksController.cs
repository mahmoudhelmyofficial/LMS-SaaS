using LMS.Areas.Instructor.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Books;
using LMS.Domain.Entities.Content;
using LMS.Domain.Enums;
using LMS.Helpers;
using LMS.Services.Interfaces;
using LMS.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة كتب المدرس - Instructor Books Controller
/// </summary>
public class BooksController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBookService _bookService;
    private readonly ISlugService _slugService;
    private readonly ICurrencyService _currencyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BooksController> _logger;
    private readonly StorageSettings _storageSettings;
    private readonly IWebHostEnvironment _environment;

    public BooksController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBookService bookService,
        ISlugService slugService,
        ICurrencyService currencyService,
        IMemoryCache cache,
        ILogger<BooksController> logger,
        IOptions<StorageSettings> storageSettings,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _bookService = bookService;
        _slugService = slugService;
        _currencyService = currencyService;
        _cache = cache;
        _logger = logger;
        _storageSettings = storageSettings.Value;
        _environment = environment;
    }

    /// <summary>
    /// قائمة كتب المدرس - Instructor's books list
    /// </summary>
    public async Task<IActionResult> Index(BookStatus? status, int page = 1, string? search = null, int? category = null)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var filter = new BookFilterRequest
            {
                Status = status,
                CategoryId = category,
                Search = search,
                Page = page,
                PageSize = 10
            };

            var booksResult = await _bookService.GetInstructorBooksAsync(userId, filter);
            var stats = await _bookService.GetInstructorBookStatsAsync(userId);

            // Map to view models
            var books = booksResult.Items.Select(b => new BookListViewModel
            {
                Id = b.Id,
                Title = b.Title,
                CoverImageUrl = b.CoverImageUrl,
                Author = b.Author,
                Price = b.Price,
                DiscountPrice = b.DiscountPrice,
                Status = b.Status,
                BookType = b.BookType,
                TotalSales = b.TotalSales,
                AverageRating = b.AverageRating,
                TotalReviews = b.TotalReviews,
                CreatedAt = b.CreatedAt,
                PublishedAt = b.PublishedAt,
                CategoryName = b.Category?.Name ?? ""
            }).ToList();

            ViewBag.Stats = stats;
            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Page = page;
            ViewBag.TotalPages = booksResult.TotalPages;
            ViewBag.TotalItems = booksResult.TotalCount;

            await PopulateCategoriesAsync();

            return View(books);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading books for instructor");
            SetErrorMessage("حدث خطأ أثناء تحميل الكتب");
            return View(new List<BookListViewModel>());
        }
    }

    /// <summary>
    /// إنشاء كتاب جديد - Create new book
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        await PopulateCategoriesAsync();
        await PopulateCoursesAsync();
        return View(new BookCreateViewModel());
    }

    /// <summary>
    /// حفظ الكتاب الجديد - Save new book
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB for PDFs and large files
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            await PopulateCoursesAsync();
            return View(model);
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Handle direct file uploads
            string? fullPdfUrl = model.CoverImageUrl; // Will be set from direct upload or existing
            string? previewPdfUrl = null;
            string? epubUrl = null;
            string? mobiUrl = null;

            // Upload PDF file if provided
            if (model.DirectPdfUpload != null)
            {
                var pdfMedia = await SaveBookFileAsync(
                    model.DirectPdfUpload, 
                    $"Book PDF - {model.Title}",
                    $"ملف PDF الكامل للكتاب: {model.Title}",
                    userId);
                fullPdfUrl = pdfMedia.FileUrl;
                _logger.LogInformation("PDF file uploaded directly for book: {FileName}", model.DirectPdfUpload.FileName);
            }

            // Upload Preview PDF if provided
            if (model.DirectPreviewPdfUpload != null)
            {
                var previewMedia = await SaveBookFileAsync(
                    model.DirectPreviewPdfUpload,
                    $"Book Preview - {model.Title}",
                    $"ملف المعاينة للكتاب: {model.Title}",
                    userId);
                previewPdfUrl = previewMedia.FileUrl;
                _logger.LogInformation("Preview PDF file uploaded directly for book: {FileName}", model.DirectPreviewPdfUpload.FileName);
            }

            // Upload EPUB if provided
            if (model.DirectEpubUpload != null)
            {
                var epubMedia = await SaveBookFileAsync(
                    model.DirectEpubUpload,
                    $"Book EPUB - {model.Title}",
                    $"ملف EPUB للكتاب: {model.Title}",
                    userId);
                epubUrl = epubMedia.FileUrl;
                _logger.LogInformation("EPUB file uploaded directly for book: {FileName}", model.DirectEpubUpload.FileName);
            }

            // Upload MOBI if provided
            if (model.DirectMobiUpload != null)
            {
                var mobiMedia = await SaveBookFileAsync(
                    model.DirectMobiUpload,
                    $"Book MOBI - {model.Title}",
                    $"ملف MOBI للكتاب: {model.Title}",
                    userId);
                mobiUrl = mobiMedia.FileUrl;
                _logger.LogInformation("MOBI file uploaded directly for book: {FileName}", model.DirectMobiUpload.FileName);
            }

            var request = new CreateBookRequest
            {
                Title = model.Title,
                ShortDescription = model.ShortDescription,
                Description = model.Description,
                Author = model.Author,
                ISBN = model.ISBN,
                Language = model.Language,
                PageCount = model.PageCount,
                PublicationDate = model.PublicationDate,
                Publisher = model.Publisher,
                CategoryId = model.CategoryId,
                SubCategoryId = model.SubCategoryId,
                CoverImageUrl = model.CoverImageUrl,
                Price = model.Price,
                DiscountPrice = model.DiscountPrice,
                IsFree = model.IsFree,
                BookType = model.BookType,
                AvailableFormats = model.AvailableFormats,
                HasPhysicalCopy = model.HasPhysicalCopy,
                PhysicalPrice = model.PhysicalPrice,
                PhysicalStock = model.PhysicalStock,
                RelatedCourseId = model.RelatedCourseId,
                IncludedWithCourse = model.IncludedWithCourse,
                InstructorId = userId
            };

            var result = await _bookService.CreateBookAsync(request);

            // Update book with uploaded file URLs if book was created successfully
            if (result.IsSuccess && result.Value != null)
            {
                var book = await _context.Books.FindAsync(result.Value.Id);
                if (book != null)
                {
                    if (!string.IsNullOrEmpty(fullPdfUrl) && fullPdfUrl != model.CoverImageUrl)
                        book.FullPdfUrl = fullPdfUrl;
                    if (!string.IsNullOrEmpty(previewPdfUrl))
                        book.PreviewPdfUrl = previewPdfUrl;
                    if (!string.IsNullOrEmpty(epubUrl))
                        book.EpubUrl = epubUrl;
                    if (!string.IsNullOrEmpty(mobiUrl))
                        book.MobiUrl = mobiUrl;
                    
                    await _context.SaveChangesAsync();
                }
            }

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم إنشاء الكتاب بنجاح. يمكنك الآن رفع ملفات الكتاب");
                return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إنشاء الكتاب");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book");
            SetErrorMessage("حدث خطأ أثناء إنشاء الكتاب");
        }

        await PopulateCategoriesAsync();
        await PopulateCoursesAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل الكتاب - Edit book
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;
        var book = await _context.Books
            .Include(b => b.Chapters.OrderBy(c => c.OrderIndex))
            .FirstOrDefaultAsync(b => b.Id == id && b.InstructorId == userId);

        if (book == null)
        {
            return NotFound();
        }

        var viewModel = new BookEditViewModel
        {
            Id = book.Id,
            Title = book.Title,
            ShortDescription = book.ShortDescription,
            Description = book.Description,
            Author = book.Author,
            ISBN = book.ISBN,
            Language = book.Language,
            PageCount = book.PageCount,
            PublicationDate = book.PublicationDate,
            Publisher = book.Publisher,
            Edition = book.Edition,
            CategoryId = book.CategoryId,
            SubCategoryId = book.SubCategoryId,
            CoverImageUrl = book.CoverImageUrl,
            PreviewPdfUrl = book.PreviewPdfUrl,
            FullPdfUrl = book.FullPdfUrl,
            EpubUrl = book.EpubUrl,
            MobiUrl = book.MobiUrl,
            FileSizeBytes = book.FileSizeBytes,
            Price = book.Price,
            DiscountPrice = book.DiscountPrice,
            IsFree = book.IsFree,
            BookType = book.BookType,
            AvailableFormats = book.AvailableFormats,
            HasPhysicalCopy = book.HasPhysicalCopy,
            PhysicalPrice = book.PhysicalPrice,
            PhysicalStock = book.PhysicalStock,
            EnableDRM = book.EnableDRM,
            AllowPrinting = book.AllowPrinting,
            MaxDownloads = book.MaxDownloads,
            EnableWatermark = book.EnableWatermark,
            MetaTitle = book.MetaTitle,
            MetaDescription = book.MetaDescription,
            MetaKeywords = book.MetaKeywords,
            RelatedCourseId = book.RelatedCourseId,
            IncludedWithCourse = book.IncludedWithCourse,
            AllowReviews = book.AllowReviews,
            Status = book.Status,
            CreatedAt = book.CreatedAt,
            PublishedAt = book.PublishedAt,
            TotalSales = book.TotalSales,
            TotalDownloads = book.TotalDownloads,
            AverageRating = book.AverageRating,
            TotalReviews = book.TotalReviews
        };

        ViewBag.Chapters = book.Chapters.Select(c => new BookChapterViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            PageNumber = c.PageNumber,
            EndPageNumber = c.EndPageNumber,
            ReadingTimeMinutes = c.ReadingTimeMinutes,
            IsPreviewable = c.IsPreviewable,
            ParentChapterId = c.ParentChapterId,
            OrderIndex = c.OrderIndex
        }).ToList();

        await PopulateCategoriesAsync();
        await PopulateCoursesAsync();

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الكتاب - Save book edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync();
            await PopulateCoursesAsync();
            return View(model);
        }

        try
        {
            var request = new UpdateBookRequest
            {
                Title = model.Title,
                ShortDescription = model.ShortDescription,
                Description = model.Description,
                Author = model.Author,
                ISBN = model.ISBN,
                Language = model.Language,
                PageCount = model.PageCount,
                PublicationDate = model.PublicationDate,
                Publisher = model.Publisher,
                Edition = model.Edition,
                CategoryId = model.CategoryId,
                SubCategoryId = model.SubCategoryId,
                CoverImageUrl = model.CoverImageUrl,
                PreviewPdfUrl = model.PreviewPdfUrl,
                FullPdfUrl = model.FullPdfUrl,
                EpubUrl = model.EpubUrl,
                MobiUrl = model.MobiUrl,
                FileSizeBytes = model.FileSizeBytes,
                Price = model.Price,
                DiscountPrice = model.DiscountPrice,
                IsFree = model.IsFree,
                BookType = model.BookType,
                AvailableFormats = model.AvailableFormats,
                HasPhysicalCopy = model.HasPhysicalCopy,
                PhysicalPrice = model.PhysicalPrice,
                PhysicalStock = model.PhysicalStock,
                EnableDRM = model.EnableDRM,
                AllowPrinting = model.AllowPrinting,
                MaxDownloads = model.MaxDownloads,
                EnableWatermark = model.EnableWatermark,
                MetaTitle = model.MetaTitle,
                MetaDescription = model.MetaDescription,
                MetaKeywords = model.MetaKeywords,
                RelatedCourseId = model.RelatedCourseId,
                IncludedWithCourse = model.IncludedWithCourse,
                AllowReviews = model.AllowReviews,
                InstructorId = userId!
            };

            var result = await _bookService.UpdateBookAsync(id, request);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم تحديث الكتاب بنجاح");
                return RedirectToAction(nameof(Edit), new { id });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث الكتاب");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating book {BookId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الكتاب");
        }

        await PopulateCategoriesAsync();
        await PopulateCoursesAsync();
        return View(model);
    }

    /// <summary>
    /// تفاصيل الكتاب - Book details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;
        var book = await _context.Books
            .Include(b => b.Category)
            .Include(b => b.SubCategory)
            .Include(b => b.RelatedCourse)
            .Include(b => b.Chapters.OrderBy(c => c.OrderIndex))
            .Include(b => b.Purchases)
            .Include(b => b.Reviews.Where(r => r.IsApproved))
            .FirstOrDefaultAsync(b => b.Id == id && b.InstructorId == userId);

        if (book == null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);

        var earnings = await _context.InstructorEarnings
            .Where(e => e.BookId == id)
            .ToListAsync();

        var viewModel = new BookDetailsViewModel
        {
            Id = book.Id,
            Title = book.Title,
            Slug = book.Slug,
            ShortDescription = book.ShortDescription,
            Description = book.Description,
            Author = book.Author,
            ISBN = book.ISBN,
            Language = book.Language,
            PageCount = book.PageCount,
            PublicationDate = book.PublicationDate,
            Publisher = book.Publisher,
            Edition = book.Edition,
            CoverImageUrl = book.CoverImageUrl,
            Price = book.Price,
            DiscountPrice = book.DiscountPrice,
            IsFree = book.IsFree,
            BookType = book.BookType,
            AvailableFormats = book.AvailableFormats,
            HasPhysicalCopy = book.HasPhysicalCopy,
            PhysicalPrice = book.PhysicalPrice,
            PhysicalStock = book.PhysicalStock,
            Status = book.Status,
            CreatedAt = book.CreatedAt,
            PublishedAt = book.PublishedAt,
            RejectionReason = book.RejectionReason,
            TotalSales = book.TotalSales,
            TotalDownloads = book.TotalDownloads,
            TotalReviews = book.TotalReviews,
            AverageRating = book.AverageRating,
            ViewCount = book.ViewCount,
            TotalRevenue = book.Purchases.Sum(p => p.PaidAmount),
            TotalEarnings = earnings.Sum(e => e.NetAmount),
            CategoryName = book.Category?.Name ?? "",
            SubCategoryName = book.SubCategory?.Name,
            RelatedCourseName = book.RelatedCourse?.Title,
            Chapters = book.Chapters.Select(c => new BookChapterViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                PageNumber = c.PageNumber,
                EndPageNumber = c.EndPageNumber,
                ReadingTimeMinutes = c.ReadingTimeMinutes,
                IsPreviewable = c.IsPreviewable,
                OrderIndex = c.OrderIndex
            }).ToList(),
            SalesThisMonth = book.Purchases.Count(p => p.PurchasedAt >= firstDayOfMonth),
            RevenueThisMonth = book.Purchases
                .Where(p => p.PurchasedAt >= firstDayOfMonth)
                .Sum(p => p.PaidAmount),
            DownloadsThisMonth = book.Purchases
                .Where(p => p.LastDownloadedAt >= firstDayOfMonth)
                .Sum(p => p.DownloadCount)
        };

        // Recent purchases
        ViewBag.RecentPurchases = book.Purchases
            .OrderByDescending(p => p.PurchasedAt)
            .Take(10)
            .ToList();

        // Recent reviews
        ViewBag.RecentReviews = book.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToList();

        return View(viewModel);
    }

    /// <summary>
    /// إرسال للمراجعة - Submit for review
    /// Enterprise-level implementation with comprehensive validation and error handling
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("SubmitForReview: User ID is null for book {BookId}", id);
                SetErrorMessage("يجب تسجيل الدخول للمتابعة");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Pre-validation check for better UX - validate before attempting submission
            var (canPublish, validationErrors) = await _bookService.ValidateForPublishingAsync(id);
            if (!canPublish)
            {
                var errorMessage = "لا يمكن إرسال الكتاب للمراجعة. يرجى إكمال المتطلبات التالية:\n\n" + 
                                  string.Join("\n", validationErrors.Select(e => "• " + e));
                
                _logger.LogInformation("SubmitForReview: Validation failed for book {BookId} by user {UserId}. Errors: {Errors}", 
                    id, userId, string.Join(", ", validationErrors));
                
                SetErrorMessage(errorMessage);
                return RedirectToAction(nameof(Details), new { id });
            }

            // Attempt to submit for review
            var result = await _bookService.SubmitForReviewAsync(id, userId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Book {BookId} successfully submitted for review by instructor {InstructorId}", 
                    id, userId);
                SetSuccessMessage("تم إرسال الكتاب للمراجعة بنجاح. سيتم مراجعته من قبل الإدارة قريباً.");
            }
            else
            {
                _logger.LogWarning("SubmitForReview failed for book {BookId} by user {UserId}. Error: {Error}", 
                    id, userId, result.Error);
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء إرسال الكتاب للمراجعة. يرجى المحاولة مرة أخرى.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SubmitForReview for book {BookId}", id);
            SetErrorMessage("حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إلغاء النشر - Unpublish book
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _bookService.UnpublishBookAsync(id, userId!);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم إلغاء نشر الكتاب");
        }
        else
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إلغاء النشر");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف الكتاب - Delete book
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _bookService.DeleteBookAsync(id, userId!);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم حذف الكتاب بنجاح");
            return RedirectToAction(nameof(Index));
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف الكتاب");
        return RedirectToAction(nameof(Details), new { id });
    }

    #region Chapters

    /// <summary>
    /// إضافة فصل - Add chapter
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChapter(int bookId, BookChapterViewModel model)
    {
        var userId = _currentUserService.UserId;

        var request = new BookChapterRequest
        {
            Title = model.Title,
            Description = model.Description,
            PageNumber = model.PageNumber,
            EndPageNumber = model.EndPageNumber,
            ReadingTimeMinutes = model.ReadingTimeMinutes,
            IsPreviewable = model.IsPreviewable,
            ParentChapterId = model.ParentChapterId
        };

        var result = await _bookService.AddChapterAsync(bookId, userId!, request);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم إضافة الفصل بنجاح");
        }
        else
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إضافة الفصل");
        }

        return RedirectToAction(nameof(Edit), new { id = bookId });
    }

    /// <summary>
    /// حذف فصل - Delete chapter
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChapter(int bookId, int chapterId)
    {
        var userId = _currentUserService.UserId;
        var result = await _bookService.DeleteChapterAsync(chapterId, userId!);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم حذف الفصل بنجاح");
        }
        else
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف الفصل");
        }

        return RedirectToAction(nameof(Edit), new { id = bookId });
    }

    /// <summary>
    /// إعادة ترتيب الفصول - Reorder chapters
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderChapters(int bookId, [FromBody] List<int> chapterIds)
    {
        var userId = _currentUserService.UserId;
        var result = await _bookService.ReorderChaptersAsync(bookId, userId!, chapterIds);

        return Json(new { success = result.IsSuccess, message = result.Error });
    }

    #endregion

    #region Reviews

    /// <summary>
    /// الرد على مراجعة - Respond to review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondToReview(int bookId, int reviewId, string response)
    {
        var userId = _currentUserService.UserId;
        var result = await _bookService.RespondToReviewAsync(reviewId, userId!, response);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم إضافة الرد بنجاح");
        }
        else
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إضافة الرد");
        }

        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    #endregion

    #region Category Management (On-the-fly)

    /// <summary>
    /// الحصول على التصنيفات الفرعية - Get Subcategories (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubcategories(int categoryId)
    {
        var subcategories = await _context.Categories
            .Where(c => c.ParentCategoryId == categoryId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name })
            .ToListAsync();

        return Json(subcategories);
    }

    /// <summary>
    /// إضافة تصنيف جديد - Add new category (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory([FromBody] AddCategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف مطلوب" });
        }

        try
        {
            // Check if category with same name exists
            var exists = await _context.Categories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentCategoryId == null && !c.IsDeleted);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف بنفس الاسم" });
            }

            // Generate slug
            var slug = _slugService.GenerateSlug(model.Name);
            var slugExists = await _context.Categories.AnyAsync(c => c.Slug == slug);
            if (slugExists)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
            }

            var category = new LMS.Domain.Entities.Courses.Category
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                IconClass = model.Icon ?? "book",
                ParentCategoryId = null,
                IsActive = true,
                DisplayOrder = await _context.Categories.Where(c => c.ParentCategoryId == null).CountAsync() + 1
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} created by instructor {InstructorId} from Books",
                category.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف بنجاح",
                category = new
                {
                    id = category.Id,
                    name = category.Name,
                    slug = category.Slug
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category by instructor {InstructorId} from Books", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف" });
        }
    }

    /// <summary>
    /// إضافة تصنيف فرعي جديد - Add new subcategory (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubcategory([FromBody] AddSubcategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (model.ParentCategoryId <= 0)
        {
            return Json(new { success = false, message = "التصنيف الرئيسي مطلوب" });
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف الفرعي مطلوب" });
        }

        try
        {
            // Verify parent category exists
            var parentExists = await _context.Categories
                .AnyAsync(c => c.Id == model.ParentCategoryId && !c.IsDeleted);

            if (!parentExists)
            {
                return Json(new { success = false, message = "التصنيف الرئيسي غير موجود" });
            }

            // Check if subcategory with same name exists under parent
            var exists = await _context.Categories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentCategoryId == model.ParentCategoryId && !c.IsDeleted);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف فرعي بنفس الاسم" });
            }

            // Generate slug
            var slug = _slugService.GenerateSlug(model.Name);
            var slugExists = await _context.Categories.AnyAsync(c => c.Slug == slug);
            if (slugExists)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
            }

            var subcategory = new LMS.Domain.Entities.Courses.Category
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                ParentCategoryId = model.ParentCategoryId,
                IsActive = true,
                DisplayOrder = await _context.Categories.Where(c => c.ParentCategoryId == model.ParentCategoryId).CountAsync() + 1
            };

            _context.Categories.Add(subcategory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subcategory {SubcategoryId} created under {ParentId} by instructor {InstructorId} from Books",
                subcategory.Id, model.ParentCategoryId, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف الفرعي بنجاح",
                subcategory = new
                {
                    id = subcategory.Id,
                    name = subcategory.Name,
                    slug = subcategory.Slug,
                    parentId = subcategory.ParentCategoryId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding subcategory by instructor {InstructorId} from Books", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف الفرعي" });
        }
    }

    #endregion

    #region Private Helpers

    private async Task PopulateCategoriesAsync()
    {
        var categories = await _context.Categories
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name");

        var subCategories = await _context.Categories
            .Where(c => c.ParentCategoryId != null && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.SubCategories = subCategories;
    }

    private async Task PopulateCoursesAsync()
    {
        var userId = _currentUserService.UserId;
        var courses = await _context.Courses
            .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
            .OrderBy(c => c.Title)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.Courses = new SelectList(courses, "Id", "Title");
    }

    /// <summary>
    /// حفظ ملف كتاب - Save book file (similar to MediaLibraryController.SaveUploadedFileAsync)
    /// </summary>
    private async Task<Media> SaveBookFileAsync(IFormFile file, string title, string? description, string userId)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mediaType = DetectMediaType(extension);
        
        // MIME type map for book files
        var mimeTypeMap = new Dictionary<string, string>
        {
            { ".pdf", "application/pdf" },
            { ".epub", "application/epub+zip" },
            { ".mobi", "application/x-mobipocket-ebook" }
        };
        var mimeType = mimeTypeMap.GetValueOrDefault(extension, "application/octet-stream");

        // Validate file size - use larger limit for PDFs and documents
        long maxSizeBytes;
        if (extension == ".pdf" || mediaType == "Document")
        {
            maxSizeBytes = _storageSettings.MaxDocumentSizeMB * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                throw new InvalidOperationException(
                    $"حجم ملف {extension.ToUpperInvariant()} يتجاوز الحد المسموح ({_storageSettings.MaxDocumentSizeMB} MB). " +
                    $"حجم الملف الحالي: {file.Length / (1024.0 * 1024.0):F2} MB");
            }
        }
        else
        {
            maxSizeBytes = _storageSettings.MaxFileSizeMB * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                throw new InvalidOperationException(
                    $"حجم الملف يتجاوز الحد المسموح ({_storageSettings.MaxFileSizeMB} MB). " +
                    $"حجم الملف الحالي: {file.Length / (1024.0 * 1024.0):F2} MB");
            }
        }

        // Validate file extension
        var allowedExtensions = new[] { ".pdf", ".epub", ".mobi" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"نوع الملف {extension.ToUpperInvariant()} غير مدعوم. " +
                $"الأنواع المدعومة: {string.Join(", ", allowedExtensions)}");
        }

        // Create storage folder
        var folderPath = Path.Combine("uploads", "books", userId, DateTime.UtcNow.ToString("yyyy/MM"));
        var absoluteFolderPath = Path.Combine(_environment.WebRootPath, folderPath);
        Directory.CreateDirectory(absoluteFolderPath);

        // Generate unique file name
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absoluteFilePath = Path.Combine(absoluteFolderPath, storedFileName);
        var relativeFilePath = $"/{folderPath}/{storedFileName}".Replace("\\", "/");

        // Save file
        using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Create Media entity
        var media = new Media
        {
            Title = title,
            Description = description,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FileUrl = relativeFilePath,
            ThumbnailUrl = null,
            Extension = extension,
            MimeType = mimeType,
            MediaType = mediaType,
            FileSize = file.Length,
            StorageProvider = "Local",
            StoragePath = relativeFilePath,
            Folder = folderPath,
            UploadedById = userId,
            IsPublic = false, // Book files are not public by default
            Purpose = "BookFile"
        };

        _context.Media.Add(media);
        await _context.SaveChangesAsync();

        return media;
    }

    private static string DetectMediaType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "Document",
            ".epub" => "Document",
            ".mobi" => "Document",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" => "Image",
            ".mp4" or ".webm" or ".mov" or ".avi" or ".mkv" or ".wmv" => "Video",
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" or ".aac" => "Audio",
            ".doc" or ".docx" or ".ppt" or ".pptx" or ".xls" or ".xlsx" or ".txt" or ".rtf" => "Document",
            _ => "Other"
        };
    }

    #endregion
}

