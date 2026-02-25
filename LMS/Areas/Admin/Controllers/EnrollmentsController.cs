using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة التسجيلات - Enrollments Management Controller
/// </summary>
public class EnrollmentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnrollmentsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public EnrollmentsController(
        ApplicationDbContext context,
        ILogger<EnrollmentsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة جميع التسجيلات - All enrollments list
    /// </summary>
    public async Task<IActionResult> Index(string? searchTerm, EnrollmentStatus? status, int? courseId, int page = 1)
    {
        var query = _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e =>
                (e.Student != null && e.Student.FirstName != null && e.Student.FirstName.Contains(searchTerm)) ||
                (e.Student != null && e.Student.LastName != null && e.Student.LastName.Contains(searchTerm)) ||
                (e.Course != null && e.Course.Title != null && e.Course.Title.Contains(searchTerm)));
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(e => e.CourseId == courseId.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("enrollments", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Status = status;
        ViewBag.CourseId = courseId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        // Statistics
        ViewBag.TotalEnrollments = await _context.Enrollments.CountAsync();
        ViewBag.ActiveEnrollments = await _context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active);
        ViewBag.CompletedEnrollments = await _context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Completed);
        ViewBag.NewEnrollmentsThisMonth = await _context.Enrollments
            .CountAsync(e => e.EnrolledAt >= DateTime.UtcNow.AddMonths(-1));

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();

        return View(enrollments);
    }

    /// <summary>
    /// التسجيلات النشطة - Active enrollments
    /// </summary>
    public async Task<IActionResult> Active(string? searchTerm, int? courseId, int page = 1)
    {
        var query = _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Where(e => e.Status == EnrollmentStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e =>
                (e.Student != null && e.Student.FirstName != null && e.Student.FirstName.Contains(searchTerm)) ||
                (e.Student != null && e.Student.LastName != null && e.Student.LastName.Contains(searchTerm)) ||
                (e.Course != null && e.Course.Title != null && e.Course.Title.Contains(searchTerm)));
        }

        if (courseId.HasValue)
        {
            query = query.Where(e => e.CourseId == courseId.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("active_enrollments", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.CourseId = courseId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();

        return View(enrollments);
    }

    /// <summary>
    /// التسجيلات المكتملة - Completed enrollments
    /// </summary>
    public async Task<IActionResult> Completed(string? searchTerm, int? courseId, int page = 1)
    {
        var query = _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Where(e => e.Status == EnrollmentStatus.Completed)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e =>
                (e.Student != null && e.Student.FirstName != null && e.Student.FirstName.Contains(searchTerm)) ||
                (e.Student != null && e.Student.LastName != null && e.Student.LastName.Contains(searchTerm)) ||
                (e.Course != null && e.Course.Title != null && e.Course.Title.Contains(searchTerm)));
        }

        if (courseId.HasValue)
        {
            query = query.Where(e => e.CourseId == courseId.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("completed_enrollments", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var enrollments = await query
            .OrderByDescending(e => e.CompletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.CourseId = courseId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();

        return View(enrollments);
    }

    /// <summary>
    /// تفاصيل التسجيل - Enrollment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Include(e => e.LessonProgress)
                .ThenInclude(lp => lp.Lesson)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollment == null)
            return NotFound();

        return View(enrollment);
    }

    /// <summary>
    /// تغيير حالة التسجيل - Change enrollment status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, EnrollmentStatus newStatus)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null)
            return NotFound();

        var oldStatus = enrollment.Status;
        enrollment.Status = newStatus;

        if (newStatus == EnrollmentStatus.Completed)
        {
            enrollment.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Enrollment {EnrollmentId} status changed from {OldStatus} to {NewStatus}",
            id, oldStatus, newStatus);

        SetSuccessMessage("تم تغيير حالة التسجيل بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إلغاء التسجيل - Cancel enrollment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null)
            return NotFound();

        enrollment.Status = EnrollmentStatus.Cancelled;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Enrollment {EnrollmentId} cancelled. Reason: {Reason}", id, reason);

        SetSuccessMessage("تم إلغاء التسجيل بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

