using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.LiveSessions;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة حضور البث المباشر - Live Class Attendance Controller
/// </summary>
public class LiveClassAttendanceController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassAttendanceController> _logger;

    public LiveClassAttendanceController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILiveSessionService liveSessionService,
        ILogger<LiveClassAttendanceController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الحضور - Attendance List
    /// </summary>
    public async Task<IActionResult> Index(int? liveClassId = null)
    {
        var userId = _currentUserService.UserId;

        try
        {
            // If no liveClassId provided, show all live classes
            if (!liveClassId.HasValue)
            {
                var liveClasses = await _context.LiveClasses
                    .Include(lc => lc.Course)
                    .Where(lc => lc.InstructorId == userId)
                    .OrderByDescending(lc => lc.ScheduledAt)
                    .Select(lc => new LiveClassAttendanceSummaryViewModel
                    {
                        LiveClassId = lc.Id,
                        Title = lc.Title,
                        CourseName = lc.Course.Title,
                        ScheduledAt = lc.ScheduledAt,
                        Duration = lc.DurationMinutes,
                        TotalStudents = _context.LiveClassAttendances.Count(a => a.LiveClassId == lc.Id),
                        PresentStudents = _context.LiveClassAttendances.Count(a => a.LiveClassId == lc.Id && a.IsPresent),
                        IsCompleted = lc.ScheduledAt.AddMinutes(lc.DurationMinutes) < DateTime.UtcNow
                    })
                    .ToListAsync();

                return View("SelectLiveClass", liveClasses);
            }

            var liveClass = await _context.LiveClasses
                .Include(lc => lc.Course)
                .FirstOrDefaultAsync(lc => lc.Id == liveClassId && lc.InstructorId == userId);

            if (liveClass == null)
            {
                _logger.LogWarning("NotFound: Live class {LiveClassId} not found or instructor {InstructorId} unauthorized.", liveClassId, userId);
                SetErrorMessage("الحصة المباشرة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            var attendances = await _context.LiveClassAttendances
                .Include(lca => lca.Student)
                .Where(lca => lca.LiveClassId == liveClassId)
                .OrderBy(lca => lca.Student.FirstName)
                .ToListAsync();

            ViewBag.LiveClass = liveClass;
            _logger.LogInformation("Instructor {InstructorId} viewing attendance for live class {LiveClassId}.", userId, liveClassId);
            return View(attendances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading attendance list for live class {LiveClassId} by instructor {InstructorId}.", liveClassId, userId);
            SetErrorMessage("حدث خطأ أثناء تحميل قائمة الحضور.");
            return View(new List<LiveClassAttendance>());
        }
    }

    /// <summary>
    /// تسجيل الحضور - Mark Attendance
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAttendance(int liveClassId, string studentId, bool isPresent)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == liveClassId && lc.InstructorId == userId);

        if (liveClass == null)
        {
            _logger.LogWarning("NotFound: Live class {LiveClassId} not found or instructor {InstructorId} unauthorized.", liveClassId, userId);
            return NotFound();
        }

        // Note: Authorization is already enforced by the InstructorId filter in the query above

        // Verify student is enrolled in the course
        var isEnrolled = await _context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == liveClass.CourseId);

        if (!isEnrolled)
        {
            _logger.LogWarning("Student {StudentId} is not enrolled in course {CourseId} for live class {LiveClassId}.", studentId, liveClass.CourseId, liveClassId);
            return BadRequest(new { success = false, message = "الطالب غير مسجل في الدورة" });
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                var attendance = await _context.LiveClassAttendances
                    .FirstOrDefaultAsync(lca => lca.LiveClassId == liveClassId && lca.StudentId == studentId);

                if (attendance == null)
                {
                    attendance = new LiveClassAttendance
                    {
                        LiveClassId = liveClassId,
                        StudentId = studentId,
                        IsPresent = isPresent,
                        JoinedAt = isPresent ? DateTime.UtcNow : null
                    };
                    _context.LiveClassAttendances.Add(attendance);
                }
                else
                {
                    attendance.IsPresent = isPresent;
                    if (isPresent && !attendance.JoinedAt.HasValue)
                    {
                        attendance.JoinedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} marked attendance for student {StudentId} in live class {LiveClassId} as {Status}.", 
                userId, studentId, liveClassId, isPresent ? "present" : "absent");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking attendance for student {StudentId} in live class {LiveClassId}.", studentId, liveClassId);
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء تسجيل الحضور" });
        }
    }

    /// <summary>
    /// تصدير قائمة الحضور - Export Attendance
    /// </summary>
    public async Task<IActionResult> Export(int liveClassId, string format = "csv")
    {
        var userId = _currentUserService.UserId;

        try
        {
            var liveClass = await _context.LiveClasses
                .Include(lc => lc.Course)
                .FirstOrDefaultAsync(lc => lc.Id == liveClassId && lc.InstructorId == userId);

            if (liveClass == null)
            {
                _logger.LogWarning("NotFound: Live class {LiveClassId} not found or instructor {InstructorId} unauthorized for export.", liveClassId, userId);
                SetErrorMessage("الحصة المباشرة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            // Note: Authorization is already enforced by the InstructorId filter in the query above

            var attendances = await _context.LiveClassAttendances
                .Include(lca => lca.Student)
                .Where(lca => lca.LiveClassId == liveClassId)
                .OrderBy(lca => lca.Student.FirstName)
                .ToListAsync();

            // Generate CSV content
            if (format.ToLower() == "csv")
            {
                var csv = new System.Text.StringBuilder();
                
                // Add headers with detailed status and score columns
                csv.AppendLine("اسم الطالب,البريد الإلكتروني,الحالة,الحالة التفصيلية,وقت الدخول,وقت المغادرة,مدة الحضور (دقائق),درجة الحضور,دقائق التأخير,سبب العذر");
                
                // Add data
                foreach (var attendance in attendances)
                {
                    var studentName = $"{attendance.Student.FirstName} {attendance.Student.LastName}";
                    var email = attendance.Student.Email ?? "غير متوفر";
                    var status = attendance.IsPresent ? "حاضر" : "غائب";
                    var detailedStatus = attendance.AttendanceStatusDetail switch
                    {
                        AttendanceStatus.Present => "حاضر",
                        AttendanceStatus.Late => "متأخر",
                        AttendanceStatus.Absent => "غائب",
                        AttendanceStatus.LeftEarly => "غادر مبكراً",
                        AttendanceStatus.Excused => "معذور",
                        _ => "مسجل"
                    };
                    var joinedAt = attendance.JoinedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    var leftAt = attendance.LeftAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    var duration = attendance.DurationMinutes.ToString();
                    var score = attendance.AttendanceScore.ToString("F1");
                    var lateMinutes = attendance.LateMinutes.ToString();
                    var excuseReason = attendance.ExcuseReason ?? "-";
                    
                    csv.AppendLine($"\"{studentName}\",\"{email}\",\"{status}\",\"{detailedStatus}\",\"{joinedAt}\",\"{leftAt}\",\"{duration}\",\"{score}\",\"{lateMinutes}\",\"{excuseReason}\"");
                }

                // Return CSV file
                var fileName = $"attendance_{liveClass.Title}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                
                _logger.LogInformation("Instructor {InstructorId} exported attendance for live class {LiveClassId} in CSV format. Records: {Count}", 
                    userId, liveClassId, attendances.Count);
                
                return File(bytes, "text/csv", fileName);
            }
            else
            {
                // Excel or other formats not yet implemented
                _logger.LogWarning("Export format {Format} not supported for live class {LiveClassId}", format, liveClassId);
                SetWarningMessage($"صيغة التصدير {format.ToUpper()} غير مدعومة حالياً. يرجى استخدام CSV");
                return RedirectToAction(nameof(Index), new { liveClassId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting attendance for live class {LiveClassId} by instructor {InstructorId}.", liveClassId, userId);
            SetErrorMessage("حدث خطأ أثناء تصدير قائمة الحضور.");
            return RedirectToAction(nameof(Index), new { liveClassId });
        }
    }

    /// <summary>
    /// تفاصيل حضور طالب - Attendance detail for a student
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var attendance = await _context.LiveClassAttendances
            .Include(a => a.Student)
            .Include(a => a.LiveClass)
                .ThenInclude(lc => lc.Course)
            .FirstOrDefaultAsync(a => a.Id == id && a.LiveClass.InstructorId == userId);

        if (attendance == null)
        {
            SetErrorMessage("سجل الحضور غير موجود");
            return NotFound();
        }

        return View(attendance);
    }

    /// <summary>
    /// تحديث حضور مجمع - Bulk mark attendance
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkMarkAttendance(int liveClassId, List<string> studentIds, AttendanceStatus status)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == liveClassId && lc.InstructorId == userId);

        if (liveClass == null)
        {
            return NotFound();
        }

        try
        {
            var result = await _liveSessionService.BulkMarkAttendanceAsync(liveClassId, studentIds, status);
            if (result)
            {
                _logger.LogInformation(
                    "Instructor {InstructorId} bulk marked {Count} students as {Status} in live class {LiveClassId}",
                    userId, studentIds.Count, status, liveClassId);
                SetSuccessMessage($"تم تحديث حالة الحضور لـ {studentIds.Count} طالب");
            }
            else
            {
                SetErrorMessage("فشل تحديث حالة الحضور");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk marking attendance for live class {LiveClassId}", liveClassId);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة الحضور");
        }

        return RedirectToAction(nameof(Index), new { liveClassId });
    }

    /// <summary>
    /// قبول عذر غياب - Excuse absence
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcuseAbsence(int attendanceId, string reason)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var attendance = await _context.LiveClassAttendances
                .Include(a => a.LiveClass)
                .FirstOrDefaultAsync(a => a.Id == attendanceId && a.LiveClass.InstructorId == userId);

            if (attendance == null)
            {
                return NotFound();
            }

            var result = await _liveSessionService.MarkAttendanceStatusAsync(attendanceId, AttendanceStatus.Excused, reason);
            if (result)
            {
                _logger.LogInformation(
                    "Instructor {InstructorId} excused student {StudentId} in live class {LiveClassId}. Reason: {Reason}",
                    userId, attendance.StudentId, attendance.LiveClassId, reason);
                SetSuccessMessage("تم قبول عذر الغياب");
            }
            else
            {
                SetErrorMessage("فشل قبول العذر");
            }

            return RedirectToAction(nameof(Index), new { liveClassId = attendance.LiveClassId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error excusing absence for attendance {AttendanceId}", attendanceId);
            SetErrorMessage("حدث خطأ أثناء قبول العذر");
            return RedirectToAction(nameof(Index));
        }
    }
}

