using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// سجلات نشاط النظام - System Activity Logs Controller
/// </summary>
public class ActivityLogsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ActivityLogsController> _logger;

    public ActivityLogsController(
        ApplicationDbContext context,
        ILogger<ActivityLogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة السجلات - Activity Logs List
    /// </summary>
    public async Task<IActionResult> Index(string? userId, string? activityType, string? entityType, DateTime? fromDate)
    {
        var query = _context.ActivityLogs
            .Include(al => al.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(al => al.UserId == userId);
        }

        if (!string.IsNullOrEmpty(activityType))
        {
            query = query.Where(al => al.ActivityType == activityType);
        }

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(al => al.EntityType == entityType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(al => al.Timestamp >= fromDate.Value);
        }

        var logs = await query
            .OrderByDescending(al => al.Timestamp)
            .Take(500)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.ActivityType = activityType;
        ViewBag.EntityType = entityType;
        ViewBag.FromDate = fromDate;

        return View(logs);
    }

    /// <summary>
    /// تفاصيل السجل - Log Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var log = await _context.ActivityLogs
            .Include(al => al.User)
            .FirstOrDefaultAsync(al => al.Id == id);

        if (log == null)
            return NotFound();

        return View(log);
    }

    /// <summary>
    /// حذف السجلات القديمة - Delete Old Logs
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOldLogs(int daysOld = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        
        var oldLogs = await _context.ActivityLogs
            .Where(al => al.Timestamp < cutoffDate)
            .ToListAsync();

        var count = oldLogs.Count;

        _context.ActivityLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {count} سجل قديم");
        return RedirectToAction(nameof(Index));
    }
}

