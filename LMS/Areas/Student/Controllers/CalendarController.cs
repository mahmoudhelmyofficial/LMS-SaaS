using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التقويم - Student Calendar Controller
/// </summary>
public class CalendarController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CalendarController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// عرض التقويم - Calendar view
    /// </summary>
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var userId = _currentUserService.UserId;

        var targetDate = new DateTime(
            year ?? DateTime.Now.Year,
            month ?? DateTime.Now.Month,
            1);

        ViewBag.CurrentMonth = targetDate;
        ViewBag.PreviousMonth = targetDate.AddMonths(-1);
        ViewBag.NextMonth = targetDate.AddMonths(1);

        // Get all events for the month
        var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var events = await _context.CalendarEvents
            .Include(e => e.Course)
            .Include(e => e.LiveClass)
            .Include(e => e.Assignment)
            .Where(e => e.UserId == userId && 
                e.StartTime >= monthStart && 
                e.StartTime <= monthEnd)
            .OrderBy(e => e.StartTime)
            .ToListAsync();

        return View(events);
    }

    /// <summary>
    /// الأحداث القادمة - Upcoming events
    /// </summary>
    public async Task<IActionResult> Upcoming()
    {
        var userId = _currentUserService.UserId;

        var events = await _context.CalendarEvents
            .Include(e => e.Course)
            .Include(e => e.LiveClass)
            .Include(e => e.Assignment)
            .Where(e => e.UserId == userId && e.StartTime >= DateTime.UtcNow)
            .OrderBy(e => e.StartTime)
            .Take(20)
            .ToListAsync();

        return View(events);
    }

    /// <summary>
    /// إضافة حدث جديد - Add new event
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

        return View(new CalendarEventViewModel 
        { 
            StartTime = DateTime.Now.AddHours(1),
            EndTime = DateTime.Now.AddHours(2)
        });
    }

    /// <summary>
    /// حفظ الحدث الجديد - Save new event
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CalendarEventViewModel model)
    {
        var userId = _currentUserService.UserId!;

        if (ModelState.IsValid)
        {
            try
            {
                // Validation: End time should be after start time
                if (model.EndTime.HasValue && model.EndTime.Value <= model.StartTime)
                {
                    ModelState.AddModelError(nameof(model.EndTime), "وقت الانتهاء يجب أن يكون بعد وقت البدء");
                    var coursesError = await _context.Enrollments
                        .Where(e => e.StudentId == userId)
                        .Include(e => e.Course)
                        .Select(e => e.Course)
                        .ToListAsync();
                    ViewBag.Courses = coursesError;
                    return View(model);
                }

                var calendarEvent = new CalendarEvent
                {
                    UserId = userId,
                    Title = model.Title,
                    Description = model.Description,
                    EventType = model.EventType,
                    StartTime = model.StartTime,
                    EndTime = model.EndTime ?? model.StartTime.AddHours(1),
                    CourseId = model.CourseId,
                    Location = model.Location,
                    IsAllDay = model.IsAllDay,
                    Color = model.Color,
                    SendReminder = model.SendReminder,
                    ReminderMinutesBefore = model.ReminderMinutesBefore
                };

                _context.CalendarEvents.Add(calendarEvent);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Calendar event {EventId} created by user {UserId}", 
                    calendarEvent.Id, userId);

                SetSuccessMessage("تم إضافة الحدث بنجاح");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar event for user {UserId}", userId);
                SetErrorMessage("حدث خطأ أثناء إضافة الحدث");
            }
        }

        // Reload courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(model);
    }

    /// <summary>
    /// تفاصيل الحدث - Event details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var calendarEvent = await _context.CalendarEvents
            .Include(e => e.Course)
            .Include(e => e.LiveClass)
            .Include(e => e.Assignment)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (calendarEvent == null)
        {
            SetErrorMessage("الحدث غير موجود");
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new CalendarEventViewModel
        {
            Id = calendarEvent.Id,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            EventType = calendarEvent.EventType,
            StartTime = calendarEvent.StartTime,
            EndTime = calendarEvent.EndTime,
            CourseId = calendarEvent.CourseId,
            Location = calendarEvent.Location,
            IsAllDay = calendarEvent.IsAllDay,
            Color = calendarEvent.Color,
            SendReminder = calendarEvent.SendReminder,
            ReminderMinutesBefore = calendarEvent.ReminderMinutesBefore
        };

        ViewBag.Event = calendarEvent;
        return View(viewModel);
    }

    /// <summary>
    /// تعديل حدث - Edit event
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var calendarEvent = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (calendarEvent == null)
        {
            SetErrorMessage("الحدث غير موجود");
            return RedirectToAction(nameof(Index));
        }

        var model = new CalendarEventViewModel
        {
            Id = calendarEvent.Id,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            EventType = calendarEvent.EventType,
            StartTime = calendarEvent.StartTime,
            EndTime = calendarEvent.EndTime,
            CourseId = calendarEvent.CourseId,
            Location = calendarEvent.Location,
            IsAllDay = calendarEvent.IsAllDay,
            Color = calendarEvent.Color,
            SendReminder = calendarEvent.SendReminder,
            ReminderMinutesBefore = calendarEvent.ReminderMinutesBefore
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
    /// حفظ تعديلات الحدث - Save event changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CalendarEventViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        if (ModelState.IsValid)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

                if (calendarEvent == null)
                {
                    SetErrorMessage("الحدث غير موجود");
                    return RedirectToAction(nameof(Index));
                }

                // Validation: End time should be after start time
                if (model.EndTime.HasValue && model.EndTime.Value <= model.StartTime)
                {
                    ModelState.AddModelError(nameof(model.EndTime), "وقت الانتهاء يجب أن يكون بعد وقت البدء");
                    var coursesError = await _context.Enrollments
                        .Where(e => e.StudentId == userId)
                        .Include(e => e.Course)
                        .Select(e => e.Course)
                        .ToListAsync();
                    ViewBag.Courses = coursesError;
                    return View(model);
                }

                calendarEvent.Title = model.Title;
                calendarEvent.Description = model.Description;
                calendarEvent.EventType = model.EventType;
                calendarEvent.StartTime = model.StartTime;
                calendarEvent.EndTime = model.EndTime ?? model.StartTime.AddHours(1);
                calendarEvent.CourseId = model.CourseId;
                calendarEvent.Location = model.Location;
                calendarEvent.IsAllDay = model.IsAllDay;
                calendarEvent.Color = model.Color;
                calendarEvent.SendReminder = model.SendReminder;
                calendarEvent.ReminderMinutesBefore = model.ReminderMinutesBefore;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Calendar event {EventId} updated by user {UserId}", id, userId);

                SetSuccessMessage("تم تحديث الحدث بنجاح");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating calendar event {EventId} for user {UserId}", id, userId);
                SetErrorMessage("حدث خطأ أثناء تحديث الحدث");
            }
        }

        // Reload courses
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = courses;

        return View(model);
    }

    /// <summary>
    /// حذف حدث - Delete event
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var calendarEvent = await _context.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (calendarEvent == null)
            {
                SetErrorMessage("الحدث غير موجود");
                return RedirectToAction(nameof(Index));
            }

            _context.CalendarEvents.Remove(calendarEvent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Calendar event {EventId} deleted by user {UserId}", id, userId);

            SetSuccessMessage("تم حذف الحدث بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting calendar event {EventId} for user {UserId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف الحدث");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الحصول على الأحداث للتقويم (API JSON) - Get events for calendar (JSON API)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents(DateTime? start, DateTime? end)
    {
        try
        {
            var userId = _currentUserService.UserId;

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new List<object>());
            }

            // Default to current month if dates not provided
            var startDate = start ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = end ?? startDate.AddMonths(1).AddDays(-1);

            _logger.LogInformation("Loading calendar events for user {UserId} from {Start} to {End}", 
                userId, startDate, endDate);

            var events = await _context.CalendarEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId && 
                    e.StartTime >= startDate && 
                    e.StartTime <= endDate)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    start = e.StartTime,
                    end = e.EndTime,
                    allDay = e.IsAllDay,
                    color = e.Color ?? "#667eea",
                    eventType = e.EventType ?? "custom"
                })
                .ToListAsync();

            _logger.LogInformation("Loaded {Count} events for user {UserId}", events.Count, userId);

            return Json(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar events");
            return Json(new List<object>());
        }
    }
}

