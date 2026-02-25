using System.Text.Json;
using LMS.Areas.Admin.Controllers;
using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Books;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using LMS.Services.PaymentGateways;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// متحكم الدفع متعدد البوابات - Multi-Gateway Checkout Controller
/// Supports all payment methods for Egyptian and Gulf markets
/// </summary>
public class MultiGatewayCheckoutController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IShoppingCartService _cartService;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ICurrencyService _currencyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MultiGatewayCheckoutController> _logger;

    public MultiGatewayCheckoutController(
        ApplicationDbContext context,
        IShoppingCartService cartService,
        IPaymentGatewayFactory gatewayFactory,
        ICurrencyService currencyService,
        ICurrentUserService currentUserService,
        ILogger<MultiGatewayCheckoutController> logger)
    {
        _context = context;
        _cartService = cartService;
        _gatewayFactory = gatewayFactory;
        _currencyService = currencyService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// صفحة الدفع - Checkout page with all available payment methods
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Index)) });
        }

        var cart = await _cartService.GetCartAsync(userId);
        if (cart == null || !cart.Items.Any())
        {
            SetWarningMessage("سلة التسوق فارغة");
            return RedirectToAction("Index", "Courses", new { area = "Student" });
        }

        // Get user info
        var user = await _context.Users.FindAsync(userId);

        // Get user country (default to Egypt)
        var userCountry = user?.Country ?? "EG";

        // Get available payment methods
        var availableMethods = await _gatewayFactory.GetAvailableMethodsAsync(
            userCountry, 
            cart.TotalAmount, 
            "EGP");

        // Get saved payment methods
        var savedPaymentMethods = await _context.Set<UserPaymentMethod>()
            .Where(pm => pm.UserId == userId && pm.IsActive)
            .ToListAsync();

        // Determine recommended gateway
        var recommendedGateway = GetRecommendedGateway(userCountry, cart.TotalAmount);

        var viewModel = new MultiGatewayCheckoutViewModel
        {
            Cart = cart,
            AvailablePaymentMethods = availableMethods,
            UserCountry = userCountry,
            RecommendedGateway = recommendedGateway,
            BillingName = user?.FullName ?? "",
            BillingEmail = user?.Email ?? "",
            BillingPhone = user?.PhoneNumber,
            SavedPaymentMethods = savedPaymentMethods
        };

        return View(viewModel);
    }

    /// <summary>
    /// بدء الدفع - Process payment with selected gateway
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(MultiGatewayCheckoutRequest request)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new PaymentInitiationResult
            {
                Success = false,
                Message = "يرجى تسجيل الدخول أولاً"
            });
        }

        try
        {
            var cart = await _cartService.GetCartAsync(userId);
            if (cart == null || !cart.Items.Any())
            {
                return Json(new PaymentInitiationResult
                {
                    Success = false,
                    Message = "سلة التسوق فارغة"
                });
            }

            var user = await _context.Users.FindAsync(userId);

            _logger.LogInformation("Processing payment for user {UserId} with gateway {Gateway}",
                userId, request.SelectedGateway);

            // Get the payment gateway
            IPaymentGatewayService gateway;
            try
            {
                gateway = _gatewayFactory.GetGateway(request.SelectedGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get gateway {Gateway}", request.SelectedGateway);
                return Json(new PaymentInitiationResult
                {
                    Success = false,
                    Message = "بوابة الدفع غير متاحة حالياً"
                });
            }

            // Build payment request
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var orderId = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            var paymentRequest = new CreatePaymentIntentRequest
            {
                Amount = cart.TotalAmount,
                Currency = "EGP",
                CustomerId = userId,
                Description = $"شراء {cart.Items.Count} عنصر",
                CustomerEmail = user?.Email ?? request.BillingEmail,
                CustomerName = user?.FullName ?? request.BillingName,
                CustomerPhone = user?.PhoneNumber ?? request.BillingPhone,
                CustomerPhoneCountryCode = request.UserCountry == "EG" ? "20" : "966",
                OrderId = orderId,
                SuccessUrl = $"{baseUrl}/Student/MultiGatewayCheckout/Success?orderId={orderId}",
                CancelUrl = $"{baseUrl}/Student/MultiGatewayCheckout/Cancel?orderId={orderId}",
                WebhookUrl = $"{baseUrl}/api/webhooks/{request.SelectedGateway.ToString().ToLower()}",
                SavePaymentMethod = request.SavePaymentMethod,
                InstallmentMonths = request.InstallmentMonths,
                LineItems = cart.Items.Select(i => new PaymentLineItem
                {
                    Name = i.Title,
                    Description = i.Type.ToString(),
                    Price = i.Price,
                    Quantity = 1
                }).ToList(),
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", userId },
                    { "cart_id", cart.Id.ToString() },
                    { "coupon_code", cart.AppliedCoupon?.Code ?? "" }
                },
                ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            // Create payment intent
            var result = await gateway.CreatePaymentIntentAsync(paymentRequest);

            if (!result.Success)
            {
                _logger.LogWarning("Payment creation failed for {Gateway}: {Error}",
                    request.SelectedGateway, result.ErrorMessage);

                return Json(new PaymentInitiationResult
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "فشل في إنشاء طلب الدفع"
                });
            }

            // Create pending payment records
            await CreatePendingPaymentsAsync(userId, cart, result, request.SelectedGateway, orderId);

            _logger.LogInformation("Payment initiated for order {OrderId} with gateway {Gateway}",
                orderId, request.SelectedGateway);

            // Return appropriate response based on gateway type
            if (request.SelectedGateway == PaymentGatewayType.BankTransfer)
            {
                return Json(new PaymentInitiationResult
                {
                    Success = true,
                    Gateway = request.SelectedGateway,
                    IsBankTransfer = true,
                    BankDetails = result.BankDetails,
                    RedirectUrl = $"/Student/MultiGatewayCheckout/BankTransferInstructions?reference={result.ReferenceNumber}"
                });
            }

            if (request.SelectedGateway == PaymentGatewayType.Fawry)
            {
                return Json(new PaymentInitiationResult
                {
                    Success = true,
                    Gateway = request.SelectedGateway,
                    IsFawry = true,
                    FawryReferenceNumber = result.ReferenceNumber,
                    FawryExpiresAt = result.ExpiresAt,
                    RedirectUrl = $"/Student/MultiGatewayCheckout/FawryReference?reference={result.ReferenceNumber}"
                });
            }

            return Json(new PaymentInitiationResult
            {
                Success = true,
                Gateway = request.SelectedGateway,
                RedirectUrl = result.RedirectUrl,
                RequiresRedirect = result.RequiresRedirect,
                ClientSecret = result.ClientSecret,
                PaymentIntentId = result.PaymentIntentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing error");
            return Json(new PaymentInitiationResult
            {
                Success = false,
                Message = "حدث خطأ أثناء معالجة الدفع"
            });
        }
    }

    /// <summary>
    /// صفحة نجاح الدفع - Payment success page
    /// </summary>
    public async Task<IActionResult> Success(string orderId, string? paymentIntentId = null)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Success), new { orderId }) });
        }

        _logger.LogInformation("Payment success callback for order {OrderId}, user {UserId}", orderId, userId);

        try
        {
            // Look up payments by orderId
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Book)
                .Where(p => p.Metadata != null && p.Metadata.Contains(orderId) && p.StudentId == userId)
                .ToListAsync();

            // Fallback: try matching by TransactionId
            if (!payments.Any())
            {
                payments = await _context.Payments
                    .Include(p => p.Course)
                    .Include(p => p.Book)
                    .Where(p => p.TransactionId == orderId && p.StudentId == userId)
                    .ToListAsync();
            }

            if (!payments.Any())
            {
                _logger.LogWarning("No payments found for order {OrderId}, user {UserId}. Payment may still be processing via webhook.", orderId, userId);
                ViewBag.OrderId = orderId;
                ViewBag.PaymentPending = true;
                return View();
            }

            // Check payment status
            var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
            var pendingPayments = payments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing).ToList();

            if (!completedPayments.Any() && pendingPayments.Any())
            {
                // Payment still being processed (waiting for webhook)
                _logger.LogInformation("Payments for order {OrderId} still pending, showing processing page", orderId);
                ViewBag.OrderId = orderId;
                ViewBag.PaymentPending = true;
                return View();
            }

            // Process completed payments: create enrollments/purchases if not already done
            int enrollmentCount = 0;
            int bookPurchaseCount = 0;

            foreach (var payment in completedPayments)
            {
                if (payment.ProductType == ProductType.Course && payment.CourseId.HasValue)
                {
                    // Check if enrollment already exists (may have been created by webhook)
                    var enrollmentExists = await _context.Enrollments
                        .AnyAsync(e => e.StudentId == userId && e.CourseId == payment.CourseId.Value
                            && e.Status != EnrollmentStatus.Cancelled && e.Status != EnrollmentStatus.Refunded);

                    if (!enrollmentExists && payment.Course != null && !payment.Course.IsDeleted)
                    {
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
                        enrollmentCount++;

                        _logger.LogInformation("Enrollment created for course {CourseId} via multi-gateway success for user {UserId}",
                            payment.CourseId, userId);
                    }

                    // Create invoice if not exists
                    var invoiceExists = await _context.Invoices.AnyAsync(i => i.PaymentId == payment.Id);
                    if (!invoiceExists)
                    {
                        _context.Invoices.Add(new Invoice
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
                        });
                    }
                }
                else if (payment.ProductType == ProductType.Book && payment.BookId.HasValue)
                {
                    // Check if book purchase already exists
                    var purchaseExists = await _context.BookPurchases
                        .AnyAsync(bp => bp.StudentId == userId && bp.BookId == payment.BookId.Value && bp.IsActive);

                    if (!purchaseExists && payment.Book != null && !payment.Book.IsDeleted)
                    {
                        _context.BookPurchases.Add(new BookPurchase
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
                        });
                        bookPurchaseCount++;

                        _logger.LogInformation("Book purchase created for book {BookId} via multi-gateway success for user {UserId}",
                            payment.BookId, userId);
                    }

                    // Create invoice if not exists
                    var invoiceExists = await _context.Invoices.AnyAsync(i => i.PaymentId == payment.Id);
                    if (!invoiceExists)
                    {
                        _context.Invoices.Add(new Invoice
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
                        });
                    }
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

                        var invoiceExists = await _context.Invoices.AnyAsync(i => i.PaymentId == payment.Id);
                        if (!invoiceExists)
                        {
                            _context.Invoices.Add(new Invoice
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
                            });
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

                        var invoiceExists = await _context.Invoices.AnyAsync(i => i.PaymentId == payment.Id);
                        if (!invoiceExists)
                        {
                            _context.Invoices.Add(new Invoice
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
                            });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Clear cart only when at least one enrollment or purchase was created (avoid clearing on full failure)
            if (enrollmentCount > 0 || bookPurchaseCount > 0)
                await _cartService.ClearCartAsync(userId);

            // Send notification if enrollments or purchases were created
            if (enrollmentCount > 0 || bookPurchaseCount > 0)
            {
                var message = new System.Text.StringBuilder("تم شراء ");
                if (enrollmentCount > 0) message.Append($"{enrollmentCount} دورة ");
                if (enrollmentCount > 0 && bookPurchaseCount > 0) message.Append("و ");
                if (bookPurchaseCount > 0) message.Append($"{bookPurchaseCount} كتاب ");
                message.Append("بنجاح!");

                _context.Notifications.Add(new Domain.Entities.Notifications.Notification
                {
                    UserId = userId,
                    Title = "تم الشراء بنجاح! 🎉",
                    Message = message.ToString(),
                    Type = Domain.Enums.NotificationType.Purchase,
                    ActionUrl = enrollmentCount > 0 ? "/Student/Courses/MyCourses" : "/Student/Books/MyLibrary",
                    ActionText = enrollmentCount > 0 ? "عرض دوراتي" : "عرض مكتبتي",
                    IsRead = false
                });

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Multi-gateway payment success processed: {EnrollmentCount} enrollments, {BookPurchaseCount} book purchases for order {OrderId}",
                enrollmentCount, bookPurchaseCount, orderId);

            ViewBag.OrderId = orderId;
            ViewBag.EnrollmentCount = enrollmentCount;
            ViewBag.BookPurchaseCount = bookPurchaseCount;
            ViewBag.PaymentPending = false;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing multi-gateway payment success for order {OrderId}", orderId);
            ViewBag.OrderId = orderId;
            ViewBag.PaymentPending = true;
            return View();
        }
    }

    /// <summary>
    /// صفحة إلغاء الدفع - Payment cancelled page
    /// </summary>
    public IActionResult Cancel(string orderId)
    {
        _logger.LogInformation("Payment cancelled for order {OrderId}", orderId);
        SetWarningMessage("تم إلغاء عملية الدفع");
        return View();
    }

    /// <summary>
    /// تعليمات التحويل البنكي - Bank transfer instructions
    /// </summary>
    public async Task<IActionResult> BankTransferInstructions(string reference)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(BankTransferInstructions), new { reference }) });
        }

        var pendingPayment = await _context.Set<PendingManualPayment>()
            .FirstOrDefaultAsync(p => p.ReferenceNumber == reference && p.StudentId == userId);

        if (pendingPayment == null)
        {
            return NotFound();
        }

        var viewModel = new BankTransferStatusViewModel
        {
            ReferenceNumber = pendingPayment.ReferenceNumber,
            Amount = pendingPayment.Amount,
            Currency = pendingPayment.Currency,
            Status = pendingPayment.Status.ToString(),
            StatusAr = GetStatusArabic(pendingPayment.Status),
            ExpiresAt = pendingPayment.ExpiresAt,
            IsExpired = pendingPayment.ExpiresAt < DateTime.UtcNow,
            CanUploadProof = pendingPayment.Status == ManualPaymentStatus.AwaitingProof,
            BankName = pendingPayment.RecipientBankName,
            AccountName = pendingPayment.RecipientAccountName,
            AccountNumber = pendingPayment.RecipientAccountNumber,
            IBAN = pendingPayment.RecipientIBAN,
            SwiftCode = pendingPayment.RecipientSwiftCode
        };

        return View(viewModel);
    }

    /// <summary>
    /// رفع إثبات الدفع - Submit payment proof
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitPaymentProof(SubmitPaymentProofViewModel model, IFormFile? proofImage)
    {
        var pendingPayment = await _context.Set<PendingManualPayment>()
            .FirstOrDefaultAsync(p => p.ReferenceNumber == model.ReferenceNumber &&
                                     p.StudentId == _currentUserService.UserId);

        if (pendingPayment == null)
        {
            return NotFound();
        }

        if (pendingPayment.Status != ManualPaymentStatus.AwaitingProof)
        {
            SetWarningMessage("لا يمكن تقديم إثبات لهذه الدفعة");
            return RedirectToAction(nameof(BankTransferInstructions), new { reference = model.ReferenceNumber });
        }

        // Upload proof image
        string? proofImageUrl = null;
        if (proofImage != null && proofImage.Length > 0)
        {
            // Save to uploads folder
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "payment-proofs");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{pendingPayment.ReferenceNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(proofImage.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await proofImage.CopyToAsync(stream);
            }

            proofImageUrl = $"/uploads/payment-proofs/{fileName}";
        }

        // Update pending payment
        pendingPayment.ProofImageUrl = proofImageUrl;
        pendingPayment.TransferReference = model.TransferReference;
        pendingPayment.TransferDate = model.TransferDate;
        pendingPayment.SenderName = model.SenderName;
        pendingPayment.SenderBankName = model.SenderBankName;
        pendingPayment.StudentNotes = model.Notes;
        pendingPayment.ProofSubmittedAt = DateTime.UtcNow;
        pendingPayment.Status = ManualPaymentStatus.ProofSubmitted;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment proof submitted for {Reference}", model.ReferenceNumber);

        SetSuccessMessage("تم تقديم إثبات الدفع بنجاح. سيتم مراجعته خلال 24 ساعة.");
        return RedirectToAction(nameof(BankTransferStatus), new { reference = model.ReferenceNumber });
    }

    /// <summary>
    /// حالة التحويل البنكي - Bank transfer status
    /// </summary>
    public async Task<IActionResult> BankTransferStatus(string reference)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(BankTransferStatus), new { reference }) });
        }

        var pendingPayment = await _context.Set<PendingManualPayment>()
            .FirstOrDefaultAsync(p => p.ReferenceNumber == reference && p.StudentId == userId);

        if (pendingPayment == null)
        {
            return NotFound();
        }

        var viewModel = new BankTransferStatusViewModel
        {
            ReferenceNumber = pendingPayment.ReferenceNumber,
            Amount = pendingPayment.Amount,
            Currency = pendingPayment.Currency,
            Status = pendingPayment.Status.ToString(),
            StatusAr = GetStatusArabic(pendingPayment.Status),
            ExpiresAt = pendingPayment.ExpiresAt,
            IsExpired = pendingPayment.ExpiresAt < DateTime.UtcNow && 
                       pendingPayment.Status == ManualPaymentStatus.AwaitingProof,
            CanUploadProof = pendingPayment.Status == ManualPaymentStatus.AwaitingProof,
            ProofImageUrl = pendingPayment.ProofImageUrl,
            ProofSubmittedAt = pendingPayment.ProofSubmittedAt,
            RejectionReason = pendingPayment.RejectionReason
        };

        return View(viewModel);
    }

    /// <summary>
    /// رقم فوري المرجعي - Fawry reference page
    /// </summary>
    public async Task<IActionResult> FawryReference(string reference)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(FawryReference), new { reference }) });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == reference && p.StudentId == userId);

        var viewModel = new FawryPaymentViewModel
        {
            ReferenceNumber = reference,
            Amount = payment?.TotalAmount ?? 0,
            Currency = payment?.Currency ?? "EGP",
            ExpiresAt = DateTime.UtcNow.AddHours(48), // Default 48 hours
            IsExpired = false,
            IsPaid = payment?.Status == PaymentStatus.Completed,
            Status = payment?.Status.ToString() ?? "Pending",
            FormattedAmount = _currencyService.FormatAmount(payment?.TotalAmount ?? 0, "EGP"),
            ExpiryDisplay = "صالح لمدة 48 ساعة"
        };

        return View(viewModel);
    }

    /// <summary>
    /// فحص حالة الدفع فوري - Check Fawry payment status (AJAX endpoint)
    /// Used for auto-refresh functionality on FawryReference page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckFawryStatus(string referenceNumber)
    {
        try
        {
            if (string.IsNullOrEmpty(referenceNumber))
            {
                return Json(new { success = false, message = "الرقم المرجعي غير صحيح" });
            }

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
            }

            // Check payment status in database (restrict to current user)
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == referenceNumber && p.StudentId == userId);

            if (payment == null)
            {
                // Try to check status from Fawry gateway
                try
                {
                    var fawryGateway = _gatewayFactory.GetGateway(PaymentGatewayType.Fawry);
                    var statusResult = await fawryGateway.CheckPaymentStatusAsync(referenceNumber);

                    return Json(new
                    {
                        success = true,
                        isPaid = statusResult.IsPaid,
                        isExpired = statusResult.IsExpired,
                        status = statusResult.Status,
                        statusAr = GetFawryStatusArabic(statusResult.Status)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check Fawry status for {Reference}", referenceNumber);
                    return Json(new { success = false, isPaid = false, isExpired = false, status = "pending" });
                }
            }

            return Json(new
            {
                success = true,
                isPaid = payment.Status == PaymentStatus.Completed,
                isExpired = payment.Status == PaymentStatus.Expired || payment.Status == PaymentStatus.Cancelled,
                status = payment.Status.ToString(),
                statusAr = GetPaymentStatusArabic(payment.Status)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Fawry status for {Reference}", referenceNumber);
            return Json(new { success = false, message = "حدث خطأ في فحص حالة الدفع" });
        }
    }

    /// <summary>
    /// Get Arabic translation for Fawry status
    /// </summary>
    private static string GetFawryStatusArabic(string status)
    {
        return status?.ToUpperInvariant() switch
        {
            "PAID" => "تم الدفع ✓",
            "UNPAID" or "NEW" => "في انتظار الدفع",
            "EXPIRED" => "انتهت الصلاحية",
            "REFUNDED" => "تم الاسترداد",
            "FAILED" => "فشل الدفع",
            _ => "غير معروف"
        };
    }

    /// <summary>
    /// Get Arabic translation for payment status
    /// </summary>
    private static string GetPaymentStatusArabic(PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Pending => "في انتظار الدفع",
            PaymentStatus.Completed => "تم الدفع ✓",
            PaymentStatus.Failed => "فشل الدفع",
            PaymentStatus.Cancelled => "ملغي",
            PaymentStatus.Expired => "انتهت الصلاحية",
            PaymentStatus.Refunded => "تم الاسترداد",
            _ => "غير معروف"
        };
    }

    #region Private Methods

    private async Task CreatePendingPaymentsAsync(
        string userId,
        ShoppingCartInfo cart,
        PaymentIntentResult result,
        PaymentGatewayType gateway,
        string orderId)
    {
        var transactionId = result.PaymentIntentId ?? result.ReferenceNumber ?? orderId;

        var metadataJson = $"{{\"orderId\":\"{orderId}\"}}";
        foreach (var item in cart.Items)
        {
            var payment = new Payment
            {
                TransactionId = transactionId,
                StudentId = userId,
                CourseId = item.Type == ProductType.Course ? item.ItemId : null,
                BookId = item.Type == ProductType.Book ? item.ItemId : null,
                BundleId = item.Type == ProductType.CourseBundle ? (item.BundleId ?? item.ItemId) : null,
                BookBundleId = item.Type == ProductType.BookBundle ? (item.BundleId ?? item.ItemId) : null,
                ProductType = item.Type,
                PurchaseType = item.Type.ToString(),
                OriginalAmount = item.OriginalPrice,
                DiscountAmount = item.DiscountAmount,
                TotalAmount = item.Price,
                Currency = "EGP",
                CouponCode = cart.AppliedCoupon?.Code,
                Provider = MapGatewayToProvider(gateway),
                PaymentMethod = gateway.ToString(),
                Status = PaymentStatus.Pending,
                Metadata = metadataJson
            };

            _context.Payments.Add(payment);
        }

        await _context.SaveChangesAsync();
    }

    private static PaymentProvider MapGatewayToProvider(PaymentGatewayType gateway)
    {
        return gateway switch
        {
            PaymentGatewayType.Stripe => PaymentProvider.Stripe,
            PaymentGatewayType.PayPal => PaymentProvider.PayPal,
            PaymentGatewayType.Paymob => PaymentProvider.Paymob,
            PaymentGatewayType.Fawry => PaymentProvider.Fawry,
            PaymentGatewayType.VodafoneCash or 
            PaymentGatewayType.OrangeCash or 
            PaymentGatewayType.EtisalatCash => PaymentProvider.VodafoneCash,
            PaymentGatewayType.BankTransfer or 
            PaymentGatewayType.Manual => PaymentProvider.BankTransfer,
            _ => PaymentProvider.CreditCard
        };
    }

    private static PaymentGatewayType? GetRecommendedGateway(string country, decimal amount)
    {
        return country.ToUpperInvariant() switch
        {
            "EG" => amount < 100 ? PaymentGatewayType.Fawry : PaymentGatewayType.Paymob,
            "SA" => PaymentGatewayType.Tap,
            "AE" or "KW" or "BH" or "QA" or "OM" => PaymentGatewayType.MyFatoorah,
            _ => PaymentGatewayType.Stripe
        };
    }

    private static string GetStatusArabic(ManualPaymentStatus status)
    {
        return status switch
        {
            ManualPaymentStatus.AwaitingProof => "في انتظار إثبات الدفع",
            ManualPaymentStatus.ProofSubmitted => "تم تقديم الإثبات - بانتظار المراجعة",
            ManualPaymentStatus.UnderReview => "قيد المراجعة",
            ManualPaymentStatus.Approved => "تم الاعتماد ✓",
            ManualPaymentStatus.Rejected => "مرفوض",
            ManualPaymentStatus.Expired => "منتهي الصلاحية",
            _ => "غير معروف"
        };
    }

    #endregion
}

