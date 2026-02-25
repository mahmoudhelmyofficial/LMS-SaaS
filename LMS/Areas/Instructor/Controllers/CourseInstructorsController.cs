using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة المدرسين المساعدين - Co-Instructors Management Controller
/// </summary>
public class CourseInstructorsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CourseInstructorsController> _logger;

    public CourseInstructorsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CourseInstructorsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المدرسين المساعدين للدورة - Course instructors list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId = null)
    {
        var userId = _currentUserService.UserId;

        // If no courseId provided, show all courses with their co-instructors
        if (!courseId.HasValue)
        {
            var courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .Select(c => new CourseInstructorSummaryViewModel
                {
                    CourseId = c.Id,
                    CourseTitle = c.Title,
                    ThumbnailUrl = c.ThumbnailUrl,
                    CoInstructorCount = _context.CourseInstructors.Count(ci => ci.CourseId == c.Id && !ci.IsPrimaryOwner),
                    IsPublished = c.Status == LMS.Domain.Enums.CourseStatus.Published
                })
                .OrderByDescending(c => c.IsPublished)
                .ThenBy(c => c.CourseTitle)
                .ToListAsync();

            return View("SelectCourse", courses);
        }

        // Verify course ownership
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        var instructors = await _context.CourseInstructors
            .Include(ci => ci.Instructor)
            .Where(ci => ci.CourseId == courseId)
            .OrderByDescending(ci => ci.IsPrimaryOwner)
            .ThenBy(ci => ci.DisplayOrder)
            .ToListAsync();

        ViewBag.Course = course;
        return View(instructors);
    }

    /// <summary>
    /// إضافة مدرس مساعد - Add co-instructor
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Add(int courseId)
    {
        var userId = _currentUserService.UserId;

        // Verify course ownership
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        // Get available instructors (those with InstructorProfile and not already added)
        var existingInstructorIds = await _context.CourseInstructors
            .Where(ci => ci.CourseId == courseId)
            .Select(ci => ci.InstructorId)
            .ToListAsync();

        var availableInstructors = await _context.InstructorProfiles
            .Include(ip => ip.User)
            .Where(ip => ip.IsApproved && 
                !existingInstructorIds.Contains(ip.UserId) && 
                ip.UserId != userId)
            .Select(ip => new
            {
                ip.UserId,
                ip.User.FullName,
                ip.User.Email
            })
            .ToListAsync();

        ViewBag.AvailableInstructors = availableInstructors;
        ViewBag.CourseName = course.Title;

        return View(new CourseInstructorViewModel { CourseId = courseId });
    }

    /// <summary>
    /// حفظ المدرس المساعد - Save co-instructor
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(CourseInstructorViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            // Verify course ownership
            var targetCourse = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (targetCourse == null)
                return NotFound();

            // Check if instructor already exists
            var exists = await _context.CourseInstructors
                .AnyAsync(ci => ci.CourseId == model.CourseId && ci.InstructorId == model.InstructorId);

            if (exists)
            {
                SetErrorMessage("هذا المدرس مضاف بالفعل للدورة");
                return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
            }

            var courseInstructor = new CourseInstructor
            {
                CourseId = model.CourseId,
                InstructorId = model.InstructorId,
                Role = model.Role,
                IsPrimaryOwner = false,
                RevenueSharePercentage = model.RevenueSharePercentage,
                CanEditContent = model.CanEditContent,
                CanManageStudents = model.CanManageStudents,
                CanGradeAssignments = model.CanGradeAssignments,
                CanHostLiveClasses = model.CanHostLiveClasses,
                CanReplyDiscussions = model.CanReplyDiscussions,
                CanSendAnnouncements = model.CanSendAnnouncements,
                CanViewAnalytics = model.CanViewAnalytics,
                CanManagePricing = model.CanManagePricing,
                CanManageCoupons = model.CanManageCoupons,
                CustomTitle = model.CustomTitle,
                Notes = model.Notes,
                IsActive = true
            };

            _context.CourseInstructors.Add(courseInstructor);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة المدرس المساعد بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }

        // Reload available instructors
        var existingInstructorIds = await _context.CourseInstructors
            .Where(ci => ci.CourseId == model.CourseId)
            .Select(ci => ci.InstructorId)
            .ToListAsync();

        var availableInstructors = await _context.InstructorProfiles
            .Include(ip => ip.User)
            .Where(ip => ip.IsApproved && !existingInstructorIds.Contains(ip.UserId))
            .Select(ip => new
            {
                ip.UserId,
                ip.User.FullName,
                ip.User.Email
            })
            .ToListAsync();

        ViewBag.AvailableInstructors = availableInstructors;

        var course = await _context.Courses.FindAsync(model.CourseId);
        ViewBag.CourseName = course?.Title;

        return View(model);
    }

    /// <summary>
    /// تعديل صلاحيات مدرس مساعد - Edit co-instructor permissions
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var courseInstructor = await _context.CourseInstructors
            .Include(ci => ci.Course)
            .Include(ci => ci.Instructor)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.Course.InstructorId == userId);

        if (courseInstructor == null)
            return NotFound();

        var model = new CourseInstructorViewModel
        {
            Id = courseInstructor.Id,
            CourseId = courseInstructor.CourseId,
            InstructorId = courseInstructor.InstructorId,
            Role = courseInstructor.Role,
            RevenueSharePercentage = courseInstructor.RevenueSharePercentage,
            CanEditContent = courseInstructor.CanEditContent,
            CanManageStudents = courseInstructor.CanManageStudents,
            CanGradeAssignments = courseInstructor.CanGradeAssignments,
            CanHostLiveClasses = courseInstructor.CanHostLiveClasses,
            CanReplyDiscussions = courseInstructor.CanReplyDiscussions,
            CanSendAnnouncements = courseInstructor.CanSendAnnouncements,
            CanViewAnalytics = courseInstructor.CanViewAnalytics,
            CanManagePricing = courseInstructor.CanManagePricing,
            CanManageCoupons = courseInstructor.CanManageCoupons,
            CustomTitle = courseInstructor.CustomTitle,
            Notes = courseInstructor.Notes
        };

        ViewBag.CourseName = courseInstructor.Course.Title;
        ViewBag.InstructorName = courseInstructor.Instructor.FullName;

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات المدرس المساعد - Save co-instructor changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseInstructorViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var courseInstructor = await _context.CourseInstructors
                .Include(ci => ci.Course)
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.Course.InstructorId == userId);

            if (courseInstructor == null)
                return NotFound();

            courseInstructor.Role = model.Role;
            courseInstructor.RevenueSharePercentage = model.RevenueSharePercentage;
            courseInstructor.CanEditContent = model.CanEditContent;
            courseInstructor.CanManageStudents = model.CanManageStudents;
            courseInstructor.CanGradeAssignments = model.CanGradeAssignments;
            courseInstructor.CanHostLiveClasses = model.CanHostLiveClasses;
            courseInstructor.CanReplyDiscussions = model.CanReplyDiscussions;
            courseInstructor.CanSendAnnouncements = model.CanSendAnnouncements;
            courseInstructor.CanViewAnalytics = model.CanViewAnalytics;
            courseInstructor.CanManagePricing = model.CanManagePricing;
            courseInstructor.CanManageCoupons = model.CanManageCoupons;
            courseInstructor.CustomTitle = model.CustomTitle;
            courseInstructor.Notes = model.Notes;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث صلاحيات المدرس المساعد بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = courseInstructor.CourseId });
        }

        return View(model);
    }

    /// <summary>
    /// إزالة مدرس مساعد - Remove co-instructor
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = _currentUserService.UserId;

        var courseInstructor = await _context.CourseInstructors
            .Include(ci => ci.Course)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.Course.InstructorId == userId);

        if (courseInstructor == null)
            return NotFound();

        var courseId = courseInstructor.CourseId;

        _context.CourseInstructors.Remove(courseInstructor);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إزالة المدرس المساعد بنجاح");
        return RedirectToAction(nameof(Index), new { courseId });
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var userId = _currentUserService.UserId;

        var courseInstructor = await _context.CourseInstructors
            .Include(ci => ci.Course)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.Course.InstructorId == userId);

        if (courseInstructor == null)
            return NotFound();

        courseInstructor.IsActive = !courseInstructor.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(courseInstructor.IsActive ? "تفعيل" : "تعطيل")} المدرس المساعد");
        return RedirectToAction(nameof(Index), new { courseId = courseInstructor.CourseId });
    }

    /// <summary>
    /// إعادة ترتيب المدرسين - Reorder instructors
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id, int newOrder)
    {
        var userId = _currentUserService.UserId;

        var courseInstructor = await _context.CourseInstructors
            .Include(ci => ci.Course)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.Course.InstructorId == userId);

        if (courseInstructor == null)
            return NotFound();

        courseInstructor.DisplayOrder = newOrder;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }
}

