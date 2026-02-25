using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة جلسات المستخدمين - User Sessions Controller
/// </summary>
public class UserSessionsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserSessionsController> _logger;

    public UserSessionsController(
        ApplicationDbContext context,
        ILogger<UserSessionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الجلسات النشطة - Active Sessions List
    /// </summary>
    public async Task<IActionResult> Index(string? userId, bool? isActive)
    {
        var query = _context.UserSessions
            .Include(us => us.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(us => us.UserId == userId);
        }

        if (isActive.HasValue)
        {
            query = query.Where(us => us.IsActive == isActive.Value);
        }

        var sessions = await query
            .OrderByDescending(us => us.LastActivityAt)
            .Take(200)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.IsActive = isActive;

        return View(sessions);
    }

    /// <summary>
    /// تفاصيل الجلسة - Session Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var session = await _context.UserSessions
            .Include(us => us.User)
            .FirstOrDefaultAsync(us => us.Id == id);

        if (session == null)
            return NotFound();

        return View(session);
    }

    /// <summary>
    /// إنهاء الجلسة - Terminate Session
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int id)
    {
        var session = await _context.UserSessions.FindAsync(id);
        if (session == null)
            return NotFound();

        session.IsActive = false;
        session.LogoutTime = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إنهاء الجلسة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إنهاء جميع جلسات المستخدم - Terminate All User Sessions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateAllUserSessions(string userId)
    {
        var sessions = await _context.UserSessions
            .Where(us => us.UserId == userId && us.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم إنهاء {sessions.Count} جلسة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تنظيف الجلسات المنتهية - Clean Expired Sessions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanExpiredSessions(int hoursInactive = 24)
    {
        var cutoffDate = DateTime.UtcNow.AddHours(-hoursInactive);
        
        var expiredSessions = await _context.UserSessions
            .Where(us => us.IsActive && us.LastActivityAt < cutoffDate)
            .ToListAsync();

        var count = expiredSessions.Count;

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم تنظيف {count} جلسة منتهية");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إنهاء جميع الجلسات النشطة - Terminate All Active Sessions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateAll()
    {
        var activeSessions = await _context.UserSessions
            .Where(us => us.IsActive)
            .ToListAsync();

        var count = activeSessions.Count;

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning("Admin terminated all {Count} active sessions", count);
        SetSuccessMessage($"تم إنهاء {count} جلسة نشطة");
        return RedirectToAction(nameof(Index));
    }
}

