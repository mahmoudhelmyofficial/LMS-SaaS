using LMS.Areas.Admin.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الكتب - Admin Books Controller
/// </summary>
public class BooksController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBookService _bookService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBookService bookService,
        ILogger<BooksController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _bookService = bookService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الكتب - Books list
    /// </summary>
    public async Task<IActionResult> Index(BookAdminFilterViewModel filter)
    {
        try
        {
            // Ensure safe pagination (global query filters on Category/Instructor would exclude books if we Include them)
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var query = _context.Books.AsQueryable();

            // Apply filters
            if (filter.Status.HasValue)
            {
                query = query.Where(b => b.Status == filter.Status.Value);
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == filter.CategoryId.Value ||
                                        b.SubCategoryId == filter.CategoryId.Value);
            }

            if (!string.IsNullOrEmpty(filter.InstructorId))
            {
                query = query.Where(b => b.InstructorId == filter.InstructorId);
            }

            if (!string.IsNullOrEmpty(filter.Search))
            {
                query = query.Where(b => b.Title.Contains(filter.Search) ||
                                        (b.Author != null && b.Author.Contains(filter.Search)) ||
                                        (b.ISBN != null && b.ISBN.Contains(filter.Search)));
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt <= filter.ToDate.Value);
            }

            // Apply sorting (guard null SortBy for first load / form binding)
            var sortBy = string.IsNullOrEmpty(filter.SortBy) ? "CreatedAt" : filter.SortBy;
            query = sortBy switch
            {
                "Title" => filter.SortDescending ? query.OrderByDescending(b => b.Title) : query.OrderBy(b => b.Title),
                "Price" => filter.SortDescending ? query.OrderByDescending(b => b.Price) : query.OrderBy(b => b.Price),
                "Sales" => filter.SortDescending ? query.OrderByDescending(b => b.TotalSales) : query.OrderBy(b => b.TotalSales),
                "Rating" => filter.SortDescending ? query.OrderByDescending(b => b.AverageRating) : query.OrderBy(b => b.AverageRating),
                "Status" => filter.SortDescending ? query.OrderByDescending(b => b.Status) : query.OrderBy(b => b.Status),
                _ => filter.SortDescending ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            // Project without Include so books are not excluded by Category/Instructor query filters (e.g. soft-deleted users)
            var pageData = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.CoverImageUrl,
                    b.Author,
                    b.Price,
                    b.DiscountPrice,
                    b.Status,
                    b.BookType,
                    b.TotalSales,
                    b.TotalDownloads,
                    b.AverageRating,
                    b.TotalReviews,
                    b.CreatedAt,
                    b.PublishedAt,
                    b.SubmittedForReviewAt,
                    b.CategoryId,
                    b.InstructorId
                })
                .ToListAsync();

            // Resolve category and instructor names without query filters so all books show (e.g. deleted/missing users)
            var categoryIds = pageData.Select(x => x.CategoryId).Distinct().ToList();
            var categoryNames = categoryIds.Count > 0
                ? await _context.Categories
                    .IgnoreQueryFilters()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name ?? "غير مصنف")
                : new Dictionary<int, string>();

            var instructorIds = pageData.Select(x => x.InstructorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var instructorNames = instructorIds.Count > 0
                ? await _context.Users
                    .IgnoreQueryFilters()
                    .Where(u => instructorIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
                    .ToDictionaryAsync(x => x.Id, x => x.Name)
                : new Dictionary<string, string>();

            var books = pageData.Select(b => new BookAdminListViewModel
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
                TotalDownloads = b.TotalDownloads,
                AverageRating = b.AverageRating,
                TotalReviews = b.TotalReviews,
                CreatedAt = b.CreatedAt,
                PublishedAt = b.PublishedAt,
                SubmittedForReviewAt = b.SubmittedForReviewAt,
                CategoryName = categoryNames.GetValueOrDefault(b.CategoryId, "غير مصنف"),
                InstructorName = instructorNames.GetValueOrDefault(b.InstructorId ?? string.Empty, "غير محدد"),
                InstructorId = b.InstructorId ?? string.Empty
            }).ToList();

            // Stats summary (never let stats failure hide the books list)
            BookStatsAdminViewModel stats;
            try
            {
                stats = await GetBookStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetBookStatsAsync failed; using default stats");
                stats = new BookStatsAdminViewModel();
            }
            ViewBag.Stats = stats;
            ViewBag.Filter = new BookAdminFilterViewModel
            {
                Status = filter.Status,
                CategoryId = filter.CategoryId,
                InstructorId = filter.InstructorId,
                Search = filter.Search,
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                SortBy = sortBy,
                SortDescending = filter.SortDescending,
                Page = page,
                PageSize = pageSize
            };
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            await PopulateFilterDropdownsAsync();

            return View(books);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading books list. Filter: Status={Status}, CategoryId={CategoryId}, Search={Search}. Inner: {Inner}", 
                filter.Status, filter.CategoryId, filter.Search, ex.InnerException?.Message ?? ex.Message);
            
            // Provide user-friendly error with context
            SetErrorMessage("حدث خطأ أثناء تحميل قائمة الكتب. يرجى تحديث الصفحة أو المحاولة لاحقاً", "An error occurred while loading the books list. Please refresh or try again later.");
            
            // Try to load stats anyway for context
            try 
            { 
                ViewBag.Stats = await GetBookStatsAsync();
            } 
            catch 
            { 
                ViewBag.Stats = new BookStatsAdminViewModel();
            }
            
            ViewBag.Filter = filter;
            ViewBag.TotalCount = 0;
            ViewBag.TotalPages = 0;
            
            try { await PopulateFilterDropdownsAsync(); } catch { }
            
            return View(new List<BookAdminListViewModel>());
        }
    }

    /// <summary>
    /// الكتب المعلقة للمراجعة - Pending review books
    /// </summary>
    public async Task<IActionResult> PendingReview()
    {
        try
        {
            var pageData = await _context.Books
                .Where(b => b.Status == BookStatus.PendingReview)
                .OrderBy(b => b.SubmittedForReviewAt)
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.CoverImageUrl,
                    b.Author,
                    b.Price,
                    b.DiscountPrice,
                    b.Status,
                    b.BookType,
                    b.TotalSales,
                    b.CreatedAt,
                    b.SubmittedForReviewAt,
                    b.CategoryId,
                    b.InstructorId
                })
                .ToListAsync();

            if (pageData.Count == 0)
            {
                return View(new List<BookAdminListViewModel>());
            }

            var categoryNames = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => pageData.Select(x => x.CategoryId).Distinct().Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name ?? "غير مصنف");

            var instructorIds = pageData.Select(x => x.InstructorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var instructorNames = instructorIds.Count > 0
                ? await _context.Users.IgnoreQueryFilters()
                    .Where(u => instructorIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
                    .ToDictionaryAsync(x => x.Id, x => x.Name)
                : new Dictionary<string, string>();

            var books = pageData.Select(b => new BookAdminListViewModel
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
                CreatedAt = b.CreatedAt,
                SubmittedForReviewAt = b.SubmittedForReviewAt,
                CategoryName = categoryNames.GetValueOrDefault(b.CategoryId, "غير مصنف"),
                InstructorName = instructorNames.GetValueOrDefault(b.InstructorId ?? string.Empty, "غير محدد"),
                InstructorId = b.InstructorId ?? string.Empty
            }).ToList();

            return View(books);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending review books. Exception: {Message}", ex.Message);
            SetErrorMessage("حدث خطأ أثناء تحميل الكتب المعلقة. يرجى تحديث الصفحة أو المحاولة لاحقاً", "An error occurred while loading pending books. Please refresh or try again later.");
            return View(new List<BookAdminListViewModel>());
        }
    }

    /// <summary>
    /// تفاصيل الكتاب - Book details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var book = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.SubCategory)
                .Include(b => b.Instructor)
                .Include(b => b.Chapters.OrderBy(c => c.OrderIndex))
                .Include(b => b.Purchases)
                    .ThenInclude(p => p.Student)
                .Include(b => b.Reviews)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var earnings = await _context.InstructorEarnings
                .Where(e => e.BookId == id)
                .ToListAsync();

            var viewModel = new BookAdminDetailsViewModel
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
                FullPdfUrl = book.FullPdfUrl,
                EpubUrl = book.EpubUrl,
                MobiUrl = book.MobiUrl,
                FileSizeBytes = book.FileSizeBytes,
                Price = book.Price,
                DiscountPrice = book.DiscountPrice,
                IsFree = book.IsFree,
                Currency = book.Currency,
                BookType = book.BookType,
                AvailableFormats = book.AvailableFormats,
                HasPhysicalCopy = book.HasPhysicalCopy,
                PhysicalPrice = book.PhysicalPrice,
                PhysicalStock = book.PhysicalStock,
                Status = book.Status,
                CreatedAt = book.CreatedAt,
                PublishedAt = book.PublishedAt,
                SubmittedForReviewAt = book.SubmittedForReviewAt,
                RejectionReason = book.RejectionReason,
                ApprovedBy = book.ApprovedBy,
                RejectedBy = book.RejectedBy,
                EnableDRM = book.EnableDRM,
                AllowPrinting = book.AllowPrinting,
                MaxDownloads = book.MaxDownloads,
                EnableWatermark = book.EnableWatermark,
                TotalSales = book.TotalSales,
                TotalDownloads = book.TotalDownloads,
                TotalReviews = book.TotalReviews,
                AverageRating = book.AverageRating,
                ViewCount = book.ViewCount,
                TotalRevenue = book.Purchases.Sum(p => p.PaidAmount),
                InstructorEarnings = earnings.Sum(e => e.NetAmount),
                PlatformEarnings = earnings.Sum(e => e.PlatformCommission),
                InstructorId = book.InstructorId,
                InstructorName = book.Instructor?.FullName ?? "",
                InstructorEmail = book.Instructor?.Email,
                InstructorPhone = book.Instructor?.PhoneNumber,
                InstructorCommissionRate = book.InstructorCommissionRate,
                CategoryId = book.CategoryId,
                CategoryName = book.Category?.Name ?? "",
                SubCategoryName = book.SubCategory?.Name,
                IsFeatured = book.IsFeatured,
                IsBestseller = book.IsBestseller,
                AllowReviews = book.AllowReviews,
                Chapters = book.Chapters.Select(c => new BookChapterAdminViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    PageNumber = c.PageNumber,
                    EndPageNumber = c.EndPageNumber,
                    IsPreviewable = c.IsPreviewable,
                    OrderIndex = c.OrderIndex
                }).ToList(),
                RecentPurchases = book.Purchases
                    .OrderByDescending(p => p.PurchasedAt)
                    .Take(10)
                    .Select(p => new BookPurchaseAdminViewModel
                    {
                        Id = p.Id,
                        StudentName = p.Student?.FullName ?? "",
                        StudentEmail = p.Student?.Email ?? "",
                        PaidAmount = p.PaidAmount,
                        PurchasedAt = p.PurchasedAt,
                        DownloadCount = p.DownloadCount,
                        IsRefunded = p.IsRefunded,
                        PhysicalStatus = p.PhysicalStatus
                    }).ToList(),
                RecentReviews = book.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(10)
                    .Select(r => new BookReviewAdminViewModel
                    {
                        Id = r.Id,
                        StudentName = r.Student?.FullName ?? "",
                        Rating = r.Rating,
                        Comment = r.Comment,
                        IsApproved = r.IsApproved,
                        IsReported = r.IsReported,
                        ReportCount = r.ReportCount,
                        CreatedAt = r.CreatedAt
                    }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading book details {BookId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الكتاب", "An error occurred while loading book details.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// نشر الكتاب - Publish book
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        try
        {
            var adminId = _currentUserService.UserId;
            var result = await _bookService.PublishBookAsync(id, adminId!);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم نشر الكتاب بنجاح", "Book published successfully.");
            }
            else
            {
                SetErrorMessage(result.Error ?? CultureExtensions.T("حدث خطأ أثناء نشر الكتاب", "An error occurred while publishing the book."));
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing book {BookId}", id);
            SetErrorMessage("حدث خطأ أثناء نشر الكتاب", "An error occurred while publishing the book.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// رفض الكتاب - Reject book
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(BookApprovalViewModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                SetErrorMessage("يجب إدخال سبب الرفض", "Rejection reason is required.");
                return RedirectToAction(nameof(Details), new { id = model.BookId });
            }

            var adminId = _currentUserService.UserId;
            var result = await _bookService.RejectBookAsync(model.BookId, adminId!, model.Reason);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم رفض الكتاب", "Book rejected.");
            }
            else
            {
                SetErrorMessage(result.Error ?? CultureExtensions.T("حدث خطأ أثناء رفض الكتاب", "An error occurred while rejecting the book."));
            }

            return RedirectToAction(nameof(Details), new { id = model.BookId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting book {BookId}", model.BookId);
            SetErrorMessage("حدث خطأ أثناء رفض الكتاب", "An error occurred while rejecting the book.");
            return RedirectToAction(nameof(Details), new { id = model.BookId });
        }
    }

    /// <summary>
    /// تعليق الكتاب - Suspend book
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(BookApprovalViewModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                SetErrorMessage("يجب إدخال سبب التعليق", "Comment reason is required.");
                return RedirectToAction(nameof(Details), new { id = model.BookId });
            }

            var adminId = _currentUserService.UserId;
            var result = await _bookService.SuspendBookAsync(model.BookId, adminId!, model.Reason);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم تعليق الكتاب", "Book suspended.");
            }
            else
            {
                SetErrorMessage(result.Error ?? CultureExtensions.T("حدث خطأ أثناء تعليق الكتاب", "An error occurred while suspending the book."));
            }

            return RedirectToAction(nameof(Details), new { id = model.BookId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending book {BookId}", model.BookId);
            SetErrorMessage("حدث خطأ أثناء تعليق الكتاب", "An error occurred while suspending the book.");
            return RedirectToAction(nameof(Details), new { id = model.BookId });
        }
    }

    /// <summary>
    /// تعديل الكتاب - Edit book (admin settings)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BookAdminEditViewModel model)
    {
        try
        {
            var book = await _context.Books.FindAsync(model.Id);
            if (book == null)
            {
                return NotFound();
            }

            book.IsFeatured = model.IsFeatured;
            book.IsBestseller = model.IsBestseller;
            book.InstructorCommissionRate = model.InstructorCommissionRate;
            book.CategoryId = model.CategoryId;
            book.SubCategoryId = model.SubCategoryId;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الكتاب بنجاح", "Book updated successfully.");
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing book {BookId}", model.Id);
            SetErrorMessage("حدث خطأ أثناء تحديث الكتاب", "An error occurred while updating the book.");
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }

    /// <summary>
    /// الموافقة على مراجعة - Approve review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveReview(int id, int bookId)
    {
        try
        {
            var review = await _context.BookReviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            review.ApprovedBy = _currentUserService.UserId;
            review.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _bookService.UpdateBookStatisticsAsync(review.BookId);

            SetSuccessMessage("تم الموافقة على المراجعة", "Review approved.");
            return RedirectToAction(nameof(Details), new { id = bookId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving review {ReviewId}", id);
            SetErrorMessage("حدث خطأ أثناء الموافقة على المراجعة", "An error occurred while approving the review.");
            return RedirectToAction(nameof(Details), new { id = bookId });
        }
    }

    /// <summary>
    /// إخفاء مراجعة - Hide review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideReview(int id, int bookId)
    {
        try
        {
            var review = await _context.BookReviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsHidden = true;
            await _context.SaveChangesAsync();
            await _bookService.UpdateBookStatisticsAsync(review.BookId);

            SetSuccessMessage("تم إخفاء المراجعة", "Review hidden.");
            return RedirectToAction(nameof(Details), new { id = bookId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding review {ReviewId}", id);
            SetErrorMessage("حدث خطأ أثناء إخفاء المراجعة", "An error occurred while hiding the review.");
            return RedirectToAction(nameof(Details), new { id = bookId });
        }
    }

    /// <summary>
    /// إحصائيات الكتب - Book statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var stats = await GetBookStatsAsync();
            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading book statistics");
            SetErrorMessage("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics.");
            return View(new BookStatsAdminViewModel());
        }
    }

    #region Private Helpers

    private async Task<BookStatsAdminViewModel> GetBookStatsAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var books = await _context.Books.ToListAsync();
        var purchases = await _context.BookPurchases
            .Where(p => !p.IsRefunded)
            .ToListAsync();

        var stats = new BookStatsAdminViewModel
        {
            TotalBooks = books.Count,
            PublishedBooks = books.Count(b => b.Status == BookStatus.Published),
            PendingReviewBooks = books.Count(b => b.Status == BookStatus.PendingReview),
            DraftBooks = books.Count(b => b.Status == BookStatus.Draft),
            RejectedBooks = books.Count(b => b.Status == BookStatus.Rejected),
            SuspendedBooks = books.Count(b => b.Status == BookStatus.Suspended),
            TotalSales = purchases.Count,
            TotalRevenue = purchases.Sum(p => p.PaidAmount),
            SalesToday = purchases.Count(p => p.PurchasedAt.Date == today),
            SalesThisWeek = purchases.Count(p => p.PurchasedAt >= weekStart),
            SalesThisMonth = purchases.Count(p => p.PurchasedAt >= monthStart),
            RevenueToday = purchases.Where(p => p.PurchasedAt.Date == today).Sum(p => p.PaidAmount),
            RevenueThisWeek = purchases.Where(p => p.PurchasedAt >= weekStart).Sum(p => p.PaidAmount),
            RevenueThisMonth = purchases.Where(p => p.PurchasedAt >= monthStart).Sum(p => p.PaidAmount)
        };

        // Platform earnings
        var platformRate = 30m; // 30% default platform commission
        stats.PlatformEarnings = stats.TotalRevenue * (platformRate / 100);

        // Top selling books (avoid Include/join to Instructor so soft-deleted users don't exclude books)
        try
        {
            var topSellingData = await _context.Books
                .Where(b => b.Status == BookStatus.Published)
                .OrderByDescending(b => b.TotalSales)
                .Take(5)
                .Select(b => new { b.Id, b.Title, b.CoverImageUrl, b.InstructorId, b.TotalSales, b.AverageRating })
                .ToListAsync();
            var topSellingRevenue = topSellingData.Count > 0
                ? await _context.BookPurchases
                    .Where(p => !p.IsRefunded && topSellingData.Select(b => b.Id).Contains(p.BookId))
                    .GroupBy(p => p.BookId)
                    .Select(g => new { BookId = g.Key, Revenue = g.Sum(p => p.PaidAmount) })
                    .ToDictionaryAsync(x => x.BookId, x => x.Revenue)
                : new Dictionary<int, decimal>();
            var topInstructorIds = topSellingData.Select(b => b.InstructorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var topInstructorNames = topInstructorIds.Count > 0
                ? await _context.Users.IgnoreQueryFilters()
                    .Where(u => topInstructorIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
                    .ToDictionaryAsync(x => x.Id, x => x.Name)
                : new Dictionary<string, string>();
            stats.TopSellingBooks = topSellingData.Select(b => new TopBookViewModel
            {
                Id = b.Id,
                Title = b.Title,
                CoverImageUrl = b.CoverImageUrl,
                InstructorName = topInstructorNames.GetValueOrDefault(b.InstructorId ?? string.Empty, "غير محدد"),
                Sales = b.TotalSales,
                Revenue = topSellingRevenue.GetValueOrDefault(b.Id, 0),
                Rating = b.AverageRating
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetBookStatsAsync: TopSellingBooks failed");
            stats.TopSellingBooks = new List<TopBookViewModel>();
        }

        // Top rated books
        try
        {
            var topRatedData = await _context.Books
                .Where(b => b.Status == BookStatus.Published && b.TotalReviews >= 5)
                .OrderByDescending(b => b.AverageRating)
                .Take(5)
                .Select(b => new { b.Id, b.Title, b.CoverImageUrl, b.InstructorId, b.TotalSales, b.AverageRating })
                .ToListAsync();
            var topRatedInstructorIds = topRatedData.Select(b => b.InstructorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var topRatedInstructorNames = topRatedInstructorIds.Count > 0
                ? await _context.Users.IgnoreQueryFilters()
                    .Where(u => topRatedInstructorIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
                    .ToDictionaryAsync(x => x.Id, x => x.Name)
                : new Dictionary<string, string>();
            stats.TopRatedBooks = topRatedData.Select(b => new TopBookViewModel
            {
                Id = b.Id,
                Title = b.Title,
                CoverImageUrl = b.CoverImageUrl,
                InstructorName = topRatedInstructorNames.GetValueOrDefault(b.InstructorId ?? string.Empty, "غير محدد"),
                Sales = b.TotalSales,
                Rating = b.AverageRating
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetBookStatsAsync: TopRatedBooks failed");
            stats.TopRatedBooks = new List<TopBookViewModel>();
        }

        // Monthly data for charts (last 6 months)
        for (int i = 5; i >= 0; i--)
        {
            var month = now.AddMonths(-i);
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            var monthlySales = purchases.Count(p => p.PurchasedAt >= firstDay && p.PurchasedAt <= lastDay);
            var monthlyRevenue = purchases
                .Where(p => p.PurchasedAt >= firstDay && p.PurchasedAt <= lastDay)
                .Sum(p => p.PaidAmount);

            stats.MonthlySales.Add(monthlySales);
            stats.MonthlyRevenue.Add(monthlyRevenue);
            stats.MonthLabels.Add(month.ToString("MMM yyyy"));
        }

        return stats;
    }

    private async Task PopulateFilterDropdownsAsync()
    {
        try
        {
            var categories = await _context.Categories
                .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PopulateFilterDropdownsAsync: failed to load categories");
            ViewBag.Categories = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }

        try
        {
            // Use IsApproved (mapped column); Status is a computed property and cannot be translated to SQL
            var instructors = await _context.Users
                .Where(u => _context.InstructorProfiles.Any(p => p.UserId == u.Id && p.IsApproved))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName })
                .ToListAsync();
            ViewBag.Instructors = new SelectList(instructors, "Id", "FullName");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PopulateFilterDropdownsAsync: failed to load instructors");
            ViewBag.Instructors = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }

        try
        {
            ViewBag.Statuses = new SelectList(
                Enum.GetValues<BookStatus>().Select(s => new {
                    Value = (int)s,
                    Text = s switch
                    {
                        BookStatus.Draft => "مسودة",
                        BookStatus.PendingReview => "قيد المراجعة",
                        BookStatus.Published => "منشور",
                        BookStatus.Rejected => "مرفوض",
                        BookStatus.Suspended => "معلق",
                        BookStatus.Archived => "مؤرشف",
                        _ => s.ToString()
                    }
                }),
                "Value", "Text");
        }
        catch
        {
            ViewBag.Statuses = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }
    }

    #endregion
}

