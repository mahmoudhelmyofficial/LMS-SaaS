using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الإشارات المرجعية - Bookmarks Controller
/// </summary>
public class BookmarksController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BookmarksController> _logger;

    public BookmarksController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<BookmarksController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الإشارات المرجعية - Bookmarks List
    /// Supports filtering by type, courseId, or enrollmentId
    /// </summary>
    public async Task<IActionResult> Index(string? type, int? courseId, int? enrollmentId)
    {
        var userId = _currentUserService.UserId;

        // If enrollmentId is provided, get the courseId from enrollment
        if (enrollmentId.HasValue && !courseId.HasValue)
        {
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.Id == enrollmentId.Value && e.StudentId == userId);
            
            if (enrollment != null)
            {
                courseId = enrollment.CourseId;
            }
        }

        var query = _context.Bookmarks
            .Include(b => b.Course)
            .Include(b => b.Lesson)
                .ThenInclude(l => l!.Module)
                    .ThenInclude(m => m.Course)
            .Where(b => b.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(b => b.BookmarkType == type);
        }

        // Filter by courseId - include both course bookmarks and lesson bookmarks for that course
        if (courseId.HasValue)
        {
            query = query.Where(b => 
                b.CourseId == courseId.Value || 
                (b.Lesson != null && b.Lesson.Module.CourseId == courseId.Value));
        }

        var bookmarks = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.CourseId = courseId;
        ViewBag.EnrollmentId = enrollmentId;

        // Get course info for display if filtering by course
        if (courseId.HasValue)
        {
            ViewBag.Course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courseId.Value);
        }

        return View(bookmarks);
    }

    /// <summary>
    /// إضافة إشارة مرجعية - Add Bookmark
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(BookmarkCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (ModelState.IsValid)
        {
            // Check if bookmark already exists
            var exists = await _context.Bookmarks.AnyAsync(b =>
                b.UserId == userId &&
                b.BookmarkType == model.BookmarkType &&
                (model.BookmarkType == "Course" ? b.CourseId == model.CourseId : b.LessonId == model.LessonId));

            if (exists)
            {
                SetErrorMessage("الإشارة المرجعية موجودة بالفعل");
                return RedirectToAction(nameof(Index));
            }

            var bookmark = new Bookmark
            {
                UserId = userId!,
                BookmarkType = model.BookmarkType,
                CourseId = model.CourseId,
                LessonId = model.LessonId,
                Note = model.Note
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة الإشارة المرجعية بنجاح");
            return RedirectToAction(nameof(Index));
        }

        SetErrorMessage("حدث خطأ أثناء إضافة الإشارة المرجعية");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تعديل ملاحظة الإشارة المرجعية - Edit Bookmark Note
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditNote(int id)
    {
        var userId = _currentUserService.UserId;

        var bookmark = await _context.Bookmarks
            .Include(b => b.Course)
            .Include(b => b.Lesson)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (bookmark == null)
            return NotFound();

        var viewModel = new BookmarkEditViewModel
        {
            Id = bookmark.Id,
            Note = bookmark.Note,
            Title = bookmark.BookmarkType == "Course" 
                ? bookmark.Course?.Title 
                : bookmark.Lesson?.Title
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ ملاحظة الإشارة المرجعية - Save Bookmark Note
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNote(int id, BookmarkEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (bookmark == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            bookmark.Note = model.Note;
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الملاحظة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// حذف الإشارة المرجعية - Delete Bookmark
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (bookmark == null)
            return NotFound();

        _context.Bookmarks.Remove(bookmark);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الإشارة المرجعية بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// التحقق من وجود إشارة مرجعية - Check if bookmark exists (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckExists(string type, int? courseId, int? lessonId)
    {
        var userId = _currentUserService.UserId;

        var exists = await _context.Bookmarks.AnyAsync(b =>
            b.UserId == userId &&
            b.BookmarkType == type &&
            (type == "Course" ? b.CourseId == courseId : b.LessonId == lessonId));

        return Ok(new { exists });
    }
}

