using LMS.Areas.Student.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// تصفح وشراء الكتب - Student Books Controller
/// </summary>
public class BooksController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBookService _bookService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBookService bookService,
        IHttpClientFactory httpClientFactory,
        ILogger<BooksController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _bookService = bookService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// تصفح الكتب - Browse books
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        int? category,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool? free,
        BookType? type,
        BookFormat? format,
        string? language,
        int? rating,
        string? instructorId,
        string sort = "PublishedAt",
        bool desc = true,
        int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;

            var filter = new BookBrowseFilter
            {
                CategoryId = category,
                Search = search,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                IsFree = free,
                BookType = type,
                Format = format,
                Language = language,
                MinRating = rating,
                InstructorId = instructorId,
                SortBy = sort,
                SortDescending = desc,
                Page = page,
                PageSize = 12
            };

            var result = await _bookService.GetPublishedBooksAsync(filter);

            // Get user's purchased and cart books
            var purchasedBookIds = new HashSet<int>();
            var cartBookIds = new HashSet<int>();

            if (!string.IsNullOrEmpty(userId))
            {
                purchasedBookIds = (await _context.BookPurchases
                    .Where(p => p.StudentId == userId && p.IsActive && !p.IsRefunded)
                    .Select(p => p.BookId)
                    .ToListAsync()).ToHashSet();

                cartBookIds = (await _context.CartItems
                    .Where(c => c.UserId == userId && c.BookId.HasValue)
                    .Select(c => c.BookId!.Value)
                    .ToListAsync()).ToHashSet();
            }

            var viewModel = new BrowseBooksViewModel
            {
                Books = result.Items.Select(b => new BookDisplayViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Slug = b.Slug,
                    ShortDescription = b.ShortDescription,
                    CoverImageUrl = b.CoverImageUrl,
                    Author = b.Author,
                    Price = b.Price,
                    DiscountPrice = b.DiscountPrice,
                    IsFree = b.IsFree,
                    BookType = b.BookType,
                    AvailableFormats = b.AvailableFormats,
                    AverageRating = b.AverageRating,
                    TotalReviews = b.TotalReviews,
                    TotalSales = b.TotalSales,
                    InstructorName = b.Instructor?.FullName ?? "",
                    InstructorImageUrl = b.Instructor?.ProfileImageUrl,
                    CategoryName = b.Category?.Name ?? "",
                    IsPurchased = purchasedBookIds.Contains(b.Id),
                    IsInCart = cartBookIds.Contains(b.Id),
                    IsFeatured = b.IsFeatured,
                    IsBestseller = b.IsBestseller,
                    IsNew = b.IsNew,
                    DiscountPercentage = b.DiscountPercentage
                }).ToList(),
                TotalCount = result.TotalCount,
                Page = page,
                PageSize = 12,
                CategoryId = category,
                Search = search,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                IsFree = free,
                BookType = type,
                Format = format,
                Language = language,
                MinRating = rating,
                SortBy = sort,
                SortDescending = desc,
                InstructorId = instructorId
            };

            // Load categories for filter
            viewModel.Categories = await GetCategoryFiltersAsync();
            viewModel.Languages = await GetLanguageFiltersAsync();

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing books");
            SetErrorMessage("حدث خطأ أثناء تحميل الكتب");
            var errorModel = new BrowseBooksViewModel();
            errorModel.Categories = await GetCategoryFiltersAsync();
            errorModel.Languages = await GetLanguageFiltersAsync();
            return View(errorModel);
        }
    }

    /// <summary>
    /// تفاصيل الكتاب بالمعرف - Book details by ID
    /// </summary>
    [AllowAnonymous]
    [Route("[area]/[controller]/[action]/{id:int}")]
    public async Task<IActionResult> DetailsById(int id)
    {
        try
        {
            var book = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.SubCategory)
                .Include(b => b.Instructor)
                .Include(b => b.RelatedCourse)
                .Include(b => b.Chapters.OrderBy(c => c.OrderIndex))
                .FirstOrDefaultAsync(b => b.Id == id && b.Status == BookStatus.Published);

            if (book == null)
            {
                SetErrorMessage("الكتاب غير موجود أو غير متاح");
                return RedirectToAction(nameof(Index));
            }

            // Redirect to slug-based URL for SEO
            return RedirectToAction(nameof(Details), new { slug = book.Slug });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading book details for ID {BookId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الكتاب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تفاصيل الكتاب - Book details
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Details(string slug)
    {
        try
        {
            // Try to find by slug first
            var book = await _bookService.GetBookBySlugAsync(slug);
            
            // If not found by slug, try parsing as ID (fallback for old links)
            if (book == null && int.TryParse(slug, out var bookId))
            {
                book = await _context.Books
                    .Include(b => b.Category)
                    .Include(b => b.SubCategory)
                    .Include(b => b.Instructor)
                    .Include(b => b.RelatedCourse)
                    .Include(b => b.Chapters.OrderBy(c => c.OrderIndex))
                    .FirstOrDefaultAsync(b => b.Id == bookId && b.Status == BookStatus.Published);
            }
            
            if (book == null)
            {
                SetErrorMessage("الكتاب غير موجود أو غير متاح");
                return RedirectToAction(nameof(Index));
            }

            // Record view
            await _bookService.RecordViewAsync(book.Id);

            var userId = _currentUserService.UserId;
            var isPurchased = false;
            var isInCart = false;

            if (!string.IsNullOrEmpty(userId))
            {
                isPurchased = await _bookService.HasStudentPurchasedAsync(userId, book.Id);
                isInCart = await _context.CartItems
                    .AnyAsync(c => c.UserId == userId && c.BookId == book.Id);
            }

            // Get instructor info
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(p => p.UserId == book.InstructorId);

            var instructorBookCount = await _context.Books
                .CountAsync(b => b.InstructorId == book.InstructorId && b.Status == BookStatus.Published);

            var instructorCourseCount = await _context.Courses
                .CountAsync(c => c.InstructorId == book.InstructorId && c.Status == CourseStatus.Published);

            // Get reviews
            var reviews = await _bookService.GetBookReviewsAsync(book.Id, 1, 5);

            // Get related books
            var relatedBooks = await _context.Books
                .Include(b => b.Instructor)
                .Where(b => b.CategoryId == book.CategoryId && 
                           b.Id != book.Id && 
                           b.Status == BookStatus.Published)
                .Take(4)
                .ToListAsync();

            // Get more books by instructor
            var instructorBooks = await _context.Books
                .Include(b => b.Category)
                .Where(b => b.InstructorId == book.InstructorId && 
                           b.Id != book.Id && 
                           b.Status == BookStatus.Published)
                .Take(4)
                .ToListAsync();

            var viewModel = new BookDetailsStudentViewModel
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
                PreviewPdfUrl = book.PreviewPdfUrl,
                Price = book.Price,
                DiscountPrice = book.DiscountPrice,
                IsFree = book.IsFree,
                Currency = book.Currency,
                DiscountPercentage = book.DiscountPercentage,
                BookType = book.BookType,
                AvailableFormats = book.AvailableFormats,
                HasPhysicalCopy = book.HasPhysicalCopy,
                PhysicalPrice = book.PhysicalPrice,
                PhysicalStock = book.PhysicalStock,
                AverageRating = book.AverageRating,
                TotalReviews = book.TotalReviews,
                TotalSales = book.TotalSales,
                InstructorId = book.InstructorId,
                InstructorName = book.Instructor?.FullName ?? "",
                InstructorImageUrl = book.Instructor?.ProfileImageUrl,
                InstructorBio = instructorProfile?.Bio,
                InstructorBookCount = instructorBookCount,
                InstructorCourseCount = instructorCourseCount,
                CategoryId = book.CategoryId,
                CategoryName = book.Category?.Name ?? "",
                SubCategoryName = book.SubCategory?.Name,
                RelatedCourseId = book.RelatedCourseId,
                RelatedCourseName = book.RelatedCourse?.Title,
                IncludedWithCourse = book.IncludedWithCourse,
                IsPurchased = isPurchased,
                IsInCart = isInCart,
                IsFeatured = book.IsFeatured,
                IsBestseller = book.IsBestseller,
                IsNew = book.IsNew,
                Chapters = book.Chapters.Select(c => new BookChapterDisplayViewModel
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
                Reviews = reviews.Items.Select(r => new BookReviewDisplayViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Title = r.Title,
                    Comment = r.Comment,
                    StudentName = r.Student?.FullName ?? "مستخدم",
                    StudentImageUrl = r.Student?.ProfileImageUrl,
                    IsVerifiedPurchase = r.IsVerifiedPurchase,
                    CreatedAt = r.CreatedAt,
                    InstructorResponse = r.InstructorResponse,
                    InstructorRespondedAt = r.InstructorRespondedAt,
                    HelpfulVotes = r.HelpfulVotes,
                    IsFeatured = r.IsFeatured
                }).ToList(),
                RelatedBooks = relatedBooks.Select(b => new BookDisplayViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Slug = b.Slug,
                    CoverImageUrl = b.CoverImageUrl,
                    Author = b.Author,
                    Price = b.Price,
                    DiscountPrice = b.DiscountPrice,
                    IsFree = b.IsFree,
                    AverageRating = b.AverageRating,
                    TotalReviews = b.TotalReviews,
                    InstructorName = b.Instructor?.FullName ?? ""
                }).ToList(),
                InstructorBooks = instructorBooks.Select(b => new BookDisplayViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Slug = b.Slug,
                    CoverImageUrl = b.CoverImageUrl,
                    Author = b.Author,
                    Price = b.Price,
                    DiscountPrice = b.DiscountPrice,
                    IsFree = b.IsFree,
                    AverageRating = b.AverageRating,
                    TotalReviews = b.TotalReviews,
                    CategoryName = b.Category?.Name ?? ""
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading book details");
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الكتاب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// مكتبتي - My Library
    /// </summary>
    [Authorize]
    public async Task<IActionResult> MyLibrary()
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            var purchases = (await _bookService.GetStudentBooksAsync(userId))
                .Where(p => p.Book != null)
                .ToList();

            // Check which books user has reviewed
            var reviewedBookIds = await _context.BookReviews
                .Where(r => r.StudentId == userId)
                .Select(r => r.BookId)
                .ToListAsync();

            var userRatings = await _context.BookReviews
                .Where(r => r.StudentId == userId)
                .ToDictionaryAsync(r => r.BookId, r => r.Rating);

            var viewModel = purchases.Select(p => new MyLibraryViewModel
            {
                Id = p.Id,
                BookId = p.BookId,
                BookTitle = p.Book.Title,
                CoverImageUrl = p.Book.CoverImageUrl,
                Author = p.Book.Author,
                PurchasedAt = p.PurchasedAt,
                PurchaseType = p.PurchaseType,
                PurchasedFormat = p.PurchasedFormat,
                DownloadCount = p.DownloadCount,
                MaxDownloads = p.MaxDownloads,
                ExpiresAt = p.ExpiresAt,
                LastDownloadedAt = p.LastDownloadedAt,
                HasReviewed = reviewedBookIds.Contains(p.BookId),
                UserRating = userRatings.ContainsKey(p.BookId) ? userRatings[p.BookId] : null,
                PhysicalStatus = p.PhysicalStatus,
                TrackingNumber = p.TrackingNumber
            }).ToList();

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user library");
            SetErrorMessage("حدث خطأ أثناء تحميل المكتبة");
            return View(new List<MyLibraryViewModel>());
        }
    }

    /// <summary>
    /// الحصول على كتاب مجاني - Get free book (direct download without cart/checkout)
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> FreeDownload(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var book = await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id && b.Status == BookStatus.Published);
        if (book == null)
        {
            SetErrorMessage("الكتاب غير موجود أو غير متاح");
            return RedirectToAction(nameof(Index));
        }

        var effectivePrice = book.DiscountPrice ?? book.Price;
        if (effectivePrice > 0) // free when IsFree or effective price is zero
        {
            SetErrorMessage("هذا الكتاب ليس مجانياً");
            return RedirectToAction(nameof(Details), new { slug = book.Slug });
        }

        if (await _bookService.HasStudentPurchasedAsync(userId, book.Id))
        {
            return RedirectToAction(nameof(Download), new { id = book.Id });
        }

        var result = await _bookService.PurchaseBookAsync(userId, book.Id, null);
        if (!result.IsSuccess)
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء الحصول على الكتاب");
            return RedirectToAction(nameof(Details), new { slug = book.Slug });
        }

        SetSuccessMessage("تم إضافة الكتاب إلى مكتبتك. يمكنك تحميله الآن.");
        return RedirectToAction(nameof(Download), new { id = book.Id });
    }

    /// <summary>
    /// تحميل الكتاب - Download book
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            var purchase = await _bookService.GetStudentBookPurchaseAsync(userId, id);

            if (purchase == null)
            {
                SetErrorMessage("لم تقم بشراء هذا الكتاب");
                return RedirectToAction(nameof(MyLibrary));
            }

            var viewModel = new BookDownloadViewModel
            {
                PurchaseId = purchase.Id,
                BookId = purchase.BookId,
                BookTitle = purchase.Book.Title,
                CoverImageUrl = purchase.Book.CoverImageUrl,
                AvailableFormats = purchase.PurchasedFormat,
                DownloadCount = purchase.DownloadCount,
                MaxDownloads = purchase.MaxDownloads,
                LastDownloadedAt = purchase.LastDownloadedAt
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing download for book {BookId}", id);
            SetErrorMessage("حدث خطأ أثناء تحضير التحميل");
            return RedirectToAction(nameof(MyLibrary));
        }
    }

    /// <summary>
    /// تنفيذ التحميل - Execute download
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteDownload(int purchaseId, BookFormat format)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(MyLibrary)) });
            }
            
            var purchase = await _bookService.GetPurchaseAsync(purchaseId);

            if (purchase == null || purchase.StudentId != userId)
            {
                return NotFound();
            }

            if ((purchase.PurchasedFormat & format) == 0)
            {
                SetErrorMessage("التنسيق المطلوب غير مشمول في شرائك");
                return RedirectToAction(nameof(Download), new { id = purchase.BookId });
            }

            // Generate download token (counter is recorded only after successful file delivery in GetFile)
            var tokenResult = await _bookService.GenerateDownloadTokenAsync(purchaseId, format);
            if (!tokenResult.IsSuccess)
            {
                SetErrorMessage(tokenResult.Error ?? "لا يمكن التحميل");
                return RedirectToAction(nameof(Download), new { id = purchase.BookId });
            }

            return RedirectToAction(nameof(GetFile), new { token = tokenResult.Value, format = (int)format });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing download for purchase {PurchaseId}", purchaseId);
            SetErrorMessage("حدث خطأ أثناء التحميل");
            return RedirectToAction(nameof(MyLibrary));
        }
    }

    /// <summary>
    /// تحميل الملف بالرمز - Get file by token (streams file; counter incremented only after successful delivery)
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetFile(string token, int format)
    {
        try
        {
            if (!Enum.IsDefined(typeof(BookFormat), format))
            {
                SetErrorMessage("تنسيق التحميل غير صالح");
                return RedirectToAction(nameof(MyLibrary));
            }

            var bookFormat = (BookFormat)format;
            var fileInfoResult = await _bookService.GetDownloadFileInfoByTokenAsync(token, bookFormat);
            if (!fileInfoResult.IsSuccess)
            {
                SetErrorMessage(fileInfoResult.Error ?? "الملف غير متاح");
                return RedirectToAction(nameof(MyLibrary));
            }

            var (fileUrl, contentType, fileName, purchaseId) = fileInfoResult.Value;

            if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                var baseUri = new Uri(Request.Scheme + "://" + Request.Host.Value + "/");
                if (!Uri.TryCreate(baseUri, fileUrl.TrimStart('/'), out uri))
                {
                    SetErrorMessage("رابط الملف غير صالح");
                    return RedirectToAction(nameof(MyLibrary));
                }
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            var clearResult = await _bookService.ClearDownloadTokenAsync(purchaseId);
            if (!clearResult.IsSuccess)
            {
                await stream.DisposeAsync();
                response.Dispose();
                _logger.LogWarning("Could not clear download token for purchase {PurchaseId}, aborting delivery", purchaseId);
                SetErrorMessage("حدث خطأ أثناء تحضير التحميل. يرجى المحاولة لاحقاً.");
                return RedirectToAction(nameof(MyLibrary));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();

            var requestServices = HttpContext.RequestServices;
            Response.OnCompleted(async () =>
            {
                try
                {
                    using (var scope = requestServices.CreateScope())
                    {
                        var bookService = scope.ServiceProvider.GetRequiredService<IBookService>();
                        await bookService.RecordDownloadAsync(purchaseId, ipAddress, userAgent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording download after delivery for purchase {PurchaseId}", purchaseId);
                }
            });

            return File(stream, contentType, fileName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch book file for token");
            SetErrorMessage("تعذر تحميل الملف. يرجى المحاولة لاحقاً.");
            return RedirectToAction(nameof(MyLibrary));
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout or cancellation while fetching book file for token");
            SetErrorMessage("انتهت مهلة التحميل. يرجى المحاولة لاحقاً.");
            return RedirectToAction(nameof(MyLibrary));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download cancelled for token");
            SetErrorMessage("تم إلغاء التحميل.");
            return RedirectToAction(nameof(MyLibrary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file for token");
            SetErrorMessage("حدث خطأ أثناء تحميل الملف");
            return RedirectToAction(nameof(MyLibrary));
        }
    }

    /// <summary>
    /// إضافة مراجعة - Add review
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(AddBookReviewViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            if (model.BookId <= 0 || model.Rating < 1 || model.Rating > 5)
            {
                SetErrorMessage("بيانات التقييم غير صالحة");
                return RedirectToAction(nameof(MyLibrary));
            }
            
            var result = await _bookService.AddReviewAsync(userId, model.BookId, model.Rating, model.Comment);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم إضافة تقييمك بنجاح");
            }
            else
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء إضافة التقييم");
            }

            return RedirectToAction(nameof(MyLibrary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding review");
            SetErrorMessage("حدث خطأ أثناء إضافة التقييم");
            return RedirectToAction(nameof(MyLibrary));
        }
    }

    /// <summary>
    /// معاينة الكتاب - Preview book (supports both ID and slug; same resolution as Details)
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Preview(int? id, string? slug)
    {
        try
        {
            Domain.Entities.Books.Book? book = null;

            // Same resolution as Details: prefer slug via GetBookBySlugAsync (published only)
            if (!string.IsNullOrEmpty(slug))
            {
                book = await _bookService.GetBookBySlugAsync(slug);
            }

            if (book == null && id.HasValue)
            {
                book = await _context.Books
                    .FirstOrDefaultAsync(b => b.Id == id.Value && b.Status == BookStatus.Published);
            }
            
            if (book == null)
            {
                _logger.LogWarning("Book preview not found for id={Id}, slug={Slug}", id, slug);
                return NotFound();
            }

            if (string.IsNullOrEmpty(book.PreviewPdfUrl))
            {
                _logger.LogWarning("Book {BookId} has no preview PDF configured (PreviewPdfUrl empty). Set it in admin or implement IBookFileService and stream via GetPreviewFileAsync for file-based previews.", book.Id);
                return NotFound();
            }

            return Redirect(book.PreviewPdfUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading book preview for ID {BookId} or slug {Slug}", id, slug);
            return NotFound();
        }
    }

    /// <summary>
    /// الحصول على بيانات الكتاب عبر AJAX - Get book data via AJAX
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetBook(int? id, string? slug)
    {
        try
        {
            Domain.Entities.Books.Book? book = null;
            
            if (id.HasValue)
            {
                book = await _context.Books
                    .Include(b => b.Category)
                    .Include(b => b.Instructor)
                    .FirstOrDefaultAsync(b => b.Id == id.Value && b.Status == BookStatus.Published);
            }
            else if (!string.IsNullOrEmpty(slug))
            {
                book = await _context.Books
                    .Include(b => b.Category)
                    .Include(b => b.Instructor)
                    .FirstOrDefaultAsync(b => b.Slug == slug && b.Status == BookStatus.Published);
            }
            
            if (book == null)
            {
                return Json(new { success = false, message = "الكتاب غير موجود" });
            }

            var userId = _currentUserService.UserId;
            var isPurchased = false;
            var isInCart = false;

            if (!string.IsNullOrEmpty(userId))
            {
                isPurchased = await _context.BookPurchases
                    .AnyAsync(p => p.StudentId == userId && p.BookId == book.Id && p.IsActive && !p.IsRefunded);
                isInCart = await _context.CartItems
                    .AnyAsync(c => c.UserId == userId && c.BookId == book.Id);
            }

            return Json(new
            {
                success = true,
                book = new
                {
                    book.Id,
                    book.Title,
                    book.Slug,
                    book.ShortDescription,
                    book.CoverImageUrl,
                    book.Author,
                    book.Price,
                    book.DiscountPrice,
                    book.IsFree,
                    book.AverageRating,
                    book.TotalReviews,
                    CategoryName = book.Category?.Name ?? "",
                    InstructorName = book.Instructor?.FullName ?? "",
                    isPurchased,
                    isInCart,
                    hasPreview = !string.IsNullOrEmpty(book.PreviewPdfUrl)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting book data for ID {BookId} or slug {Slug}", id, slug);
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل بيانات الكتاب" });
        }
    }

    /// <summary>
    /// معاينة الكتاب عبر AJAX - Preview book via AJAX
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewBook(int? id, string? slug)
    {
        try
        {
            Domain.Entities.Books.Book? book = null;
            
            if (id.HasValue)
            {
                book = await _context.Books.FindAsync(id.Value);
            }
            else if (!string.IsNullOrEmpty(slug))
            {
                book = await _context.Books.FirstOrDefaultAsync(b => b.Slug == slug);
            }
            
            if (book == null)
            {
                return Json(new { success = false, message = "الكتاب غير موجود" });
            }

            if (string.IsNullOrEmpty(book.PreviewPdfUrl))
            {
                return Json(new { success = false, message = "لا تتوفر معاينة لهذا الكتاب" });
            }

            // Record preview view
            await _bookService.RecordViewAsync(book.Id);

            return Json(new { success = true, previewUrl = book.PreviewPdfUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting book preview for ID {BookId} or slug {Slug}", id, slug);
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل المعاينة" });
        }
    }

    /// <summary>
    /// إضافة الكتاب للسلة عبر AJAX - Add book to cart via AJAX
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int? id, string? slug)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "يجب تسجيل الدخول أولاً", requireLogin = true });
            }

            Domain.Entities.Books.Book? book = null;
            
            if (id.HasValue)
            {
                book = await _context.Books.FindAsync(id.Value);
            }
            else if (!string.IsNullOrEmpty(slug))
            {
                book = await _context.Books.FirstOrDefaultAsync(b => b.Slug == slug);
            }
            
            if (book == null || book.Status != BookStatus.Published)
            {
                return Json(new { success = false, message = "الكتاب غير موجود أو غير متاح" });
            }

            // Check if already purchased
            var isPurchased = await _context.BookPurchases
                .AnyAsync(p => p.StudentId == userId && p.BookId == book.Id && p.IsActive && !p.IsRefunded);

            if (isPurchased)
            {
                return Json(new { success = false, message = "لديك هذا الكتاب بالفعل" });
            }

            // Check if already in cart
            var existingCartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.BookId == book.Id);

            if (existingCartItem != null)
            {
                return Json(new { success = false, message = "الكتاب موجود في سلة التسوق بالفعل" });
            }

            // Add to cart (CartItem is in Learning namespace; use ProductType.Book)
            var price = book.DiscountPrice ?? book.Price;
            var cartItem = new Domain.Entities.Learning.CartItem
            {
                UserId = userId,
                ItemType = Domain.Enums.ProductType.Book,
                BookId = book.Id,
                PriceAtAdd = price,
                Currency = book.Currency ?? "EGP",
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();

            // Get updated cart count
            var cartCount = await _context.CartItems.CountAsync(c => c.UserId == userId);

            _logger.LogInformation("Book {BookId} added to cart by user {UserId}", book.Id, userId);

            return Json(new { 
                success = true, 
                message = "تمت إضافة الكتاب إلى السلة", 
                cartCount,
                bookTitle = book.Title
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding book to cart for ID {BookId} or slug {Slug}", id, slug);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة الكتاب للسلة" });
        }
    }

    /// <summary>
    /// ابدأ القراءة - Start reading (redirects to download page for purchased books)
    /// </summary>
    [Authorize]
    public async Task<IActionResult> Read(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Check if user has purchased this book
        var purchase = await _context.BookPurchases
            .Include(bp => bp.Book)
            .FirstOrDefaultAsync(bp => bp.StudentId == userId && bp.BookId == id);

        if (purchase == null)
        {
            // Not purchased, redirect to book details
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }
            
            SetWarningMessage("يجب شراء الكتاب أولاً للبدء في القراءة");
            return RedirectToAction(nameof(Details), new { slug = book.Slug });
        }

        // User has purchased the book, redirect to download page (id = BookId)
        return RedirectToAction(nameof(Download), new { id = purchase.BookId });
    }

    #region Private Helpers

    private async Task<List<CategoryFilterOption>> GetCategoryFiltersAsync()
    {
        var categories = await _context.Categories
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryFilterOption
            {
                Id = c.Id,
                Name = c.Name,
                BookCount = c.Books.Count(b => b.Status == BookStatus.Published),
                SubCategories = c.SubCategories
                    .Where(sc => !sc.IsDeleted)
                    .Select(sc => new CategoryFilterOption
                    {
                        Id = sc.Id,
                        Name = sc.Name,
                        BookCount = sc.Books.Count(b => b.Status == BookStatus.Published)
                    }).ToList()
            })
            .ToListAsync();

        return categories;
    }

    private async Task<List<LanguageFilterOption>> GetLanguageFiltersAsync()
    {
        var languages = await _context.Books
            .Where(b => b.Status == BookStatus.Published)
            .GroupBy(b => b.Language)
            .Select(g => new LanguageFilterOption
            {
                Code = g.Key,
                Name = g.Key == "ar" ? "العربية" : g.Key == "en" ? "English" : g.Key,
                BookCount = g.Count()
            })
            .ToListAsync();

        return languages;
    }

    #endregion
}

