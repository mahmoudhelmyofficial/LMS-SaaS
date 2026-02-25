using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Security;
using LMS.Domain.Entities.Users;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الأمان - Security Management Controller
/// </summary>
public class SecurityController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SecurityController> _logger;
    private readonly ISystemConfigurationService _configService;

    public SecurityController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<SecurityController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// لوحة الأمان - Security Dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Get security statistics
        var today = DateTime.UtcNow.Date;
        var lastWeek = today.AddDays(-7);

        // Login stats - use AttemptedAt instead of NotMapped LoginAt
        var recentLogins = await _context.LoginLogs
            .Where(l => l.AttemptedAt >= lastWeek)
            .ToListAsync();

        var successfulLogins = recentLogins.Count(l => l.IsSuccessful);
        var failedLogins = recentLogins.Count(l => !l.IsSuccessful);

        // Calculate security score
        var twoFactorEnabled = await _context.Users.CountAsync(u => u.TwoFactorEnabled);
        var totalUsers = await _context.Users.CountAsync();
        var twoFactorRate = totalUsers > 0 ? (twoFactorEnabled * 100 / totalUsers) : 0;

        // Security checks
        var isDataEncrypted = true; // Always true (built-in)
        var isCsrfEnabled = true; // Always true (built-in)
        var isAuditLogEnabled = true; // Always true
        var isTwoFactorEnforced = twoFactorRate >= 50;

        // Calculate overall security score
        var securityChecks = new[] { isDataEncrypted, isCsrfEnabled, isAuditLogEnabled, isTwoFactorEnforced };
        var securityScore = (securityChecks.Count(c => c) * 100) / securityChecks.Length;

        // Get recent security events - use AttemptedAt instead of NotMapped LoginAt
        var recentEvents = await _context.LoginLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.AttemptedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.SecurityScore = securityScore;
        ViewBag.IsDataEncrypted = isDataEncrypted;
        ViewBag.IsCsrfEnabled = isCsrfEnabled;
        ViewBag.IsAuditLogEnabled = isAuditLogEnabled;
        ViewBag.IsTwoFactorEnforced = isTwoFactorEnforced;
        ViewBag.SuccessfulLogins = successfulLogins;
        ViewBag.FailedLogins = failedLogins;
        ViewBag.TwoFactorRate = twoFactorRate;
        ViewBag.RecentEvents = recentEvents;

        return View();
    }

    #region Security Navigation Actions

    /// <summary>
    /// جلسات المستخدمين - User Sessions (redirects to ActiveSessions)
    /// </summary>
    public IActionResult UserSessions()
    {
        return RedirectToAction(nameof(ActiveSessions));
    }

    /// <summary>
    /// سجل النشاط - Activity Log (redirects to LoginLogs)
    /// </summary>
    public IActionResult ActivityLog()
    {
        return RedirectToAction(nameof(LoginLogs));
    }

    /// <summary>
    /// المصادقة الثنائية - Two Factor (redirects to TwoFactorSettings)
    /// </summary>
    public IActionResult TwoFactor()
    {
        return RedirectToAction(nameof(TwoFactorSettings));
    }

    #endregion

    #region Roles & Permissions

    /// <summary>
    /// إدارة الأدوار - Roles Management
    /// </summary>
    public async Task<IActionResult> Roles()
    {
        var roles = await _context.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        // Get user count per role
        var roleCounts = new Dictionary<string, int>();
        foreach (var role in roles)
        {
            var count = await _context.UserRoles.CountAsync(ur => ur.RoleId == role.Id);
            roleCounts[role.Id] = count;
        }

        ViewBag.RoleCounts = roleCounts;
        return View(roles);
    }

    /// <summary>
    /// إنشاء دور جديد - Create new role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string roleName, string? description)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetErrorMessage("يجب إدخال اسم الدور");
            return RedirectToAction(nameof(Roles));
        }

        // Check if role exists
        var exists = await _context.Roles.AnyAsync(r => r.NormalizedName == roleName.ToUpperInvariant());
        if (exists)
        {
            SetErrorMessage("الدور موجود بالفعل");
            return RedirectToAction(nameof(Roles));
        }

        var role = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleName} created", roleName);
        SetSuccessMessage("تم إنشاء الدور بنجاح");
        return RedirectToAction(nameof(Roles));
    }

    /// <summary>
    /// حذف دور - Delete role
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        // Check if role has users
        var hasUsers = await _context.UserRoles.AnyAsync(ur => ur.RoleId == id);
        if (hasUsers)
        {
            SetErrorMessage("لا يمكن حذف دور يحتوي على مستخدمين");
            return RedirectToAction(nameof(Roles));
        }

        // Prevent deleting system roles
        var systemRoles = new[] { "Admin", "Instructor", "Student" };
        if (systemRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
        {
            SetErrorMessage("لا يمكن حذف الأدوار الأساسية للنظام");
            return RedirectToAction(nameof(Roles));
        }

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role {RoleName} deleted", role.Name);
        SetSuccessMessage("تم حذف الدور بنجاح");
        return RedirectToAction(nameof(Roles));
    }

    /// <summary>
    /// إدارة الصلاحيات - Permissions Management
    /// </summary>
    public async Task<IActionResult> Permissions()
    {
        // Get all permissions from the system
        var permissions = new List<PermissionViewModel>
        {
            // User Management
            new() { Category = "إدارة المستخدمين", Name = "Users.View", DisplayName = "عرض المستخدمين" },
            new() { Category = "إدارة المستخدمين", Name = "Users.Create", DisplayName = "إضافة مستخدم" },
            new() { Category = "إدارة المستخدمين", Name = "Users.Edit", DisplayName = "تعديل مستخدم" },
            new() { Category = "إدارة المستخدمين", Name = "Users.Delete", DisplayName = "حذف مستخدم" },
            
            // Courses Management
            new() { Category = "إدارة الدورات", Name = "Courses.View", DisplayName = "عرض الدورات" },
            new() { Category = "إدارة الدورات", Name = "Courses.Create", DisplayName = "إنشاء دورة" },
            new() { Category = "إدارة الدورات", Name = "Courses.Edit", DisplayName = "تعديل دورة" },
            new() { Category = "إدارة الدورات", Name = "Courses.Delete", DisplayName = "حذف دورة" },
            new() { Category = "إدارة الدورات", Name = "Courses.Approve", DisplayName = "الموافقة على الدورات" },
            
            // Financial
            new() { Category = "المالية", Name = "Payments.View", DisplayName = "عرض المدفوعات" },
            new() { Category = "المالية", Name = "Payments.Process", DisplayName = "معالجة المدفوعات" },
            new() { Category = "المالية", Name = "Refunds.View", DisplayName = "عرض المستردات" },
            new() { Category = "المالية", Name = "Refunds.Process", DisplayName = "معالجة المستردات" },
            
            // Reports
            new() { Category = "التقارير", Name = "Reports.View", DisplayName = "عرض التقارير" },
            new() { Category = "التقارير", Name = "Reports.Export", DisplayName = "تصدير التقارير" },
            
            // Settings
            new() { Category = "الإعدادات", Name = "Settings.View", DisplayName = "عرض الإعدادات" },
            new() { Category = "الإعدادات", Name = "Settings.Edit", DisplayName = "تعديل الإعدادات" }
        };

        var roles = await _context.Roles.OrderBy(r => r.Name).ToListAsync();
        ViewBag.Roles = roles;

        return View(permissions);
    }

    #endregion

    #region Login Logs

    /// <summary>
    /// سجلات تسجيل الدخول - Login logs
    /// </summary>
    public async Task<IActionResult> LoginLogs(DateTime? from, DateTime? to, bool? success, string? userId, int page = 1)
    {
        try
        {
            var query = _context.LoginLogs
                .Include(l => l.User)
                .AsQueryable();

            // Use AttemptedAt instead of LoginAt (NotMapped property) for SQL translation
            if (from.HasValue)
                query = query.Where(l => l.AttemptedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(l => l.AttemptedAt <= to.Value);

            if (success.HasValue)
                query = query.Where(l => l.IsSuccessful == success.Value);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(l => l.UserId == userId);

            var pageSize = await _configService.GetPaginationSizeAsync("login_logs", 50);
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var logs = await query
                .OrderByDescending(l => l.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Success = success;
            ViewBag.UserId = userId;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل سجلات تسجيل الدخول - Error loading login logs");
            SetWarningMessage("تعذر تحميل سجلات تسجيل الدخول. يرجى المحاولة مرة أخرى.");
            
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Success = success;
            ViewBag.UserId = userId;
            ViewBag.Page = page;
            
            return View(new List<LoginLog>());
        }
    }

    /// <summary>
    /// تفاصيل سجل الدخول - Login log details
    /// </summary>
    public async Task<IActionResult> LoginLogDetails(int id)
    {
        var log = await _context.LoginLogs
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (log == null)
            return NotFound();

        return View(log);
    }

    /// <summary>
    /// تصدير سجلات الدخول - Export login logs
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportLoginLogs(DateTime? from, DateTime? to)
    {
        var query = _context.LoginLogs
            .Include(l => l.User)
            .AsQueryable();

        // Use AttemptedAt instead of NotMapped LoginAt for SQL translation
        if (from.HasValue)
            query = query.Where(l => l.AttemptedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.AttemptedAt <= to.Value);

        var logs = await query
            .OrderByDescending(l => l.AttemptedAt)
            .ToListAsync();

        // Generate CSV export
        try
        {
            var csv = "ID,User Name,User Email,IP Address,User Agent,Login At,Is Successful,Failure Reason\n";
            foreach (var log in logs)
            {
                var userName = log.User != null 
                    ? $"{log.User.FirstName ?? ""} {log.User.LastName ?? ""}".Trim() 
                    : "Unknown User";
                var userEmail = log.User?.Email ?? "";
                var userAgent = log.UserAgent?.Replace("\"", "\"\"") ?? "";
                var failureReason = log.FailureReason?.Replace("\"", "\"\"") ?? "";
                
                csv += $"{log.Id}," +
                       $"\"{userName}\"," +
                       $"\"{userEmail}\"," +
                       $"\"{log.IpAddress}\"," +
                       $"\"{userAgent}\"," +
                       $"{log.LoginAt:yyyy-MM-dd HH:mm}," +
                       $"{log.IsSuccessful}," +
                       $"\"{failureReason}\"\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"login-logs-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Login logs export generated. Total: {Count} records", logs.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting login logs");
            SetErrorMessage("حدث خطأ أثناء التصدير");
            return RedirectToAction(nameof(LoginLogs));
        }
    }

    #endregion

    #region User Sessions

    /// <summary>
    /// الجلسات النشطة - Active sessions
    /// </summary>
    public async Task<IActionResult> ActiveSessions(string? userId, int page = 1)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            // Use actual mapped properties instead of computed IsExpired property
            // IsExpired is computed as: ExpiresAt < DateTime.UtcNow || !IsActive
            // So active, non-expired sessions are: IsActive && ExpiresAt >= now
            var query = _context.UserSessions
                .Include(s => s.User)
                .Where(s => s.IsActive && s.ExpiresAt >= now);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(s => s.UserId == userId);

            var pageSize = await _configService.GetPaginationSizeAsync("active_sessions", 20);
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var sessions = await query
                .OrderByDescending(s => s.LastActivityAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.UserId = userId;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تحميل الجلسات النشطة - Error loading active sessions");
            SetWarningMessage("تعذر تحميل الجلسات النشطة. يرجى المحاولة مرة أخرى.");
            
            ViewBag.UserId = userId;
            ViewBag.Page = page;
            
            return View(new List<UserSession>());
        }
    }

    /// <summary>
    /// جميع الجلسات - All sessions
    /// </summary>
    public async Task<IActionResult> AllSessions(string? userId, bool? active, int page = 1)
    {
        var query = _context.UserSessions
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(s => s.UserId == userId);

        if (active.HasValue)
            query = query.Where(s => s.IsActive == active.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("all_sessions", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.Active = active;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;

        return View(sessions);
    }

    /// <summary>
    /// إنهاء الجلسة - Terminate session
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateSession(int id)
    {
        var session = await _context.UserSessions.FindAsync(id);
        if (session == null)
            return NotFound();

        session.IsActive = false;
        session.TerminatedAt = DateTime.UtcNow;
        session.TerminationReason = "Terminated by administrator";

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إنهاء الجلسة بنجاح");
        return RedirectToAction(nameof(ActiveSessions));
    }

    /// <summary>
    /// إنهاء جميع جلسات المستخدم - Terminate all user sessions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateUserSessions(string userId)
    {
        var sessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.TerminatedAt = DateTime.UtcNow;
            session.TerminationReason = "All sessions terminated by administrator";
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم إنهاء {sessions.Count} جلسة نشطة للمستخدم");
        return RedirectToAction(nameof(ActiveSessions));
    }

    #endregion

    #region Two-Factor Authentication

    /// <summary>
    /// إعدادات المصادقة الثنائية - Two-factor authentication settings
    /// </summary>
    public async Task<IActionResult> TwoFactorSettings(string? userId, int page = 1)
    {
        var query = _context.TwoFactorSettings
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(s => s.UserId == userId);

        var pageSize = await _configService.GetPaginationSizeAsync("two_factor_settings", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var settings = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;

        return View(settings);
    }

    /// <summary>
    /// تفاصيل إعدادات المصادقة الثنائية - Two-factor setting details
    /// </summary>
    public async Task<IActionResult> TwoFactorDetails(int id)
    {
        var setting = await _context.TwoFactorSettings
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (setting == null)
            return NotFound();

        return View(setting);
    }

    /// <summary>
    /// تعطيل المصادقة الثنائية للمستخدم - Disable 2FA for user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTwoFactor(int id)
    {
        var setting = await _context.TwoFactorSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        setting.IsEnabled = false;
        setting.DisabledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعطيل المصادقة الثنائية بنجاح");
        return RedirectToAction(nameof(TwoFactorSettings));
    }

    #endregion

    #region IP Blocking

    /// <summary>
    /// عناوين IP المحظورة - Blocked IPs
    /// </summary>
    public async Task<IActionResult> BlockedIps(int page = 1)
    {
        var query = _context.BlockedIps
            .Include(b => b.BlockedBy)
            .AsQueryable();
        
        var pageSize = await _configService.GetPaginationSizeAsync("blocked_ips", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var blockedIps = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;
        return View(blockedIps);
    }

    /// <summary>
    /// إضافة IP محظور - Add blocked IP
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddBlockedIp(string? ip = null, string? reason = null)
    {
        // Get blocked IP statistics - use actual mapped properties instead of NotMapped IsActive
        // IsActive is computed as: ExpiresAt == null || ExpiresAt > DateTime.UtcNow
        var now = DateTime.UtcNow;
        var blockedIps = await _context.BlockedIps
            .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
            .ToListAsync();
        var today = now.Date;
        
        // Get login attempts that were blocked today
        var blockedAttemptsToday = await _context.LoginLogs
            .CountAsync(la => la.AttemptedAt >= today && !la.IsSuccessful && 
                blockedIps.Select(b => b.IpAddress).Contains(la.IpAddress));
        
        // Get IPs expiring soon (within 7 days)
        var expiringSoon = blockedIps.Count(b => b.ExpiresAt.HasValue && 
            b.ExpiresAt.Value <= now.AddDays(7));

        ViewBag.TotalBlockedIps = blockedIps.Count;
        ViewBag.BlockedAttemptsToday = blockedAttemptsToday;
        ViewBag.ExpiringSoon = expiringSoon;
        ViewBag.RecentBlockedIps = blockedIps.OrderByDescending(b => b.CreatedAt).Take(5).ToList();

        // Pre-populate model if IP is provided from query string
        var model = new BlockedIpViewModel();
        if (!string.IsNullOrEmpty(ip))
        {
            model.IpAddress = ip;
        }
        if (!string.IsNullOrEmpty(reason))
        {
            model.Reason = reason;
        }

        return View(model);
    }

    /// <summary>
    /// حفظ IP محظور - Save blocked IP
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlockedIp(BlockedIpViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Get current admin user ID
            var currentUserId = _userManager.GetUserId(User);
            
            var blockedIp = new BlockedIp
            {
                IpAddress = model.IpAddress,
                Reason = model.Reason,
                BlockedUntil = model.IsPermanent ? null : model.BlockedUntil,
                IsPermanent = model.IsPermanent,
                Country = model.Country,
                Notes = model.Notes,
                BlockedById = currentUserId
            };

            _context.BlockedIps.Add(blockedIp);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("IP {IpAddress} blocked by admin {AdminId}", model.IpAddress, currentUserId);

            SetSuccessMessage("تم حظر عنوان IP بنجاح");
            return RedirectToAction(nameof(BlockedIps));
        }

        // Re-populate ViewBag for form re-display
        await PopulateAddBlockedIpViewBag();
        return View(model);
    }
    
    /// <summary>
    /// Populate ViewBag for AddBlockedIp view
    /// </summary>
    private async Task PopulateAddBlockedIpViewBag()
    {
        var blockedIps = await _context.BlockedIps.Where(b => b.ExpiresAt == null || b.ExpiresAt > DateTime.UtcNow).ToListAsync();
        var today = DateTime.UtcNow.Date;
        
        var blockedAttemptsToday = await _context.LoginLogs
            .CountAsync(la => la.AttemptedAt >= today && !la.IsSuccessful && 
                blockedIps.Select(b => b.IpAddress).Contains(la.IpAddress));
        
        var expiringSoon = blockedIps.Count(b => b.ExpiresAt.HasValue && 
            b.ExpiresAt.Value <= DateTime.UtcNow.AddDays(7));

        ViewBag.TotalBlockedIps = blockedIps.Count;
        ViewBag.BlockedAttemptsToday = blockedAttemptsToday;
        ViewBag.ExpiringSoon = expiringSoon;
        ViewBag.RecentBlockedIps = blockedIps.OrderByDescending(b => b.CreatedAt).Take(5).ToList();
    }

    /// <summary>
    /// إلغاء حظر IP - Unblock IP
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockIp(int id)
    {
        var blockedIp = await _context.BlockedIps.FindAsync(id);
        if (blockedIp == null)
            return NotFound();

        blockedIp.IsActive = false;
        blockedIp.UnblockedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إلغاء حظر عنوان IP بنجاح");
        return RedirectToAction(nameof(BlockedIps));
    }

    /// <summary>
    /// حذف IP من السجل - Delete blocked IP record permanently
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlockedIp(int id)
    {
        var blockedIp = await _context.BlockedIps.FindAsync(id);
        if (blockedIp == null)
            return NotFound();

        _context.BlockedIps.Remove(blockedIp);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Blocked IP {IpAddress} deleted from records", blockedIp.IpAddress);
        SetSuccessMessage("تم حذف عنوان IP من السجل بنجاح");
        return RedirectToAction(nameof(BlockedIps));
    }

    /// <summary>
    /// تنظيف عناوين IP المنتهية - Cleanup expired blocked IPs
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanupExpiredIps()
    {
        var expiredIps = await _context.BlockedIps
            .Where(b => b.ExpiresAt.HasValue && b.ExpiresAt.Value < DateTime.UtcNow)
            .ToListAsync();

        if (!expiredIps.Any())
        {
            SetWarningMessage("لا توجد عناوين IP منتهية الصلاحية");
            return RedirectToAction(nameof(BlockedIps));
        }

        _context.BlockedIps.RemoveRange(expiredIps);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} expired blocked IPs", expiredIps.Count);
        SetSuccessMessage($"تم حذف {expiredIps.Count} عنوان IP منتهي الصلاحية");
        return RedirectToAction(nameof(BlockedIps));
    }

    #endregion

    #region Country Restrictions

    /// <summary>
    /// القيود الجغرافية - Country restrictions
    /// </summary>
    public async Task<IActionResult> CountryRestrictions(int page = 1)
    {
        var query = _context.CountryRestrictions.AsQueryable();
        
        var pageSize = await _configService.GetPaginationSizeAsync("country_restrictions", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var restrictions = await query
            .OrderBy(c => c.CountryName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;
        return View(restrictions);
    }

    /// <summary>
    /// إضافة قيد جغرافي - Add country restriction
    /// </summary>
    [HttpGet]
    public IActionResult AddCountryRestriction()
    {
        return View(new CountryRestrictionViewModel());
    }

    /// <summary>
    /// حفظ القيد الجغرافي - Save country restriction
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCountryRestriction(CountryRestrictionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var restriction = new CountryRestriction
            {
                CountryCode = model.CountryCode,
                CountryName = model.CountryName,
                RestrictionType = model.RestrictionType,
                Reason = model.Reason,
                IsActive = true
            };

            _context.CountryRestrictions.Add(restriction);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة القيد الجغرافي بنجاح");
            return RedirectToAction(nameof(CountryRestrictions));
        }

        return View(model);
    }

    /// <summary>
    /// حذف قيد جغرافي - Delete country restriction
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCountryRestriction(int id)
    {
        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        _context.CountryRestrictions.Remove(restriction);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القيد الجغرافي بنجاح");
        return RedirectToAction(nameof(CountryRestrictions));
    }

    /// <summary>
    /// تبديل حالة القيد - Toggle restriction status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCountryRestriction(int id)
    {
        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        restriction.IsActive = !restriction.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(restriction.IsActive ? "تفعيل" : "تعطيل")} القيد الجغرافي");
        return RedirectToAction(nameof(CountryRestrictions));
    }

    #endregion

    #region Security Dashboard

    /// <summary>
    /// لوحة الأمان - Security dashboard
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        
        // Use AttemptedAt instead of NotMapped LoginAt, and use actual properties instead of computed IsExpired
        // IsExpired is computed as: ExpiresAt < DateTime.UtcNow || !IsActive
        var viewModel = new SecurityDashboardViewModel
        {
            TotalActiveSessions = await _context.UserSessions.CountAsync(s => s.IsActive && s.ExpiresAt >= now),
            FailedLoginsToday = await _context.LoginLogs
                .CountAsync(l => !l.IsSuccessful && l.AttemptedAt >= today),
            BlockedIpsCount = await _context.BlockedIps.CountAsync(b => b.IsActive),
            TwoFactorEnabledUsers = await _context.TwoFactorSettings.CountAsync(s => s.IsEnabled),
            RecentFailedLogins = await _context.LoginLogs
                .Include(l => l.User)
                .Where(l => !l.IsSuccessful)
                .OrderByDescending(l => l.AttemptedAt)
                .Take(10)
                .ToListAsync(),
            RecentBlockedIps = await _context.BlockedIps
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync()
        };
        
        // Handle SuspiciousActivities separately with in-memory grouping to avoid SQL translation issues
        var failedLogins = await _context.LoginLogs
            .Where(l => !l.IsSuccessful)
            .Select(l => new { l.IpAddress, l.AttemptedAt })
            .ToListAsync();
            
        viewModel.SuspiciousActivities = failedLogins
            .GroupBy(l => l.IpAddress)
            .Where(g => g.Count() >= 5)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new SuspiciousActivityViewModel
            {
                IpAddress = g.Key!,
                FailedAttempts = g.Count(),
                LastAttempt = g.Max(l => l.AttemptedAt)
            })
            .ToList();

        return View(viewModel);
    }

    #endregion
}

