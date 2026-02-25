using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Entities.Books;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// نظام الدفع - Checkout Controller with Real Payments & Cart
/// </summary>
public class CheckoutController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly IShoppingCartService _cartService;
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly IFreeEnrollmentService _freeEnrollmentService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly IGiftPurchaseService _giftPurchaseService;
    private readonly IFlowValidationService _flowValidationService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        IShoppingCartService cartService,
        IPaymentGatewayService paymentGatewayService,
        IFreeEnrollmentService freeEnrollmentService,
        IInstructorNotificationService instructorNotificationService,
        IGiftPurchaseService giftPurchaseService,
        IFlowValidationService flowValidationService,
        ILogger<CheckoutController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _cartService = cartService;
        _paymentGatewayService = paymentGatewayService;
        _freeEnrollmentService = freeEnrollmentService;
        _instructorNotificationService = instructorNotificationService;
        _giftPurchaseService = giftPurchaseService;
        _flowValidationService = flowValidationService;
        _logger = logger;
    }

    /// <summary>
    /// التسجيل المجاني في دورة - Enroll in free course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FreeEnroll(int courseId, string? couponCode = null, bool returnJson = false)
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

        FreeEnrollmentResult result;

        if (!string.IsNullOrEmpty(couponCode))
        {
            result = await _freeEnrollmentService.EnrollWithFreeCouponAsync(userId, courseId, couponCode);
        }
        else
        {
            result = await _freeEnrollmentService.EnrollInFreeCourseAsync(userId, courseId);
        }

        if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (result.Success)
            {
                return Json(new { 
                    success = true, 
                    message = result.Message,
                    enrollmentId = result.EnrollmentId,
                    redirectUrl = Url.Action("Learn", "Courses", new { id = courseId })
                });
            }
            return Json(new { success = false, message = result.Message });
        }

        if (result.Success)
        {
            SetSuccessMessage(result.Message);
            return RedirectToAction("Learn", "Courses", new { id = courseId });
        }

        SetErrorMessage(result.Message);
        return RedirectToAction("Preview", "Courses", new { id = courseId });
    }

    /// <summary>
    /// التسجيل المجاني (GET) - Free enrollment landing
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FreeEnroll(int courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action("FreeEnroll", new { courseId }) });
        }

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == CourseStatus.Published);

        if (course == null)
        {
            SetErrorMessage("الدورة غير موجودة");
            return RedirectToAction("Index", "Courses");
        }

        var effectivePrice = course.DiscountPrice ?? course.Price;
        if (effectivePrice > 0)
        {
            // Not a free course, redirect to normal checkout
            return RedirectToAction("AddToCart", new { courseId });
        }

        // Direct free enrollment
        var result = await _freeEnrollmentService.EnrollInFreeCourseAsync(userId, courseId);

        if (result.Success)
        {
            SetSuccessMessage(result.Message);
            return RedirectToAction("Learn", "Courses", new { id = courseId });
        }

        SetErrorMessage(result.Message);
        return RedirectToAction("Preview", "Courses", new { id = courseId });
    }

    /// <summary>
    /// سلة التسوق - Shopping Cart
    /// </summary>
    public async Task<IActionResult> Cart()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Cart)) });
            }
            var cart = await _cartService.GetCartAsync(userId);
            
            // Validate cart items
            var errors = await _cartService.ValidateCartAsync(userId);
            if (errors.Any())
            {
                ViewBag.ValidationErrors = errors;
            }

            return View(cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cart for user");
            SetErrorMessage("حدث خطأ أثناء تحميل سلة التسوق");
            return RedirectToAction("Index", "Dashboard", new { area = "Student" });
        }
    }

    /// <summary>
    /// إضافة إلى السلة - Add to cart (GET - for direct URL access)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddToCart(int courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(AddToCart), new { courseId }) });
        }

        var flowResult = await _flowValidationService.ValidateCheckoutFlowAsync(userId, courseId);
        if (!flowResult.IsValid)
        {
            if (flowResult.ErrorCode == "FREE_COURSE")
                return RedirectToAction(nameof(FreeEnroll), new { courseId });
            SetErrorMessage(flowResult.ErrorMessage ?? "لا يمكن إضافة هذه الدورة إلى السلة");
            return Redirect(string.IsNullOrEmpty(flowResult.RedirectUrl) ? Url.Action("Preview", "Courses", new { area = "Student", id = courseId })! : flowResult.RedirectUrl);
        }

        var result = await _cartService.AddToCartAsync(userId, courseId);

        if (result.Success)
        {
            SetSuccessMessage(result.Message ?? "تمت الإضافة إلى السلة");
            return RedirectToAction(nameof(Cart));
        }

        SetErrorMessage(result.Message ?? "حدث خطأ أثناء الإضافة إلى السلة");
        return RedirectToAction("Preview", "Courses", new { area = "Student", id = courseId });
    }

    /// <summary>
    /// إضافة إلى السلة - Add to cart (POST - for AJAX calls and form submissions)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCartPost(int courseId, bool returnJson = false)
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

        var flowResult = await _flowValidationService.ValidateCheckoutFlowAsync(userId, courseId);
        if (!flowResult.IsValid)
        {
            if (flowResult.ErrorCode == "FREE_COURSE")
            {
                if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = flowResult.ErrorMessage, redirectUrl = Url.Action(nameof(FreeEnroll), new { courseId }) });
                return RedirectToAction(nameof(FreeEnroll), new { courseId });
            }
            if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = flowResult.ErrorMessage ?? "لا يمكن الإضافة", redirectUrl = flowResult.RedirectUrl });
            SetErrorMessage(flowResult.ErrorMessage ?? "لا يمكن إضافة هذه الدورة إلى السلة");
            return Redirect(string.IsNullOrEmpty(flowResult.RedirectUrl) ? Url.Action("Preview", "Courses", new { area = "Student", id = courseId })! : flowResult.RedirectUrl);
        }

        var result = await _cartService.AddToCartAsync(userId, courseId);

        // Check if AJAX request
        if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (result.Success)
            {
                return Json(new { success = true, message = result.Message ?? "تمت الإضافة إلى السلة" });
            }
            return Json(new { success = false, message = result.Message ?? "حدث خطأ أثناء الإضافة إلى السلة" });
        }
        
        // Regular form submission - redirect
        if (result.Success)
        {
            SetSuccessMessage(result.Message ?? "تمت الإضافة إلى السلة");
            return RedirectToAction(nameof(Cart));
        }

        SetErrorMessage(result.Message ?? "حدث خطأ أثناء الإضافة إلى السلة");
        return RedirectToAction("Preview", "Courses", new { area = "Student", id = courseId });
    }

    /// <summary>
    /// إزالة من السلة - Remove from cart
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int courseId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        var result = await _cartService.RemoveFromCartAsync(userId, courseId);

        if (result.Success)
        {
            SetSuccessMessage(result.Message!);
        }
        else
        {
            SetErrorMessage(result.Message!);
        }

        return RedirectToAction(nameof(Cart));
    }

    /// <summary>
    /// إضافة كتاب إلى السلة - Add book to cart (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddBookToCart(int bookId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(AddBookToCart), new { bookId }) });
        }

        var result = await _cartService.AddBookToCartAsync(userId, bookId);

        if (result.Success)
        {
            SetSuccessMessage(result.Message ?? "تمت إضافة الكتاب إلى السلة");
            return RedirectToAction(nameof(Cart));
        }

        SetErrorMessage(result.Message ?? "حدث خطأ أثناء الإضافة إلى السلة");
        return RedirectToAction("DetailsById", "Books", new { area = "Student", id = bookId });
    }

    /// <summary>
    /// إضافة كتاب إلى السلة - Add book to cart (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBookToCartPost(int bookId, bool returnJson = false)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
            }
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(AddBookToCart), new { bookId }) });
        }

        var result = await _cartService.AddBookToCartAsync(userId, bookId);

        if (returnJson || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (result.Success)
            {
                return Json(new { success = true, message = result.Message ?? "تمت إضافة الكتاب إلى السلة" });
            }
            return Json(new { success = false, message = result.Message ?? "حدث خطأ أثناء الإضافة" });
        }

        if (result.Success)
        {
            SetSuccessMessage(result.Message ?? "تمت إضافة الكتاب إلى السلة");
            return RedirectToAction(nameof(Cart));
        }

        SetErrorMessage(result.Message ?? "حدث خطأ أثناء الإضافة إلى السلة");
        return RedirectToAction("DetailsById", "Books", new { area = "Student", id = bookId });
    }

    /// <summary>
    /// إزالة كتاب من السلة - Remove book from cart
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBookFromCart(int bookId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        var result = await _cartService.RemoveBookFromCartAsync(userId, bookId);

        if (result.Success)
        {
            SetSuccessMessage(result.Message!);
        }
        else
        {
            SetErrorMessage(result.Message!);
        }

        return RedirectToAction(nameof(Cart));
    }

    /// <summary>
    /// تطبيق كوبون - Apply coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string couponCode)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { Success = false, Message = "يرجى تسجيل الدخول أولاً" });
        }
        var result = await _cartService.ApplyCouponAsync(userId, couponCode);

        return Json(result);
    }

    /// <summary>
    /// إزالة الكوبون - Remove coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoupon()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { Success = false, Message = "يرجى تسجيل الدخول أولاً" });
        }
        var result = await _cartService.RemoveCouponAsync(userId);

        return Json(result);
    }

    /// <summary>
    /// تطبيق بطاقة هدية - Apply gift card
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyGiftCard(string giftCardCode)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        try
        {
            // Validate gift card
            var giftCard = await _context.GiftCards
                .FirstOrDefaultAsync(g => g.Code == giftCardCode.ToUpperInvariant() && 
                                          g.Status == Domain.Entities.Payments.GiftCardStatus.Active);

            if (giftCard == null)
            {
                return Json(new { success = false, message = "بطاقة الهدية غير موجودة أو غير نشطة" });
            }

            if (giftCard.RemainingBalance <= 0)
            {
                return Json(new { success = false, message = "رصيد بطاقة الهدية صفر" });
            }

            if (giftCard.ExpiresAt.HasValue && giftCard.ExpiresAt.Value < DateTime.UtcNow)
            {
                return Json(new { success = false, message = "بطاقة الهدية منتهية الصلاحية" });
            }

            // Store gift card in session for checkout
            HttpContext.Session.SetString("AppliedGiftCard", giftCardCode);
            HttpContext.Session.SetString("GiftCardBalance", giftCard.RemainingBalance.ToString());

            return Json(new { 
                success = true, 
                message = $"تم تطبيق بطاقة الهدية بنجاح. الرصيد المتاح: {giftCard.RemainingBalance} {giftCard.Currency}",
                balance = giftCard.RemainingBalance,
                currency = giftCard.Currency
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying gift card {Code} for user {UserId}", giftCardCode, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء تطبيق بطاقة الهدية" });
        }
    }

    /// <summary>
    /// إزالة بطاقة الهدية - Remove gift card
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveGiftCard()
    {
        HttpContext.Session.Remove("AppliedGiftCard");
        HttpContext.Session.Remove("GiftCardBalance");
        
        return Json(new { success = true, message = "تمت إزالة بطاقة الهدية" });
    }

    /// <summary>
    /// تتبع رابط الإحالة - Track affiliate referral
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ref(string code, int? courseId = null)
    {
        try
        {
            // Store affiliate code in cookie for 30 days
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(30),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax
            };
            
            Response.Cookies.Append("affiliate_code", code, cookieOptions);
            
            if (courseId.HasValue)
            {
                Response.Cookies.Append("affiliate_course", courseId.Value.ToString(), cookieOptions);
            }

            // Log affiliate click
            _logger.LogInformation("Affiliate referral: Code={Code}, CourseId={CourseId}", code, courseId);

            // Redirect to course or homepage
            if (courseId.HasValue)
            {
                return RedirectToAction("Preview", "Courses", new { id = courseId.Value });
            }
            
            return RedirectToAction("Index", "Home", new { area = "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing affiliate referral");
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }

    /// <summary>
    /// صفحة الدفع - Checkout page
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Index)) });
        }

        try
        {
            // Get cart
            var cart = await _cartService.GetCartAsync(userId);
            
            if (!cart.Items.Any())
            {
                SetErrorMessage("سلة التسوق فارغة");
                return RedirectToAction(nameof(Cart));
            }

            // Validate cart
            var errors = await _cartService.ValidateCartAsync(userId);
            if (errors.Any())
            {
                SetErrorMessage("بعض العناصر في سلتك غير متاحة. يرجى المراجعة.");
                return RedirectToAction(nameof(Cart));
            }

            // Get user info
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            // Get user's saved payment methods
            var savedPaymentMethods = await _context.UserPaymentMethods
                .Where(pm => pm.UserId == userId && pm.IsActive)
                .OrderByDescending(pm => pm.IsDefault)
                .ToListAsync();

            // Get available payment gateways from database
            var availablePaymentGateways = await _context.PaymentGatewaySettings
                .Where(pg => pg.IsEnabled)
                .OrderBy(pg => pg.DisplayOrder)
                .Select(pg => new AvailablePaymentMethod
                {
                    Id = pg.Id,
                    Name = pg.Name,
                    DisplayName = pg.DisplayNameAr ?? pg.DisplayName ?? pg.Name,
                    Description = pg.DescriptionAr ?? pg.Description,
                    IconUrl = pg.IconUrl,
                    GatewayType = pg.GatewayType.ToString(),
                    IsDefault = pg.IsDefault,
                    SupportsRecurring = pg.SupportsRecurring,
                    SupportsSaveCard = pg.SupportsSaveCard,
                    SupportsInstallments = pg.SupportsInstallments,
                    MinAmount = pg.MinAmount,
                    MaxAmount = pg.MaxAmount
                })
                .ToListAsync();

            // Create view model
            var viewModel = new EnhancedCheckoutViewModel
            {
                Cart = cart,
                BillingEmail = user.Email ?? string.Empty,
                BillingName = user.FullName,
                BillingPhone = user.PhoneNumber,
                SavedPaymentMethods = savedPaymentMethods,
                AvailablePaymentGateways = availablePaymentGateways
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading checkout page for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة الدفع");
            return RedirectToAction(nameof(Cart));
        }
    }

    /// <summary>
    /// معالجة الدفع - Process payment with real gateway
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment([FromBody] EnhancedCheckoutViewModel? model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }
        if (model == null)
        {
            return Json(new { success = false, message = "بيانات الدفع غير صالحة" });
        }
        if (string.IsNullOrWhiteSpace(model.PaymentMethodId))
        {
            return Json(new { success = false, message = "يرجى اختيار أو إدخال طريقة الدفع" });
        }

        try
        {
            // Get cart
            var cart = await _cartService.GetCartAsync(userId);
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                return Json(new { success = false, message = "سلة التسوق فارغة" });
            }

            // Validate cart
            var errors = await _cartService.ValidateCartAsync(userId);
            if (errors.Any())
            {
                return Json(new { success = false, message = "بعض العناصر غير متاحة", errors });
            }

            PaymentIntentResult? paymentIntentResult = null;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Get or create Stripe customer
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    throw new InvalidOperationException("User not found");
                }
                
                string? stripeCustomerId = user.StripeCustomerId;

                if (string.IsNullOrEmpty(stripeCustomerId))
                {
                    var customerResult = await _paymentGatewayService.CreateCustomerAsync(new CreateCustomerRequest
                    {
                        Email = user.Email ?? throw new InvalidOperationException("User email is required"),
                        Name = user.FullName ?? "Customer",
                        Phone = user.PhoneNumber,
                        Metadata = new Dictionary<string, string>
                        {
                            { "UserId", userId }
                        }
                    });

                    if (customerResult.Success && customerResult.CustomerId != null)
                    {
                        stripeCustomerId = customerResult.CustomerId;
                        user.StripeCustomerId = stripeCustomerId;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        throw new Exception("Failed to create Stripe customer");
                    }
                }

                // Create payment intent
                var courseCount = cart.Items.Count(i => i.ItemType == ProductType.Course);
                var bookCount = cart.Items.Count(i => i.ItemType == ProductType.Book);
                var description = new List<string>();
                if (courseCount > 0) description.Add($"{courseCount} course(s)");
                if (bookCount > 0) description.Add($"{bookCount} book(s)");

                paymentIntentResult = await _paymentGatewayService.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
                {
                    Amount = cart.Totals.Total,
                    Currency = cart.Totals.Currency,
                    CustomerId = stripeCustomerId,
                    PaymentMethodId = model.PaymentMethodId,
                    Description = $"Purchase of {string.Join(" and ", description)}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "UserId", userId },
                        { "ItemCount", cart.Items.Count.ToString() },
                        { "CourseCount", courseCount.ToString() },
                        { "BookCount", bookCount.ToString() },
                        { "CouponCode", cart.CouponCode ?? "" }
                    }
                });

                if (!paymentIntentResult.Success)
                {
                    throw new InvalidOperationException(paymentIntentResult.ErrorMessage);
                }

                // Create payment records for each item (course or book)
                var paymentRecords = new List<Payment>();
                
                var totalItemPrices = cart.Items.Sum(i => i.FinalPrice);

                foreach (var item in cart.Items)
                {
                    var payment = new Payment
                    {
                        TransactionId = paymentIntentResult.PaymentIntentId!,
                        StudentId = userId,
                        CourseId = item.ItemType == ProductType.Course ? item.CourseId : null,
                        BookId = item.ItemType == ProductType.Book ? item.BookId : null,
                        BundleId = item.ItemType == ProductType.CourseBundle ? (item.BundleId ?? item.ItemId) : null,
                        BookBundleId = item.ItemType == ProductType.BookBundle ? (item.BundleId ?? item.ItemId) : null,
                        ProductType = item.ItemType,
                        PurchaseType = item.ItemType == ProductType.Book ? "Book" : (item.ItemType == ProductType.CourseBundle ? "CourseBundle" : (item.ItemType == ProductType.BookBundle ? "BookBundle" : "Course")),
                        OriginalAmount = item.Price,
                        DiscountAmount = (item.Price - item.FinalPrice) +
                                       (totalItemPrices > 0 ? Math.Round(cart.DiscountAmount * (item.FinalPrice / totalItemPrices), 2) : 0),
                        TaxAmount = totalItemPrices > 0 ? Math.Round(cart.Totals.TaxAmount * (item.FinalPrice / totalItemPrices), 2) : 0,
                        TaxPercentage = cart.Totals.TaxRate,
                        TotalAmount = item.FinalPrice
                                     - (totalItemPrices > 0 ? Math.Round(cart.DiscountAmount * (item.FinalPrice / totalItemPrices), 2) : 0)
                                     + (totalItemPrices > 0 ? Math.Round(cart.Totals.TaxAmount * (item.FinalPrice / totalItemPrices), 2) : 0),
                        Currency = cart.Totals.Currency,
                        CouponCode = cart.CouponCode,
                        Provider = PaymentProvider.Stripe,
                        PaymentMethod = "card",
                        Status = PaymentStatus.Pending,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    };

                    _context.Payments.Add(payment);
                    paymentRecords.Add(payment);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment intent created for user {UserId}, PaymentIntent: {PaymentIntentId}", 
                    userId, paymentIntentResult.PaymentIntentId);
            });

            if (paymentIntentResult != null && !paymentIntentResult.Success)
            {
                return Json(new { success = false, message = paymentIntentResult.ErrorMessage });
            }

            // Audit: payment intent created
            if (paymentIntentResult != null && !string.IsNullOrEmpty(paymentIntentResult.PaymentIntentId))
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = userId,
                    ActivityType = "PaymentIntentCreated",
                    Description = "Payment intent created for checkout",
                    EntityType = "Payment",
                    EntityName = paymentIntentResult.PaymentIntentId,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                clientSecret = paymentIntentResult?.ClientSecret,
                paymentIntentId = paymentIntentResult?.PaymentIntentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة الدفع" });
        }
    }

    /// <summary>
    /// تأكيد نجاح الدفع - Confirm payment success (called after Stripe confirmation)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(string paymentIntentId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return Json(new { success = false, message = "معرف الدفع غير صالح" });
        }

        try
        {
            // Confirm payment with Stripe
            var confirmationResult = await _paymentGatewayService.ConfirmPaymentAsync(paymentIntentId);

            if (!confirmationResult.Success || confirmationResult.Status != "succeeded")
            {
                return Json(new { success = false, message = "فشل تأكيد الدفع" });
            }

            // Update payment records and create enrollments/book purchases
            var payments = await _context.Payments
                .Include(p => p.Course)
                    .ThenInclude(c => c!.Instructor)
                .Include(p => p.Book)
                    .ThenInclude(b => b!.Instructor)
                .Where(p => p.TransactionId == paymentIntentId && p.StudentId == userId)
                .ToListAsync();

            if (!payments.Any())
            {
                return Json(new { success = false, message = "لم يتم العثور على بيانات الدفع" });
            }

            int enrollmentCount = 0;
            int bookPurchaseCount = 0;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                foreach (var payment in payments)
                {
                    // Idempotency: skip already-completed payments to prevent duplicate processing
                    if (payment.Status == PaymentStatus.Completed)
                    {
                        _logger.LogInformation("Payment {PaymentId} already completed, skipping duplicate processing", payment.Id);
                        continue;
                    }

                    // Update payment status
                    payment.Status = PaymentStatus.Completed;
                    payment.CompletedAt = DateTime.UtcNow;

                    // Create invoice
                    var invoice = new Invoice
                    {
                        InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{payment.Id:D6}",
                        PaymentId = payment.Id,
                        StudentId = userId,
                        IssuedDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow,
                        Status = "Paid",
                        SubTotal = payment.OriginalAmount,
                        TaxAmount = payment.TaxAmount,
                        DiscountAmount = payment.DiscountAmount,
                        TotalAmount = payment.TotalAmount,
                        Currency = payment.Currency
                    };

                    _context.Invoices.Add(invoice);

                    if (payment.ProductType == ProductType.Course && payment.CourseId.HasValue)
                    {
                        // Guard: Check if student is already enrolled
                        var alreadyEnrolled = await _context.Enrollments
                            .AnyAsync(e => e.StudentId == userId && e.CourseId == payment.CourseId.Value
                                && e.Status != EnrollmentStatus.Cancelled && e.Status != EnrollmentStatus.Refunded);
                        if (alreadyEnrolled)
                        {
                            _logger.LogWarning("Student {UserId} already enrolled in course {CourseId}, skipping duplicate enrollment",
                                userId, payment.CourseId.Value);
                            continue;
                        }

                        // Guard: Skip if course is deleted
                        if (payment.Course == null || payment.Course.IsDeleted)
                        {
                            _logger.LogWarning("Course {CourseId} is deleted or null, skipping enrollment for payment {PaymentId}",
                                payment.CourseId, payment.Id);
                            payment.Status = PaymentStatus.Failed;
                            payment.FailureReason = "Course no longer available";
                            continue;
                        }

                        // Create course enrollment
                        var enrollment = new Enrollment
                        {
                            StudentId = userId,
                            CourseId = payment.CourseId.Value,
                            Status = EnrollmentStatus.Active,
                            EnrolledAt = DateTime.UtcNow,
                            PaidAmount = payment.TotalAmount,
                            Currency = payment.Currency,
                            CouponCode = payment.CouponCode,
                            DiscountAmount = payment.DiscountAmount,
                            IsFree = payment.TotalAmount == 0,
                            TotalLessons = await _context.Lessons
                                .CountAsync(l => l.Module.CourseId == payment.CourseId)
                        };

                        _context.Enrollments.Add(enrollment);
                        await _context.SaveChangesAsync();

                        payment.EnrollmentId = enrollment.Id;

                        // Calculate instructor earnings for course
                        await CreateInstructorEarningsAsync(payment);

                        // Update course stats
                        if (payment.Course != null)
                        {
                            payment.Course.TotalStudents++;
                        }

                        // Send course enrollment notifications
                        await SendEnrollmentNotificationsAsync(payment, enrollment);
                        enrollmentCount++;
                    }
                    else if (payment.ProductType == ProductType.Book && payment.BookId.HasValue)
                    {
                        // Guard: Check if book already purchased
                        var alreadyPurchased = await _context.BookPurchases
                            .AnyAsync(bp => bp.StudentId == userId && bp.BookId == payment.BookId.Value && bp.IsActive);
                        if (alreadyPurchased)
                        {
                            _logger.LogWarning("Student {UserId} already purchased book {BookId}, skipping duplicate",
                                userId, payment.BookId.Value);
                            continue;
                        }

                        // Guard: Skip if book is deleted
                        if (payment.Book == null || payment.Book.IsDeleted)
                        {
                            _logger.LogWarning("Book {BookId} is deleted or null, skipping purchase for payment {PaymentId}",
                                payment.BookId, payment.Id);
                            payment.Status = PaymentStatus.Failed;
                            payment.FailureReason = "Book no longer available";
                            continue;
                        }

                        // Create book purchase
                        var bookPurchase = new BookPurchase
                        {
                            StudentId = userId,
                            BookId = payment.BookId.Value,
                            PaymentId = payment.Id,
                            PurchaseType = BookPurchaseType.Digital,
                            PurchasedFormat = payment.Book?.AvailableFormats ?? BookFormat.PDF,
                            PaidAmount = payment.TotalAmount,
                            Currency = payment.Currency,
                            DiscountAmount = payment.DiscountAmount,
                            CouponCode = payment.CouponCode,
                            PurchasedAt = DateTime.UtcNow,
                            IsActive = true,
                            MaxDownloads = 5,
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                        };

                        _context.BookPurchases.Add(bookPurchase);
                        await _context.SaveChangesAsync();

                        // Calculate instructor earnings for book
                        await CreateBookEarningsAsync(payment);

                        // Update book stats
                        if (payment.Book != null)
                        {
                            payment.Book.TotalSales++;
                        }

                        // Send book purchase notifications
                        await SendBookPurchaseNotificationsAsync(payment);
                        bookPurchaseCount++;
                    }
                    else if (payment.ProductType == ProductType.CourseBundle && payment.BundleId.HasValue)
                    {
                        var bundle = await _context.CourseBundles
                            .Include(b => b.BundleCourses)
                            .ThenInclude(bc => bc.Course)
                            .FirstOrDefaultAsync(b => b.Id == payment.BundleId.Value && b.IsActive);

                        if (bundle != null)
                        {
                            foreach (var bc in bundle.BundleCourses.Where(bc => bc.Course != null && !bc.Course.IsDeleted))
                            {
                                var alreadyEnrolled = await _context.Enrollments
                                    .AnyAsync(e => e.StudentId == userId && e.CourseId == bc.CourseId
                                        && e.Status != EnrollmentStatus.Cancelled && e.Status != EnrollmentStatus.Refunded);
                                if (alreadyEnrolled) continue;

                                var totalLessons = await _context.Lessons.CountAsync(l => l.Module.CourseId == bc.CourseId);
                                _context.Enrollments.Add(new Enrollment
                                {
                                    StudentId = userId,
                                    CourseId = bc.CourseId,
                                    Status = EnrollmentStatus.Active,
                                    EnrolledAt = DateTime.UtcNow,
                                    PaidAmount = 0,
                                    Currency = payment.Currency,
                                    CouponCode = payment.CouponCode,
                                    DiscountAmount = 0,
                                    IsFree = false,
                                    TotalLessons = totalLessons
                                });
                                enrollmentCount++;
                            }
                        }
                    }
                    else if (payment.ProductType == ProductType.BookBundle && payment.BookBundleId.HasValue)
                    {
                        var bundle = await _context.BookBundles
                            .Include(b => b.Items)
                            .ThenInclude(bi => bi.Book)
                            .FirstOrDefaultAsync(b => b.Id == payment.BookBundleId.Value && b.IsActive);

                        if (bundle?.Items != null)
                        {
                            foreach (var bi in bundle.Items.Where(bi => bi.Book != null && !bi.Book.IsDeleted))
                            {
                                var alreadyPurchased = await _context.BookPurchases
                                    .AnyAsync(bp => bp.StudentId == userId && bp.BookId == bi.BookId && bp.IsActive);
                                if (alreadyPurchased) continue;

                                _context.BookPurchases.Add(new BookPurchase
                                {
                                    StudentId = userId,
                                    BookId = bi.BookId,
                                    PaymentId = payment.Id,
                                    PurchaseType = BookPurchaseType.Digital,
                                    PurchasedFormat = bi.Book.AvailableFormats,
                                    PaidAmount = 0,
                                    Currency = payment.Currency,
                                    DiscountAmount = 0,
                                    CouponCode = payment.CouponCode,
                                    PurchasedAt = DateTime.UtcNow,
                                    IsActive = true,
                                    MaxDownloads = 5,
                                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                                });
                                bookPurchaseCount++;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
            });

            // Consume gift card balance if applied
            var appliedGiftCardCode = HttpContext.Session.GetString("AppliedGiftCard");
            if (!string.IsNullOrEmpty(appliedGiftCardCode))
            {
                try
                {
                    var giftCard = await _context.GiftCards
                        .FirstOrDefaultAsync(g => g.Code == appliedGiftCardCode.ToUpperInvariant()
                            && g.Status == Domain.Entities.Payments.GiftCardStatus.Active);

                    if (giftCard != null && giftCard.RemainingBalance > 0)
                    {
                        var totalPaid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.TotalAmount);
                        var amountToDeduct = Math.Min(giftCard.RemainingBalance, totalPaid);

                        if (amountToDeduct > 0)
                        {
                            var transaction = new Domain.Entities.Payments.GiftCardTransaction
                            {
                                GiftCardId = giftCard.Id,
                                Type = Domain.Entities.Payments.GiftCardTransactionType.Redemption,
                                Amount = amountToDeduct,
                                BalanceBefore = giftCard.RemainingBalance,
                                BalanceAfter = giftCard.RemainingBalance - amountToDeduct,
                                Description = $"Payment for {payments.Count} item(s)",
                                UserId = userId,
                                TransactionDate = DateTime.UtcNow,
                                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                            };

                            giftCard.RemainingBalance -= amountToDeduct;
                            giftCard.LastUsedAt = DateTime.UtcNow;

                            if (giftCard.RemainingBalance <= 0)
                            {
                                giftCard.Status = Domain.Entities.Payments.GiftCardStatus.Redeemed;
                            }

                            _context.GiftCardTransactions.Add(transaction);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation("Gift card {Code} consumed {Amount} for user {UserId}",
                                appliedGiftCardCode, amountToDeduct, userId);
                        }
                    }

                    HttpContext.Session.Remove("AppliedGiftCard");
                    HttpContext.Session.Remove("GiftCardBalance");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming gift card {Code} for user {UserId}", appliedGiftCardCode, userId);
                    // Don't fail the payment over gift card issue
                }
            }

            // Clear cart
            await _cartService.ClearCartAsync(userId);

            _logger.LogInformation("Payment confirmed for {CourseCount} courses and {BookCount} books, user {UserId}", 
                enrollmentCount, bookPurchaseCount, userId);

            // Audit: payment confirmed
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                ActivityType = "PaymentConfirmed",
                Description = "Payment confirmed; enrollments and invoices created",
                EntityType = "Payment",
                EntityName = paymentIntentId,
                Details = $"{{\"enrollmentCount\":{enrollmentCount},\"bookPurchaseCount\":{bookPurchaseCount}}}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم الشراء بنجاح",
                enrollmentCount,
                bookPurchaseCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment {PaymentIntentId} for user {UserId}", 
                paymentIntentId, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء تأكيد الدفع" });
        }
    }

    /// <summary>
    /// صفحة النجاح - Success page (supports paymentIntentId from checkout or paymentId from Payment Retry)
    /// </summary>
    public async Task<IActionResult> Success(string? paymentIntentId, int? paymentId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (string.IsNullOrWhiteSpace(paymentIntentId) && (!paymentId.HasValue || paymentId.Value <= 0))
            return NotFound();

        List<Payment> payments;
        if (!string.IsNullOrWhiteSpace(paymentIntentId))
        {
            payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Enrollment)
                .Include(p => p.Book)
                .Where(p => p.TransactionId == paymentIntentId && p.StudentId == userId)
                .ToListAsync();
        }
        else
        {
            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Enrollment)
                .Include(p => p.Book)
                .FirstOrDefaultAsync(p => p.Id == paymentId!.Value && p.StudentId == userId);
            if (payment == null || string.IsNullOrEmpty(payment.TransactionId))
                return NotFound();
            payments = new List<Payment> { payment };
            paymentIntentId = payment.TransactionId;
        }

        if (!payments.Any())
            return NotFound();

        var coursePayments = payments.Where(p => p.ProductType == ProductType.Course && p.Course != null).ToList();
        var bookPayments = payments.Where(p => p.ProductType == ProductType.Book && p.Book != null).ToList();

        var viewModel = new PaymentSuccessViewModel
        {
            TransactionId = paymentIntentId,
            TotalAmount = payments.Sum(p => p.TotalAmount),
            Currency = payments.First().Currency,
            CoursesCount = coursePayments.Count,
            BooksCount = bookPayments.Count,
            Courses = coursePayments.Select(p => new PurchasedCourseInfo
            {
                CourseId = p.CourseId!.Value,
                CourseName = p.Course!.Title,
                EnrollmentId = p.EnrollmentId ?? 0
            }).ToList(),
            Books = bookPayments.Select(p => new PurchasedBookInfo
            {
                BookId = p.BookId!.Value,
                BookTitle = p.Book!.Title,
                CoverImageUrl = p.Book.CoverImageUrl
            }).ToList(),
            CompletedAt = payments.First().CompletedAt ?? DateTime.UtcNow
        };

        return View(viewModel);
    }

    /// <summary>
    /// معالجة دفع الهدية - Process gift payment (create Stripe PaymentIntent)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessGiftPayment([FromBody] ProcessGiftPaymentRequest? model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        if (model == null || model.GiftId <= 0 || string.IsNullOrWhiteSpace(model.PaymentMethodId))
            return Json(new { success = false, message = "بيانات الدفع غير صالحة" });

        try
        {
            var gift = await _context.Set<GiftPurchase>()
                .FirstOrDefaultAsync(g => g.Id == model.GiftId && g.PurchaserId == userId);
            if (gift == null)
                return Json(new { success = false, message = "الهدية غير موجودة" });
            if (gift.Status != GiftStatus.Pending)
                return Json(new { success = false, message = "تم دفع هذه الهدية مسبقاً" });
            if (gift.Amount <= 0)
                return Json(new { success = false, message = "مبلغ الهدية غير صالح" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود" });

            string? stripeCustomerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                var customerResult = await _paymentGatewayService.CreateCustomerAsync(new CreateCustomerRequest
                {
                    Email = user.Email ?? "",
                    Name = user.FullName ?? "Customer",
                    Phone = user.PhoneNumber,
                    Metadata = new Dictionary<string, string> { { "UserId", userId } }
                });
                if (!customerResult.Success || string.IsNullOrEmpty(customerResult.CustomerId))
                    return Json(new { success = false, message = "فشل إنشاء حساب الدفع" });
                stripeCustomerId = customerResult.CustomerId;
                user.StripeCustomerId = stripeCustomerId;
                await _context.SaveChangesAsync();
            }

            var paymentIntentResult = await _paymentGatewayService.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
            {
                Amount = gift.Amount,
                Currency = gift.Currency ?? "EGP",
                CustomerId = stripeCustomerId,
                PaymentMethodId = model.PaymentMethodId,
                Description = $"هدية: {gift.ProductTitle ?? "منتج"}",
                Metadata = new Dictionary<string, string>
                {
                    { "UserId", userId },
                    { "type", "gift" },
                    { "giftId", gift.Id.ToString() }
                }
            });

            if (!paymentIntentResult.Success || string.IsNullOrEmpty(paymentIntentResult.PaymentIntentId))
                return Json(new { success = false, message = paymentIntentResult.ErrorMessage ?? "فشل إنشاء طلب الدفع" });

            var payment = new Payment
            {
                TransactionId = paymentIntentResult.PaymentIntentId,
                StudentId = userId,
                OriginalAmount = gift.Amount,
                TotalAmount = gift.Amount,
                Currency = gift.Currency ?? "EGP",
                ProductType = ProductType.Course,
                PurchaseType = "Gift",
                Status = PaymentStatus.Pending,
                Provider = PaymentProvider.Stripe,
                PaymentMethod = "card",
                Metadata = JsonSerializer.Serialize(new { type = "gift", giftId = gift.Id }),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Gift payment intent created for gift {GiftId}, user {UserId}", gift.Id, userId);
            return Json(new
            {
                success = true,
                clientSecret = paymentIntentResult.ClientSecret,
                paymentIntentId = paymentIntentResult.PaymentIntentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing gift payment for gift {GiftId}", model.GiftId);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة الدفع" });
        }
    }

    /// <summary>
    /// تأكيد دفع الهدية - Confirm gift payment after Stripe success
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmGiftPayment([FromBody] ConfirmGiftPaymentRequest? model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        if (model == null || string.IsNullOrWhiteSpace(model.PaymentIntentId))
            return Json(new { success = false, message = "معرف الدفع غير صالح" });

        try
        {
            var confirmationResult = await _paymentGatewayService.ConfirmPaymentAsync(model.PaymentIntentId);
            if (!confirmationResult.Success || confirmationResult.Status != "succeeded")
                return Json(new { success = false, message = "فشل تأكيد الدفع" });

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == model.PaymentIntentId && p.StudentId == userId);
            if (payment == null)
                return Json(new { success = false, message = "لم يتم العثور على بيانات الدفع" });

            if (payment.Status == PaymentStatus.Completed)
            {
                _logger.LogInformation("Gift payment {PaymentId} already completed", payment.Id);
                return Json(new { success = true });
            }

            int? giftId = null;
            if (!string.IsNullOrEmpty(payment.Metadata))
            {
                try
                {
                    var meta = JsonSerializer.Deserialize<JsonElement>(payment.Metadata);
                    if (meta.TryGetProperty("giftId", out var g) && g.TryGetInt32(out var id))
                        giftId = id;
                }
                catch { /* ignore */ }
            }
            if (!giftId.HasValue)
                return Json(new { success = false, message = "بيانات الدفع غير مكتملة" });

            await _context.ExecuteInTransactionAsync(async () =>
            {
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var confirmResult = await _giftPurchaseService.ConfirmGiftPaymentAsync(giftId.Value, payment.Id);
                if (!confirmResult.Success)
                    throw new InvalidOperationException(confirmResult.ErrorMessage ?? "فشل تأكيد الهدية");
            });

            _logger.LogInformation("Gift {GiftId} payment confirmed for user {UserId}", giftId.Value, userId);
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming gift payment");
            return Json(new { success = false, message = "حدث خطأ أثناء التأكيد" });
        }
    }

    /// <summary>
    /// الشراء السريع لدورة واحدة - Quick buy single course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> QuickBuy(int courseId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action("QuickBuy", new { courseId }) });
        }

        var flowResult = await _flowValidationService.ValidateCheckoutFlowAsync(userId, courseId);
        if (!flowResult.IsValid)
        {
            if (flowResult.ErrorCode == "FREE_COURSE")
                return RedirectToAction(nameof(FreeEnroll), new { courseId });
            SetErrorMessage(flowResult.ErrorMessage ?? "لا يمكن شراء هذه الدورة");
            return Redirect(string.IsNullOrEmpty(flowResult.RedirectUrl) ? Url.Action("Preview", "Courses", new { area = "Student", id = courseId })! : flowResult.RedirectUrl);
        }

        // Add to cart and check result
        var cartResult = await _cartService.AddToCartAsync(userId, courseId);
        if (!cartResult.Success)
        {
            SetErrorMessage(cartResult.Message ?? "حدث خطأ أثناء إضافة الدورة إلى السلة");
            return RedirectToAction("Preview", "Courses", new { area = "Student", id = courseId });
        }

        // Redirect to checkout
        return RedirectToAction(nameof(Index));
    }

    #region Private Helper Methods

    private async Task CreateInstructorEarningsAsync(Payment payment)
    {
        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == payment.Course!.InstructorId);

        if (instructorProfile != null)
        {
            var (platformCommission, instructorAmount) = BusinessRuleHelper.CalculateCommission(
                payment.TotalAmount, 
                instructorProfile.CommissionRate);

            var earning = new Domain.Entities.Financial.InstructorEarning
            {
                InstructorId = payment.Course!.InstructorId,
                CourseId = payment.CourseId,
                PaymentId = payment.Id,
                EarningType = "sale",
                GrossAmount = payment.TotalAmount,
                PlatformCommissionRate = 100 - instructorProfile.CommissionRate,
                PlatformCommission = platformCommission,
                InstructorRate = instructorProfile.CommissionRate,
                NetAmount = instructorAmount,
                Currency = payment.Currency,
                Status = "pending",
                AvailableDate = BusinessRuleHelper.CalculateEarningsAvailabilityDate(DateTime.UtcNow)
            };

            instructorProfile.TotalEarnings += instructorAmount;
            instructorProfile.PendingBalance += instructorAmount;
            _context.InstructorEarnings.Add(earning);
        }
    }

    private async Task SendEnrollmentNotificationsAsync(Payment payment, Enrollment enrollment)
    {
        var student = await _context.Users.FindAsync(payment.StudentId);
        
        // Notify student
        if (student?.Email != null && payment.Course != null)
        {
            await _emailService.SendEnrollmentConfirmationAsync(
                student.Email,
                payment.Course.Title,
                student.FullName
            );

            var studentNotification = new Notification
            {
                UserId = payment.StudentId,
                Title = "مرحباً بك في الدورة! 🎉",
                Message = $"تم تسجيلك بنجاح في دورة {payment.Course.Title}. يمكنك البدء في التعلم الآن!",
                Type = NotificationType.NewEnrollment,
                ActionUrl = $"/Student/Courses/Learn/{payment.CourseId}",
                ActionText = "ابدأ التعلم",
                IsRead = false
            };
            _context.Notifications.Add(studentNotification);
        }

        // Notify instructor (unified path: DB + SignalR + Web Push via IInstructorNotificationService)
        if (!string.IsNullOrEmpty(payment.Course?.InstructorId))
        {
            var studentName = student?.FullName ?? student?.FirstName ?? "طالب";
            _ = await _instructorNotificationService.NotifyNewEnrollmentAsync(
                payment.Course.InstructorId,
                payment.CourseId!.Value,
                payment.StudentId,
                studentName);
        }
    }

    private async Task CreateBookEarningsAsync(Payment payment)
    {
        if (payment.Book == null) return;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == payment.Book.InstructorId);

        if (instructorProfile != null)
        {
            var (platformCommission, instructorAmount) = BusinessRuleHelper.CalculateCommission(
                payment.TotalAmount, 
                instructorProfile.CommissionRate);

            var earning = new Domain.Entities.Financial.InstructorEarning
            {
                InstructorId = payment.Book.InstructorId,
                BookId = payment.BookId,
                PaymentId = payment.Id,
                EarningType = "book_sale",
                GrossAmount = payment.TotalAmount,
                PlatformCommissionRate = 100 - instructorProfile.CommissionRate,
                PlatformCommission = platformCommission,
                InstructorRate = instructorProfile.CommissionRate,
                NetAmount = instructorAmount,
                Currency = payment.Currency,
                Status = "pending",
                AvailableDate = BusinessRuleHelper.CalculateEarningsAvailabilityDate(DateTime.UtcNow)
            };

            instructorProfile.TotalEarnings += instructorAmount;
            instructorProfile.PendingBalance += instructorAmount;
            _context.InstructorEarnings.Add(earning);
        }
    }

    private async Task SendBookPurchaseNotificationsAsync(Payment payment)
    {
        var student = await _context.Users.FindAsync(payment.StudentId);
        
        // Notify student
        if (student?.Email != null && payment.Book != null)
        {
            var studentNotification = new Notification
            {
                UserId = payment.StudentId,
                Title = "تم شراء الكتاب بنجاح! 📚",
                Message = $"أصبح كتاب \"{payment.Book.Title}\" متاحاً لك الآن. يمكنك تحميله من مكتبتك.",
                Type = NotificationType.Purchase,
                ActionUrl = "/Student/Books/MyLibrary",
                ActionText = "عرض مكتبتي",
                IsRead = false
            };
            _context.Notifications.Add(studentNotification);
        }

        // Notify instructor
        if (payment.Book?.Instructor?.Email != null)
        {
            var instructorNotification = new Notification
            {
                UserId = payment.Book.InstructorId,
                Title = $"عملية بيع جديدة لكتاب {payment.Book.Title}",
                Message = $"قام {student?.FullName} بشراء كتابك \"{payment.Book.Title}\"",
                Type = NotificationType.Sale,
                ActionUrl = $"/Instructor/Books/Details/{payment.BookId}",
                ActionText = "عرض الكتاب",
                IsRead = false
            };
            _context.Notifications.Add(instructorNotification);
        }
    }

    #endregion
}

