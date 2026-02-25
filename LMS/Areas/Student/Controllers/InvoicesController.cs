using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الفواتير - Invoices Controller
/// </summary>
public class InvoicesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfGenerationService _pdfService;
    private readonly IInvoicePdfService _invoicePdfService;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPdfGenerationService pdfService,
        IInvoicePdfService invoicePdfService,
        ILogger<InvoicesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pdfService = pdfService;
        _invoicePdfService = invoicePdfService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الفواتير - Invoices list
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var query = _context.Invoices
            .Include(i => i.Payment)
                .ThenInclude(p => p.Course)
            .Where(i => i.StudentId == userId);

        var invoices = await query
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(i => new InvoiceDisplayViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                IssuedDate = i.IssuedAt,
                DueDate = i.DueDate,
                Status = i.Status,
                SubTotal = i.SubTotal,
                TaxAmount = i.Tax,
                DiscountAmount = i.Discount,
                TotalAmount = i.TotalAmount,
                Currency = i.Currency,
                CourseName = i.Payment.Course != null ? i.Payment.Course.Title : null
            })
            .ToListAsync();

        ViewBag.Page = page;

        return View(invoices);
    }

    /// <summary>
    /// عرض الفاتورة بالمعامل - View invoice by transaction id (e.g. from checkout success).
    /// Redirects to View(id) for the first invoice of that transaction.
    /// </summary>
    public async Task<IActionResult> ViewByTransaction(string transactionId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        transactionId = transactionId?.Trim();
        if (string.IsNullOrEmpty(transactionId) || transactionId.Length > 255)
            return NotFound();

        var invoice = await _context.Invoices
            .Include(i => i.Payment)
            .Where(i => i.StudentId == userId && i.Payment != null && i.Payment.TransactionId == transactionId)
            .OrderBy(i => i.Id)
            .FirstOrDefaultAsync();

        if (invoice == null)
            return NotFound();

        return RedirectToAction(nameof(View), new { id = invoice.Id });
    }

    /// <summary>
    /// عرض الفاتورة - View invoice
    /// </summary>
    public async Task<IActionResult> View(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var invoice = await _context.Invoices
            .Include(i => i.Payment)
                .ThenInclude(p => p.Course)
                    .ThenInclude(c => c.Instructor)
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == id && i.StudentId == userId);

        if (invoice == null)
            return NotFound();

        return View(invoice);
    }

    /// <summary>
    /// تحميل الفاتورة PDF - Download invoice as PDF
    /// </summary>
    public async Task<IActionResult> Download(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var invoice = await _context.Invoices
            .Include(i => i.Payment)
                .ThenInclude(p => p.Course)
            .Include(i => i.Student)
            .FirstOrDefaultAsync(i => i.Id == id && i.StudentId == userId);

        if (invoice == null)
            return NotFound();

        try
        {
            // Generate PDF
            var pdfBytes = _pdfService.GenerateInvoicePdf(invoice);
            
            var fileName = $"Invoice_{invoice.InvoiceNumber}.pdf";
            
            _logger.LogInformation("Invoice PDF generated for invoice {InvoiceId} by student {StudentId}", 
                id, userId);
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice PDF for invoice {InvoiceId}", id);
            SetErrorMessage("حدث خطأ أثناء إنشاء ملف PDF");
            return RedirectToAction(nameof(View), new { id });
        }
    }

    /// <summary>
    /// تحميل الفاتورة HTML - Download invoice as printable HTML
    /// </summary>
    public async Task<IActionResult> DownloadHtml(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var invoice = await _context.Invoices
            .Include(i => i.Payment)
            .FirstOrDefaultAsync(i => i.Id == id && i.StudentId == userId);

        if (invoice == null)
            return NotFound();

        try
        {
            var result = await _invoicePdfService.GenerateInvoicePdfAsync(id);
            
            if (!result.Success || string.IsNullOrEmpty(result.HtmlContent))
            {
                SetErrorMessage(result.ErrorMessage ?? "حدث خطأ أثناء إنشاء الفاتورة");
                return RedirectToAction(nameof(View), new { id });
            }

            _logger.LogInformation("Invoice HTML generated for invoice {InvoiceId} by student {StudentId}", 
                id, userId);

            return Content(result.HtmlContent, "text/html", System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice HTML for invoice {InvoiceId}", id);
            SetErrorMessage("حدث خطأ أثناء إنشاء الفاتورة");
            return RedirectToAction(nameof(View), new { id });
        }
    }

    /// <summary>
    /// طباعة الفاتورة - Print invoice (opens printable version)
    /// </summary>
    public async Task<IActionResult> Print(int id)
    {
        return await DownloadHtml(id);
    }
}

