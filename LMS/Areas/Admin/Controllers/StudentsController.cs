using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Users;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الطلاب - Students Management Controller
/// </summary>
public class StudentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StudentsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public StudentsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<StudentsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الطلاب - Students list
    /// </summary>
    public async Task<IActionResult> Index(string? searchTerm, string? status, int page = 1)
    {
        var studentRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == Constants.Roles.Student);

        var studentUserIds = studentRole != null
            ? await _context.UserRoles
                .Where(ur => ur.RoleId == studentRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync()
            : new List<string>();

        var query = _context.Users
            .Include(u => u.Profile)
            .Include(u => u.Enrollments)
            .Where(u => !u.IsDeleted && studentUserIds.Contains(u.Id))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.FirstName.Contains(searchTerm) ||
                u.LastName.Contains(searchTerm) ||
                u.Email!.Contains(searchTerm));
        }

        if (status == "active")
        {
            query = query.Where(u => u.IsActive && u.EmailConfirmed);
        }
        else if (status == "inactive")
        {
            query = query.Where(u => !u.IsActive);
        }
        else if (status == "pending")
        {
            query = query.Where(u => !u.EmailConfirmed);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("students", 20);
        var totalCount = await query.CountAsync();
        var students = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        // Statistics
        ViewBag.TotalStudents = studentUserIds.Count;
        ViewBag.ActiveStudents = await _context.Users
            .CountAsync(u => studentUserIds.Contains(u.Id) && u.IsActive && !u.IsDeleted);
        ViewBag.NewStudentsThisMonth = await _context.Users
            .CountAsync(u => studentUserIds.Contains(u.Id) && !u.IsDeleted && 
                           u.CreatedAt >= DateTime.UtcNow.AddMonths(-1));
        ViewBag.StudentsWithEnrollments = await _context.Users
            .CountAsync(u => studentUserIds.Contains(u.Id) && !u.IsDeleted && 
                           u.Enrollments.Any());

        return View(students);
    }

    /// <summary>
    /// تفاصيل الطالب - Student details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var student = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.Enrollments)
                .ThenInclude(e => e.Course)
            .Include(u => u.Certificates)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (student == null)
            return NotFound();

        return View(student);
    }

    /// <summary>
    /// تفعيل/تعطيل الطالب - Toggle student status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var student = await _userManager.FindByIdAsync(id);
        if (student == null)
            return NotFound();

        student.IsActive = !student.IsActive;
        await _userManager.UpdateAsync(student);

        _logger.LogInformation("Student {StudentId} status toggled to {Status}", id, student.IsActive);
        SetSuccessMessage(student.IsActive ? "تم تفعيل الطالب بنجاح" : "تم تعطيل الطالب بنجاح");
        
        return RedirectToAction(nameof(Details), new { id });
    }
}

