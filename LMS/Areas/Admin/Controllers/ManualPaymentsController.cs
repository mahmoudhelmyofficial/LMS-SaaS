using System.Text.Json;
using LMS.Data;
using LMS.Domain.Entities.Books;
using LMS.Domain.Entities.Financial;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الدفعات اليدوية - Manual Payments Management Controller
/// Admin verification for bank transfers and manual payments
/// </summary>
public class ManualPaymentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly IShoppingCartService _cartService;
    private readonly ILogger<ManualPaymentsController> _logger;

    public ManualPaymentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        IShoppingCartService cartService,
        ILogger<ManualPaymentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الدفعات المعلقة - Pending payments list
    /// </summary>
    public async Task<IActionResult> Index(string? status = null, string? search = null)
    {
        var query = _context.Set<PendingManualPayment>()
            .Include(p => p.Student)
            .Include(p => p.ReviewedBy)
            .AsQueryable();

        // Filter by status
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ManualPaymentStatus>(status, out var statusEnum))
        {
            query = query.Where(p => p.Status == statusEnum);
        }
        else
        {
            // Default: show pending proof submitted and under review
            query = query.Where(p => 
                p.Status == ManualPaymentStatus.ProofSubmitted || 
                p.Status == ManualPaymentStatus.UnderReview);
        }

        // Search
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p =>
                p.ReferenceNumber.Contains(search) ||
                p.Student.Email!.Contains(search) ||
                p.Student.FirstName.Contains(search) ||
                p.Student.LastName.Contains(search) ||
                (p.TransferReference != null && p.TransferReference.Contains(search)));
        }

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.CurrentStatus = status;
        ViewBag.Search = search;
        ViewBag.StatusCounts = await GetStatusCountsAsync();

        return View(payments);
    }

    /// <summary>
    /// تفاصيل الدفعة - Payment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Set<PendingManualPayment>()
            .Include(p => p.Student)
            .Include(p => p.ReviewedBy)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        // Parse cart items
        var cartItems = new List<CartItemSnapshot>();
        try
        {
            if (!string.IsNullOrEmpty(payment.CartItemsJson))
            {
                cartItems = JsonSerializer.Deserialize<List<CartItemSnapshot>>(payment.CartItemsJson) ?? new();
            }
        }
        catch { }

        ViewBag.CartItems = cartItems;

        return View(payment);
    }

    /// <summary>
    /// اعتماد الدفعة - Approve payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var manualPayment = await _context.Set<PendingManualPayment>()
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (manualPayment == null)
            return NotFound();

        if (manualPayment.Status == ManualPaymentStatus.Approved)
        {
            SetErrorMessage("تم اعتماد هذه الدفعة مسبقاً", "This payment has already been approved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var adminId = _currentUserService.UserId!;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Update manual payment status
                manualPayment.Status = ManualPaymentStatus.Approved;
                manualPayment.ReviewedById = adminId;
                manualPayment.ReviewedAt = DateTime.UtcNow;
                manualPayment.AdminNotes = notes;

                // Parse and process cart items
                var cartItems = JsonSerializer.Deserialize<List<CartItemSnapshot>>(manualPayment.CartItemsJson) 
                    ?? new List<CartItemSnapshot>();

                foreach (var item in cartItems)
                {
                    // Create payment record
                    var payment = new Payment
                    {
                        TransactionId = manualPayment.ReferenceNumber,
                        StudentId = manualPayment.StudentId,
                        CourseId = item.CourseId,
                        BookId = item.BookId,
                        ProductType = item.ProductType,
                        PurchaseType = item.ProductType == ProductType.Book ? "Book" : "Course",
                        OriginalAmount = item.OriginalPrice,
                        DiscountAmount = item.DiscountAmount,
                        TotalAmount = item.FinalPrice,
                        Currency = manualPayment.Currency,
                        CouponCode = manualPayment.CouponCode,
                        Provider = PaymentProvider.BankTransfer,
                        PaymentMethod = "BankTransfer",
                        Status = PaymentStatus.Completed,
                        CompletedAt = DateTime.UtcNow,
                        VerifiedBy = adminId,
                        AdminNotes = notes
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Create invoice
                    var invoice = new Invoice
                    {
                        InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{payment.Id:D6}",
                        PaymentId = payment.Id,
                        StudentId = manualPayment.StudentId,
                        IssuedDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow,
                        Status = "Paid",
                        SubTotal = item.OriginalPrice,
                        DiscountAmount = item.DiscountAmount,
                        TotalAmount = item.FinalPrice,
                        Currency = manualPayment.Currency
                    };
                    _context.Invoices.Add(invoice);

                    // Create enrollment or book purchase
                    if (item.ProductType == ProductType.Course && item.CourseId.HasValue)
                    {
                        await CreateEnrollmentAsync(payment, item.CourseId.Value);
                    }
                    else if (item.ProductType == ProductType.Book && item.BookId.HasValue)
                    {
                        await CreateBookPurchaseAsync(payment, item.BookId.Value);
                    }

                    // Create instructor earnings
                    await CreateInstructorEarningsAsync(payment, item);
                }

                await _context.SaveChangesAsync();
            });

            // Send notification to student
            await SendApprovalNotificationAsync(manualPayment);

            _logger.LogInformation("Manual payment {Reference} approved by {Admin}",
                manualPayment.ReferenceNumber, adminId);

            SetSuccessMessage("تم اعتماد الدفعة بنجاح وتفعيل الاشتراكات", "Payment approved successfully and subscriptions activated.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve manual payment {Id}", id);
            SetErrorMessage("حدث خطأ أثناء اعتماد الدفعة", "An error occurred while approving the payment.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// رفض الدفعة - Reject payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            SetErrorMessage("يرجى إدخال سبب الرفض", "Please enter the rejection reason.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var manualPayment = await _context.Set<PendingManualPayment>()
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (manualPayment == null)
            return NotFound();

        var adminId = _currentUserService.UserId!;

        manualPayment.Status = ManualPaymentStatus.Rejected;
        manualPayment.ReviewedById = adminId;
        manualPayment.ReviewedAt = DateTime.UtcNow;
        manualPayment.RejectionReason = reason;

        await _context.SaveChangesAsync();

        // Send notification to student
        await SendRejectionNotificationAsync(manualPayment, reason);

        _logger.LogInformation("Manual payment {Reference} rejected by {Admin}: {Reason}",
            manualPayment.ReferenceNumber, adminId, reason);

        SetSuccessMessage("تم رفض الدفعة", "Payment rejected.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// طلب معلومات إضافية - Request more info
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMoreInfo(int id, string message)
    {
        var manualPayment = await _context.Set<PendingManualPayment>()
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (manualPayment == null)
            return NotFound();

        manualPayment.Status = ManualPaymentStatus.AwaitingProof;
        manualPayment.AdminNotes = message;

        await _context.SaveChangesAsync();

        // Send notification to student
        var notification = new Notification
        {
            UserId = manualPayment.StudentId,
            Title = "مطلوب معلومات إضافية لعملية الدفع",
            Message = message,
            Type = NotificationType.Payment,
            ActionUrl = $"/Student/Checkout/BankTransferStatus?reference={manualPayment.ReferenceNumber}",
            ActionText = "عرض التفاصيل",
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم طلب معلومات إضافية من الطالب", "Additional information requested from the student.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تمديد صلاحية الدفعة - Extend payment expiry
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendExpiry(int id, int days = 3)
    {
        var manualPayment = await _context.Set<PendingManualPayment>()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (manualPayment == null)
            return NotFound();

        manualPayment.ExpiresAt = DateTime.UtcNow.AddDays(days);
        await _context.SaveChangesAsync();

        SetSuccessMessage(string.Format(CultureExtensions.T("تم تمديد صلاحية الدفعة لـ {0} أيام إضافية", "Payment validity extended by {0} day(s)."), days));
        return RedirectToAction(nameof(Details), new { id });
    }

    #region Private Methods

    private async Task<Dictionary<string, int>> GetStatusCountsAsync()
    {
        var counts = await _context.Set<PendingManualPayment>()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count);

        return counts;
    }

    private async Task CreateEnrollmentAsync(Payment payment, int courseId)
    {
        var existingEnrollment = await _context.Enrollments
            .AnyAsync(e => e.StudentId == payment.StudentId && e.CourseId == courseId);

        if (existingEnrollment) return;

        var enrollment = new Enrollment
        {
            StudentId = payment.StudentId,
            CourseId = courseId,
            Status = EnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow,
            PaidAmount = payment.TotalAmount,
            Currency = payment.Currency,
            CouponCode = payment.CouponCode,
            DiscountAmount = payment.DiscountAmount,
            IsFree = payment.TotalAmount == 0,
            TotalLessons = await _context.Lessons.CountAsync(l => l.Module.CourseId == courseId)
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        payment.EnrollmentId = enrollment.Id;
        payment.CourseId = courseId;

        // Update course stats
        var course = await _context.Courses.FindAsync(courseId);
        if (course != null)
        {
            course.TotalStudents++;
        }
    }

    private async Task CreateBookPurchaseAsync(Payment payment, int bookId)
    {
        var existingPurchase = await _context.BookPurchases
            .AnyAsync(p => p.StudentId == payment.StudentId && p.BookId == bookId && p.IsActive);

        if (existingPurchase) return;

        var book = await _context.Books.FindAsync(bookId);

        var bookPurchase = new BookPurchase
        {
            StudentId = payment.StudentId,
            BookId = bookId,
            PaymentId = payment.Id,
            PurchaseType = BookPurchaseType.Digital,
            PurchasedFormat = book?.AvailableFormats ?? BookFormat.PDF,
            PaidAmount = payment.TotalAmount,
            Currency = payment.Currency,
            DiscountAmount = payment.DiscountAmount,
            CouponCode = payment.CouponCode,
            PurchasedAt = DateTime.UtcNow,
            IsActive = true,
            MaxDownloads = 5
        };

        _context.BookPurchases.Add(bookPurchase);

        payment.BookId = bookId;

        if (book != null)
        {
            book.TotalSales++;
        }
    }

    private async Task CreateInstructorEarningsAsync(Payment payment, CartItemSnapshot item)
    {
        string? instructorId = null;

        if (item.ProductType == ProductType.Course && item.CourseId.HasValue)
        {
            var course = await _context.Courses.FindAsync(item.CourseId);
            instructorId = course?.InstructorId;
        }
        else if (item.ProductType == ProductType.Book && item.BookId.HasValue)
        {
            var book = await _context.Books.FindAsync(item.BookId);
            instructorId = book?.InstructorId;
        }

        if (string.IsNullOrEmpty(instructorId)) return;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == instructorId);

        if (instructorProfile == null) return;

        var (platformCommission, instructorAmount) = BusinessRuleHelper.CalculateCommission(
            payment.TotalAmount,
            instructorProfile.CommissionRate);

        var earning = new InstructorEarning
        {
            InstructorId = instructorId,
            CourseId = item.CourseId,
            BookId = item.BookId,
            PaymentId = payment.Id,
            EarningType = item.ProductType == ProductType.Book ? "book_sale" : "sale",
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

    private async Task SendApprovalNotificationAsync(PendingManualPayment payment)
    {
        var notification = new Notification
        {
            UserId = payment.StudentId,
            Title = "تم اعتماد الدفعة بنجاح! ✅",
            Message = $"تم التحقق من تحويلك البنكي رقم {payment.ReferenceNumber} وتفعيل اشتراكاتك",
            Type = NotificationType.Payment,
            ActionUrl = "/Student/Dashboard",
            ActionText = "عرض دوراتي",
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send email
        if (payment.Student?.Email != null)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    payment.Student.Email,
                    "تم اعتماد الدفعة - LMS",
                    $"تم التحقق من تحويلك البنكي رقم {payment.ReferenceNumber} وتفعيل اشتراكاتك بنجاح.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval email");
            }
        }
    }

    private async Task SendRejectionNotificationAsync(PendingManualPayment payment, string reason)
    {
        var notification = new Notification
        {
            UserId = payment.StudentId,
            Title = "لم يتم اعتماد الدفعة",
            Message = $"عذراً، لم نتمكن من التحقق من تحويلك البنكي. السبب: {reason}",
            Type = NotificationType.Payment,
            ActionUrl = "/Student/Checkout/Cart",
            ActionText = "إعادة المحاولة",
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send email
        if (payment.Student?.Email != null)
        {
            try
            {
                await _emailService.SendEmailAsync(
                    payment.Student.Email,
                    "لم يتم اعتماد الدفعة - LMS",
                    $"عذراً، لم نتمكن من التحقق من تحويلك البنكي رقم {payment.ReferenceNumber}. السبب: {reason}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection email");
            }
        }
    }

    #endregion
}

/// <summary>
/// نموذج عنصر السلة المحفوظ - Cart item snapshot for JSON storage
/// </summary>
public class CartItemSnapshot
{
    public ProductType ProductType { get; set; }
    public int? CourseId { get; set; }
    public int? BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
}

