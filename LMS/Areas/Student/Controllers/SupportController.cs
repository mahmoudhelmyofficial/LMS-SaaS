using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Support;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الدعم الفني - Support Controller
/// </summary>
public class SupportController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<SupportController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة تذاكر الدعم - Support Tickets List
    /// </summary>
    public async Task<IActionResult> Index(TicketStatus? status)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var query = _context.SupportTickets
            .Include(t => t.CategoryEntity)
            .Include(t => t.AssignedTo)
            .Where(t => t.UserId == userId)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.Status = status;
        return View(tickets);
    }

    /// <summary>
    /// تفاصيل التذكرة - Ticket Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var ticket = await _context.SupportTickets
            .Include(t => t.User)
            .Include(t => t.CategoryEntity)
            .Include(t => t.AssignedTo)
            .Include(t => t.Replies)
                .ThenInclude(r => r.Sender)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null)
            return NotFound();

        return View(ticket);
    }

    /// <summary>
    /// إنشاء تذكرة جديدة - Create new ticket
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _context.TicketCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        return View(new SupportTicketCreateViewModel());
    }

    /// <summary>
    /// حفظ التذكرة الجديدة - Save new ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupportTicketCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (ModelState.IsValid)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            var ticket = new SupportTicket
            {
                UserId = userId!,
                TicketNumber = GenerateTicketNumber(),
                Subject = model.Subject,
                Description = model.Description,
                CategoryId = model.CategoryId,
                Priority = model.Priority,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Status = TicketStatus.New
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء التذكرة بنجاح. رقم التذكرة: " + ticket.TicketNumber);
            return RedirectToAction(nameof(Details), new { id = ticket.Id });
        }

        ViewBag.Categories = await _context.TicketCategories
            .Where(c => c.IsActive)
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// إضافة رد على التذكرة - Add Reply to Ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReply(int ticketId, string message)
    {
        var userId = _currentUserService.UserId;

        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId);

        if (ticket == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(message))
        {
            SetErrorMessage("الرجاء إدخال رسالة");
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var reply = new TicketReply
        {
            TicketId = ticketId,
            UserId = userId!,
            Message = message,
            IsStaffReply = false
        };

        _context.TicketReplies.Add(reply);

        // Update ticket
        ticket.LastReplyAt = DateTime.UtcNow;
        ticket.LastReplyByStaff = false;

        // Reopen ticket if it was closed
        if (ticket.Status == TicketStatus.Closed)
        {
            ticket.Status = TicketStatus.InProgress;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إضافة ردك بنجاح");
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    /// <summary>
    /// إغلاق التذكرة - Close Ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var userId = _currentUserService.UserId;

        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null)
            return NotFound();

        if (ticket.Status == TicketStatus.Closed)
        {
            SetErrorMessage("التذكرة مغلقة بالفعل");
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إغلاق التذكرة بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إعادة فتح التذكرة - Reopen Ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id)
    {
        var userId = _currentUserService.UserId;

        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null)
            return NotFound();

        if (ticket.Status != TicketStatus.Closed)
        {
            SetErrorMessage("التذكرة ليست مغلقة");
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.Status = TicketStatus.InProgress;
        ticket.ClosedAt = null;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إعادة فتح التذكرة بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تقييم الدعم - Rate Support
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int id, int rating, string? feedback)
    {
        var userId = _currentUserService.UserId;

        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null)
            return NotFound();

        if (rating < 1 || rating > 5)
        {
            SetErrorMessage("التقييم يجب أن يكون بين 1 و 5");
            return RedirectToAction(nameof(Details), new { id });
        }

        ticket.Rating = rating;
        ticket.RatingFeedback = feedback;

        await _context.SaveChangesAsync();

        SetSuccessMessage("شكراً لتقييمك");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// توليد رقم تذكرة فريد - Generate unique ticket number
    /// </summary>
    private string GenerateTicketNumber()
    {
        return "TKT-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
    }
}

