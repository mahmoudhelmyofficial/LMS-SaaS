using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة وحدات الدورة - Course Modules Controller
/// </summary>
public class ModulesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ModulesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة جميع الوحدات لجميع الدورات - All Modules List
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int page = 1)
    {
        var userId = _currentUserService.UserId;

        try
        {
            // If courseId is provided, show modules for that specific course
            if (courseId.HasValue && courseId.Value > 0)
            {
                var course = await _context.Courses
                    .Include(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                    .FirstOrDefaultAsync(c => c.Id == courseId.Value && c.InstructorId == userId);

                if (course == null)
                {
                    _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", courseId, userId);
                    SetErrorMessage("لم يتم العثور على الدورة");
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Course = course;
                ViewBag.CourseId = courseId;
                ViewBag.ShowAllModules = false;
                
                var modules = course.Modules.OrderBy(m => m.OrderIndex).ToList();

                // Statistics for course-specific view
                ViewBag.TotalModules = modules.Count;
                ViewBag.PublishedModules = modules.Count(m => m.IsPublished);
                ViewBag.DraftModules = modules.Count(m => !m.IsPublished);
                ViewBag.TotalLessons = modules.SelectMany(m => m.Lessons).Count();

                _logger.LogInformation("Instructor {InstructorId} viewed modules for course {CourseId}. Count: {Count}", 
                    userId, courseId, modules.Count);

                // Get all courses for filter dropdown
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .OrderBy(c => c.Title)
                    .Select(c => new { c.Id, c.Title })
                    .ToListAsync();

                return View(modules);
            }

            // Otherwise, show all modules across all courses
            var query = _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .Where(m => m.Course.InstructorId == userId);

            var totalCount = await query.CountAsync();
            var allModules = await query
                .OrderBy(m => m.Course.Title)
                .ThenBy(m => m.OrderIndex)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            // Get all courses for filter dropdown
            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            // Statistics for all modules view
            var allModulesForStats = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .Where(m => m.Course.InstructorId == userId)
                .ToListAsync();

            ViewBag.TotalModules = allModulesForStats.Count;
            ViewBag.PublishedModules = allModulesForStats.Count(m => m.IsPublished);
            ViewBag.DraftModules = allModulesForStats.Count(m => !m.IsPublished);
            ViewBag.TotalLessons = allModulesForStats.SelectMany(m => m.Lessons).Count();

            ViewBag.ShowAllModules = true;
            ViewBag.CourseId = null;
            ViewBag.Course = null;
            ViewBag.Page = page;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

            _logger.LogInformation("Instructor {InstructorId} viewed all modules. Total: {Count}", 
                userId, totalCount);

            return View(allModules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading modules for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل الوحدات");
            return View(new List<Domain.Entities.Courses.Module>());
        }
    }

    /// <summary>
    /// تفاصيل الوحدة - Module Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var module = await _context.Modules
            .Include(m => m.Course)
            .Include(m => m.Lessons)
                .ThenInclude(l => l.Quizzes)
            .Include(m => m.Lessons)
                .ThenInclude(l => l.Assignments)
            .FirstOrDefaultAsync(m => m.Id == id && m.Course.InstructorId == userId);

        if (module == null)
        {
            _logger.LogWarning("Module {ModuleId} not found for instructor {InstructorId}", id, userId);
            return NotFound();
        }

        ViewBag.Course = module.Course;
        
        return View(module);
    }

    /// <summary>
    /// إنشاء وحدة جديدة - Create new module (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var userId = _currentUserService.UserId;
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        ViewBag.Course = course;
        return View(new ModuleCreateViewModel { CourseId = courseId });
    }

    /// <summary>
    /// إضافة وحدة جديدة - Add new module (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ModuleCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        var course = await _context.Courses
            .Include(c => c.Modules)
            .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

        if (course == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            var maxOrder = course.Modules.Any() ? course.Modules.Max(m => m.OrderIndex) : 0;

            var module = new Module
            {
                CourseId = model.CourseId,
                Title = model.Title,
                Description = model.Description,
                OrderIndex = maxOrder + 1,
                IsPublished = false
            };

            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة الوحدة بنجاح");
            return RedirectToAction("Edit", "Courses", new { id = model.CourseId });
        }

        ViewBag.Course = course;
        return View(model);
    }

    /// <summary>
    /// تعديل الوحدة - Edit module (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        var module = await _context.Modules
            .Include(m => m.Course)
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == id && m.Course.InstructorId == userId);

        if (module == null)
            return NotFound();

        var viewModel = new ModuleEditViewModel
        {
            Id = module.Id,
            CourseId = module.CourseId,
            Title = module.Title,
            Description = module.Description,
            IsPublished = module.IsPublished,
            OrderIndex = module.OrderIndex
        };

        ViewBag.Course = module.Course;
        ViewBag.Lessons = module.Lessons.OrderBy(l => l.OrderIndex).ToList();

        return View(viewModel);
    }

    /// <summary>
    /// تعديل الوحدة - Edit module (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ModuleEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        var module = await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == id && m.Course.InstructorId == userId);

        if (module == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            module.Title = model.Title;
            module.Description = model.Description;
            module.IsPublished = model.IsPublished;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الوحدة بنجاح");
            return RedirectToAction("Edit", "Courses", new { id = module.CourseId });
        }

        ViewBag.Course = module.Course;
        return View(model);
    }

    /// <summary>
    /// حذف الوحدة - Delete module
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        int? courseId = null;

        try
        {
            var module = await _context.Modules
                .Include(m => m.Course)
                    .ThenInclude(c => c.Enrollments)
                .Include(m => m.Lessons)
                    .ThenInclude(l => l.Quiz)
                .Include(m => m.Lessons)
                    .ThenInclude(l => l.Assignment)
                .FirstOrDefaultAsync(m => m.Id == id && m.Course.InstructorId == userId);

            if (module == null)
            {
                _logger.LogWarning("Module {ModuleId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("الوحدة غير موجودة أو ليس لديك صلاحية الوصول إليها");
                return RedirectToAction("Index", "Courses");
            }

            courseId = module.CourseId;

            // Use BusinessRuleHelper for validation
            var lessonCount = module.Lessons?.Count ?? 0;
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteModule(lessonCount, module.IsPublished);

            if (!canDelete)
            {
                _logger.LogWarning("Cannot delete module {ModuleId}: {Reason}", id, reason);
                SetErrorMessage(reason ?? "لا يمكن حذف هذه الوحدة");
                return RedirectToAction("Edit", "Courses", new { id = courseId });
            }

            // Additional check: prevent deletion if course has enrollments and module is published
            if (module.IsPublished && module.Course.Enrollments != null && module.Course.Enrollments.Any())
            {
                _logger.LogWarning("Cannot delete published module {ModuleId} from course with enrollments", id);
                SetErrorMessage("لا يمكن حذف وحدة منشورة من دورة بها تسجيلات. قم بإلغاء نشرها أولاً");
                return RedirectToAction("Edit", "Courses", new { id = courseId });
            }

            // Check for quiz or assignment attempts
            foreach (var lesson in module.Lessons ?? Enumerable.Empty<Lesson>())
            {
                if (lesson.Quiz != null)
                {
                    var hasQuizAttempts = await _context.QuizAttempts.AnyAsync(qa => qa.QuizId == lesson.Quiz.Id);
                    if (hasQuizAttempts)
                    {
                        _logger.LogWarning("Cannot delete module {ModuleId} - has quiz attempts", id);
                        SetErrorMessage("لا يمكن حذف الوحدة لأنها تحتوي على اختبارات تم حلها من قبل الطلاب");
                        return RedirectToAction("Edit", "Courses", new { id = courseId });
                    }
                }
                
                if (lesson.Assignment != null)
                {
                    var hasSubmissions = await _context.AssignmentSubmissions.AnyAsync(s => s.AssignmentId == lesson.Assignment.Id);
                    if (hasSubmissions)
                    {
                        _logger.LogWarning("Cannot delete module {ModuleId} - has assignment submissions", id);
                        SetErrorMessage("لا يمكن حذف الوحدة لأنها تحتوي على تكليفات تم تسليمها من قبل الطلاب");
                        return RedirectToAction("Edit", "Courses", new { id = courseId });
                    }
                }
            }

            var moduleTitle = module.Title;
            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                // Delete related entities first (order matters for FK constraints)
                foreach (var lesson in module.Lessons ?? Enumerable.Empty<Lesson>())
                {
                    // Delete assignment submissions before assignment (FK)
                    if (lesson.Assignment != null)
                    {
                        var submissions = await _context.AssignmentSubmissions
                            .Where(s => s.AssignmentId == lesson.Assignment.Id)
                            .ToListAsync();
                        _context.AssignmentSubmissions.RemoveRange(submissions);
                    }

                    // Delete quiz questions and options
                    if (lesson.Quiz != null)
                    {
                        var questions = await _context.Questions.Where(q => q.QuizId == lesson.Quiz.Id).ToListAsync();
                        foreach (var question in questions)
                        {
                            var options = await _context.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync();
                            _context.QuestionOptions.RemoveRange(options);
                        }
                        _context.Questions.RemoveRange(questions);
                        _context.Quizzes.Remove(lesson.Quiz);
                    }

                    // Delete assignments
                    if (lesson.Assignment != null)
                    {
                        _context.Assignments.Remove(lesson.Assignment);
                    }

                    // Delete lesson progress records (FK from LessonProgress to Lesson)
                    var progressRecords = await _context.LessonProgresses.Where(lp => lp.LessonId == lesson.Id).ToListAsync();
                    _context.LessonProgresses.RemoveRange(progressRecords);

                    // Delete lesson resources
                    var resources = await _context.LessonResources.Where(r => r.LessonId == lesson.Id).ToListAsync();
                    _context.LessonResources.RemoveRange(resources);
                }

                // Delete all lessons
                if (module.Lessons != null && module.Lessons.Any())
                {
                    _context.Lessons.RemoveRange(module.Lessons);
                }

                // Delete the module
                _context.Modules.Remove(module);
                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                _logger.LogError("Transaction failed while deleting module {ModuleId}: {Error}", id, error);
                SetErrorMessage(error ?? "حدث خطأ أثناء حذف الوحدة");
                return RedirectToAction("Edit", "Courses", new { id = courseId });
            }

            _logger.LogInformation("Module {ModuleId} '{Title}' deleted by instructor {InstructorId}", 
                id, moduleTitle, userId);

            SetSuccessMessage($"تم حذف الوحدة '{moduleTitle}' بنجاح");
            return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error deleting module {ModuleId}", id);
            SetErrorMessage("لا يمكن حذف الوحدة بسبب وجود بيانات مرتبطة بها. يرجى حذف الدروس والمحتوى أولاً.");
            
            if (courseId.HasValue)
                return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting module {ModuleId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الوحدة. يرجى المحاولة مرة أخرى.");
            
            if (courseId.HasValue)
                return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
    }

    /// <summary>
    /// تبديل حالة النشر - Toggle publish status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var userId = _currentUserService.UserId;
        int? courseId = null;

        try
        {
            var module = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == id && m.Course.InstructorId == userId);

            if (module == null)
            {
                _logger.LogWarning("Module {ModuleId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("الوحدة غير موجودة أو ليس لديك صلاحية الوصول إليها");
                return RedirectToAction("Index", "Courses");
            }

            courseId = module.CourseId;

            // If trying to publish, validate module is ready
            if (!module.IsPublished)
            {
                var lessonCount = module.Lessons?.Count ?? 0;
                var hasVideoLesson = module.Lessons?.Any(l => l.Type == Domain.Enums.LessonType.Video) ?? false;

                var (canPublish, reason) = BusinessRuleHelper.CanPublishModule(lessonCount, hasVideoLesson);

                if (!canPublish)
                {
                    _logger.LogWarning("Cannot publish module {ModuleId}: {Reason}", id, reason);
                    SetErrorMessage(reason ?? "لا يمكن نشر هذه الوحدة");
                    return RedirectToAction("Edit", "Courses", new { id = courseId });
                }
            }

            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                module.IsPublished = !module.IsPublished;
                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                _logger.LogError("Transaction failed while toggling module {ModuleId} publish status: {Error}", id, error);
                SetErrorMessage(error ?? "حدث خطأ أثناء تغيير حالة النشر");
                return RedirectToAction("Edit", "Courses", new { id = courseId });
            }

            _logger.LogInformation("Module {ModuleId} publish status changed to {IsPublished} by instructor {InstructorId}", 
                id, module.IsPublished, userId);

            SetSuccessMessage(module.IsPublished ? "تم نشر الوحدة بنجاح" : "تم إلغاء نشر الوحدة");
            return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error toggling publish status for module {ModuleId}", id);
            SetErrorMessage("حدث خطأ في قاعدة البيانات أثناء تغيير حالة النشر");
            
            if (courseId.HasValue)
                return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling publish status for module {ModuleId}", id);
            SetErrorMessage("حدث خطأ أثناء تغيير حالة النشر. يرجى المحاولة مرة أخرى.");
            
            if (courseId.HasValue)
                return RedirectToAction(nameof(Index), "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
    }

    /// <summary>
    /// تحديث ترتيب الوحدات - Update modules order (transactional for data integrity)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrder([FromBody] List<ModuleOrderItem> items)
    {
        if (items == null || !items.Any())
            return BadRequest(new { success = false, message = "لا توجد عناصر للترتيب" });

        var userId = _currentUserService.UserId;

        var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
        {
            foreach (var item in items)
            {
                var module = await _context.Modules
                    .Include(m => m.Course)
                    .FirstOrDefaultAsync(m => m.Id == item.Id && m.Course.InstructorId == userId);

                if (module != null)
                {
                    module.OrderIndex = item.Order;
                }
            }
            await _context.SaveChangesAsync();
        }, _logger);

        if (!success)
        {
            _logger.LogWarning("UpdateOrder failed: {Error}", error);
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء تحديث الترتيب. يرجى المحاولة مرة أخرى." });
        }
        return Ok(new { success = true, message = "تم تحديث الترتيب بنجاح" });
    }

    /// <summary>
    /// تحديث ترتيب الدروس داخل الوحدة - Update lessons order within module (transactional)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLessonsOrder([FromBody] List<LessonOrderItem> items)
    {
        if (items == null || !items.Any())
            return BadRequest(new { success = false, message = "لا توجد عناصر للترتيب" });

        var userId = _currentUserService.UserId;

        var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
        {
            foreach (var item in items)
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == item.Id && l.Module.Course.InstructorId == userId);

                if (lesson != null)
                {
                    lesson.OrderIndex = item.Order;
                }
            }
            await _context.SaveChangesAsync();
        }, _logger);

        if (!success)
        {
            _logger.LogWarning("UpdateLessonsOrder failed: {Error}", error);
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء تحديث ترتيب الدروس. يرجى المحاولة مرة أخرى." });
        }
        return Ok(new { success = true, message = "تم تحديث ترتيب الدروس بنجاح" });
    }

    /// <summary>
    /// تعديل سريع للوحدة - Quick edit module title/description via AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEditModule([FromBody] ModuleQuickEditModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Json(new { success = false, message = "عنوان الوحدة مطلوب" });

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == model.Id && m.Course.InstructorId == userId);

            if (module == null)
            {
                _logger.LogWarning("QuickEditModule: Module {ModuleId} not found for instructor {InstructorId}", model.Id, userId);
                return Json(new { success = false, message = "الوحدة غير موجودة" });
            }

            module.Title = model.Title.Trim();
            if (model.Description != null)
                module.Description = model.Description.Trim();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Module {ModuleId} quick-edited by instructor {InstructorId}", model.Id, userId);
            return Json(new { success = true, message = "تم تحديث الوحدة بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QuickEditModule for module {ModuleId}", model.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث الوحدة" });
        }
    }

    /// <summary>
    /// نشر مجمع للوحدات - Bulk publish modules
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkPublish([FromBody] BulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي وحدات" });

            var modules = await _context.Modules
                .Include(m => m.Course)
                .Include(m => m.Lessons)
                .Where(m => model.Ids.Contains(m.Id) && m.Course.InstructorId == userId)
                .ToListAsync();

            if (!modules.Any())
                return Json(new { success = false, message = "لم يتم العثور على وحدات" });

            var publishedCount = 0;
            var errors = new List<string>();

            foreach (var module in modules)
            {
                if (module.IsPublished) continue;

                var lessonCount = module.Lessons?.Count ?? 0;
                var hasVideoLesson = module.Lessons?.Any(l => l.Type == Domain.Enums.LessonType.Video) ?? false;
                var (canPublish, reason) = BusinessRuleHelper.CanPublishModule(lessonCount, hasVideoLesson);

                if (!canPublish)
                {
                    errors.Add($"'{module.Title}': {reason}");
                    continue;
                }

                module.IsPublished = true;
                publishedCount++;
            }

            await _context.SaveChangesAsync();

            var message = $"تم نشر {publishedCount} وحدة بنجاح";
            if (errors.Any())
                message += $". تعذر نشر {errors.Count}: {string.Join("، ", errors)}";

            _logger.LogInformation("Bulk publish: {Count} modules published by instructor {InstructorId}", publishedCount, userId);
            return Json(new { success = true, message, count = publishedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkPublish modules");
            return Json(new { success = false, message = "حدث خطأ أثناء نشر الوحدات" });
        }
    }

    /// <summary>
    /// إلغاء نشر مجمع للوحدات - Bulk unpublish modules
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUnpublish([FromBody] BulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي وحدات" });

            var modules = await _context.Modules
                .Include(m => m.Course)
                .Where(m => model.Ids.Contains(m.Id) && m.Course.InstructorId == userId)
                .ToListAsync();

            if (!modules.Any())
                return Json(new { success = false, message = "لم يتم العثور على وحدات" });

            var count = 0;
            foreach (var module in modules)
            {
                if (!module.IsPublished) continue;
                module.IsPublished = false;
                count++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bulk unpublish: {Count} modules unpublished by instructor {InstructorId}", count, userId);
            return Json(new { success = true, message = $"تم إلغاء نشر {count} وحدة بنجاح", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkUnpublish modules");
            return Json(new { success = false, message = "حدث خطأ أثناء إلغاء نشر الوحدات" });
        }
    }

    /// <summary>
    /// حذف مجمع للوحدات - Bulk delete modules
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDeleteModules([FromBody] BulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي وحدات" });

            var modules = await _context.Modules
                .Include(m => m.Course)
                    .ThenInclude(c => c.Enrollments)
                .Include(m => m.Lessons)
                    .ThenInclude(l => l.Quizzes)
                .Include(m => m.Lessons)
                    .ThenInclude(l => l.Assignments)
                .Where(m => model.Ids.Contains(m.Id) && m.Course.InstructorId == userId)
                .ToListAsync();

            if (!modules.Any())
                return Json(new { success = false, message = "لم يتم العثور على وحدات" });

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var module in modules)
            {
                var lessonCount = module.Lessons?.Count ?? 0;
                var (canDelete, reason) = BusinessRuleHelper.CanDeleteModule(lessonCount, module.IsPublished);

                if (!canDelete)
                {
                    errors.Add($"'{module.Title}': {reason}");
                    continue;
                }

                if (module.IsPublished && module.Course.Enrollments != null && module.Course.Enrollments.Any())
                {
                    errors.Add($"'{module.Title}': لا يمكن حذف وحدة منشورة من دورة بها تسجيلات");
                    continue;
                }

                // Check for quiz attempts and assignment submissions
                var hasAttempts = false;
                foreach (var lesson in module.Lessons ?? Enumerable.Empty<Lesson>())
                {
                    if (lesson.Quiz != null)
                    {
                        hasAttempts = await _context.QuizAttempts.AnyAsync(qa => qa.QuizId == lesson.Quiz.Id);
                        if (hasAttempts) break;
                    }
                    if (lesson.Assignment != null)
                    {
                        hasAttempts = await _context.AssignmentSubmissions.AnyAsync(s => s.AssignmentId == lesson.Assignment.Id);
                        if (hasAttempts) break;
                    }
                }

                if (hasAttempts)
                {
                    errors.Add($"'{module.Title}': تحتوي على اختبارات أو تكليفات تم حلها");
                    continue;
                }

                // Delete related entities
                foreach (var lesson in module.Lessons ?? Enumerable.Empty<Lesson>())
                {
                    var progressRecords = await _context.LessonProgresses.Where(lp => lp.LessonId == lesson.Id).ToListAsync();
                    _context.LessonProgresses.RemoveRange(progressRecords);

                    var resources = await _context.LessonResources.Where(r => r.LessonId == lesson.Id).ToListAsync();
                    _context.LessonResources.RemoveRange(resources);

                    if (lesson.Quiz != null)
                    {
                        var questions = await _context.Questions.Where(q => q.QuizId == lesson.Quiz.Id).ToListAsync();
                        foreach (var question in questions)
                        {
                            var options = await _context.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync();
                            _context.QuestionOptions.RemoveRange(options);
                        }
                        _context.Questions.RemoveRange(questions);
                        _context.Quizzes.Remove(lesson.Quiz);
                    }

                    if (lesson.Assignment != null)
                    {
                        var submissions = await _context.AssignmentSubmissions.Where(s => s.AssignmentId == lesson.Assignment.Id).ToListAsync();
                        _context.AssignmentSubmissions.RemoveRange(submissions);
                        _context.Assignments.Remove(lesson.Assignment);
                    }
                }

                if (module.Lessons != null && module.Lessons.Any())
                    _context.Lessons.RemoveRange(module.Lessons);

                _context.Modules.Remove(module);
                deletedCount++;
            }

            await _context.SaveChangesAsync();

            var message = $"تم حذف {deletedCount} وحدة بنجاح";
            if (errors.Any())
                message += $". تعذر حذف {errors.Count}: {string.Join("، ", errors)}";

            _logger.LogInformation("Bulk delete: {Count} modules deleted by instructor {InstructorId}", deletedCount, userId);
            return Json(new { success = true, message, count = deletedCount, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkDeleteModules");
            return Json(new { success = false, message = "حدث خطأ أثناء حذف الوحدات" });
        }
    }
}
