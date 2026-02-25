using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة العمولات - Commissions Controller
/// Handles instructor earnings and commission tracking
/// </summary>
public class CommissionsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CommissionsController> _logger;

    public CommissionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CommissionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة العمولات - Commissions List
    /// </summary>
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;

        // Validate user authentication
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Commissions page accessed without valid UserId");
            SetErrorMessage("لم يتم التعرف على المستخدم. يرجى تسجيل الدخول مرة أخرى");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            _logger.LogInformation("Instructor {InstructorId} accessing commissions list. FromDate: {FromDate}, ToDate: {ToDate}", 
                userId, fromDate, toDate);

            var query = _context.InstructorEarnings
                .Include(ie => ie.Course)
                .Include(ie => ie.Payment)
                .Where(ie => ie.InstructorId == userId)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(ie => ie.EarnedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(ie => ie.EarnedAt <= toDate.Value.AddDays(1)); // Include the entire end date
            }

            var earnings = await query
                .OrderByDescending(ie => ie.EarnedAt)
                .ToListAsync();

            var summary = new
            {
                TotalEarnings = earnings.Sum(e => e.InstructorAmount),
                TotalCommission = earnings.Sum(e => e.PlatformCommission),
                Count = earnings.Count
            };

            ViewBag.Summary = summary;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            _logger.LogInformation("Instructor {InstructorId} viewed {Count} commission records", userId, earnings.Count);

            return View(earnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading commissions list for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل قائمة العمولات");
            
            ViewBag.Summary = new { TotalEarnings = 0m, TotalCommission = 0m, Count = 0 };
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            
            return View(new List<Domain.Entities.Financial.InstructorEarning>());
        }
    }

    /// <summary>
    /// تفاصيل العمولة - Commission Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        // Validate user authentication
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Commission details accessed without valid UserId. EarningId: {EarningId}", id);
            SetErrorMessage("لم يتم التعرف على المستخدم. يرجى تسجيل الدخول مرة أخرى");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // First check if earning exists at all
            var earningExists = await _context.InstructorEarnings.AnyAsync(ie => ie.Id == id);
            
            if (!earningExists)
            {
                _logger.LogWarning("Commission {EarningId} not found in database", id);
                SetErrorMessage("العمولة غير موجودة");
                return NotFound();
            }

            // Now check with ownership
            var earning = await _context.InstructorEarnings
                .Include(ie => ie.Course)
                .Include(ie => ie.Payment)
                    .ThenInclude(p => p.Student)
                .FirstOrDefaultAsync(ie => ie.Id == id && ie.InstructorId == userId);

            if (earning == null)
            {
                // Earning exists but doesn't belong to this instructor - potential unauthorized access
                _logger.LogWarning("SECURITY: Instructor {InstructorId} attempted to access commission {EarningId} belonging to another instructor", 
                    userId, id);
                SetErrorMessage("غير مصرح لك بعرض هذه العمولة");
                return Forbid();
            }

            _logger.LogInformation("Instructor {InstructorId} viewed commission details {EarningId}", userId, id);

            return View(earning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading commission details {EarningId} for instructor {InstructorId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل العمولة");
            return RedirectToAction(nameof(Index));
        }
    }
}

