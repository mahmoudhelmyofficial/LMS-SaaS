using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الهدايا - Gift controller
/// Enterprise-level gift purchasing and redemption
/// </summary>
public class GiftsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGiftPurchaseService _giftService;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<GiftsController> _logger;

    public GiftsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGiftPurchaseService giftService,
        ICurrencyService currencyService,
        ILogger<GiftsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _giftService = giftService;
        _currencyService = currencyService;
        _logger = logger;
    }

    /// <summary>
    /// صفحة الهدايا - Gifts page (sent and received)
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var giftsInfo = await _giftService.GetUserGiftsAsync(userId);
        return View(giftsInfo);
    }

    /// <summary>
    /// إهداء دورة - Gift a course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SendCourse(int courseId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var course = await _context.Courses
            .Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == CourseStatus.Published);

        if (course == null)
        {
            SetErrorMessage("الدورة غير موجودة");
            return RedirectToAction("Index", "Courses");
        }

        var viewModel = new SendGiftViewModel
        {
            ProductType = ProductType.Course,
            ProductId = courseId,
            ProductTitle = course.Title,
            ProductThumbnail = course.ThumbnailUrl,
            InstructorName = course.Instructor != null 
                ? $"{course.Instructor.FirstName} {course.Instructor.LastName}" 
                : null,
            Price = course.DiscountPrice ?? course.Price,
            OriginalPrice = course.Price,
            Currency = "EGP",
            FormattedPrice = _currencyService.FormatAmount(course.DiscountPrice ?? course.Price, "EGP")
        };

        return View("Send", viewModel);
    }

    /// <summary>
    /// إهداء كتاب - Gift a book
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SendBook(int bookId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var book = await _context.Books
            .Include(b => b.Instructor)
            .FirstOrDefaultAsync(b => b.Id == bookId && b.Status == BookStatus.Published);

        if (book == null)
        {
            SetErrorMessage("الكتاب غير موجود");
            return RedirectToAction("Index", "Books");
        }

        var viewModel = new SendGiftViewModel
        {
            ProductType = ProductType.Book,
            ProductId = bookId,
            ProductTitle = book.Title,
            ProductThumbnail = book.CoverImageUrl,
            InstructorName = book.Instructor != null 
                ? $"{book.Instructor.FirstName} {book.Instructor.LastName}" 
                : null,
            Price = book.DiscountPrice ?? book.Price,
            OriginalPrice = book.Price,
            Currency = "EGP",
            FormattedPrice = _currencyService.FormatAmount(book.DiscountPrice ?? book.Price, "EGP")
        };

        return View("Send", viewModel);
    }

    /// <summary>
    /// معالجة إرسال الهدية - Process gift send
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendGiftViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        if (!ModelState.IsValid)
        {
            // Reload product info
            if (model.ProductType == ProductType.Course)
            {
                var course = await _context.Courses.FindAsync(model.ProductId);
                if (course != null)
                {
                    model.ProductTitle = course.Title;
                    model.ProductThumbnail = course.ThumbnailUrl;
                    model.Price = course.DiscountPrice ?? course.Price;
                    model.Currency = "EGP";
                    model.FormattedPrice = _currencyService.FormatAmount(model.Price, "EGP");
                }
            }
            return View(model);
        }

        // Create gift purchase
        var result = await _giftService.CreateGiftPurchaseAsync(new CreateGiftRequest
        {
            PurchaserId = userId,
            RecipientEmail = model.RecipientEmail,
            RecipientName = model.RecipientName,
            ProductType = model.ProductType,
            ProductId = model.ProductId,
            PersonalMessage = model.PersonalMessage,
            Currency = model.Currency,
            DeliveryDate = model.SendNow ? null : model.DeliveryDate
        });

        if (!result.Success)
        {
            SetErrorMessage(result.ErrorMessage ?? "حدث خطأ أثناء إنشاء الهدية");
            return View(model);
        }

        // Store gift ID in session for checkout
        HttpContext.Session.SetInt32("GiftPurchaseId", result.GiftId);

        // Redirect to checkout with gift flag
        return RedirectToAction("GiftCheckout", new { giftId = result.GiftId });
    }

    /// <summary>
    /// صفحة دفع الهدية - Gift checkout page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GiftCheckout(int giftId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var gift = await _context.Set<GiftPurchase>()
            .FirstOrDefaultAsync(g => g.Id == giftId && g.PurchaserId == userId);

        if (gift == null)
        {
            SetErrorMessage("الهدية غير موجودة");
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new GiftCheckoutViewModel
        {
            GiftId = gift.Id,
            GiftCode = gift.GiftCode,
            ProductTitle = gift.ProductTitle ?? "منتج",
            RecipientName = gift.RecipientName,
            RecipientEmail = gift.RecipientEmail,
            PersonalMessage = gift.PersonalMessage,
            Amount = gift.Amount,
            Currency = "EGP",
            FormattedAmount = _currencyService.FormatAmount(gift.Amount, "EGP"),
            DeliveryDate = gift.DeliveryDate
        };

        return View(viewModel);
    }

    /// <summary>
    /// صفحة نجاح دفع الهدية - Gift payment success (sets message and redirects to Index)
    /// </summary>
    [HttpGet]
    public IActionResult GiftPaymentSuccess()
    {
        SetSuccessMessage("تم إرسال الهدية بنجاح! سيتم إبلاغ المستلم.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// استرداد هدية - Redeem gift
    /// </summary>
    [HttpGet]
    public IActionResult Redeem(string? code = null)
    {
        var viewModel = new RedeemGiftViewModel
        {
            GiftCode = code
        };
        return View(viewModel);
    }

    /// <summary>
    /// معالجة استرداد الهدية - Process gift redemption
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem(RedeemGiftViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        if (string.IsNullOrWhiteSpace(model.GiftCode))
        {
            ModelState.AddModelError(nameof(model.GiftCode), "يرجى إدخال رمز الهدية");
            return View(model);
        }

        var result = await _giftService.RedeemGiftAsync(model.GiftCode.Trim(), userId);

        if (!result.Success)
        {
            ModelState.AddModelError(nameof(model.GiftCode), result.ErrorMessage ?? "حدث خطأ");
            return View(model);
        }

        // Show success page with gift details
        var successViewModel = new GiftRedemptionSuccessViewModel
        {
            ProductType = result.ProductType,
            ProductId = result.ProductId,
            ProductTitle = result.ProductTitle,
            SenderName = result.SenderName,
            PersonalMessage = result.PersonalMessage
        };

        return View("RedeemSuccess", successViewModel);
    }
}

#region View Models

public class SendGiftViewModel
{
    public ProductType ProductType { get; set; }
    public int ProductId { get; set; }
    public string? ProductTitle { get; set; }
    public string? ProductThumbnail { get; set; }
    public string? InstructorName { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? FormattedPrice { get; set; }

    [Required(ErrorMessage = "البريد الإلكتروني للمستلم مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    public string RecipientEmail { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RecipientName { get; set; }

    [MaxLength(500)]
    public string? PersonalMessage { get; set; }

    public bool SendNow { get; set; } = true;

    public DateTime? DeliveryDate { get; set; }
}

public class GiftCheckoutViewModel
{
    public int GiftId { get; set; }
    public string GiftCode { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? PersonalMessage { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? FormattedAmount { get; set; }
    public DateTime? DeliveryDate { get; set; }
}

public class RedeemGiftViewModel
{
    [Required(ErrorMessage = "رمز الهدية مطلوب")]
    public string? GiftCode { get; set; }
}

public class GiftRedemptionSuccessViewModel
{
    public ProductType ProductType { get; set; }
    public int ProductId { get; set; }
    public string? ProductTitle { get; set; }
    public string? SenderName { get; set; }
    public string? PersonalMessage { get; set; }
}

#endregion

