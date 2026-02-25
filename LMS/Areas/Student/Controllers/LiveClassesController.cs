using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.LiveSessions;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// البث المباشر للطلاب - Student Live Classes Controller
/// </summary>
public class LiveClassesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassesController> _logger;

    public LiveClassesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILiveSessionService liveSessionService,
        ILogger<LiveClassesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة البث المباشر - Live Classes List
    /// </summary>
    public async Task<IActionResult> Index(string? pricingFilter = null)
    {
        var userId = _currentUserService.UserId;

        // Get enrolled courses (active only - enterprise alignment with service)
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .ToListAsync();

        // Get schedules the student is enrolled in
        var enrolledScheduleIds = await _context.LiveSessionScheduleEnrollments
            .Where(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Active)
            .Select(e => e.LiveSessionScheduleId)
            .ToListAsync();

        // Get purchased session IDs
        var purchasedSessionIds = await _context.LiveSessionPurchases
            .Where(p => p.StudentId == userId && p.Status == SessionPurchaseStatus.Active)
            .Select(p => p.LiveClassId)
            .ToListAsync();

        var query = _context.LiveClasses
            .Include(lc => lc.Course)
            .Include(lc => lc.Instructor)
            .Include(lc => lc.Lesson)
            .Include(lc => lc.Recordings)
            .Include(lc => lc.Schedule)
            .Where(lc => !lc.IsDeleted)
            .Where(lc => enrolledCourseIds.Contains(lc.CourseId) || lc.IsFreeForAll ||
                         lc.PricingType == LiveSessionPricingType.Paid ||
                         lc.PricingType == LiveSessionPricingType.Free ||
                         (lc.LiveSessionScheduleId.HasValue && enrolledScheduleIds.Contains(lc.LiveSessionScheduleId.Value)) ||
                         purchasedSessionIds.Contains(lc.Id))
            .Where(lc => lc.ScheduledStartTime >= DateTime.UtcNow.AddHours(-2) || 
                         (lc.Status == LiveClassStatus.Completed && lc.Recordings.Any(r => r.IsPublished)))
            .AsQueryable();

        // Apply pricing filter
        if (!string.IsNullOrEmpty(pricingFilter))
        {
            if (Enum.TryParse<LiveSessionPricingType>(pricingFilter, out var pricingType))
            {
                query = query.Where(lc => lc.PricingType == pricingType);
            }
        }

        var liveClasses = await query
            .OrderBy(lc => lc.ScheduledStartTime)
            .ToListAsync();

        // Access per session via service (same logic as Details/Join - includes subscription)
        var accessibleLiveClassIds = await _liveSessionService.GetAccessibleLiveClassIdsForStudentAsync(userId!, liveClasses.Select(lc => lc.Id));

        // Get attendance records
        var attendanceRecords = await _context.LiveClassAttendances
            .Where(lca => lca.StudentId == userId)
            .ToListAsync();

        ViewBag.AttendanceRecords = attendanceRecords;
        ViewBag.PurchasedSessionIds = purchasedSessionIds;
        ViewBag.EnrolledScheduleIds = enrolledScheduleIds;
        ViewBag.EnrolledCourseIds = enrolledCourseIds;
        ViewBag.AccessibleLiveClassIds = accessibleLiveClassIds;
        ViewBag.PricingFilter = pricingFilter;

        return View(liveClasses);
    }

    /// <summary>
    /// تفاصيل البث المباشر - Live Class Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Course)
            .Include(lc => lc.Instructor)
            .Include(lc => lc.Lesson)
            .Include(lc => lc.Recordings)
            .Include(lc => lc.Schedule)
            .FirstOrDefaultAsync(lc => lc.Id == id && !lc.IsDeleted);

        if (liveClass == null)
        {
            SetErrorMessage("الجلسة غير موجودة أو تم حذفها");
            return RedirectToAction(nameof(Index));
        }

        // Single source of truth for access (same as Join - includes subscription)
        var canAccess = await _liveSessionService.CanStudentAccessSessionAsync(id, userId!);

        // Granular flags for UI labels only (aligned with service: active enrollment)
        var isEnrolledInCourse = await _context.Enrollments
            .AnyAsync(e => e.StudentId == userId && e.CourseId == liveClass.CourseId && e.Status == EnrollmentStatus.Active);

        var isPurchased = await _liveSessionService.HasStudentPurchasedSessionAsync(id, userId!);

        var isScheduleEnrolled = liveClass.LiveSessionScheduleId.HasValue &&
            await _liveSessionService.HasStudentPurchasedScheduleAsync(liveClass.LiveSessionScheduleId.Value, userId!);

        // Get attendance record
        var attendance = await _context.LiveClassAttendances
            .FirstOrDefaultAsync(lca => lca.LiveClassId == id && lca.StudentId == userId);

        var isSubscriptionAccess = canAccess && !isPurchased && !isScheduleEnrolled && !isEnrolledInCourse &&
            !liveClass.IsFreeForAll && liveClass.PricingType != LiveSessionPricingType.Free;

        ViewBag.Attendance = attendance;
        ViewBag.CanAccess = canAccess;
        ViewBag.IsPurchased = isPurchased;
        ViewBag.IsScheduleEnrolled = isScheduleEnrolled;
        ViewBag.IsEnrolledInCourse = isEnrolledInCourse;
        ViewBag.IsSubscriptionAccess = isSubscriptionAccess;

        return View(liveClass);
    }

    /// <summary>
    /// الانضمام للبث المباشر - Join Live Class
    /// </summary>
    public async Task<IActionResult> Join(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Course)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (liveClass == null)
            return NotFound();

        // Check access via unified service
        var canAccess = await _liveSessionService.CanStudentAccessSessionAsync(id, userId!);

        // Fallback: also check course enrollment, free-for-all, and active subscription
        if (!canAccess)
        {
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == userId && e.CourseId == liveClass.CourseId);

            canAccess = isEnrolled || liveClass.IsFreeForAll || liveClass.PricingType == LiveSessionPricingType.Free;

            // Check subscription access for subscription-only sessions
            if (!canAccess && (liveClass.PricingType == LiveSessionPricingType.SubscriptionOnly ||
                               liveClass.PricingType == LiveSessionPricingType.Paid))
            {
                canAccess = await _context.Subscriptions
                    .Include(s => s.Plan)
                    .AnyAsync(s => s.UserId == userId &&
                                  (s.Status == "Active" || s.Status == "Trialing") &&
                                  s.CurrentPeriodEnd > DateTime.UtcNow &&
                                  s.Plan != null && s.Plan.AccessToLiveClasses);
            }
        }

        if (!canAccess)
        {
            // Redirect to checkout if paid session
            if (liveClass.PricingType == LiveSessionPricingType.Paid && liveClass.Price > 0)
            {
                SetWarningMessage("يجب شراء هذه الجلسة للانضمام");
                return RedirectToAction("Checkout", "SessionCheckout", new { id });
            }
            SetErrorMessage("يجب التسجيل في الدورة للانضمام");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Check max participants limit
        if (liveClass.MaxParticipants.HasValue)
        {
            var currentAttendees = await _context.LiveClassAttendances
                .CountAsync(a => a.LiveClassId == id && a.IsPresent);
            if (currentAttendees >= liveClass.MaxParticipants.Value)
            {
                _logger.LogWarning("Live class {LiveClassId} has reached max participants ({Max})", id, liveClass.MaxParticipants.Value);
                SetWarningMessage("عذراً، تم اكتمال العدد المسموح به للمشاركين في هذه الجلسة.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Check if class is live or upcoming
        if (liveClass.Status != LiveClassStatus.Live && 
            liveClass.Status != LiveClassStatus.Scheduled)
        {
            SetErrorMessage("هذه الجلسة غير متاحة حالياً");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Record join via service (handles attendance tracking)
        try
        {
            var deviceType = Request.Headers["User-Agent"].ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _liveSessionService.RecordStudentJoinAsync(id, userId!, deviceType, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record join via service for session {SessionId}, falling back to direct DB", id);

            // Fallback: Create or update attendance record directly
            var attendance = await _context.LiveClassAttendances
                .FirstOrDefaultAsync(lca => lca.LiveClassId == id && lca.StudentId == userId);

            if (attendance == null)
            {
                attendance = new LiveClassAttendance
                {
                    LiveClassId = id,
                    StudentId = userId!,
                    JoinedAt = DateTime.UtcNow,
                    IsPresent = true
                };
                _context.LiveClassAttendances.Add(attendance);
            }
            else
            {
                attendance.JoinedAt = DateTime.UtcNow;
                attendance.IsPresent = true;
            }

            await _context.SaveChangesAsync();
        }

        // Redirect to meeting URL
        if (string.IsNullOrWhiteSpace(liveClass.MeetingUrl))
        {
            _logger.LogWarning("Live class {LiveClassId} has no meeting URL", id);
            SetErrorMessage("رابط الاجتماع غير متاح حالياً. يرجى التواصل مع المدرس.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return Redirect(liveClass.MeetingUrl);
    }

    /// <summary>
    /// مغادرة البث المباشر - Leave Live Class (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var result = await _liveSessionService.RecordStudentLeaveAsync(id, userId!);
            return Json(new { success = result, message = result ? "تم تسجيل المغادرة" : "حدث خطأ" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording leave for session {SessionId}, student {StudentId}", id, userId);

            // Fallback: update directly
            var attendance = await _context.LiveClassAttendances
                .FirstOrDefaultAsync(lca => lca.LiveClassId == id && lca.StudentId == userId);

            if (attendance != null)
            {
                attendance.LeftAt = DateTime.UtcNow;
                if (attendance.JoinedAt.HasValue)
                {
                    attendance.DurationMinutes = (int)(DateTime.UtcNow - attendance.JoinedAt.Value).TotalMinutes;
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "تم تسجيل المغادرة" });
            }

            return Json(new { success = false, message = "لم يتم العثور على سجل الحضور" });
        }
    }

    /// <summary>
    /// التسجيل في جلسة البث المباشر - Enroll in Live Class
    /// For free live classes that don't require course enrollment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var liveClass = await _context.LiveClasses
                .Include(lc => lc.Course)
                .FirstOrDefaultAsync(lc => lc.Id == id);

            if (liveClass == null)
            {
                SetErrorMessage("الجلسة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            // Check if already enrolled in the course
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == userId && e.CourseId == liveClass.CourseId);

            if (isEnrolled)
            {
                SetInfoMessage("أنت مسجل بالفعل في هذه الدورة");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Only allow enrollment for free-for-all classes or free courses
            if (!liveClass.IsFreeForAll && liveClass.Course.Price > 0)
            {
                SetErrorMessage("يجب شراء الدورة للتسجيل في هذه الجلسة");
                return RedirectToAction("Preview", "Courses", new { id = liveClass.CourseId });
            }

            // Enroll in the course if it's free
            if (liveClass.Course.Price == 0)
            {
                var enrollment = new Domain.Entities.Learning.Enrollment
                {
                    StudentId = userId,
                    CourseId = liveClass.CourseId,
                    EnrolledAt = DateTime.UtcNow,
                    Status = Domain.Enums.EnrollmentStatus.Active,
                    ProgressPercentage = 0,
                    IsFree = true
                };

                _context.Enrollments.Add(enrollment);
                await _context.SaveChangesAsync();

                SetSuccessMessage("تم تسجيلك في الدورة بنجاح. يمكنك الآن حضور الجلسة");
                _logger.LogInformation("Student {StudentId} enrolled in course {CourseId} via live class {LiveClassId}", 
                    userId, liveClass.CourseId, id);
            }
            else if (liveClass.IsFreeForAll)
            {
                // For free-for-all classes, just create an attendance record
                var existingAttendance = await _context.LiveClassAttendances
                    .AnyAsync(lca => lca.LiveClassId == id && lca.StudentId == userId);

                if (!existingAttendance)
                {
                    var attendance = new LiveClassAttendance
                    {
                        LiveClassId = id,
                        StudentId = userId,
                        JoinedAt = null, // Will be set when they actually join
                        IsPresent = false
                    };

                    _context.LiveClassAttendances.Add(attendance);
                    await _context.SaveChangesAsync();
                }

                SetSuccessMessage("تم تسجيلك في الجلسة المجانية بنجاح");
                _logger.LogInformation("Student {StudentId} registered for free live class {LiveClassId}", userId, id);
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling student {StudentId} in live class {LiveClassId}", userId, id);
            SetErrorMessage("حدث خطأ أثناء التسجيل");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// التسجيلات - Recordings
    /// </summary>
    public async Task<IActionResult> Recordings(int? courseId)
    {
        var userId = _currentUserService.UserId;

        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Select(e => e.CourseId)
            .ToListAsync();

        // Get accessible recordings via service (includes purchased sessions/schedules)
        var accessibleRecordings = await _liveSessionService.GetAccessibleRecordingsForStudentAsync(userId!);
        var accessibleRecordingIds = accessibleRecordings.Select(r => r.Id).ToHashSet();

        var query = _context.LiveClassRecordings
            .Include(lcr => lcr.LiveClass)
                .ThenInclude(lc => lc.Course)
            .Where(lcr => lcr.IsPublished &&
                          (enrolledCourseIds.Contains(lcr.LiveClass.CourseId) ||
                           accessibleRecordingIds.Contains(lcr.Id) ||
                           !lcr.AccessRequiresPurchase))
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(lcr => lcr.LiveClass.CourseId == courseId.Value);
        }

        var recordings = await query
            .OrderByDescending(lcr => lcr.RecordedAt)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => enrolledCourseIds.Contains(c.Id))
            .ToListAsync();

        ViewBag.CourseId = courseId;

        // Get recording statistics
        ViewBag.TotalRecordings = recordings.Count;
        ViewBag.WatchedRecordings = recordings.Count(r => r.ViewCount > 0);
        ViewBag.TotalWatchHours = recordings.Sum(r => r.DurationMinutes) / 60;

        return View(recordings);
    }

    /// <summary>
    /// مشاهدة التسجيل - Watch Recording
    /// </summary>
    public async Task<IActionResult> WatchRecording(int id)
    {
        var userId = _currentUserService.UserId;

        var recording = await _context.LiveClassRecordings
            .Include(lcr => lcr.LiveClass)
                .ThenInclude(lc => lc.Course)
            .FirstOrDefaultAsync(lcr => lcr.Id == id);

        if (recording == null)
            return NotFound();

        // Check access via service first
        var canAccess = await _liveSessionService.CanStudentAccessRecordingAsync(id, userId!);

        if (!canAccess)
        {
            // Fallback: check course enrollment
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == userId && e.CourseId == recording.LiveClass.CourseId);

            canAccess = isEnrolled || !recording.AccessRequiresPurchase;
        }

        if (!canAccess)
        {
            SetErrorMessage("يجب شراء الجلسة أو التسجيل في الدورة لمشاهدة التسجيل");
            return RedirectToAction(nameof(Recordings));
        }

        // Increment view count
        recording.ViewCount++;
        await _context.SaveChangesAsync();

        return View(recording);
    }
}

