using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Entities.Settings;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// شراء الجلسات المباشرة - Session Checkout Controller
/// </summary>
public class SessionCheckoutController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SessionCheckoutController> _logger;

    public SessionCheckoutController(
        ApplicationDbContext context,
        ILiveSessionService liveSessionService,
        ICurrentUserService currentUserService,
        ILogger<SessionCheckoutController> logger)
    {
        _context = context;
        _liveSessionService = liveSessionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private string GetUserId() => _currentUserService.UserId ?? string.Empty;

    /// <summary>
    /// صفحة الدفع للجلسة - Session Checkout Page
    /// </summary>
    public async Task<IActionResult> Checkout(int id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Checkout), new { id }) });
        }

        // Check if already purchased
        var isPurchased = await _liveSessionService.HasStudentPurchasedSessionAsync(id, userId);
        if (isPurchased)
        {
            SetInfoMessage("لقد قمت بشراء هذه الجلسة بالفعل");
            return RedirectToAction("Details", "LiveClasses", new { area = "Student", id });
        }

        var session = await _context.LiveClasses
            .Include(lc => lc.Instructor)
            .FirstOrDefaultAsync(lc => lc.Id == id && !lc.IsDeleted);

        if (session == null)
        {
            SetErrorMessage("الجلسة غير موجودة");
            return RedirectToAction("Index", "LiveClasses", new { area = "Student" });
        }

        // Cannot purchase cancelled or completed sessions
        if (session.Status == LiveClassStatus.Cancelled || session.Status == LiveClassStatus.Completed)
        {
            SetErrorMessage("لا يمكن شراء جلسة ملغية أو مكتملة");
            return RedirectToAction("Details", "LiveClasses", new { area = "Student", id });
        }

        if (session.PricingType == LiveSessionPricingType.Free || session.IsFreeForAll)
        {
            SetInfoMessage("هذه الجلسة مجانية");
            return RedirectToAction("Details", "LiveClasses", new { area = "Student", id });
        }

        var vm = new SessionCheckoutViewModel
        {
            LiveClassId = session.Id,
            SessionTitle = session.Title,
            InstructorName = session.Instructor?.FullName ?? session.Instructor?.UserName ?? "مدرس",
            ScheduledStartTime = session.ScheduledStartTime,
            DurationMinutes = session.DurationMinutes,
            Price = session.Price,
            Currency = session.PriceCurrency,
            Subject = session.Subject
        };

        // Load payment gateways (never null for view)
        var gateways = await _context.PaymentGatewaySettings
            .Where(g => g.IsEnabled && !g.IsDeleted)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();
        ViewBag.PaymentGateways = gateways ?? new List<PaymentGatewaySetting>();

        return View(vm);
    }

    /// <summary>
    /// معالجة دفع الجلسة - Process Session Payment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(int liveClassId, string paymentMethod)
    {
        var userId = GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action(nameof(Checkout), new { id = liveClassId }) });
        }

        // Duplicate purchase guard
        var alreadyPurchased = await _liveSessionService.HasStudentPurchasedSessionAsync(liveClassId, userId);
        if (alreadyPurchased)
        {
            SetInfoMessage("لقد قمت بشراء هذه الجلسة بالفعل");
            return RedirectToAction("Details", "LiveClasses", new { area = "Student", id = liveClassId });
        }

        try
        {
            var session = await _context.LiveClasses
                .Include(lc => lc.Instructor)
                .FirstOrDefaultAsync(lc => lc.Id == liveClassId && !lc.IsDeleted);

            if (session == null)
            {
                SetErrorMessage("الجلسة غير موجودة");
                return RedirectToAction("Index", "LiveClasses", new { area = "Student" });
            }

            if (session.Status == LiveClassStatus.Cancelled || session.Status == LiveClassStatus.Completed)
            {
                SetErrorMessage("لا يمكن شراء جلسة ملغية أو مكتملة");
                return RedirectToAction("Details", "LiveClasses", new { area = "Student", id = liveClassId });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                var payment = new Payment
                {
                    TransactionId = $"SESS_{liveClassId}_{userId}_{DateTime.UtcNow.Ticks}",
                    StudentId = userId,
                    OriginalAmount = session.Price,
                    TotalAmount = session.Price,
                    Currency = session.PriceCurrency,
                    Status = PaymentStatus.Pending,
                    Provider = Enum.TryParse<PaymentProvider>(paymentMethod, out var provider) ? provider : PaymentProvider.Paymob,
                    PaymentMethod = paymentMethod,
                    Metadata = $"{{\"type\":\"session\",\"liveClassId\":{liveClassId}}}"
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                var purchase = await _liveSessionService.CreateSessionPurchaseAsync(
                    liveClassId, userId, payment.Id, session.Price);

                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _liveSessionService.ActivateSessionPurchaseAsync(purchase.Id);

                _context.Invoices.Add(new Invoice
                {
                    InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{payment.Id:D6}",
                    PaymentId = payment.Id,
                    StudentId = userId,
                    IssuedDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow,
                    Status = "Paid",
                    SubTotal = session.Price,
                    TotalAmount = session.Price,
                    Currency = session.PriceCurrency,
                    ItemDescription = $"جلسة مباشرة: {session.Title}"
                });

                _context.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = "تم شراء الجلسة بنجاح! 🎓",
                    Message = $"تم شراء جلسة \"{session.Title}\" بنجاح. يمكنك الآن الانضمام عند بدء الجلسة.",
                    Type = NotificationType.Purchase,
                    ActionUrl = $"/Student/LiveClasses/Details/{liveClassId}",
                    ActionText = "عرض الجلسة",
                    IsRead = false
                });

                if (!string.IsNullOrEmpty(session.InstructorId))
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = session.InstructorId,
                        Title = "عملية شراء جديدة لجلستك 💰",
                        Message = $"قام طالب بشراء جلسة \"{session.Title}\"",
                        Type = NotificationType.Sale,
                        ActionUrl = $"/Instructor/LiveClasses/Details/{liveClassId}",
                        ActionText = "عرض الجلسة",
                        IsRead = false
                    });
                }

                await _context.SaveChangesAsync();
            });

            SetSuccessMessage("تم شراء الجلسة بنجاح! يمكنك الآن الانضمام للجلسة");
            return RedirectToAction(nameof(PaymentSuccess), new { liveClassId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing session payment for session {SessionId}", liveClassId);
            SetErrorMessage("حدث خطأ أثناء معالجة الدفع");
            return RedirectToAction(nameof(Checkout), new { id = liveClassId });
        }
    }

    /// <summary>
    /// صفحة نجاح الدفع - Payment Success Page
    /// </summary>
    public async Task<IActionResult> PaymentSuccess(int liveClassId)
    {
        var session = await _context.LiveClasses
            .Include(lc => lc.Instructor)
            .FirstOrDefaultAsync(lc => lc.Id == liveClassId);

        if (session == null)
            return RedirectToAction("Index", "LiveClasses", new { area = "Student" });

        ViewBag.Session = session;
        return View();
    }

    /// <summary>
    /// صفحة فشل الدفع - Payment Failed Page
    /// </summary>
    public IActionResult PaymentFailed(int liveClassId)
    {
        ViewBag.LiveClassId = liveClassId;
        return View();
    }
}