/// <summary>
/// نموذج الدفع المحسّن - Enhanced checkout view model
/// </summary>
public class EnhancedCheckoutViewModel
{
    public ShoppingCartInfo Cart { get; set; } = new();
    public string BillingName { get; set; } = string.Empty;
    public string BillingEmail { get; set; } = string.Empty;
    public string? BillingPhone { get; set; }
    public string? PaymentMethodId { get; set; }
    public bool SavePaymentMethod { get; set; }
    public List<Domain.Entities.Payments.UserPaymentMethod> SavedPaymentMethods { get; set; } = new();
    public List<AvailablePaymentMethod> AvailablePaymentGateways { get; set; } = new();
}

/// <summary>
/// طريقة دفع متاحة - Available payment method
/// </summary>
public class AvailablePaymentMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string GatewayType { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool SupportsRecurring { get; set; }
    public bool SupportsSaveCard { get; set; }
    public bool SupportsInstallments { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
}

/// <summary>
/// طلب معالجة دفع الهدية - Process gift payment request
/// </summary>
public class ProcessGiftPaymentRequest
{
    public int GiftId { get; set; }
    public string? PaymentMethodId { get; set; }
}

/// <summary>
/// طلب تأكيد دفع الهدية - Confirm gift payment request
/// </summary>
public class ConfirmGiftPaymentRequest
{
    public string? PaymentIntentId { get; set; }
}

/// <summary>
/// نموذج نجاح الدفع - Payment success view model
/// </summary>
public class PaymentSuccessViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public int CoursesCount { get; set; }
    public int BooksCount { get; set; }
    public List<PurchasedCourseInfo> Courses { get; set; } = new();
    public List<PurchasedBookInfo> Books { get; set; } = new();
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// معلومات الدورة المشتراة - Purchased course info
/// </summary>
public class PurchasedCourseInfo
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int EnrollmentId { get; set; }
}

/// <summary>
/// معلومات الكتاب المشترى - Purchased book info
/// </summary>
public class PurchasedBookInfo
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
}

