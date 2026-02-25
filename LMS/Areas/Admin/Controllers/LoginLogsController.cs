using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// سجلات تسجيل الدخول - Login Logs Controller
/// </summary>
public class LoginLogsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoginLogsController> _logger;

    public LoginLogsController(
        ApplicationDbContext context,
        ILogger<LoginLogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة سجلات الدخول - Login Logs List
    /// </summary>
    public async Task<IActionResult> Index(string? userId, bool? isSuccessful, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.LoginLogs
            .Include(ll => ll.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(ll => ll.UserId == userId);
        }

        if (isSuccessful.HasValue)
        {
            query = query.Where(ll => ll.IsSuccessful == isSuccessful.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(ll => ll.AttemptedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ll => ll.AttemptedAt <= toDate.Value);
        }

        var logs = await query
            .OrderByDescending(ll => ll.AttemptedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.IsSuccessful = isSuccessful;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(logs);
    }

    /// <summary>
    /// تفاصيل السجل - Log Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var log = await _context.LoginLogs
            .Include(ll => ll.User)
            .FirstOrDefaultAsync(ll => ll.Id == id);

        if (log == null)
            return NotFound();

        return View(log);
    }

    /// <summary>
    /// إحصائيات تسجيل الدخول - Login Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.LoginLogs.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(ll => ll.AttemptedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ll => ll.AttemptedAt <= toDate.Value);
        }

        var logs = await query.ToListAsync();

        var stats = new
        {
            TotalAttempts = logs.Count,
            SuccessfulLogins = logs.Count(l => l.IsSuccessful),
            FailedLogins = logs.Count(l => !l.IsSuccessful),
            UniqueUsers = logs.Where(l => l.IsSuccessful).Select(l => l.UserId).Distinct().Count(),
            TopCountries = logs.GroupBy(l => l.Country)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { Country = g.Key, Count = g.Count() })
                .ToList(),
            MostActiveHour = logs.GroupBy(l => l.LoginTime.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? 0
        };

        ViewBag.Stats = stats;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View();
    }

    /// <summary>
    /// حذف السجلات القديمة - Delete Old Logs
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOldLogs(int daysOld = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        
        var oldLogs = await _context.LoginLogs
            .Where(ll => ll.AttemptedAt < cutoffDate)
            .ToListAsync();

        var count = oldLogs.Count;

        _context.LoginLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {count} سجل قديم");
        return RedirectToAction(nameof(Index));
    }
}

