using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// الدعم الفني - Support Controller
/// </summary>
public class SupportController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SupportController> _logger;
    private readonly ICurrentUserService _currentUserService;

    public SupportController(
        ApplicationDbContext context, 
        ILogger<SupportController> logger,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// قائمة التذاكر - Tickets list
    /// </summary>
    public async Task<IActionResult> Index(TicketStatus? status, TicketPriority? priority, int page = 1)
    {
        // Get all tickets for stats (without pagination)
        var allTickets = _context.SupportTickets.AsQueryable();
        
        // Stats for cards (before filtering)
        ViewBag.OpenCount = await allTickets.CountAsync(t => t.Status == TicketStatus.Open);
        ViewBag.InProgressCount = await allTickets.CountAsync(t => t.Status == TicketStatus.InProgress);
        ViewBag.ResolvedCount = await allTickets.CountAsync(t => t.Status == TicketStatus.Resolved);
        ViewBag.ClosedCount = await allTickets.CountAsync(t => t.Status == TicketStatus.Closed);
        
        var query = _context.SupportTickets
            .Include(t => t.User)
            .Include(t => t.CategoryEntity)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        
        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

        return View(tickets);
    }

    /// <summary>
    /// تفاصيل التذكرة - Ticket details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.User)
            .Include(t => t.CategoryEntity)
            .Include(t => t.Replies)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound();

        return View(ticket);
    }

    /// <summary>
    /// الرد على التذكرة - Reply to ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string message)
    {
        var ticket = await _context.SupportTickets.FindAsync(id);
        if (ticket == null)
            return NotFound();

        var reply = new Domain.Entities.Support.TicketReply
        {
            TicketId = id,
            Message = message,
            IsFromStaff = true,
            SenderId = _currentUserService.UserId ?? throw new InvalidOperationException("User not authenticated")
        };

        ticket.Status = TicketStatus.InProgress;
        _context.TicketReplies.Add(reply);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إرسال الرد بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إغلاق التذكرة - Close ticket
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var ticket = await _context.SupportTickets.FindAsync(id);
        if (ticket == null)
            return NotFound();

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إغلاق التذكرة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تصنيفات التذاكر - Ticket categories
    /// </summary>
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.TicketCategories
            .Select(c => new {
                Category = c,
                TicketCount = _context.SupportTickets.Count(t => t.CategoryId == c.Id)
            })
            .ToListAsync();
        
        ViewBag.TicketCounts = categories.ToDictionary(c => c.Category.Id, c => c.TicketCount);
        return View(categories.Select(c => c.Category).ToList());
    }

    /// <summary>
    /// إنشاء تصنيف جديد - Create new category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("اسم التصنيف مطلوب");
            return RedirectToAction(nameof(Categories));
        }

        var category = new Domain.Entities.Support.TicketCategory
        {
            Name = name,
            Description = description ?? string.Empty
        };

        _context.TicketCategories.Add(category);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إنشاء التصنيف بنجاح");
        return RedirectToAction(nameof(Categories));
    }

    /// <summary>
    /// تعديل تصنيف - Update category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, string name, string? description)
    {
        var category = await _context.TicketCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("اسم التصنيف مطلوب");
            return RedirectToAction(nameof(Categories));
        }

        category.Name = name;
        category.Description = description ?? string.Empty;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديث التصنيف بنجاح");
        return RedirectToAction(nameof(Categories));
    }

    /// <summary>
    /// حذف تصنيف - Delete category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.TicketCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        // Check if there are tickets using this category
        var hasTickets = await _context.SupportTickets.AnyAsync(t => t.CategoryId == id);
        if (hasTickets)
        {
            SetErrorMessage("لا يمكن حذف التصنيف لأنه مرتبط بتذاكر");
            return RedirectToAction(nameof(Categories));
        }

        _context.TicketCategories.Remove(category);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التصنيف بنجاح");
        return RedirectToAction(nameof(Categories));
    }
}

