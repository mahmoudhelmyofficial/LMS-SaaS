using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة الأسئلة الشائعة للدورات - Course FAQ Management Controller
/// </summary>
public class FaqController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FaqController> _logger;

    public FaqController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FaqController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الأسئلة الشائعة - FAQ list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? category, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Faqs
            .Include(f => f.Course)
            .Include(f => f.Lesson)
            .Where(f => f.Course!.InstructorId == userId);

        if (courseId.HasValue)
            query = query.Where(f => f.CourseId == courseId.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(f => f.Category == category);

        var faqs = await query
            .OrderBy(f => f.DisplayOrder)
            .ThenByDescending(f => f.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Category = category;
        ViewBag.Page = page;

        // Get instructor's courses
        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();

        return View(faqs);
    }

    /// <summary>
    /// إنشاء سؤال جديد - Create new FAQ
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? courseId)
    {
        await PopulateCoursesDropdownAsync();
        
        var model = new FaqFormViewModel
        {
            CourseId = courseId
        };

        if (courseId.HasValue)
        {
            await PopulateLessonsDropdownAsync(courseId.Value);
        }

        return View(model);
    }

    /// <summary>
    /// حفظ السؤال الجديد - Save new FAQ
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FaqFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            // Verify course ownership
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to create FAQ for course {CourseId} they do not own.", userId, model.CourseId);
                SetErrorMessage("غير مصرح لك بإضافة أسئلة لهذه الدورة");
                return RedirectToAction(nameof(Index));
            }

            var (isValid, reason) = BusinessRuleHelper.ValidateFaq(model.Question, model.Answer);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, reason!);
                SetErrorMessage(reason!);
                await PopulateCoursesDropdownAsync();
                if (model.CourseId.HasValue)
                {
                    await PopulateLessonsDropdownAsync(model.CourseId.Value);
                }
                return View(model);
            }

            Faq? createdFaq = null;
            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                createdFaq = new Faq
                {
                    Question = model.Question,
                    Answer = model.Answer,
                    Category = model.Category,
                    CourseId = model.CourseId,
                    LessonId = model.LessonId,
                    DisplayOrder = model.DisplayOrder,
                    IsPublished = true,
                    AuthorId = userId
                };

                _context.Faqs.Add(createdFaq);
                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                SetErrorMessage(error ?? "حدث خطأ أثناء إنشاء السؤال.");
                await PopulateCoursesDropdownAsync();
                if (model.CourseId.HasValue)
                {
                    await PopulateLessonsDropdownAsync(model.CourseId.Value);
                }
                return View(model);
            }

            _logger.LogInformation("FAQ {FaqId} created for course {CourseId} by instructor {InstructorId}.", createdFaq?.Id, model.CourseId, userId);
            SetSuccessMessage("تم إنشاء السؤال بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
        }

        await PopulateCoursesDropdownAsync();
        if (model.CourseId.HasValue)
        {
            await PopulateLessonsDropdownAsync(model.CourseId.Value);
        }

        return View(model);
    }

    /// <summary>
    /// تعديل سؤال - Edit FAQ
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var faq = await _context.Faqs
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (faq == null)
            return NotFound();

        var model = new FaqFormViewModel
        {
            Id = faq.Id,
            Question = faq.Question,
            Answer = faq.Answer,
            Category = faq.Category,
            CourseId = faq.CourseId,
            LessonId = faq.LessonId,
            DisplayOrder = faq.DisplayOrder
        };

        await PopulateCoursesDropdownAsync();
        if (faq.CourseId.HasValue)
        {
            await PopulateLessonsDropdownAsync(faq.CourseId.Value);
        }

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات السؤال - Save FAQ changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FaqFormViewModel model)
    {
        if (id != model.Id)
        {
            _logger.LogWarning("BadRequest: FAQ ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, model.Id);
            SetErrorMessage("خطأ في معرّف السؤال.");
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var faq = await _context.Faqs
                .Include(f => f.Course)
                .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

            if (faq == null)
            {
                _logger.LogWarning("NotFound: FAQ {FaqId} not found or instructor {InstructorId} unauthorized.", id, userId);
                SetErrorMessage("السؤال غير موجود أو ليس لديك صلاحية عليه.");
                return NotFound();
            }

            // Authorization check
            if (faq.Course?.InstructorId != userId)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to edit FAQ {FaqId}.", userId, id);
                SetErrorMessage("غير مصرح لك بتعديل هذا السؤال.");
                return Forbid();
            }

            var (isValid, reason) = BusinessRuleHelper.ValidateFaq(model.Question, model.Answer);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, reason!);
                SetErrorMessage(reason!);
                await PopulateCoursesDropdownAsync();
                if (model.CourseId.HasValue)
                {
                    await PopulateLessonsDropdownAsync(model.CourseId.Value);
                }
                return View(model);
            }

            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    faq.Question = model.Question;
                    faq.Answer = model.Answer;
                    faq.Category = model.Category;
                    faq.LessonId = model.LessonId;
                    faq.DisplayOrder = model.DisplayOrder;

                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("FAQ {FaqId} updated by instructor {InstructorId}.", id, userId);
                SetSuccessMessage("تم تحديث السؤال بنجاح");
                return RedirectToAction(nameof(Index), new { courseId = faq.CourseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FAQ {FaqId} by instructor {InstructorId}.", id, userId);
                SetErrorMessage("حدث خطأ أثناء تحديث السؤال.");
            }
        }

        await PopulateCoursesDropdownAsync();
        if (model.CourseId.HasValue)
        {
            await PopulateLessonsDropdownAsync(model.CourseId.Value);
        }

        return View(model);
    }

    /// <summary>
    /// حذف سؤال - Delete FAQ
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var faq = await _context.Faqs
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (faq == null)
        {
            _logger.LogWarning("NotFound: FAQ {FaqId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("السؤال غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        // Authorization check
        if (faq.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to delete FAQ {FaqId}.", userId, id);
            SetErrorMessage("غير مصرح لك بحذف هذا السؤال.");
            return Forbid();
        }

        var courseId = faq.CourseId;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.Faqs.Remove(faq);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("FAQ {FaqId} deleted by instructor {InstructorId}.", id, userId);
            SetSuccessMessage("تم حذف السؤال بنجاح");
            return RedirectToAction(nameof(Index), new { courseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting FAQ {FaqId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف السؤال.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الحصول على الدروس للدورة - Get lessons for course (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLessons(int courseId)
    {
        var userId = _currentUserService.UserId;

        var lessons = await _context.Lessons
            .Include(l => l.Module)
            .Where(l => l.Module.Course.InstructorId == userId && l.Module.CourseId == courseId)
            .Select(l => new
            {
                id = l.Id,
                title = l.Title,
                moduleTitle = l.Module.Title
            })
            .ToListAsync();

        return Json(lessons);
    }

    #region Private Helpers

    private async Task PopulateCoursesDropdownAsync()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();
    }

    private async Task PopulateLessonsDropdownAsync(int courseId)
    {
        ViewBag.Lessons = await _context.Lessons
            .Include(l => l.Module)
            .Where(l => l.Module.CourseId == courseId)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = $"{l.Module.Title} - {l.Title}"
            })
            .ToListAsync();
    }

    #endregion
}

