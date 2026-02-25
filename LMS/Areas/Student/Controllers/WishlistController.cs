using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// قائمة الأمنيات - Wishlist Controller
/// </summary>
public class WishlistController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IShoppingCartService _cartService;
    private readonly ILogger<WishlistController> _logger;

    public WishlistController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IShoppingCartService cartService,
        ILogger<WishlistController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الأمنيات - Wishlist
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var wishlistItems = await _context.WishlistItems
            .Include(w => w.Course)
                .ThenInclude(c => c.Instructor)
            .Include(w => w.Course)
                .ThenInclude(c => c.Category)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync();

        return View(wishlistItems);
    }

    /// <summary>
    /// إضافة للقائمة - Add to wishlist (GET - for direct URL access)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Add(int courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Validate course exists and is published
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == Domain.Enums.CourseStatus.Published);

        if (course == null)
        {
            SetErrorMessage("الدورة غير موجودة أو غير متاحة");
            return RedirectToAction(nameof(Index));
        }

        // Check if already enrolled
        var isEnrolled = await _context.Enrollments
            .AnyAsync(e => e.StudentId == userId && e.CourseId == courseId);

        if (isEnrolled)
        {
            SetInfoMessage("أنت مسجل بالفعل في هذه الدورة");
            return RedirectToAction("Learn", "Courses", new { id = courseId });
        }

        var existing = await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (existing)
        {
            SetInfoMessage("الدورة موجودة بالفعل في قائمة الأمنيات");
            return RedirectToAction(nameof(Index));
        }

        var wishlistItem = new WishlistItem
        {
            UserId = userId,
            CourseId = courseId,
            AddedAt = DateTime.UtcNow
        };

        _context.WishlistItems.Add(wishlistItem);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تمت الإضافة إلى قائمة الأمنيات");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إضافة للقائمة - Add to wishlist (POST - for AJAX calls)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPost(int courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        // Validate course exists and is published
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == Domain.Enums.CourseStatus.Published);

        if (course == null)
        {
            return Json(new { success = false, message = "الدورة غير موجودة أو غير متاحة" });
        }

        // Check if already enrolled
        var isEnrolled = await _context.Enrollments
            .AnyAsync(e => e.StudentId == userId && e.CourseId == courseId);

        if (isEnrolled)
        {
            return Json(new { success = false, message = "أنت مسجل بالفعل في هذه الدورة", isEnrolled = true });
        }

        var existing = await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (existing)
        {
            return Json(new { success = true, message = "الدورة موجودة بالفعل في القائمة", alreadyExists = true });
        }

        var wishlistItem = new WishlistItem
        {
            UserId = userId,
            CourseId = courseId,
            AddedAt = DateTime.UtcNow
        };

        _context.WishlistItems.Add(wishlistItem);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تمت الإضافة إلى قائمة الأمنيات" });
    }

    /// <summary>
    /// تبديل حالة المفضلة - Toggle wishlist (add/remove)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        var existing = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (existing != null)
        {
            // Remove from wishlist
            _context.WishlistItems.Remove(existing);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isInWishlist = false, message = "تمت الإزالة من المفضلة" });
        }
        else
        {
            // Add to wishlist
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == Domain.Enums.CourseStatus.Published);

            if (course == null)
            {
                return Json(new { success = false, message = "الدورة غير موجودة أو غير متاحة" });
            }

            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                CourseId = courseId,
                AddedAt = DateTime.UtcNow
            };

            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isInWishlist = true, message = "تمت الإضافة إلى المفضلة" });
        }
    }

    /// <summary>
    /// إزالة من القائمة - Remove from wishlist
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var item = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.CourseId == courseId);

        if (item != null)
        {
            _context.WishlistItems.Remove(item);
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, message = "تمت الإزالة من قائمة الأمنيات" });
    }

    /// <summary>
    /// إزالة عنصر بمعرفه - Remove wishlist item by ID
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveById(int wishlistItemId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var item = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.Id == wishlistItemId && w.UserId == userId);

        if (item != null)
        {
            _context.WishlistItems.Remove(item);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إضافة الكل إلى السلة - Add all wishlist items to cart
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAllToCart()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(AddAllToCart)) });
        }

        var wishlistItems = await _context.WishlistItems
            .Where(w => w.UserId == userId)
            .Select(w => w.CourseId)
            .ToListAsync();

        if (!wishlistItems.Any())
        {
            SetErrorMessage("قائمة الرغبات فارغة");
            return RedirectToAction(nameof(Index));
        }

        var addedCount = 0;
        foreach (var courseId in wishlistItems)
        {
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == userId && e.CourseId == courseId);
            if (isEnrolled)
                continue;

            var result = await _cartService.AddToCartAsync(userId, courseId);
            if (result.Success)
                addedCount++;
        }

        SetSuccessMessage(addedCount > 0
            ? $"تمت إضافة {addedCount} دورة إلى السلة"
            : "لا توجد دورات جديدة يمكن إضافتها (الباقي مسجل فيه أو في السلة بالفعل)");
        return RedirectToAction("Cart", "Checkout", new { area = "Student" });
    }
}

