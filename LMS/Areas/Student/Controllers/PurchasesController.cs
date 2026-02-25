using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// المشتريات - Purchases Controller
/// </summary>
public class PurchasesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PurchasesController> _logger;

    public PurchasesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<PurchasesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// سجل المشتريات - Purchase history
    /// </summary>
    public async Task<IActionResult> History(int page = 1)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var pageSize = 20;

        var payments = await _context.Payments
            .Include(p => p.Course)
                .ThenInclude(c => c.Instructor)
            .Include(p => p.Book)
                .ThenInclude(b => b!.Instructor)
            .Include(p => p.BookBundle)
                .ThenInclude(bb => bb!.Instructor)
            .Include(p => p.Invoice)
            .Where(p => p.StudentId == userId && p.Status == Domain.Enums.PaymentStatus.Completed)
            .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPayments = await _context.Payments
            .CountAsync(p => p.StudentId == userId && p.Status == Domain.Enums.PaymentStatus.Completed);

        // Calculate totals
        var totalSpent = await _context.Payments
            .Where(p => p.StudentId == userId && p.Status == Domain.Enums.PaymentStatus.Completed)
            .SumAsync(p => p.TotalAmount);

        var totalCourses = await _context.Enrollments
            .CountAsync(e => e.StudentId == userId && !e.IsFree);

        var viewModel = new PurchaseHistoryViewModel
        {
            Payments = payments,
            TotalSpent = totalSpent,
            TotalCoursesPurchased = totalCourses,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalPayments / (double)pageSize)
        };

        return View(viewModel);
    }

    /// <summary>
    /// Index - Redirects to History
    /// </summary>
    public IActionResult Index()
    {
        return RedirectToAction(nameof(History));
    }

    /// <summary>
    /// تفاصيل المشتراة - Purchase details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .Include(p => p.Course)
                .ThenInclude(c => c.Instructor)
            .Include(p => p.Book)
                .ThenInclude(b => b!.Instructor)
            .Include(p => p.BookBundle)
                .ThenInclude(bb => bb!.Instructor)
            .Include(p => p.Invoice)
            .Include(p => p.Enrollment)
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

        if (payment == null)
            return NotFound();

        return View(payment);
    }

    /// <summary>
    /// تحميل الفاتورة - Download invoice
    /// </summary>
    public async Task<IActionResult> DownloadInvoice(int paymentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var payment = await _context.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.StudentId == userId);

        if (payment?.Invoice == null)
            return NotFound();

        // For now, redirect to invoice view
        return RedirectToAction("View", "Invoices", new { id = payment.Invoice.Id });
    }
}

#region View Models

public class PurchaseHistoryViewModel
{
    public List<Domain.Entities.Payments.Payment> Payments { get; set; } = new();
    public decimal TotalSpent { get; set; }
    public int TotalCoursesPurchased { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}

#endregion

