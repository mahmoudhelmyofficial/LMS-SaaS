using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// سجل الأخطاء - Error Logs Controller
/// View and manage application error logs from database
/// </summary>
public class ErrorLogsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ErrorLogsController> _logger;

    public ErrorLogsController(ApplicationDbContext context, ILogger<ErrorLogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// عرض قائمة الأخطاء - Display error list
    /// </summary>
    public async Task<IActionResult> Index(string? search = null, int days = 7, bool? resolved = null)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        var query = _context.ApplicationErrors
            .Where(e => e.Timestamp >= fromDate)
            .AsQueryable();

        // Filter by resolved status
        if (resolved.HasValue)
        {
            query = query.Where(e => e.IsResolved == resolved.Value);
        }

        // Filter by search term
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(e =>
                (e.Path != null && e.Path.ToLower().Contains(searchLower)) ||
                (e.Message != null && e.Message.ToLower().Contains(searchLower)) ||
                (e.ExceptionType != null && e.ExceptionType.ToLower().Contains(searchLower)) ||
                (e.StackTrace != null && e.StackTrace.ToLower().Contains(searchLower)) ||
                (e.UserName != null && e.UserName.ToLower().Contains(searchLower))
            );
        }

        var errors = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(200)
            .ToListAsync();

        // Stats
        ViewBag.TotalErrors = await _context.ApplicationErrors.CountAsync(e => e.Timestamp >= fromDate);
        ViewBag.TodayErrors = await _context.ApplicationErrors.CountAsync(e => e.Timestamp >= DateTime.UtcNow.Date);
        ViewBag.UnresolvedErrors = await _context.ApplicationErrors.CountAsync(e => !e.IsResolved && e.Timestamp >= fromDate);
        ViewBag.UniqueTypes = await _context.ApplicationErrors
            .Where(e => e.Timestamp >= fromDate)
            .Select(e => e.ExceptionType)
            .Distinct()
            .CountAsync();

        ViewBag.Search = search;
        ViewBag.Days = days;
        ViewBag.Resolved = resolved;

        return View(errors);
    }

    /// <summary>
    /// عرض تفاصيل الخطأ - Display error details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var error = await _context.ApplicationErrors.FindAsync(id);
        if (error == null)
        {
            return NotFound();
        }

        return View(error);
    }

    /// <summary>
    /// تحديد الخطأ كمحلول - Mark error as resolved
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkResolved(int id, string? notes = null)
    {
        var error = await _context.ApplicationErrors.FindAsync(id);
        if (error == null)
        {
            return NotFound();
        }

        error.IsResolved = true;
        error.ResolvedAt = DateTime.UtcNow;
        error.ResolutionNotes = notes;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديد الخطأ كمحلول - Error marked as resolved");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تحديد كل الأخطاء كمحلولة - Mark all errors as resolved
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllResolved()
    {
        var unresolvedErrors = await _context.ApplicationErrors
            .Where(e => !e.IsResolved)
            .ToListAsync();

        foreach (var error in unresolvedErrors)
        {
            error.IsResolved = true;
            error.ResolvedAt = DateTime.UtcNow;
            error.ResolutionNotes = "Marked resolved in bulk";
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم تحديد {unresolvedErrors.Count} خطأ كمحلول - {unresolvedErrors.Count} errors marked as resolved");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الأخطاء القديمة - Delete old errors
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOldErrors(int olderThanDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        
        var oldErrors = await _context.ApplicationErrors
            .Where(e => e.Timestamp < cutoffDate)
            .ToListAsync();

        _context.ApplicationErrors.RemoveRange(oldErrors);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {oldErrors.Count} خطأ قديم - {oldErrors.Count} old errors deleted");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف كل الأخطاء المحلولة - Delete all resolved errors
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearResolvedErrors()
    {
        var resolvedErrors = await _context.ApplicationErrors
            .Where(e => e.IsResolved)
            .ToListAsync();

        _context.ApplicationErrors.RemoveRange(resolvedErrors);
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم حذف {resolvedErrors.Count} خطأ محلول - {resolvedErrors.Count} resolved errors deleted");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الحصول على تفاصيل الخطأ كـ JSON - Get error details as JSON for copying
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetErrorJson(int id)
    {
        var error = await _context.ApplicationErrors.FindAsync(id);
        if (error == null)
        {
            return NotFound();
        }

        var errorDetails = new
        {
            error.Id,
            Timestamp = error.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            error.Path,
            error.HttpMethod,
            error.UserName,
            error.UserRoles,
            error.ExceptionType,
            error.Message,
            error.StackTrace,
            error.InnerException,
            error.StatusCode,
            error.RequestInfo,
            error.IpAddress
        };

        return Json(errorDetails);
    }
}
