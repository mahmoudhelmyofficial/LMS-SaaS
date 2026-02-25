using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// حضور البث المباشر للطلاب - Student Live Class Attendance Controller
/// </summary>
public class LiveClassAttendanceController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfGenerationService _pdfService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassAttendanceController> _logger;

    public LiveClassAttendanceController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPdfGenerationService pdfService,
        ILiveSessionService liveSessionService,
        ILogger<LiveClassAttendanceController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pdfService = pdfService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// سجل الحضور - Attendance History
    /// </summary>
    public async Task<IActionResult> Index(int? courseId)
    {
        var userId = _currentUserService.UserId;

        var query = _context.LiveClassAttendances
            .Include(lca => lca.LiveClass)
                .ThenInclude(lc => lc.Course)
            .Include(lca => lca.LiveClass.Instructor)
            .Include(lca => lca.LiveClass.Schedule)
            .Include(lca => lca.LiveClass.Recordings)
            .Where(lca => lca.StudentId == userId)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(lca => lca.LiveClass.CourseId == courseId.Value);
        }

        var attendances = await query
            .OrderByDescending(lca => lca.JoinedAt)
            .ToListAsync();

        // Get enrolled courses for filter
        var enrolledCourses = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = enrolledCourses;
        ViewBag.CourseId = courseId;

        // Calculate attendance statistics using AttendanceStatusDetail as primary source
        var totalPresent = attendances.Count(a => a.AttendanceStatusDetail == AttendanceStatus.Present || a.IsPresent);
        var totalLate = attendances.Count(a => a.AttendanceStatusDetail == AttendanceStatus.Late);
        var totalAbsent = attendances.Count(a => a.AttendanceStatusDetail == AttendanceStatus.Absent || 
            (!a.IsPresent && a.AttendanceStatusDetail != AttendanceStatus.Late && 
             a.AttendanceStatusDetail != AttendanceStatus.Excused && 
             a.AttendanceStatusDetail != AttendanceStatus.Present));
        var totalExcused = attendances.Count(a => a.AttendanceStatusDetail == AttendanceStatus.Excused);
        var avgScore = attendances.Any() ? attendances.Average(a => (double)a.AttendanceScore) : 0;

        ViewBag.TotalPresent = totalPresent;
        ViewBag.TotalLate = totalLate;
        ViewBag.TotalAbsent = totalAbsent;
        ViewBag.TotalExcused = totalExcused;
        ViewBag.AverageScore = avgScore;

        return View(attendances);
    }

    /// <summary>
    /// تفاصيل الحضور - Attendance Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var attendance = await _context.LiveClassAttendances
            .Include(lca => lca.LiveClass)
                .ThenInclude(lc => lc.Course)
            .Include(lca => lca.LiveClass.Instructor)
            .Include(lca => lca.LiveClass.Recordings)
            .Include(lca => lca.LiveClass.Schedule)
            .FirstOrDefaultAsync(lca => lca.Id == id && lca.StudentId == userId);

        if (attendance == null)
            return NotFound();

        // Pass additional attendance detail info
        ViewBag.AttendanceScore = attendance.AttendanceScore;
        ViewBag.LateMinutes = attendance.LateMinutes;
        ViewBag.EarlyLeaveMinutes = attendance.EarlyLeaveMinutes;
        ViewBag.ExcuseReason = attendance.ExcuseReason;
        ViewBag.ExcuseApproved = attendance.ExcuseApproved;
        ViewBag.MarkedByInstructor = attendance.MarkedByInstructor;
        ViewBag.AttendanceStatusDetail = attendance.AttendanceStatusDetail;

        return View(attendance);
    }

    /// <summary>
    /// شهادة الحضور - Attendance Certificate
    /// </summary>
    public async Task<IActionResult> Certificate(int id)
    {
        var userId = _currentUserService.UserId;

        var attendance = await _context.LiveClassAttendances
            .Include(lca => lca.LiveClass)
                .ThenInclude(lc => lc.Course)
            .Include(lca => lca.LiveClass.Instructor)
            .Include(lca => lca.Student)
            .FirstOrDefaultAsync(lca => lca.Id == id && lca.StudentId == userId);

        if (attendance == null)
            return NotFound();

        if (!attendance.IsPresent)
        {
            SetErrorMessage("لم يتم تسجيل حضورك في هذه الجلسة");
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            // Calculate duration
            var durationMinutes = 0;
            if (attendance.LeftAt.HasValue && attendance.JoinedAt.HasValue)
            {
                durationMinutes = (int)(attendance.LeftAt.Value - attendance.JoinedAt.Value).TotalMinutes;
            }
            else
            {
                durationMinutes = attendance.LiveClass.DurationMinutes > 0 ? attendance.LiveClass.DurationMinutes : 60;
            }

            // Generate PDF
            var pdfBytes = _pdfService.GenerateAttendanceCertificatePdf(
                attendance.Student.FullName,
                attendance.LiveClass.Title,
                attendance.JoinedAt ?? attendance.LiveClass.ScheduledStartTime,
                durationMinutes
            );
            
            var fileName = $"Attendance_Certificate_{attendance.Student.LastName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            
            _logger.LogInformation("Attendance certificate generated for attendance {AttendanceId} by student {StudentId}", 
                id, userId);
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating attendance certificate for attendance {AttendanceId}", id);
            SetErrorMessage("حدث خطأ أثناء إنشاء شهادة الحضور");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

