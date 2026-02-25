using LMS.Areas.Admin.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Users;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المستخدمين - Users Management Controller
/// </summary>
public class UsersController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<UsersController> _logger;
    private readonly ISystemConfigurationService _configService;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        ILogger<UsersController> logger,
        ISystemConfigurationService configService,
        IMemoryCache cache,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _logger = logger;
        _configService = configService;
        _cache = cache;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// قائمة المستخدمين - Users list
    /// </summary>
    public async Task<IActionResult> Index(string? searchTerm, string? role, int page = 1)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // Build query with filters
        var query = _context.Users
            .Where(u => !u.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => 
                u.FirstName.Contains(searchTerm) || 
                u.LastName.Contains(searchTerm) || 
                u.Email!.Contains(searchTerm) ||
                u.UserName!.Contains(searchTerm));
        }

        // Role filtering would require a join with UserRoles, we'll handle it in the view if needed
        // For now, we'll keep it simple and let the view display all users

        // Get paginated users
        var pageSize = await _configService.GetPaginationSizeAsync("users", 25);
        var totalUsers = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load user roles for display
        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var user in users)
        {
            userRoles[user.Id] = await _userManager.GetRolesAsync(user);
        }
        ViewBag.UserRoles = userRoles;

        // Instructor approval status for users with Instructor role (for index display)
        var instructorIds = users.Where(u => userRoles.TryGetValue(u.Id, out var r) && r.Contains(Constants.Roles.Instructor)).Select(u => u.Id).ToList();
        var instructorApproved = new Dictionary<string, bool>();
        if (instructorIds.Count > 0)
        {
            var profiles = await _context.InstructorProfiles.Where(ip => instructorIds.Contains(ip.UserId)).Select(ip => new { ip.UserId, ip.IsApproved }).ToListAsync();
            foreach (var p in profiles)
                instructorApproved[p.UserId] = p.IsApproved;
        }
        ViewBag.InstructorApproved = instructorApproved;

        // Statistics
        var activeUsers = await _context.Users.CountAsync(u => !u.IsDeleted && u.EmailConfirmed);
        var newUsersThisMonth = await _context.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt >= thisMonthStart);
        var newUsersLastMonth = await _context.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt >= lastMonthStart && u.CreatedAt < thisMonthStart);
        var pendingUsers = await _context.Users.CountAsync(u => !u.IsDeleted && !u.EmailConfirmed);

        // Calculate growth percentage
        var growthPercentage = newUsersLastMonth > 0 
            ? ((decimal)(newUsersThisMonth - newUsersLastMonth) / newUsersLastMonth) * 100 
            : 0;

        var model = new UsersListViewModel
        {
            Users = users,
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            NewUsersThisMonth = newUsersThisMonth,
            PendingUsers = pendingUsers,
            GrowthPercentage = growthPercentage,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize)
        };

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Role = role;
        ViewBag.Page = page;

        return View(model);
    }

    /// <summary>
    /// إنشاء مستخدم جديد - Create new user
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    /// <summary>
    /// حفظ المستخدم الجديد - Save new user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        try
        {
            if (ModelState.IsValid)
            {
                // Check if username already exists
                var existingUser = await _userManager.FindByNameAsync(model.UserName);
                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "اسم المستخدم موجود بالفعل");
                    return View(model);
                }

                // Check if email already exists
                existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "البريد الإلكتروني موجود بالفعل");
                    return View(model);
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    DateOfBirth = model.DateOfBirth,
                    Bio = model.Bio,
                    EmailConfirmed = model.EmailConfirmed,
                    PhoneNumberConfirmed = model.PhoneNumberConfirmed,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Assign role if provided
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        if (await _roleManager.RoleExistsAsync(model.Role))
                        {
                            await _userManager.AddToRoleAsync(user, model.Role);
                        }
                    }
                    else
                    {
                        // Default role: Student
                        await _userManager.AddToRoleAsync(user, Constants.Roles.Student);
                    }

                    // Send welcome email if requested
                    if (model.SendWelcomeEmail && !string.IsNullOrEmpty(user.Email))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(
                                user.Email,
                                "مرحباً بك في منصة LMS",
                                $@"<html><body dir='rtl'>
                                    <h2>مرحباً {user.FirstName} {user.LastName}!</h2>
                                    <p>تم إنشاء حسابك بنجاح على منصة LMS.</p>
                                    <p><strong>اسم المستخدم:</strong> {user.UserName}</p>
                                    <p><strong>البريد الإلكتروني:</strong> {user.Email}</p>
                                    <p>يمكنك الآن تسجيل الدخول والبدء في استخدام المنصة.</p>
                                    <br/>
                                    <p>فريق منصة LMS</p>
                                </body></html>",
                                true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
                        }
                    }

                    _logger.LogInformation("User {UserId} created successfully by admin", user.Id);
                    SetSuccessMessage("تم إنشاء المستخدم بنجاح");
                    return RedirectToAction(nameof(Details), new { id = user.Id });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    _logger.LogWarning("Error creating user: {Error}", error.Description);
                }
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new user");
            SetErrorMessage("حدث خطأ أثناء إنشاء المستخدم");
            return View(model);
        }
    }

    /// <summary>
    /// تفاصيل المستخدم - User details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var user = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.InstructorProfile)
            .Include(u => u.Enrollments)
                .ThenInclude(e => e.Course)
            .Include(u => u.Certificates)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.Roles = roles;

        // Load user activities for the Activity tab
        var userActivities = await _context.ActivityLogs
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync();
        ViewBag.UserActivities = userActivities;

        // Load statistics
        var enrollmentsCount = user.Enrollments?.Count ?? 0;
        var certificatesCount = user.Certificates?.Count ?? 0;
        var paymentsCount = await _context.Payments.CountAsync(p => p.StudentId == id && p.Status == Domain.Enums.PaymentStatus.Completed);
        var userPoints = await _context.UserPoints.Where(p => p.UserId == id).SumAsync(p => (int?)p.Points) ?? 0;

        ViewBag.EnrollmentsCount = enrollmentsCount;
        ViewBag.CertificatesCount = certificatesCount;
        ViewBag.PaymentsCount = paymentsCount;
        ViewBag.UserPoints = userPoints;

        return View(user);
    }

    /// <summary>
    /// تعديل المستخدم - Edit user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var viewModel = new UserEditViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive
        };

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.UserRoles = roles;

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات المستخدم - Save user edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found for editing: {UserId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Validate email format
                if (!string.IsNullOrEmpty(model.Email) && !ValidationHelper.IsValidEmail(model.Email))
                {
                    ModelState.AddModelError("Email", "البريد الإلكتروني غير صحيح");
                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.UserRoles = roles;
                    return View(model);
                }

                // Check if email changed and already exists
                if (user.Email != model.Email)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email!);
                    if (existingUser != null && existingUser.Id != id)
                    {
                        ModelState.AddModelError("Email", "البريد الإلكتروني مستخدم بالفعل");
                        var roles = await _userManager.GetRolesAsync(user);
                        ViewBag.UserRoles = roles;
                        return View(model);
                    }
                }

                user.FirstName = !string.IsNullOrWhiteSpace(model.FirstName) ? model.FirstName.Trim() : string.Empty;
                user.LastName = !string.IsNullOrWhiteSpace(model.LastName) ? model.LastName.Trim() : string.Empty;
                user.Email = model.Email;
                user.IsActive = model.IsActive;
                user.PhoneNumber = model.PhoneNumber?.Trim();

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    await SyncInstructorProfileApprovalAsync(id, model.IsActive);
                    _cache.Remove($"instructor_profile_{id}");
                    _logger.LogInformation("User {UserId} updated successfully", id);
                    SetSuccessMessage("تم تحديث المستخدم بنجاح");
                    return RedirectToAction(nameof(Details), new { id });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    _logger.LogWarning("Error updating user {UserId}: {Error}", id, error.Description);
                }
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing user {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث المستخدم");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تفعيل/تعطيل المستخدم - Toggle user status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found for status toggle: {UserId}", id);
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            await SyncInstructorProfileApprovalAsync(id, user.IsActive);
            _cache.Remove($"instructor_profile_{id}");

            // Send notification email
            if (!string.IsNullOrEmpty(user.Email))
            {
                var subject = user.IsActive ? "تم تفعيل حسابك" : "تم تعطيل حسابك";
                var body = user.IsActive
                    ? $@"<html><body dir='rtl'>
                        <h2>مرحباً {user.FirstName}</h2>
                        <p>تم تفعيل حسابك على منصة LMS.</p>
                        <p>يمكنك الآن تسجيل الدخول والوصول إلى جميع الخدمات.</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>"
                    : $@"<html><body dir='rtl'>
                        <h2>إشعار مهم</h2>
                        <p>عزيزي/عزيزتي {user.FirstName},</p>
                        <p>تم تعطيل حسابك مؤقتاً على منصة LMS.</p>
                        <p>للمزيد من المعلومات، يرجى التواصل مع الدعم الفني.</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>";

                await _emailService.SendEmailAsync(user.Email, subject, body, true);
            }

            _logger.LogInformation("User {UserId} status toggled to {Status}", id, user.IsActive);
            SetSuccessMessage(user.IsActive ? "تم تفعيل المستخدم وإرسال إشعار" : "تم تعطيل المستخدم وإرسال إشعار");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling user status {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تغيير حالة المستخدم");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إدارة أدوار المستخدم - Manage user roles
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ManageRoles(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found for role management: {UserId}", id);
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.ToListAsync();

            ViewBag.User = user;
            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = allRoles;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading role management for user {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل الأدوار");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إضافة دور للمستخدم - Add role to user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRole(string userId, string roleName)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound();
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogWarning("Role not found: {RoleName}", roleName);
                SetErrorMessage("الدور غير موجود");
                return RedirectToAction(nameof(ManageRoles), new { id = userId });
            }

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                SetWarningMessage("المستخدم لديه هذا الدور بالفعل");
                return RedirectToAction(nameof(ManageRoles), new { id = userId });
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                _logger.LogInformation("Role {RoleName} added to user {UserId}", roleName, userId);
                SetSuccessMessage($"تم إضافة دور {roleName} للمستخدم");
            }
            else
            {
                _logger.LogWarning("Failed to add role {RoleName} to user {UserId}", roleName, userId);
                SetErrorMessage("فشل إضافة الدور");
            }

            return RedirectToAction(nameof(ManageRoles), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding role to user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء إضافة الدور");
            return RedirectToAction(nameof(ManageRoles), new { id = userId });
        }
    }

    /// <summary>
    /// إزالة دور من المستخدم - Remove role from user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(string userId, string roleName)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound();
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                SetWarningMessage("المستخدم ليس لديه هذا الدور");
                return RedirectToAction(nameof(ManageRoles), new { id = userId });
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                _logger.LogInformation("Role {RoleName} removed from user {UserId}", roleName, userId);
                SetSuccessMessage($"تم إزالة دور {roleName} من المستخدم");
            }
            else
            {
                _logger.LogWarning("Failed to remove role {RoleName} from user {UserId}", roleName, userId);
                SetErrorMessage("فشل إزالة الدور");
            }

            return RedirectToAction(nameof(ManageRoles), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role from user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء إزالة الدور");
            return RedirectToAction(nameof(ManageRoles), new { id = userId });
        }
    }

    /// <summary>
    /// إعادة تعيين كلمة المرور - Reset password
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User not found for password reset: {UserId}", id);
            return NotFound();
        }

        ViewBag.User = user;
        return View();
    }

    /// <summary>
    /// إرسال رابط إعادة تعيين كلمة المرور - Send password reset link
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPasswordReset(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("User not found or has no email: {UserId}", id);
                SetErrorMessage("المستخدم غير موجود أو ليس لديه بريد إلكتروني");
                return RedirectToAction(nameof(Details), new { id });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new { userId = user.Id, token }, Request.Scheme);

            await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink!);

            _logger.LogInformation("Password reset email sent to user {UserId}", id);
            SetSuccessMessage("تم إرسال رابط إعادة تعيين كلمة المرور إلى البريد الإلكتروني");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset for user {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء إرسال رابط إعادة التعيين");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حذف المستخدم - Delete user (Soft Delete)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                _logger.LogWarning("User not found for deletion: {UserId}", id);
                return NotFound();
            }

            // Already deleted check
            if (user.IsDeleted)
            {
                SetWarningMessage("هذا المستخدم محذوف بالفعل");
                return RedirectToAction(nameof(Index));
            }

            // Soft delete - mark as deleted without removing from database
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.IsActive = false; // Also deactivate the account

            await SyncInstructorProfileApprovalAsync(id, false);
            _cache.Remove($"instructor_profile_{id}");

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} soft-deleted successfully", id);
            SetSuccessMessage("تم حذف المستخدم بنجاح (يمكن استعادته لاحقاً)");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف المستخدم");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات المستخدمين - User statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            var students = await _userManager.GetUsersInRoleAsync(Constants.Roles.Student);
            var instructors = await _userManager.GetUsersInRoleAsync(Constants.Roles.Instructor);
            var admins = await _userManager.GetUsersInRoleAsync(Constants.Roles.Admin);
            
            // Weekly growth data
            var weeklyGrowth = new List<UserGrowthData>();
            for (int i = 3; i >= 0; i--)
            {
                var weekStart = DateTime.UtcNow.AddDays(-7 * (i + 1));
                var weekEnd = DateTime.UtcNow.AddDays(-7 * i);
                var count = await _context.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt >= weekStart && u.CreatedAt < weekEnd);
                weeklyGrowth.Add(new UserGrowthData
                {
                    Label = $"الأسبوع {4 - i}",
                    Count = count
                });
            }
            
            var model = new UserStatisticsViewModel
            {
                TotalUsers = totalUsers,
                ActiveUsers = await _context.Users.CountAsync(u => u.IsActive && !u.IsDeleted),
                InactiveUsers = await _context.Users.CountAsync(u => !u.IsActive && !u.IsDeleted),
                TotalStudents = students.Count,
                TotalInstructors = instructors.Count,
                TotalAdmins = admins.Count,
                NewUsersThisMonth = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.CreatedAt >= DateTime.UtcNow.AddMonths(-1)),
                NewUsersToday = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.CreatedAt.Date == DateTime.UtcNow.Date),
                UsersLoggedInToday = await _context.Users
                    .CountAsync(u => !u.IsDeleted && u.LastLoginAt.HasValue && 
                                u.LastLoginAt.Value.Date == DateTime.UtcNow.Date),
                EmailVerifiedCount = await _context.Users.CountAsync(u => !u.IsDeleted && u.EmailConfirmed),
                PhoneVerifiedCount = await _context.Users.CountAsync(u => !u.IsDeleted && u.PhoneNumberConfirmed),
                TwoFactorEnabledCount = await _context.Users.CountAsync(u => !u.IsDeleted && u.TwoFactorEnabled),
                ProfileCompletedCount = await _context.Users.CountAsync(u => !u.IsDeleted && 
                    !string.IsNullOrEmpty(u.ProfilePictureUrl) && !string.IsNullOrEmpty(u.Bio)),
                WeeklyGrowth = weeklyGrowth
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user statistics");
            SetErrorMessage("حدث خطأ أثناء تحميل الإحصائيات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// مزامنة حالة الاعتماد لملف المدرس مع حالة نشاط المستخدم - Sync instructor profile approval with user active status
    /// When admin marks user Active in User Management, instructor can publish/send to review.
    /// </summary>
    private async Task SyncInstructorProfileApprovalAsync(string userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(Constants.Roles.Instructor)) return;

        var profile = await _context.InstructorProfiles.FirstOrDefaultAsync(ip => ip.UserId == userId);
        if (isActive)
        {
            if (profile == null)
            {
                profile = new InstructorProfile
                {
                    UserId = userId,
                    IsApproved = true,
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = _currentUserService.UserId,
                    CommissionRate = 70m,
                    MinimumWithdrawal = BusinessRuleHelper.MinimumWithdrawalAmount,
                    TotalEarnings = 0,
                    AvailableBalance = 0,
                    PendingBalance = 0,
                    TotalWithdrawn = 0
                };
                _context.InstructorProfiles.Add(profile);
            }
            else
            {
                profile.IsApproved = true;
                profile.ApprovedAt = DateTime.UtcNow;
                profile.ApprovedBy = _currentUserService.UserId;
                profile.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            if (profile != null)
            {
                profile.IsApproved = false;
                profile.ApprovedAt = null;
                profile.ApprovedBy = null;
                profile.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (profile != null)
            await _context.SaveChangesAsync();
    }
}
