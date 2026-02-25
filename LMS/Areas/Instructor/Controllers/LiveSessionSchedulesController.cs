using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.LiveSessions;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة جداول الحصص - Live Session Schedules Controller
/// </summary>
public class LiveSessionSchedulesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveSessionSchedulesController> _logger;

    public LiveSessionSchedulesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILiveSessionService liveSessionService,
        ILogger<LiveSessionSchedulesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة جداول الحصص - Schedules list
    /// </summary>
    public async Task<IActionResult> Index(LiveScheduleStatus? status = null)
    {
        var userId = _currentUserService.UserId;
        var schedules = await _liveSessionService.GetInstructorSchedulesAsync(userId!, status);

        var viewModels = schedules.Select(s => new LiveSessionScheduleDisplayViewModel
        {
            Id = s.Id,
            Title = s.Title,
            TitleAr = s.TitleAr,
            Description = s.Description,
            Price = s.Price,
            OriginalPrice = s.OriginalPrice,
            Currency = s.Currency,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            MaxStudents = s.MaxStudents,
            Status = s.Status,
            TotalSessions = s.TotalSessions,
            EnrolledCount = s.EnrolledCount,
            TotalRevenue = s.TotalRevenue,
            ThumbnailUrl = s.ThumbnailUrl,
            CreatedAt = s.CreatedAt
        }).ToList();

        ViewBag.CurrentStatus = status;
        return View(viewModels);
    }

    /// <summary>
    /// إنشاء جدول حصص جديد - Create new schedule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _currentUserService.UserId;
        await PopulateCoursesAsync(userId);
        return View(new LiveSessionScheduleCreateViewModel
        {
            StartDate = DateTime.Now.AddDays(1),
            EndDate = DateTime.Now.AddMonths(1),
            Sessions = new List<ScheduleSessionItemViewModel>
            {
                new() { ScheduleOrder = 1, ScheduledStartTime = DateTime.Now.AddDays(1), ScheduledEndTime = DateTime.Now.AddDays(1).AddHours(1) }
            }
        });
    }

    /// <summary>
    /// حفظ جدول الحصص الجديد - Save new schedule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LiveSessionScheduleCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync(userId);
            return View(model);
        }

        // Ensure sessions collection was bound and has at least one session
        if (model.Sessions == null || model.Sessions.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "يجب إضافة جلسة واحدة على الأقل لجدول الحصص");
            await PopulateCoursesAsync(userId);
            return View(model);
        }

        // Validate CourseId first (LiveClass has required CourseId FK; must select a course)
        if (!model.CourseId.HasValue || model.CourseId.Value == 0)
        {
            ModelState.AddModelError(nameof(model.CourseId), "يجب تحديد الدورة المرتبطة عند إضافة جلسات");
            await PopulateCoursesAsync(userId);
            return View(model);
        }

        // Ensure the course belongs to the current instructor
        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == model.CourseId.Value && c.InstructorId == userId && !c.IsDeleted);
        if (!courseExists)
        {
            ModelState.AddModelError(nameof(model.CourseId), "الدورة المحددة غير صالحة أو لا تخصك");
            await PopulateCoursesAsync(userId);
            return View(model);
        }

        try
        {
            var schedule = new LiveSessionSchedule
            {
                InstructorId = userId!,
                CourseId = model.CourseId,
                Title = model.Title?.Trim() ?? string.Empty,
                TitleAr = model.TitleAr?.Trim(),
                Description = model.Description?.Trim(),
                Price = model.Price,
                Currency = "EGP",
                OriginalPrice = model.OriginalPrice,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxStudents = model.MaxStudents,
                ThumbnailUrl = model.ThumbnailUrl?.Trim(),
                Status = LiveScheduleStatus.Draft,
                TotalSessions = model.Sessions.Count
            };

            var courseId = model.CourseId.Value;
            var order = 1;
            foreach (var sessionVm in model.Sessions)
            {
                // Ensure end time is valid (hidden field may be empty if JS didn't run)
                var start = sessionVm.ScheduledStartTime;
                var duration = sessionVm.DurationMinutes > 0 ? sessionVm.DurationMinutes : 60;
                var end = sessionVm.ScheduledEndTime;
                if (end == default || end <= start)
                    end = start.AddMinutes(duration);

                var title = string.IsNullOrWhiteSpace(sessionVm.Title) ? $"جلسة {order}" : sessionVm.Title.Trim();
                if (title.Length > 300) title = title.Substring(0, 300);

                schedule.Sessions.Add(new LiveClass
                {
                    InstructorId = userId!,
                    CourseId = courseId,
                    Title = title,
                    Subject = sessionVm.Subject?.Trim(),
                    ScheduledStartTime = start,
                    ScheduledEndTime = end,
                    DurationMinutes = duration,
                    Platform = string.IsNullOrWhiteSpace(sessionVm.Platform) ? "Zoom" : sessionVm.Platform.Trim(),
                    MeetingUrl = sessionVm.MeetingUrl?.Trim() ?? string.Empty,
                    MeetingId = sessionVm.MeetingId?.Trim(),
                    Password = sessionVm.Password?.Trim(),
                    ScheduleOrder = sessionVm.ScheduleOrder > 0 ? sessionVm.ScheduleOrder : order,
                    PricingType = LiveSessionPricingType.SubscriptionOnly,
                    Price = 0,
                    PriceCurrency = "EGP",
                    TimeZone = "Africa/Cairo",
                    Status = LiveClassStatus.Scheduled
                });
                order++;
            }

            var created = await _liveSessionService.CreateScheduleAsync(schedule, userId!);

            _logger.LogInformation(
                "Schedule {ScheduleId} '{Title}' created by instructor {InstructorId} with {SessionCount} sessions",
                created.Id, created.Title, userId, model.Sessions.Count);

            SetSuccessMessage("تم إنشاء جدول الحصص بنجاح");
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session schedule for instructor {InstructorId}. Message: {Message}, Inner: {Inner}",
                userId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ أثناء إنشاء جدول الحصص");
#if DEBUG
            ViewBag.ErrorDetail = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
#endif
            await PopulateCoursesAsync(userId);
            return View(model);
        }
    }

    private async Task PopulateCoursesAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        var courses = await _context.Courses
            .Where(c => c.InstructorId == userId && !c.IsDeleted)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();
        ViewBag.Courses = new SelectList(courses, "Id", "Title");
    }

    /// <summary>
    /// تفاصيل جدول الحصص - Schedule details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        var schedule = await _liveSessionService.GetScheduleWithSessionsAsync(id);

        if (schedule == null || schedule.InstructorId != userId)
        {
            SetErrorMessage("جدول الحصص غير موجود");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.EnrollmentCount = await _liveSessionService.GetScheduleEnrollmentCountAsync(id);
        ViewBag.Revenue = await _liveSessionService.GetScheduleRevenueAsync(id);
        return View(schedule);
    }

    /// <summary>
    /// تعديل جدول الحصص - Edit schedule
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        var schedule = await _liveSessionService.GetScheduleWithSessionsAsync(id);

        if (schedule == null || schedule.InstructorId != userId)
        {
            SetErrorMessage("جدول الحصص غير موجود");
            return RedirectToAction(nameof(Index));
        }

        if (schedule.Status == LiveScheduleStatus.Completed || schedule.Status == LiveScheduleStatus.Cancelled)
        {
            SetWarningMessage("لا يمكن تعديل جدول حصص مكتمل أو ملغي");
            return RedirectToAction(nameof(Details), new { id });
        }

        var courses = await _context.Courses
            .Where(c => c.InstructorId == userId && !c.IsDeleted)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();
        ViewBag.Courses = new SelectList(courses, "Id", "Title", schedule.CourseId);

        var enrollmentCount = await _liveSessionService.GetScheduleEnrollmentCountAsync(id);
        ViewBag.HasEnrollments = enrollmentCount > 0;

        var vm = new LiveSessionScheduleEditViewModel
        {
            Id = schedule.Id,
            Title = schedule.Title,
            TitleAr = schedule.TitleAr,
            Description = schedule.Description,
            Price = schedule.Price,
            OriginalPrice = schedule.OriginalPrice,
            CourseId = schedule.CourseId,
            StartDate = schedule.StartDate,
            EndDate = schedule.EndDate,
            MaxStudents = schedule.MaxStudents,
            ThumbnailUrl = schedule.ThumbnailUrl,
            Status = schedule.Status,
            EnrolledCount = schedule.EnrolledCount,
            TotalRevenue = schedule.TotalRevenue,
            Sessions = schedule.Sessions.OrderBy(s => s.ScheduleOrder).Select(s => new ScheduleSessionItemViewModel
            {
                Title = s.Title,
                Subject = s.Subject,
                ScheduledStartTime = s.ScheduledStartTime,
                ScheduledEndTime = s.ScheduledEndTime,
                DurationMinutes = s.DurationMinutes,
                Platform = s.Platform,
                MeetingUrl = s.MeetingUrl,
                MeetingId = s.MeetingId,
                Password = s.Password,
                ScheduleOrder = s.ScheduleOrder
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>
    /// حفظ تعديلات جدول الحصص - Save schedule edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LiveSessionScheduleEditViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            var courses = await _context.Courses
                .Where(c => c.InstructorId == userId && !c.IsDeleted)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();
            ViewBag.Courses = new SelectList(courses, "Id", "Title", model.CourseId);
            return View(model);
        }

        try
        {
            var schedule = await _liveSessionService.GetScheduleWithSessionsAsync(id);
            if (schedule == null || schedule.InstructorId != userId)
            {
                SetErrorMessage("جدول الحصص غير موجود");
                return RedirectToAction(nameof(Index));
            }

            schedule.Title = model.Title;
            schedule.TitleAr = model.TitleAr;
            schedule.Description = model.Description;
            schedule.Price = model.Price;
            schedule.OriginalPrice = model.OriginalPrice;
            schedule.CourseId = model.CourseId;
            schedule.StartDate = model.StartDate;
            schedule.EndDate = model.EndDate;
            schedule.MaxStudents = model.MaxStudents;
            schedule.ThumbnailUrl = model.ThumbnailUrl;

            await _liveSessionService.UpdateScheduleAsync(schedule, userId!);

            _logger.LogInformation(
                "Schedule {ScheduleId} '{Title}' updated by instructor {InstructorId}",
                id, schedule.Title, userId);

            SetSuccessMessage("تم تحديث جدول الحصص بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث جدول الحصص");
            return View(model);
        }
    }

    /// <summary>
    /// نشر جدول الحصص - Publish schedule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _liveSessionService.PublishScheduleAsync(id, userId!);
        if (result)
            SetSuccessMessage("تم نشر جدول الحصص بنجاح");
        else
            SetErrorMessage("فشل نشر جدول الحصص");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إلغاء جدول الحصص - Cancel schedule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var userId = _currentUserService.UserId;
        var result = await _liveSessionService.CancelScheduleAsync(id, userId!, reason ?? "تم الإلغاء بواسطة المدرس");
        if (result)
            SetSuccessMessage("تم إلغاء جدول الحصص");
        else
            SetErrorMessage("فشل إلغاء جدول الحصص");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف جدول الحصص - Delete schedule
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _liveSessionService.DeleteScheduleAsync(id, userId!);
        if (result)
            SetSuccessMessage("تم حذف جدول الحصص");
        else
            SetErrorMessage("لا يمكن حذف جدول حصص يحتوي على اشتراكات");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// المسجلون في الجدول - Schedule enrollments
    /// </summary>
    public async Task<IActionResult> Enrollments(int id)
    {
        var userId = _currentUserService.UserId;
        var schedule = await _context.LiveSessionSchedules
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == userId);

        if (schedule == null)
        {
            SetErrorMessage("جدول الحصص غير موجود");
            return RedirectToAction(nameof(Index));
        }

        return View(schedule);
    }

    /// <summary>
    /// تقرير الحضور - Attendance report
    /// </summary>
    public async Task<IActionResult> AttendanceReport(int id)
    {
        var userId = _currentUserService.UserId;
        var schedule = await _context.LiveSessionSchedules
            .Include(s => s.Sessions.OrderBy(ss => ss.ScheduleOrder))
                .ThenInclude(ss => ss.Attendances)
                    .ThenInclude(a => a.Student)
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == userId);

        if (schedule == null)
        {
            SetErrorMessage("جدول الحصص غير موجود");
            return RedirectToAction(nameof(Index));
        }

        var report = new ScheduleAttendanceReportViewModel
        {
            ScheduleId = schedule.Id,
            ScheduleTitle = schedule.Title,
            Sessions = schedule.Sessions.Select(s => new SessionAttendanceColumn
            {
                SessionId = s.Id,
                Title = s.Title,
                Subject = s.Subject,
                ScheduledDate = s.ScheduledStartTime
            }).ToList()
        };

        // Build student rows
        var allStudents = schedule.Enrollments
            .Where(e => e.Status == ScheduleEnrollmentStatus.Active)
            .Select(e => e.Student)
            .Distinct()
            .ToList();

        foreach (var student in allStudents)
        {
            var row = new StudentAttendanceRow
            {
                StudentId = student.Id,
                StudentName = student.FullName ?? student.UserName ?? "غير معروف",
                StudentEmail = student.Email
            };

            foreach (var session in schedule.Sessions)
            {
                var attendance = session.Attendances.FirstOrDefault(a => a.StudentId == student.Id);
                row.SessionAttendance[session.Id] = attendance?.AttendanceStatusDetail ?? AttendanceStatus.Absent;
                row.SessionScores[session.Id] = attendance?.AttendanceScore ?? 0;
            }

            row.TotalPresent = row.SessionAttendance.Count(sa => sa.Value == AttendanceStatus.Present);
            row.TotalLate = row.SessionAttendance.Count(sa => sa.Value == AttendanceStatus.Late);
            row.TotalAbsent = row.SessionAttendance.Count(sa => sa.Value == AttendanceStatus.Absent || sa.Value == AttendanceStatus.Registered);
            row.AverageScore = row.SessionScores.Any() ? row.SessionScores.Values.Average() : 0;

            report.Students.Add(row);
        }

        return View(report);
    }
}
