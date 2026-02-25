using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة البث المباشر - Live Classes Management Controller
/// </summary>
public class LiveClassesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassesController> _logger;

    public LiveClassesController(
        ApplicationDbContext context,
        INotificationService notificationService,
        IEmailService emailService,
        ILiveSessionService liveSessionService,
        ILogger<LiveClassesController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _emailService = emailService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة البث المباشر - Live Classes List
    /// </summary>
    public async Task<IActionResult> Index(string? status, int? courseId, DateTime? fromDate)
    {
        var query = _context.LiveClasses
            .Include(lc => lc.Course)
                .ThenInclude(c => c.Instructor)
            .Include(lc => lc.Instructor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<LMS.Domain.Enums.LiveClassStatus>(status, out var statusEnum))
            {
                query = query.Where(lc => lc.Status == statusEnum);
            }
        }

        if (courseId.HasValue)
        {
            query = query.Where(lc => lc.CourseId == courseId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(lc => lc.ScheduledStartTime >= fromDate.Value);
        }

        var liveClasses = await query
            .OrderByDescending(lc => lc.ScheduledStartTime)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.CourseId = courseId;
        ViewBag.FromDate = fromDate;

        return View(liveClasses);
    }

    /// <summary>
    /// إنشاء بث مباشر - Create Live Class (Admin redirects to course management)
    /// Live classes are typically created by instructors within their courses
    /// </summary>
    [HttpGet]
    public IActionResult Create(int? courseId = null)
    {
        // If courseId is provided, redirect to the course details where instructor can create live classes
        if (courseId.HasValue)
        {
            return RedirectToAction("Details", "Courses", new { id = courseId.Value });
        }
        
        // Otherwise show the schedule page where admin can see all scheduled classes
        SetWarningMessage(CultureExtensions.T("جلسات البث المباشر يتم إنشاؤها من قبل المدربين داخل الدورات. يمكنك مراجعة الجدول الزمني هنا.", "Live sessions are created by instructors within courses. You can review the schedule here."));
        return RedirectToAction(nameof(Schedule));
    }

    /// <summary>
    /// تفاصيل البث المباشر - Live Class Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Course)
                .ThenInclude(c => c.Instructor)
            .Include(lc => lc.Instructor)
            .Include(lc => lc.Lesson)
            .Include(lc => lc.Attendances)
                .ThenInclude(a => a.Student)
            .Include(lc => lc.Recordings)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (liveClass == null)
            return NotFound();

        return View(liveClass);
    }

    /// <summary>
    /// تعديل البث المباشر - Edit Live Class (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Course)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (liveClass == null)
            return NotFound();

        // Cannot edit completed or cancelled classes
        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Completed ||
            liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Cancelled)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن تعديل حصة مكتملة أو ملغية", "Cannot edit a completed or cancelled session."));
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Courses = await _context.Courses
            .Where(c => c.Status == LMS.Domain.Enums.CourseStatus.Published)
            .OrderBy(c => c.Title)
            .ToListAsync();

        return View(liveClass);
    }

    /// <summary>
    /// تعديل البث المباشر - Edit Live Class (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string title, string? description, DateTime scheduledStartTime, DateTime scheduledEndTime, string? meetingUrl)
    {
        var liveClass = await _context.LiveClasses.FindAsync(id);
        if (liveClass == null)
            return NotFound();

        // Cannot edit completed or cancelled classes
        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Completed ||
            liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Cancelled)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن تعديل حصة مكتملة أو ملغية", "Cannot edit a completed or cancelled session."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Cannot edit live classes that are currently live
        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Live)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن تعديل حصة جارية الآن", "Cannot edit a session that is currently live."));
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            SetErrorMessage(CultureExtensions.T("عنوان الحصة مطلوب", "Session title is required."));
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (scheduledEndTime <= scheduledStartTime)
        {
            SetErrorMessage(CultureExtensions.T("وقت الانتهاء يجب أن يكون بعد وقت البدء", "End time must be after start time."));
            return RedirectToAction(nameof(Edit), new { id });
        }

        liveClass.Title = title;
        liveClass.Description = description;
        liveClass.ScheduledStartTime = scheduledStartTime;
        liveClass.ScheduledEndTime = scheduledEndTime;
        liveClass.DurationMinutes = (int)(scheduledEndTime - scheduledStartTime).TotalMinutes;
        
        if (!string.IsNullOrWhiteSpace(meetingUrl))
        {
            liveClass.MeetingUrl = meetingUrl;
        }

        liveClass.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Live class {LiveClassId} updated by admin", id);
        SetSuccessMessage(CultureExtensions.T("تم تحديث الحصة المباشرة بنجاح", "Live session updated successfully."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف البث المباشر - Delete Live Class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Attendances)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (liveClass == null)
            return NotFound();

        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Live)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن حذف جلسة بث مباشر جارية", "Cannot delete a live session that is in progress."));
            return RedirectToAction(nameof(Details), new { id });
        }

        var courseId = liveClass.CourseId;

        _context.LiveClasses.Remove(liveClass);
        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم حذف جلسة البث المباشر بنجاح", "Live session deleted successfully."));
        return RedirectToAction(nameof(Index), new { courseId });
    }

    /// <summary>
    /// البث المباشر النشط حالياً - Currently Active Live Classes
    /// </summary>
    public async Task<IActionResult> Active()
    {
        var activeLiveClasses = await _context.LiveClasses
            .Include(lc => lc.Course)
                .ThenInclude(c => c.Instructor)
            .Where(lc => lc.Status == LMS.Domain.Enums.LiveClassStatus.Live)
            .OrderBy(lc => lc.ActualStartTime)
            .ToListAsync();

        ViewBag.TotalActive = activeLiveClasses.Count;

        return View(activeLiveClasses);
    }

    /// <summary>
    /// جدول البث المباشر - Live Classes Schedule
    /// </summary>
    public async Task<IActionResult> Schedule(DateTime? fromDate, DateTime? toDate)
    {
        fromDate ??= DateTime.UtcNow.Date;
        toDate ??= DateTime.UtcNow.Date.AddDays(30);

        var scheduledClasses = await _context.LiveClasses
            .Include(lc => lc.Course)
                .ThenInclude(c => c.Instructor)
            .Where(lc => lc.ScheduledStartTime >= fromDate && lc.ScheduledStartTime <= toDate)
            .Where(lc => lc.Status == LMS.Domain.Enums.LiveClassStatus.Scheduled)
            .OrderBy(lc => lc.ScheduledStartTime)
            .ToListAsync();

        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.TotalScheduled = scheduledClasses.Count;

        // Group by date for calendar view
        ViewBag.ClassesByDate = scheduledClasses
            .GroupBy(lc => lc.ScheduledStartTime.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(scheduledClasses);
    }

    /// <summary>
    /// إحصائيات البث المباشر - Live Classes Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var total = await _context.LiveClasses.CountAsync();
        var avgAttendance = total > 0 
            ? await _context.LiveClasses.AverageAsync(lc => (double)lc.AttendedCount) 
            : 0;

        var stats = new LiveClassStatisticsViewModel
        {
            TotalLiveClasses = total,
            ScheduledCount = await _context.LiveClasses.CountAsync(lc => lc.Status == LMS.Domain.Enums.LiveClassStatus.Scheduled),
            CompletedCount = await _context.LiveClasses.CountAsync(lc => lc.Status == LMS.Domain.Enums.LiveClassStatus.Completed),
            CancelledCount = await _context.LiveClasses.CountAsync(lc => lc.Status == LMS.Domain.Enums.LiveClassStatus.Cancelled),
            AvgAttendance = avgAttendance,
            RecordedCount = await _context.LiveClasses.CountAsync(lc => lc.IsRecorded),
            TotalAttendances = await _context.LiveClassAttendances.CountAsync()
        };

        return View(stats);
    }

    /// <summary>
    /// بدء الحصة الآن - Start live class immediately
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartNow(int id)
    {
        var liveClass = await _context.LiveClasses.FindAsync(id);
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Scheduled)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن بدء هذه الحصة", "Cannot start this session."));
            return RedirectToAction(nameof(Details), new { id });
        }

        liveClass.Status = LMS.Domain.Enums.LiveClassStatus.Live;
        liveClass.ActualStartTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم بدء الحصة المباشرة", "Live session started."));
        
        // Redirect to the meeting URL if available
        if (!string.IsNullOrEmpty(liveClass.MeetingUrl))
            return Redirect(liveClass.MeetingUrl);
            
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إلغاء الحصة - Cancel live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Attendances)
            .FirstOrDefaultAsync(lc => lc.Id == id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Live)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن إلغاء حصة جارية، قم بإنهائها أولاً", "Cannot cancel a live session; end it first."));
            return RedirectToAction(nameof(Details), new { id });
        }

        if (liveClass.Status == LMS.Domain.Enums.LiveClassStatus.Completed)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن إلغاء حصة مكتملة", "Cannot cancel a completed session."));
            return RedirectToAction(nameof(Details), new { id });
        }

        liveClass.Status = LMS.Domain.Enums.LiveClassStatus.Cancelled;
        liveClass.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // TODO: Send cancellation notifications to registered attendees

        SetSuccessMessage(CultureExtensions.T("تم إلغاء الحصة بنجاح", "Session cancelled successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إنهاء البث المباشر - End live class (Admin)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Attendances)
            .FirstOrDefaultAsync(lc => lc.Id == id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Live)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن إنهاء حصة غير مباشرة", "Cannot end a session that is not live."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Update attendance stats before ending
        liveClass.AttendedCount = liveClass.Attendances.Count(a => a.IsPresent);
        liveClass.Status = LMS.Domain.Enums.LiveClassStatus.Completed;
        liveClass.ActualEndTime = DateTime.UtcNow;
        liveClass.UpdatedAt = DateTime.UtcNow;

        // Calculate actual duration if we have start time
        if (liveClass.ActualStartTime.HasValue)
        {
            var duration = (DateTime.UtcNow - liveClass.ActualStartTime.Value).TotalMinutes;
            liveClass.DurationMinutes = (int)Math.Round(duration);
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Live class {LiveClassId} ended by admin at {EndTime}. Duration: {Duration} minutes, Attendees: {AttendeeCount}", 
            id, DateTime.UtcNow, liveClass.DurationMinutes, liveClass.AttendedCount);
        
        SetSuccessMessage(CultureExtensions.T("تم إنهاء البث المباشر بنجاح", "Live broadcast ended successfully."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تمديد وقت البث - Extend live class time (acts as pause by extending end time)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendTime(int id, int extensionMinutes = 10)
    {
        var liveClass = await _context.LiveClasses.FindAsync(id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Live)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن تمديد وقت حصة غير مباشرة", "Cannot extend a session that is not live."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Validate extension time (1-60 minutes)
        extensionMinutes = Math.Clamp(extensionMinutes, 1, 60);

        // Extend scheduled end time
        liveClass.ScheduledEndTime = liveClass.ScheduledEndTime.AddMinutes(extensionMinutes);
        liveClass.DurationMinutes += extensionMinutes;
        liveClass.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Live class {LiveClassId} extended by {ExtensionMinutes} minutes by admin. New end time: {NewEndTime}", 
            id, extensionMinutes, liveClass.ScheduledEndTime);
        
        SetSuccessMessage(string.Format(CultureExtensions.T("تم تمديد وقت البث بمقدار {0} دقيقة", "Broadcast time extended by {0} minute(s)."), extensionMinutes));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// تأجيل الحصة - Postpone live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Postpone(int id, string? reason = null)
    {
        var liveClass = await _context.LiveClasses.FindAsync(id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Scheduled)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن تأجيل إلا الحصص المجدولة", "Only scheduled sessions can be postponed."));
            return RedirectToAction(nameof(Details), new { id });
        }

        liveClass.Status = LMS.Domain.Enums.LiveClassStatus.Postponed;
        liveClass.CancellationReason = reason ?? "تم التأجيل بواسطة الإدارة";
        liveClass.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Live class {LiveClassId} postponed by admin. Reason: {Reason}", 
            id, liveClass.CancellationReason);
        
        SetSuccessMessage(CultureExtensions.T("تم تأجيل الحصة بنجاح", "Session postponed successfully."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// استئناف الحصة المؤجلة - Resume postponed class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id, DateTime? newStartTime = null)
    {
        var liveClass = await _context.LiveClasses.FindAsync(id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Postponed)
        {
            SetWarningMessage(CultureExtensions.T("لا يمكن استئناف إلا الحصص المؤجلة", "Only postponed sessions can be resumed."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // If new start time provided, update schedule
        if (newStartTime.HasValue && newStartTime.Value > DateTime.UtcNow)
        {
            var duration = liveClass.DurationMinutes;
            liveClass.ScheduledStartTime = newStartTime.Value;
            liveClass.ScheduledEndTime = newStartTime.Value.AddMinutes(duration);
        }

        liveClass.Status = LMS.Domain.Enums.LiveClassStatus.Scheduled;
        liveClass.CancellationReason = null;
        liveClass.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Live class {LiveClassId} resumed by admin. New start time: {StartTime}", 
            id, liveClass.ScheduledStartTime);
        
        SetSuccessMessage(CultureExtensions.T("تم استئناف جدولة الحصة بنجاح", "Session scheduling resumed successfully."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إرسال تذكير للمسجلين - Send reminder to registered attendees
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(int id)
    {
        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Attendances)
                .ThenInclude(a => a.Student)
            .FirstOrDefaultAsync(lc => lc.Id == id);
            
        if (liveClass == null)
            return NotFound();

        if (liveClass.Status != LMS.Domain.Enums.LiveClassStatus.Scheduled)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن إرسال تذكير لحصة غير مجدولة", "Cannot send reminder for an unscheduled session."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Mark reminders as sent
        foreach (var attendance in liveClass.Attendances)
        {
            attendance.ReminderSent = true;
            attendance.ReminderSentAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();

        // TODO: Actually send notifications via email/push service

        SetSuccessMessage(string.Format(CultureExtensions.T("تم إرسال التذكير إلى {0} مشترك", "Reminder sent to {0} attendee(s)."), liveClass.Attendances.Count));
        return RedirectToAction(nameof(Index));
    }
    /// <summary>
    /// إيرادات الجلسات المباشرة - Live Sessions Revenue
    /// </summary>
    public async Task<IActionResult> Revenue(DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddMonths(-3);
        to ??= DateTime.UtcNow;

        // Session purchases revenue
        var sessionPurchases = await _context.LiveSessionPurchases
            .Include(p => p.LiveClass)
                .ThenInclude(lc => lc.Instructor)
            .Where(p => p.Status == SessionPurchaseStatus.Active &&
                       p.PurchasedAt >= from && p.PurchasedAt <= to)
            .ToListAsync();

        var sessionRevenue = sessionPurchases.Sum(p => p.PaidAmount);

        // Schedule enrollments revenue
        var scheduleEnrollments = await _context.LiveSessionScheduleEnrollments
            .Include(e => e.Schedule)
                .ThenInclude(s => s.Instructor)
            .Where(e => e.Status == ScheduleEnrollmentStatus.Active &&
                       e.EnrolledAt >= from && e.EnrolledAt <= to)
            .ToListAsync();

        var scheduleRevenue = scheduleEnrollments.Sum(e => e.PaidAmount);

        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        ViewBag.SessionRevenue = sessionRevenue;
        ViewBag.ScheduleRevenue = scheduleRevenue;
        ViewBag.TotalRevenue = sessionRevenue + scheduleRevenue;
        ViewBag.SessionPurchaseCount = sessionPurchases.Count;
        ViewBag.ScheduleEnrollmentCount = scheduleEnrollments.Count;
        ViewBag.SessionPurchases = sessionPurchases;
        ViewBag.ScheduleEnrollments = scheduleEnrollments;

        // Monthly breakdown for charts
        var monthlyData = sessionPurchases
            .GroupBy(p => new { p.PurchasedAt.Year, p.PurchasedAt.Month })
            .Select(g => new { Month = $"{g.Key.Year}-{g.Key.Month:D2}", Revenue = g.Sum(p => p.PaidAmount), Count = g.Count() })
            .OrderBy(g => g.Month)
            .ToList();

        var scheduleMonthly = scheduleEnrollments
            .GroupBy(e => new { e.EnrolledAt.Year, e.EnrolledAt.Month })
            .Select(g => new { Month = $"{g.Key.Year}-{g.Key.Month:D2}", Revenue = g.Sum(e => e.PaidAmount), Count = g.Count() })
            .OrderBy(g => g.Month)
            .ToList();

        ViewBag.MonthlySessionData = monthlyData;
        ViewBag.MonthlyScheduleData = scheduleMonthly;

        return View();
    }

    /// <summary>
    /// جداول الحصص - All Schedules
    /// </summary>
    public async Task<IActionResult> Schedules(LiveScheduleStatus? status = null)
    {
        var query = _context.LiveSessionSchedules
            .Include(s => s.Instructor)
            .Include(s => s.Course)
            .Include(s => s.Sessions)
            .Include(s => s.Enrollments)
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var schedules = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        ViewBag.StatusFilter = status;

        return View(schedules);
    }

    /// <summary>
    /// تفاصيل جدول الحصص - Schedule Details
    /// </summary>
    public async Task<IActionResult> ScheduleDetails(int id)
    {
        var schedule = await _context.LiveSessionSchedules
            .Include(s => s.Instructor)
            .Include(s => s.Course)
            .Include(s => s.Sessions.OrderBy(ss => ss.ScheduleOrder))
                .ThenInclude(ss => ss.Attendances)
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (schedule == null)
            return NotFound();

        ViewBag.TotalRevenue = schedule.Enrollments
            .Where(e => e.Status == ScheduleEnrollmentStatus.Active)
            .Sum(e => e.PaidAmount);

        return View(schedule);
    }
}

/// <summary>
/// نموذج إحصائيات البث المباشر - Live Class Statistics ViewModel
/// </summary>
public class LiveClassStatisticsViewModel
{
    public int TotalLiveClasses { get; set; }
    public int ScheduledCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public double AvgAttendance { get; set; }
    public int RecordedCount { get; set; }
    public int TotalAttendances { get; set; }
}

