using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة مرفقات الدروس - Lesson Resources Controller
/// </summary>
public class LessonResourcesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LessonResourcesController> _logger;

    public LessonResourcesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<LessonResourcesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة جميع المرفقات - All resources list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int? lessonId, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.LessonResources
            .Include(r => r.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(r => r.Lesson.Module.Course.InstructorId == userId);

        if (courseId.HasValue)
        {
            query = query.Where(r => r.Lesson.Module.CourseId == courseId.Value);
        }

        if (lessonId.HasValue)
        {
            query = query.Where(r => r.LessonId == lessonId.Value);
        }

        var totalCount = await query.CountAsync();
        var resources = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        // Get instructor's courses for filter
        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.LessonId = lessonId;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);
        ViewBag.TotalCount = totalCount;

        _logger.LogInformation("Instructor {InstructorId} viewed resources list. Total: {Count}", userId, totalCount);

        return View(resources);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int lessonId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("NotFound: Lesson {LessonId} not found or instructor {InstructorId} unauthorized.", lessonId, userId);
                SetErrorMessage("الدرس غير موجود أو ليس لديك صلاحية عليه.");
                return NotFound();
            }

            // Authorization check
            if (lesson.Module?.Course?.InstructorId != userId)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to add resource to lesson {LessonId}.", userId, lessonId);
                SetErrorMessage("غير مصرح لك بإضافة مرفقات لهذا الدرس.");
                return Forbid();
            }

            ViewBag.Lesson = lesson;
            return View(new LessonResourceCreateViewModel { LessonId = lessonId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading create resource page for lesson {LessonId} by instructor {InstructorId}", lessonId, userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الصفحة.");
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonResourceCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == model.LessonId && l.Module.Course.InstructorId == userId);

        if (lesson == null)
        {
            _logger.LogWarning("NotFound: Lesson {LessonId} not found or instructor {InstructorId} unauthorized.", model.LessonId, userId);
            SetErrorMessage("الدرس غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        // Authorization check
        if (lesson.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to add resource to lesson {LessonId}.", userId, model.LessonId);
            SetErrorMessage("غير مصرح لك بإضافة مرفقات لهذا الدرس.");
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            LessonResource? resource = null;
            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    resource = new LessonResource
                    {
                        LessonId = model.LessonId,
                        Title = model.Title,
                        Description = model.Description,
                        FileUrl = model.FileUrl,
                        FileType = model.FileType,
                        FileSize = model.FileSize,
                        IsDownloadable = model.IsDownloadable
                    };

                    _context.LessonResources.Add(resource);
                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("Lesson resource {ResourceTitle} (ID: {ResourceId}) added to lesson {LessonId} by instructor {InstructorId}", model.Title, resource!.Id, model.LessonId, userId);
                SetSuccessMessage("تم إضافة المرفق بنجاح");
                return RedirectToAction("Edit", "Lessons", new { id = model.LessonId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lesson resource for lesson {LessonId} by instructor {InstructorId}", model.LessonId, userId);
                SetErrorMessage("حدث خطأ أثناء إضافة المرفق.");
            }
        }

        ViewBag.Lesson = lesson;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var resource = await _context.LessonResources
            .Include(r => r.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(r => r.Id == id && r.Lesson.Module.Course.InstructorId == userId);

        if (resource == null)
        {
            _logger.LogWarning("NotFound: Lesson resource {ResourceId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("المرفق غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        // Authorization check
        if (resource.Lesson?.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to delete lesson resource {ResourceId}.", userId, id);
            SetErrorMessage("غير مصرح لك بحذف هذا المرفق.");
            return Forbid();
        }

        var lessonId = resource.LessonId;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.LessonResources.Remove(resource);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Lesson resource {ResourceId} deleted by instructor {InstructorId}", id, userId);
            SetSuccessMessage("تم حذف المرفق بنجاح");
            return RedirectToAction("Edit", "Lessons", new { id = lessonId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lesson resource {ResourceId} by instructor {InstructorId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف المرفق.");
            return RedirectToAction("Edit", "Lessons", new { id = lessonId });
        }
    }
}

