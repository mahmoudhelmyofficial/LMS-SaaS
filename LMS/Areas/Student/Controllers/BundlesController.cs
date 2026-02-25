using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// باقات الدورات - Course Bundles Controller
/// Enterprise-level bundle browsing and purchasing
/// </summary>
public class BundlesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IShoppingCartService _cartService;
    private readonly ICurrencyService _currencyService;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<BundlesController> _logger;

    public BundlesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IShoppingCartService cartService,
        ICurrencyService currencyService,
        IPaymentGatewayFactory gatewayFactory,
        IEmailService emailService,
        ILogger<BundlesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cartService = cartService;
        _currencyService = currencyService;
        _gatewayFactory = gatewayFactory;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الباقات المتاحة - Browse available bundles
    /// </summary>
    public async Task<IActionResult> Index(string? search, string? sortBy, int page = 1)
    {
        var now = DateTime.UtcNow;
        var pageSize = 12;

        var query = _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
                    .ThenInclude(c => c.Instructor)
            .Where(b => b.IsActive &&
                       (b.ValidFrom == null || b.ValidFrom <= now) &&
                       (b.ValidTo == null || b.ValidTo >= now) &&
                       (b.MaxSales == null || b.SalesCount < b.MaxSales))
            .AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b => b.Name.Contains(search) || 
                                    (b.Description != null && b.Description.Contains(search)));
        }

        // Sorting
        query = sortBy switch
        {
            "price_asc" => query.OrderBy(b => b.Price),
            "price_desc" => query.OrderByDescending(b => b.Price),
            "savings" => query.OrderByDescending(b => b.SavingsPercentage),
            "popular" => query.OrderByDescending(b => b.SalesCount),
            "newest" => query.OrderByDescending(b => b.CreatedAt),
            _ => query.OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.IsFeatured)
        };

        var totalCount = await query.CountAsync();
        var bundles = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get user's enrolled courses to show which bundles they partially own
        var userId = _currentUserService.UserId;
        var enrolledCourseIds = new HashSet<int>();
        if (!string.IsNullOrEmpty(userId))
        {
            enrolledCourseIds = (await _context.Enrollments
                .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.CourseId)
                .ToListAsync())
                .ToHashSet();
        }

        var viewModel = new BundleListViewModel
        {
            Bundles = bundles.Select(b => new BundleDisplayViewModel
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                ShortDescription = b.ShortDescription,
                ThumbnailUrl = b.ThumbnailUrl,
                Price = b.Price,
                OriginalPrice = b.OriginalPrice,
                Currency = b.Currency,
                SavingsPercentage = b.SavingsPercentage,
                CoursesCount = b.CoursesCount,
                TotalDurationHours = b.TotalDurationHours,
                IsFeatured = b.IsFeatured,
                SalesCount = b.SalesCount,
                EnrolledCoursesCount = b.BundleCourses.Count(bc => enrolledCourseIds.Contains(bc.CourseId)),
                IsFullyOwned = b.BundleCourses.All(bc => enrolledCourseIds.Contains(bc.CourseId)),
                Courses = b.BundleCourses.Select(bc => new BundleCourseInfo
                {
                    CourseId = bc.CourseId,
                    Title = bc.Course.Title,
                    ThumbnailUrl = bc.Course.ThumbnailUrl,
                    InstructorName = bc.Course.Instructor != null 
                        ? $"{bc.Course.Instructor.FirstName} {bc.Course.Instructor.LastName}" 
                        : null,
                    IsEnrolled = enrolledCourseIds.Contains(bc.CourseId)
                }).ToList()
            }).ToList(),
            Search = search,
            SortBy = sortBy,
            Page = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    /// <summary>
    /// تفاصيل الباقة - Bundle details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var now = DateTime.UtcNow;

        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
                    .ThenInclude(c => c.Instructor)
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

        if (bundle == null)
            return NotFound();

        // Check validity
        if ((bundle.ValidFrom.HasValue && bundle.ValidFrom > now) ||
            (bundle.ValidTo.HasValue && bundle.ValidTo < now))
        {
            SetErrorMessage("هذه الباقة غير متاحة حالياً");
            return RedirectToAction(nameof(Index));
        }

        // Check max sales
        if (bundle.MaxSales.HasValue && bundle.SalesCount >= bundle.MaxSales.Value)
        {
            SetErrorMessage("نفدت الباقة");
            return RedirectToAction(nameof(Index));
        }

        // Get user's enrolled courses
        var userId = _currentUserService.UserId;
        var enrolledCourseIds = new HashSet<int>();
        if (!string.IsNullOrEmpty(userId))
        {
            enrolledCourseIds = (await _context.Enrollments
                .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.CourseId)
                .ToListAsync())
                .ToHashSet();
        }

        var viewModel = new BundleDetailsViewModel
        {
            Id = bundle.Id,
            Name = bundle.Name,
            Slug = bundle.Slug,
            ShortDescription = bundle.ShortDescription,
            Description = bundle.Description,
            ThumbnailUrl = bundle.ThumbnailUrl,
            Price = bundle.Price,
            OriginalPrice = bundle.OriginalPrice,
            Currency = bundle.Currency,
            SavingsPercentage = bundle.SavingsPercentage,
            SavingsAmount = bundle.OriginalPrice - bundle.Price,
            CoursesCount = bundle.CoursesCount,
            TotalDurationHours = bundle.TotalDurationHours,
            IsFeatured = bundle.IsFeatured,
            SalesCount = bundle.SalesCount,
            ValidFrom = bundle.ValidFrom,
            ValidTo = bundle.ValidTo,
            MaxSales = bundle.MaxSales,
            RemainingSlots = bundle.MaxSales.HasValue ? bundle.MaxSales.Value - bundle.SalesCount : null,
            IsFullyOwned = bundle.BundleCourses.All(bc => enrolledCourseIds.Contains(bc.CourseId)),
            OwnedCoursesCount = bundle.BundleCourses.Count(bc => enrolledCourseIds.Contains(bc.CourseId)),
            Courses = bundle.BundleCourses.Select(bc => new BundleCourseDetailInfo
            {
                CourseId = bc.CourseId,
                Title = bc.Course.Title,
                ShortDescription = bc.Course.ShortDescription,
                ThumbnailUrl = bc.Course.ThumbnailUrl,
                InstructorName = bc.Course.Instructor != null 
                    ? $"{bc.Course.Instructor.FirstName} {bc.Course.Instructor.LastName}" 
                    : null,
                InstructorAvatar = bc.Course.Instructor?.ProfilePictureUrl,
                Price = bc.Course.Price,
                DiscountPrice = bc.Course.DiscountPrice,
                DurationMinutes = bc.Course.DurationMinutes,
                LessonsCount = bc.Course.Modules.Sum(m => m.Lessons.Count),
                Level = bc.Course.Level.ToString(),
                Rating = bc.Course.Rating,
                TotalStudents = bc.Course.TotalStudents,
                IsEnrolled = enrolledCourseIds.Contains(bc.CourseId)
            }).ToList()
        };

        // Calculate effective price (subtract already owned courses)
        viewModel.EffectivePrice = viewModel.Price; // Full price, they get all courses
        viewModel.FormattedPrice = _currencyService.FormatAmount(viewModel.Price, "EGP");
        viewModel.FormattedOriginalPrice = _currencyService.FormatAmount(viewModel.OriginalPrice, "EGP");
        viewModel.FormattedSavings = _currencyService.FormatAmount(viewModel.SavingsAmount, "EGP");

        return View(viewModel);
    }

    /// <summary>
    /// إضافة الباقة إلى السلة - Add bundle to cart
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int bundleId, bool returnJson = false)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
            }
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _cartService.AddBundleToCartAsync(userId, bundleId);

        if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = result.Success, message = result.Message });
        }

        if (result.Success)
        {
            SetSuccessMessage(result.Message ?? "تمت إضافة الباقة إلى السلة");
            return RedirectToAction("Cart", "Checkout", new { area = "Student" });
        }

        SetErrorMessage(result.Message ?? "حدث خطأ أثناء إضافة الباقة");
        return RedirectToAction(nameof(Details), new { id = bundleId });
    }

    /// <summary>
    /// الشراء السريع للباقة - Quick buy bundle
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> QuickBuy(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Clear cart and add bundle
        await _cartService.ClearCartAsync(userId);
        var result = await _cartService.AddBundleToCartAsync(userId, id);

        if (result.Success)
        {
            return RedirectToAction("Index", "Checkout", new { area = "Student" });
        }

        SetErrorMessage(result.Message ?? "حدث خطأ");
        return RedirectToAction(nameof(Details), new { id });
    }
}

