using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// إدارة التذكيرات - Student Reminders Controller
/// </summary>
public class RemindersController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RemindersController> _logger;

    public RemindersController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<RemindersController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التذكيرات - Reminders list
    /// </summary>
    public async Task<IActionResult> Index(bool? completed, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.StudentReminders
            .Include(r => r.Course)
            .Include(r => r.Lesson)
            .Include(r => r.Assignment)
            .Where(r => r.StudentId == userId);

        if (completed.HasValue)
            query = query.Where(r => r.IsCompleted == completed.Value);

        var reminders = await query
            .OrderBy(r => r.RemindAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        // Get enrolled courses for the modal
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Completed = completed;
        ViewBag.Page = page;
        ViewBag.Courses = courses;

        return View(reminders);
    }

    /// <summary>
    /// التذكيرات القادمة - Upcoming reminders
    /// </summary>
    public async Task<IActionResult> Upcoming()
    {
        var userId = _currentUserService.UserId;

        var reminders = await _context.StudentReminders
            .Include(r => r.Course)
            .Include(r => r.Lesson)
            .Include(r => r.Assignment)
            .Where(r => r.StudentId == userId && 
                !r.IsCompleted && 
                r.RemindAt >= DateTime.UtcNow &&
                r.RemindAt <= DateTime.UtcNow.AddDays(7))
            .OrderBy(r => r.RemindAt)
            .ToListAsync();

        return View(reminders);
    }

    /// <summary>
    /// إنشاء تذكير جديد - Create new reminder
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _currentUserService.UserId;

        // Get enrolled courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(new ReminderViewModel());
    }

    /// <summary>
    /// حفظ التذكير الجديد - Save new reminder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReminderViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId!;

            var reminder = new StudentReminder
            {
                StudentId = userId,
                Title = model.Title,
                Description = model.Description,
                ReminderType = model.ReminderType,
                CourseId = model.CourseId,
                LessonId = model.LessonId,
                AssignmentId = model.AssignmentId,
                RemindAt = model.RemindAt,
                SendEmail = model.SendEmail,
                SendPush = model.SendPush
            };

            _context.StudentReminders.Add(reminder);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء التذكير بنجاح");
            return RedirectToAction(nameof(Index));
        }

        // Reload courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == _currentUserService.UserId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(model);
    }

    /// <summary>
    /// تعديل تذكير - Edit reminder
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var reminder = await _context.StudentReminders
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (reminder == null)
            return NotFound();

        var model = new ReminderViewModel
        {
            Id = reminder.Id,
            Title = reminder.Title,
            Description = reminder.Description,
            ReminderType = reminder.ReminderType,
            CourseId = reminder.CourseId,
            LessonId = reminder.LessonId,
            AssignmentId = reminder.AssignmentId,
            RemindAt = reminder.RemindAt,
            SendEmail = reminder.SendEmail,
            SendPush = reminder.SendPush
        };

        // Get enrolled courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات التذكير - Save reminder changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ReminderViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var reminder = await _context.StudentReminders
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

            if (reminder == null)
                return NotFound();

            reminder.Title = model.Title;
            reminder.Description = model.Description;
            reminder.ReminderType = model.ReminderType;
            reminder.CourseId = model.CourseId;
            reminder.LessonId = model.LessonId;
            reminder.AssignmentId = model.AssignmentId;
            reminder.RemindAt = model.RemindAt;
            reminder.SendEmail = model.SendEmail;
            reminder.SendPush = model.SendPush;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث التذكير بنجاح");
            return RedirectToAction(nameof(Index));
        }

        // Reload courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == _currentUserService.UserId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(model);
    }

    /// <summary>
    /// تحديد كمكتمل - Mark as completed
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var userId = _currentUserService.UserId;

        var reminder = await _context.StudentReminders
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (reminder == null)
            return NotFound();

        reminder.IsCompleted = true;
        reminder.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديد التذكير كمكتمل");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف تذكير - Delete reminder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var reminder = await _context.StudentReminders
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (reminder == null)
            return NotFound();

        _context.StudentReminders.Remove(reminder);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التذكير بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تأجيل تذكير - Snooze reminder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Snooze(int id, int minutes)
    {
        var userId = _currentUserService.UserId;

        var reminder = await _context.StudentReminders
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (reminder == null)
            return NotFound();

        reminder.RemindAt = DateTime.UtcNow.AddMinutes(minutes);
        reminder.SnoozeCount++;

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم تأجيل التذكير {minutes} دقيقة");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// الحصول على عدد التذكيرات القادمة - Get upcoming reminders count (API)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUpcomingCount()
    {
        var userId = _currentUserService.UserId;

        var count = await _context.StudentReminders
            .CountAsync(r => r.StudentId == userId && 
                !r.IsCompleted && 
                r.RemindAt >= DateTime.UtcNow &&
                r.RemindAt <= DateTime.UtcNow.AddDays(1));

        return Json(new { upcomingCount = count });
    }
}

