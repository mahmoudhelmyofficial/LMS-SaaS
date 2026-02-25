using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة دروس الدورات - Course Lessons Management Controller
/// </summary>
public class LessonsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LessonsController> _logger;

    public LessonsController(
        ApplicationDbContext context,
        ILogger<LessonsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الدروس - Lessons List
    /// </summary>
    public async Task<IActionResult> Index(int? moduleId, int? courseId, string? lessonType, string? searchTerm)
    {
        var query = _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.Instructor)
            .AsQueryable();

        if (moduleId.HasValue)
        {
            query = query.Where(l => l.ModuleId == moduleId.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(l => l.Module.CourseId == courseId.Value);
        }

        if (!string.IsNullOrEmpty(lessonType))
        {
            if (Enum.TryParse<LMS.Domain.Enums.LessonType>(lessonType, out var type))
            {
                query = query.Where(l => l.Type == type);
            }
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(l => l.Title.Contains(searchTerm) || l.Module.Course.Title.Contains(searchTerm));
        }

        var lessons = await query
            .OrderBy(l => l.Module.Course.Title)
            .ThenBy(l => l.Module.OrderIndex)
            .ThenBy(l => l.OrderIndex)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.Modules = await _context.Modules
            .Include(m => m.Course)
            .OrderBy(m => m.Course.Title)
            .ThenBy(m => m.OrderIndex)
            .ToListAsync();

        ViewBag.ModuleId = moduleId;
        ViewBag.CourseId = courseId;
        ViewBag.LessonType = lessonType;
        ViewBag.SearchTerm = searchTerm;

        return View(lessons);
    }

    /// <summary>
    /// تفاصيل الدرس - Lesson Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.Instructor)
            .Include(l => l.Resources)
            .Include(l => l.Quizzes)
            .Include(l => l.Assignments)
            .Include(l => l.StudentProgress)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null)
            return NotFound();

        // Get statistics
        var totalStudents = await _context.Enrollments
            .CountAsync(e => e.CourseId == lesson.Module.CourseId);

        var completedStudents = lesson.StudentProgress.Count(sp => sp.IsCompleted);

        ViewBag.TotalStudents = totalStudents;
        ViewBag.CompletedStudents = completedStudents;
        ViewBag.CompletionRate = totalStudents > 0 ? (completedStudents * 100.0 / totalStudents) : 0;

        return View(lesson);
    }

    /// <summary>
    /// حذف الدرس - Delete Lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Resources)
            .Include(l => l.Quizzes)
            .Include(l => l.Assignments)
            .Include(l => l.StudentProgress)
            .Include(l => l.StudentNotes)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null)
            return NotFound();

        // Check if there are any quiz attempts or assignment submissions
        var hasQuizAttempts = await _context.QuizAttempts
            .AnyAsync(qa => lesson.Quizzes.Select(q => q.Id).Contains(qa.QuizId));

        var hasAssignmentSubmissions = await _context.AssignmentSubmissions
            .AnyAsync(asub => lesson.Assignments.Select(a => a.Id).Contains(asub.AssignmentId));

        if (hasQuizAttempts || hasAssignmentSubmissions)
        {
            SetErrorMessage("لا يمكن حذف الدرس لأن هناك طلاب قاموا بحل الاختبارات أو إرسال التكليفات");
            return RedirectToAction(nameof(Details), new { id });
        }

        var moduleId = lesson.ModuleId;

        _context.Lessons.Remove(lesson);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الدرس بنجاح");
        return RedirectToAction(nameof(Index), new { moduleId });
    }

    /// <summary>
    /// تبديل حالة المعاينة - Toggle preview status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePreview(int id)
    {
        var lesson = await _context.Lessons.FindAsync(id);
        if (lesson == null)
            return NotFound();

        lesson.IsPreviewable = !lesson.IsPreviewable;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(lesson.IsPreviewable ? "تفعيل" : "تعطيل")} المعاينة المجانية");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إحصائيات الدروس - Lessons Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var totalLessons = await _context.Lessons.CountAsync();
        
        var stats = new LessonStatisticsViewModel
        {
            TotalLessons = totalLessons,
            VideoLessons = await _context.Lessons.CountAsync(l => l.Type == LMS.Domain.Enums.LessonType.Video),
            ArticleLessons = await _context.Lessons.CountAsync(l => l.Type == LMS.Domain.Enums.LessonType.Article),
            QuizLessons = await _context.Lessons.CountAsync(l => l.Type == LMS.Domain.Enums.LessonType.Quiz),
            AssignmentLessons = await _context.Lessons.CountAsync(l => l.Type == LMS.Domain.Enums.LessonType.Assignment),
            PreviewableLessons = await _context.Lessons.CountAsync(l => l.IsPreviewable),
            DownloadableLessons = await _context.Lessons.CountAsync(l => l.IsDownloadable),
            AvgDurationSeconds = totalLessons > 0 
                ? await _context.Lessons.AverageAsync(l => (double)l.DurationSeconds) 
                : 0,
            TotalDurationHours = await _context.Lessons.SumAsync(l => l.DurationSeconds) / 3600.0
        };

        return View(stats);
    }

    /// <summary>
    /// إدارة دروس الوحدة - Manage Module Lessons
    /// </summary>
    public async Task<IActionResult> ManageLessons(int moduleId)
    {
        try
        {
            var module = await _context.Modules
                .Include(m => m.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null)
            {
                _logger.LogWarning("Module not found: {ModuleId}", moduleId);
                return NotFound();
            }

            var lessons = await _context.Lessons
                .Where(l => l.ModuleId == moduleId)
                .OrderBy(l => l.OrderIndex)
                .ToListAsync();

            ViewBag.Module = module;
            ViewBag.Course = module.Course;
            ViewBag.TotalLessons = lessons.Count;
            ViewBag.TotalDuration = lessons.Sum(l => l.DurationSeconds) / 60;

            return View(lessons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading manage lessons for module {ModuleId}", moduleId);
            SetErrorMessage("حدث خطأ أثناء تحميل دروس الوحدة");
            return RedirectToAction("ManageModules", "Modules", new { courseId = moduleId });
        }
    }

    /// <summary>
    /// صفحة إنشاء درس جديد - Create Lesson Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? moduleId)
    {
        ViewBag.ModuleId = moduleId?.ToString();
        ViewBag.Modules = await _context.Modules
            .Include(m => m.Course)
            .OrderBy(m => m.Course.Title)
            .ThenBy(m => m.OrderIndex)
            .ToListAsync();

        var lesson = new Lesson();
        if (moduleId.HasValue)
        {
            lesson.ModuleId = moduleId.Value;
            var maxOrderIndex = await _context.Lessons
                .Where(l => l.ModuleId == moduleId.Value)
                .MaxAsync(l => (int?)l.OrderIndex) ?? 0;
            lesson.OrderIndex = maxOrderIndex + 1;
        }

        return View(lesson);
    }

    /// <summary>
    /// إنشاء درس جديد - Create Lesson POST
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lesson lesson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lesson.Title))
            {
                ModelState.AddModelError("Title", "عنوان الدرس مطلوب");
            }

            var module = await _context.Modules.FindAsync(lesson.ModuleId);
            if (module == null)
            {
                ModelState.AddModelError("ModuleId", "الوحدة المحددة غير موجودة");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ModuleId = lesson.ModuleId.ToString();
                ViewBag.Modules = await _context.Modules
                    .Include(m => m.Course)
                    .OrderBy(m => m.Course.Title)
                    .ThenBy(m => m.OrderIndex)
                    .ToListAsync();
                return View(lesson);
            }

            lesson.CreatedAt = DateTime.UtcNow;
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            // Update module lessons count
            if (module != null)
            {
                module.LessonsCount = await _context.Lessons.CountAsync(l => l.ModuleId == module.Id);
                module.TotalDurationMinutes = await _context.Lessons
                    .Where(l => l.ModuleId == module.Id)
                    .SumAsync(l => l.DurationSeconds) / 60;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Lesson created: {LessonId} for module {ModuleId}", lesson.Id, lesson.ModuleId);
            SetSuccessMessage("تم إنشاء الدرس بنجاح");
            return RedirectToAction(nameof(Index), new { moduleId = lesson.ModuleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lesson");
            SetErrorMessage("حدث خطأ أثناء إنشاء الدرس");
            ViewBag.ModuleId = lesson.ModuleId.ToString();
            ViewBag.Modules = await _context.Modules
                .Include(m => m.Course)
                .OrderBy(m => m.Course.Title)
                .ThenBy(m => m.OrderIndex)
                .ToListAsync();
            return View(lesson);
        }
    }

    /// <summary>
    /// صفحة تعديل الدرس - Edit Lesson Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null)
        {
            _logger.LogWarning("Lesson not found: {LessonId}", id);
            return NotFound();
        }

        ViewBag.Modules = await _context.Modules
            .Include(m => m.Course)
            .OrderBy(m => m.Course.Title)
            .ThenBy(m => m.OrderIndex)
            .ToListAsync();

        return View(lesson);
    }

    /// <summary>
    /// تعديل الدرس - Edit Lesson POST
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lesson lesson)
    {
        if (id != lesson.Id)
            return BadRequest();

        try
        {
            var existingLesson = await _context.Lessons.FindAsync(id);
            if (existingLesson == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(lesson.Title))
            {
                ModelState.AddModelError("Title", "عنوان الدرس مطلوب");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Modules = await _context.Modules
                    .Include(m => m.Course)
                    .OrderBy(m => m.Course.Title)
                    .ThenBy(m => m.OrderIndex)
                    .ToListAsync();
                return View(lesson);
            }

            existingLesson.Title = lesson.Title;
            existingLesson.Description = lesson.Description;
            existingLesson.Type = lesson.Type;
            existingLesson.OrderIndex = lesson.OrderIndex;
            existingLesson.DurationSeconds = lesson.DurationSeconds;
            existingLesson.VideoUrl = lesson.VideoUrl;
            existingLesson.HtmlContent = lesson.HtmlContent;
            existingLesson.IsPreviewable = lesson.IsPreviewable;
            existingLesson.IsPublished = lesson.IsPublished;
            existingLesson.IsDownloadable = lesson.IsDownloadable;
            existingLesson.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Update module statistics
            var module = await _context.Modules.FindAsync(existingLesson.ModuleId);
            if (module != null)
            {
                module.TotalDurationMinutes = await _context.Lessons
                    .Where(l => l.ModuleId == module.Id)
                    .SumAsync(l => l.DurationSeconds) / 60;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Lesson updated: {LessonId}", id);
            SetSuccessMessage("تم تحديث الدرس بنجاح");
            return RedirectToAction(nameof(Index), new { moduleId = existingLesson.ModuleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lesson {LessonId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الدرس");
            ViewBag.Modules = await _context.Modules
                .Include(m => m.Course)
                .OrderBy(m => m.Course.Title)
                .ThenBy(m => m.OrderIndex)
                .ToListAsync();
            return View(lesson);
        }
    }

    /// <summary>
    /// نسخ الدرس - Duplicate Lesson
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Duplicate(int id)
    {
        try
        {
            var originalLesson = await _context.Lessons
                .Include(l => l.Resources)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (originalLesson == null)
            {
                _logger.LogWarning("Lesson not found for duplication: {LessonId}", id);
                return NotFound();
            }

            var maxOrderIndex = await _context.Lessons
                .Where(l => l.ModuleId == originalLesson.ModuleId)
                .MaxAsync(l => (int?)l.OrderIndex) ?? 0;

            var duplicatedLesson = new Lesson
            {
                ModuleId = originalLesson.ModuleId,
                Title = $"{originalLesson.Title} (نسخة)",
                Description = originalLesson.Description,
                Type = originalLesson.Type,
                OrderIndex = maxOrderIndex + 1,
                DurationSeconds = originalLesson.DurationSeconds,
                VideoUrl = originalLesson.VideoUrl,
                VideoProvider = originalLesson.VideoProvider,
                VideoId = originalLesson.VideoId,
                VideoThumbnailUrl = originalLesson.VideoThumbnailUrl,
                HtmlContent = originalLesson.HtmlContent,
                FileUrl = originalLesson.FileUrl,
                FileType = originalLesson.FileType,
                FileSize = originalLesson.FileSize,
                FileName = originalLesson.FileName,
                IsPreviewable = originalLesson.IsPreviewable,
                IsPublished = false,
                IsDownloadable = originalLesson.IsDownloadable,
                EnableWatermark = originalLesson.EnableWatermark,
                MustComplete = originalLesson.MustComplete,
                CreatedAt = DateTime.UtcNow
            };

            _context.Lessons.Add(duplicatedLesson);
            await _context.SaveChangesAsync();

            // Update module lessons count
            var module = await _context.Modules.FindAsync(originalLesson.ModuleId);
            if (module != null)
            {
                module.LessonsCount = await _context.Lessons.CountAsync(l => l.ModuleId == module.Id);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Lesson {OriginalId} duplicated to {NewId}", id, duplicatedLesson.Id);
            SetSuccessMessage("تم نسخ الدرس بنجاح");
            return RedirectToAction(nameof(Edit), new { id = duplicatedLesson.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating lesson {LessonId}", id);
            SetErrorMessage("حدث خطأ أثناء نسخ الدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إلغاء نشر الدرس - Unpublish Lesson
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Unpublish(int id)
    {
        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                _logger.LogWarning("Lesson not found for unpublish: {LessonId}", id);
                return NotFound();
            }

            ViewBag.Lesson = lesson;
            ViewBag.Course = lesson.Module.Course;

            return View(lesson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading unpublish page for lesson {LessonId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل الصفحة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تنفيذ إلغاء نشر الدرس - Execute Unpublish Lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("UnpublishConfirm")]
    public async Task<IActionResult> UnpublishPost(int id)
    {
        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
            {
                _logger.LogWarning("Lesson not found for unpublish: {LessonId}", id);
                return NotFound();
            }

            lesson.IsPublished = false;
            lesson.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} unpublished", id);
            SetSuccessMessage("تم إلغاء نشر الدرس بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpublishing lesson {LessonId}", id);
            SetErrorMessage("حدث خطأ أثناء إلغاء نشر الدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// نشر الدرس - Publish Lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        try
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson == null)
            {
                _logger.LogWarning("Lesson not found for publish: {LessonId}", id);
                return NotFound();
            }

            lesson.IsPublished = true;
            lesson.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} published", id);
            SetSuccessMessage("تم نشر الدرس بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing lesson {LessonId}", id);
            SetErrorMessage("حدث خطأ أثناء نشر الدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// معاينة الدرس - Preview Lesson
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview(int id)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.Instructor)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null)
        {
            _logger.LogWarning("Lesson not found for preview: {LessonId}", id);
            return NotFound();
        }

        return View(lesson);
    }

    /// <summary>
    /// نشر جميع الدروس - Publish All Lessons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishAll(int moduleId)
    {
        try
        {
            var lessons = await _context.Lessons
                .Where(l => l.ModuleId == moduleId)
                .ToListAsync();

            foreach (var lesson in lessons)
            {
                lesson.IsPublished = true;
                lesson.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("All lessons in module {ModuleId} published", moduleId);
            SetSuccessMessage($"تم نشر {lessons.Count} درس بنجاح");
            return RedirectToAction(nameof(Index), new { moduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing all lessons in module {ModuleId}", moduleId);
            SetErrorMessage("حدث خطأ أثناء نشر الدروس");
            return RedirectToAction(nameof(Index), new { moduleId });
        }
    }

    /// <summary>
    /// إلغاء نشر جميع الدروس - Unpublish All Lessons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishAll(int moduleId)
    {
        try
        {
            var lessons = await _context.Lessons
                .Where(l => l.ModuleId == moduleId)
                .ToListAsync();

            foreach (var lesson in lessons)
            {
                lesson.IsPublished = false;
                lesson.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("All lessons in module {ModuleId} unpublished", moduleId);
            SetSuccessMessage($"تم إلغاء نشر {lessons.Count} درس بنجاح");
            return RedirectToAction(nameof(Index), new { moduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpublishing all lessons in module {ModuleId}", moduleId);
            SetErrorMessage("حدث خطأ أثناء إلغاء نشر الدروس");
            return RedirectToAction(nameof(Index), new { moduleId });
        }
    }

    /// <summary>
    /// إعادة ترتيب الدروس - Reorder Lessons
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Reorder(int moduleId)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module == null)
            return NotFound();

        var lessons = await _context.Lessons
            .Where(l => l.ModuleId == moduleId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync();

        ViewBag.Module = module;
        return View(lessons);
    }

    /// <summary>
    /// حفظ ترتيب الدروس - Save Lessons Order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOrder(int moduleId, int[] lessonIds)
    {
        try
        {
            for (int i = 0; i < lessonIds.Length; i++)
            {
                var lesson = await _context.Lessons.FindAsync(lessonIds[i]);
                if (lesson != null && lesson.ModuleId == moduleId)
                {
                    lesson.OrderIndex = i + 1;
                    lesson.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lessons reordered in module {ModuleId}", moduleId);
            SetSuccessMessage("تم حفظ الترتيب بنجاح");
            return RedirectToAction(nameof(Index), new { moduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving lessons order in module {ModuleId}", moduleId);
            SetErrorMessage("حدث خطأ أثناء حفظ الترتيب");
            return RedirectToAction(nameof(Reorder), new { moduleId });
        }
    }
}

/// <summary>
/// نموذج إحصائيات الدروس - Lesson Statistics ViewModel
/// </summary>
public class LessonStatisticsViewModel
{
    public int TotalLessons { get; set; }
    public int VideoLessons { get; set; }
    public int ArticleLessons { get; set; }
    public int QuizLessons { get; set; }
    public int AssignmentLessons { get; set; }
    public int PreviewableLessons { get; set; }
    public int DownloadableLessons { get; set; }
    public double AvgDurationSeconds { get; set; }
    public double TotalDurationHours { get; set; }
}

