using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// ملاحظات الطالب - Student Notes Controller
/// </summary>
public class NotesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotesController> _logger;

    public NotesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<NotesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// ملاحظات الدورة - Course Notes (by enrollment ID)
    /// </summary>
    public async Task<IActionResult> Course(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Get enrollment to verify access and get course info
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

            if (enrollment == null)
            {
                _logger.LogWarning("Enrollment not found for notes: {EnrollmentId}", enrollmentId);
                return NotFound();
            }

            // Get all lesson IDs for this course
            var lessonIds = enrollment.Course.Modules
                .SelectMany(m => m.Lessons.Select(l => l.Id))
                .ToList();

            // Get all notes for these lessons
            var notes = await _context.StudentNotes
                .Include(n => n.Lesson)
                    .ThenInclude(l => l.Module)
                .Where(n => n.StudentId == userId && lessonIds.Contains(n.LessonId))
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Enrollment = enrollment;
            ViewBag.Course = enrollment.Course;
            ViewBag.TotalNotes = notes.Count;
            ViewBag.PinnedNotes = notes.Count(n => n.IsPinned);

            return View(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course notes for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage("حدث خطأ أثناء تحميل الملاحظات");
            return RedirectToAction("Details", "Courses", new { id = enrollmentId });
        }
    }

    /// <summary>
    /// قائمة ملاحظات الطالب - Student Notes List
    /// </summary>
    public async Task<IActionResult> Index(int? lessonId, int? courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var query = _context.StudentNotes
            .Include(n => n.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(n => n.StudentId == userId)
            .AsQueryable();

        if (lessonId.HasValue)
        {
            query = query.Where(n => n.LessonId == lessonId.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(n => n.Lesson.Module.CourseId == courseId.Value);
        }

        var notes = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        ViewBag.LessonId = lessonId;
        ViewBag.CourseId = courseId;

        return View(notes);
    }

    /// <summary>
    /// إنشاء ملاحظة جديدة - Create new note
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int lessonId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Verify student is enrolled in the course
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
            return NotFound();

        var enrollment = await _context.Enrollments
            .AnyAsync(e => e.StudentId == userId && e.CourseId == lesson.Module.CourseId);

        if (!enrollment)
        {
            SetErrorMessage("يجب أن تكون مسجلاً في الدورة لإنشاء ملاحظات");
            return RedirectToAction("Index", "Courses");
        }

        var viewModel = new StudentNoteCreateViewModel
        {
            LessonId = lessonId,
            LessonTitle = lesson.Title,
            CourseTitle = lesson.Module.Course.Title
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ الملاحظة الجديدة - Save new note
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentNoteCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (ModelState.IsValid)
        {
            var note = new StudentNote
            {
                StudentId = userId!,
                LessonId = model.LessonId,
                Content = model.Content,
                VideoTimestamp = model.VideoTimestamp,
                HighlightColor = model.HighlightColor,
                IsPinned = model.IsPinned
            };

            _context.StudentNotes.Add(note);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة الملاحظة بنجاح");
            return RedirectToAction(nameof(Index), new { lessonId = model.LessonId });
        }

        return View(model);
    }

    /// <summary>
    /// تعديل الملاحظة - Edit note
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var note = await _context.StudentNotes
            .Include(n => n.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(n => n.Id == id && n.StudentId == userId);

        if (note == null)
            return NotFound();

        var viewModel = new StudentNoteEditViewModel
        {
            Id = note.Id,
            LessonId = note.LessonId,
            Content = note.Content,
            VideoTimestamp = note.VideoTimestamp,
            HighlightColor = note.HighlightColor,
            IsPinned = note.IsPinned,
            LessonTitle = note.Lesson.Title,
            CourseTitle = note.Lesson.Module.Course.Title
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الملاحظة - Save note edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentNoteEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var note = await _context.StudentNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.StudentId == userId);

        if (note == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            note.Content = model.Content;
            note.VideoTimestamp = model.VideoTimestamp;
            note.HighlightColor = model.HighlightColor;
            note.IsPinned = model.IsPinned;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الملاحظة بنجاح");
            return RedirectToAction(nameof(Index), new { lessonId = note.LessonId });
        }

        return View(model);
    }

    /// <summary>
    /// تبديل حالة التثبيت - Toggle pin status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id, int? enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var note = await _context.StudentNotes
            .Include(n => n.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(n => n.Id == id && n.StudentId == userId);

        if (note == null)
            return NotFound();

        note.IsPinned = !note.IsPinned;
        await _context.SaveChangesAsync();

        // Determine redirect target
        if (enrollmentId.HasValue)
        {
            SetSuccessMessage(note.IsPinned ? "تم تثبيت الملاحظة بنجاح" : "تم إلغاء تثبيت الملاحظة بنجاح");
            return RedirectToAction(nameof(Course), new { enrollmentId = enrollmentId.Value });
        }

        // Try to get enrollmentId from note
        if (note.EnrollmentId.HasValue)
        {
            SetSuccessMessage(note.IsPinned ? "تم تثبيت الملاحظة بنجاح" : "تم إلغاء تثبيت الملاحظة بنجاح");
            return RedirectToAction(nameof(Course), new { enrollmentId = note.EnrollmentId.Value });
        }

        // Fallback to Index
        SetSuccessMessage(note.IsPinned ? "تم تثبيت الملاحظة بنجاح" : "تم إلغاء تثبيت الملاحظة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الملاحظة - Delete note
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int? enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var note = await _context.StudentNotes
            .FirstOrDefaultAsync(n => n.Id == id && n.StudentId == userId);

        if (note == null)
            return NotFound();

        var noteEnrollmentId = note.EnrollmentId ?? enrollmentId;

        _context.StudentNotes.Remove(note);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الملاحظة بنجاح");
        
        if (noteEnrollmentId.HasValue)
        {
            return RedirectToAction(nameof(Course), new { enrollmentId = noteEnrollmentId.Value });
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إضافة ملاحظة عبر API - Add note via API (for lesson page)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote([FromBody] AddNoteRequest request)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Json(new { success = false, message = "محتوى الملاحظة مطلوب" });
        }

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == request.LessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "يجب أن تكون مسجلاً في الدورة" });
            }

            var note = new StudentNote
            {
                StudentId = userId,
                LessonId = request.LessonId,
                EnrollmentId = enrollment.Id,
                Content = request.Content,
                VideoTimestamp = request.Timestamp,
                IsPinned = false
            };

            _context.StudentNotes.Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} added note to lesson {LessonId}", userId, request.LessonId);

            return Json(new { success = true, noteId = note.Id, message = "تم حفظ الملاحظة بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding note for lesson {LessonId} by student {StudentId}", request.LessonId, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء حفظ الملاحظة" });
        }
    }

    /// <summary>
    /// جلب ملاحظات الدرس - Get lesson notes via API
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotes(int lessonId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        try
        {
            var notes = await _context.StudentNotes
                .Where(n => n.StudentId == userId && n.LessonId == lessonId)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Content,
                    n.VideoTimestamp,
                    n.IsPinned,
                    CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, notes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notes for lesson {LessonId} by student {StudentId}", lessonId, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء جلب الملاحظات" });
        }
    }
}

/// <summary>
/// طلب إضافة ملاحظة - Add note request model
/// </summary>
public class AddNoteRequest
{
    public int LessonId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? Timestamp { get; set; }
}

