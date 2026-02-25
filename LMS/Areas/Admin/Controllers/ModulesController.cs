using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة وحدات الدورات - Course Modules Management Controller
/// </summary>
public class ModulesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(
        ApplicationDbContext context,
        ILogger<ModulesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الوحدات - Modules List
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? searchTerm)
    {
        var query = _context.Modules
            .Include(m => m.Course)
                .ThenInclude(c => c.Instructor)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(m => m.CourseId == courseId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(m => m.Title.Contains(searchTerm) || m.Course.Title.Contains(searchTerm));
        }

        var modules = await query
            .OrderBy(m => m.Course.Title)
            .ThenBy(m => m.OrderIndex)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.SearchTerm = searchTerm;

        return View(modules);
    }

    /// <summary>
    /// إدارة وحدات الدورة - Manage Course Modules
    /// </summary>
    public async Task<IActionResult> ManageModules(int courseId)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                _logger.LogWarning("Course not found: {CourseId}", courseId);
                return NotFound();
            }

            var modules = await _context.Modules
                .Include(m => m.Lessons)
                .Where(m => m.CourseId == courseId)
                .OrderBy(m => m.OrderIndex)
                .ToListAsync();

            ViewBag.Course = course;
            ViewBag.TotalModules = modules.Count;
            ViewBag.TotalLessons = modules.Sum(m => m.Lessons.Count);

            return View(modules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading manage modules for course {CourseId}", courseId);
            SetErrorMessage("حدث خطأ أثناء تحميل وحدات الدورة");
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }
    }

    /// <summary>
    /// إنشاء وحدة جديدة - Create Module
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModule(int courseId, string title, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                SetErrorMessage("عنوان الوحدة مطلوب");
                return RedirectToAction(nameof(ManageModules), new { courseId });
            }

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                _logger.LogWarning("Course not found: {CourseId}", courseId);
                return NotFound();
            }

            var maxOrderIndex = await _context.Modules
                .Where(m => m.CourseId == courseId)
                .MaxAsync(m => (int?)m.OrderIndex) ?? 0;

            var module = new Module
            {
                CourseId = courseId,
                Title = title,
                Description = description,
                OrderIndex = maxOrderIndex + 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Module created for course {CourseId}", courseId);
            SetSuccessMessage("تم إنشاء الوحدة بنجاح");
            return RedirectToAction(nameof(ManageModules), new { courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating module for course {CourseId}", courseId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الوحدة");
            return RedirectToAction(nameof(ManageModules), new { courseId });
        }
    }

    /// <summary>
    /// تحديث ترتيب الوحدات - Update Modules Order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateModulesOrder(int courseId, List<int> moduleIds)
    {
        try
        {
            if (moduleIds == null || !moduleIds.Any())
            {
                SetErrorMessage("لا توجد وحدات لتحديث ترتيبها");
                return RedirectToAction(nameof(ManageModules), new { courseId });
            }

            var modules = await _context.Modules
                .Where(m => m.CourseId == courseId)
                .ToListAsync();

            for (int i = 0; i < moduleIds.Count; i++)
            {
                var module = modules.FirstOrDefault(m => m.Id == moduleIds[i]);
                if (module != null)
                {
                    module.OrderIndex = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Modules order updated for course {CourseId}", courseId);
            SetSuccessMessage("تم تحديث ترتيب الوحدات بنجاح");
            return RedirectToAction(nameof(ManageModules), new { courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating modules order for course {CourseId}", courseId);
            SetErrorMessage("حدث خطأ أثناء تحديث الترتيب");
            return RedirectToAction(nameof(ManageModules), new { courseId });
        }
    }

    /// <summary>
    /// تفاصيل الوحدة - Module Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
                .ThenInclude(c => c.Instructor)
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
            return NotFound();

        return View(module);
    }

    /// <summary>
    /// حذف الوحدة - Delete Module
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var module = await _context.Modules
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
            return NotFound();

        if (module.Lessons.Any())
        {
            SetErrorMessage($"لا يمكن حذف الوحدة لأنها تحتوي على {module.Lessons.Count} درس");
            return RedirectToAction(nameof(Details), new { id });
        }

        var courseId = module.CourseId;

        _context.Modules.Remove(module);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الوحدة بنجاح");
        return RedirectToAction(nameof(Index), new { courseId });
    }

    /// <summary>
    /// إحصائيات الوحدات - Modules Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var stats = new ModuleStatisticsViewModel
        {
            TotalModules = await _context.Modules.CountAsync(),
            TotalLessons = await _context.Lessons.CountAsync(),
            EmptyModules = await _context.Modules.CountAsync(m => m.LessonsCount == 0),
            AvgLessonsPerModule = await _context.Modules.AverageAsync(m => (double)m.LessonsCount),
            AvgDurationMinutes = await _context.Modules.AverageAsync(m => (double)m.TotalDurationMinutes)
        };

        return View(stats);
    }

    /// <summary>
    /// صفحة إنشاء وحدة جديدة - Create Module Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? courseId)
    {
        ViewBag.CourseId = courseId?.ToString();
        ViewBag.Courses = await _context.Courses
            .OrderBy(c => c.Title)
            .ToListAsync();

        var module = new Module();
        if (courseId.HasValue)
        {
            module.CourseId = courseId.Value;
            var maxOrderIndex = await _context.Modules
                .Where(m => m.CourseId == courseId.Value)
                .MaxAsync(m => (int?)m.OrderIndex) ?? 0;
            module.OrderIndex = maxOrderIndex + 1;
        }

        return View(module);
    }

    /// <summary>
    /// إنشاء وحدة جديدة - Create Module POST
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Module module)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(module.Title))
            {
                ModelState.AddModelError("Title", "عنوان الوحدة مطلوب");
            }

            var course = await _context.Courses.FindAsync(module.CourseId);
            if (course == null)
            {
                ModelState.AddModelError("CourseId", "الدورة المحددة غير موجودة");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CourseId = module.CourseId.ToString();
                ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
                return View(module);
            }

            module.CreatedAt = DateTime.UtcNow;
            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Module created: {ModuleId} for course {CourseId}", module.Id, module.CourseId);
            SetSuccessMessage("تم إنشاء الوحدة بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = module.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating module");
            SetErrorMessage("حدث خطأ أثناء إنشاء الوحدة");
            ViewBag.CourseId = module.CourseId.ToString();
            ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
            return View(module);
        }
    }

    /// <summary>
    /// صفحة تعديل الوحدة - Edit Module Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (module == null)
        {
            _logger.LogWarning("Module not found: {ModuleId}", id);
            return NotFound();
        }

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
        return View(module);
    }

    /// <summary>
    /// تعديل الوحدة - Edit Module POST
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Module module)
    {
        if (id != module.Id)
            return BadRequest();

        try
        {
            var existingModule = await _context.Modules.FindAsync(id);
            if (existingModule == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(module.Title))
            {
                ModelState.AddModelError("Title", "عنوان الوحدة مطلوب");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
                return View(module);
            }

            existingModule.Title = module.Title;
            existingModule.Description = module.Description;
            existingModule.OrderIndex = module.OrderIndex;
            existingModule.IsPublished = module.IsPublished;
            existingModule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Module updated: {ModuleId}", id);
            SetSuccessMessage("تم تحديث الوحدة بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = existingModule.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module {ModuleId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الوحدة");
            ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
            return View(module);
        }
    }

    /// <summary>
    /// نسخ الوحدة - Duplicate Module
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Duplicate(int id)
    {
        try
        {
            var originalModule = await _context.Modules
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (originalModule == null)
            {
                _logger.LogWarning("Module not found for duplication: {ModuleId}", id);
                return NotFound();
            }

            var maxOrderIndex = await _context.Modules
                .Where(m => m.CourseId == originalModule.CourseId)
                .MaxAsync(m => (int?)m.OrderIndex) ?? 0;

            var duplicatedModule = new Module
            {
                CourseId = originalModule.CourseId,
                Title = $"{originalModule.Title} (نسخة)",
                Description = originalModule.Description,
                OrderIndex = maxOrderIndex + 1,
                IsPublished = false,
                IsFreePreview = originalModule.IsFreePreview,
                CreatedAt = DateTime.UtcNow
            };

            _context.Modules.Add(duplicatedModule);
            await _context.SaveChangesAsync();

            // Duplicate lessons
            foreach (var lesson in originalModule.Lessons)
            {
                var duplicatedLesson = new Lesson
                {
                    ModuleId = duplicatedModule.Id,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    Type = lesson.Type,
                    OrderIndex = lesson.OrderIndex,
                    DurationSeconds = lesson.DurationSeconds,
                    VideoUrl = lesson.VideoUrl,
                    VideoProvider = lesson.VideoProvider,
                    VideoId = lesson.VideoId,
                    HtmlContent = lesson.HtmlContent,
                    FileUrl = lesson.FileUrl,
                    FileType = lesson.FileType,
                    IsPreviewable = lesson.IsPreviewable,
                    IsPublished = false,
                    IsDownloadable = lesson.IsDownloadable,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Lessons.Add(duplicatedLesson);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Module {OriginalId} duplicated to {NewId}", id, duplicatedModule.Id);
            SetSuccessMessage("تم نسخ الوحدة بنجاح");
            return RedirectToAction(nameof(Edit), new { id = duplicatedModule.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating module {ModuleId}", id);
            SetErrorMessage("حدث خطأ أثناء نسخ الوحدة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

/// <summary>
/// نموذج إحصائيات الوحدات - Module Statistics ViewModel
/// </summary>
public class ModuleStatisticsViewModel
{
    public int TotalModules { get; set; }
    public int TotalLessons { get; set; }
    public int EmptyModules { get; set; }
    public double AvgLessonsPerModule { get; set; }
    public double AvgDurationMinutes { get; set; }
}

