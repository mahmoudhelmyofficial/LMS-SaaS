using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// تتبع تقدم الطلاب - Student Progress Tracking Controller
/// Handles student progress monitoring and report exports
/// </summary>
public class ProgressController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExportService _exportService;
    private readonly ILogger<ProgressController> _logger;

    public ProgressController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IExportService exportService,
        ILogger<ProgressController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// نظرة عامة على تقدم الطلاب - Progress Overview (Index)
    /// </summary>
    public async Task<IActionResult> Index(int? courseId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            // Get instructor's courses for filter
            var courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.Courses = courses;
            ViewBag.CourseId = courseId;

            // Base query for enrollments
            var enrollmentsQuery = _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Include(e => e.LessonProgress)
                .Where(e => e.Course.InstructorId == userId);

            if (courseId.HasValue)
            {
                enrollmentsQuery = enrollmentsQuery.Where(e => e.CourseId == courseId.Value);
            }

            var enrollments = await enrollmentsQuery
                .OrderByDescending(e => e.EnrolledAt)
                .Take(50)
                .ToListAsync();

            // Calculate statistics
            var allEnrollments = await _context.Enrollments
                .Where(e => e.Course.InstructorId == userId)
                .ToListAsync();

            ViewBag.TotalStudents = allEnrollments.Select(e => e.StudentId).Distinct().Count();
            ViewBag.TotalEnrollments = allEnrollments.Count;
            ViewBag.CompletedEnrollments = allEnrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed);
            ViewBag.AverageProgress = allEnrollments.Any() 
                ? allEnrollments.Average(e => (double)e.ProgressPercentage).ToString("F1") 
                : "0";
            ViewBag.CompletionRate = allEnrollments.Any() 
                ? (allEnrollments.Count(e => e.Status == Domain.Enums.EnrollmentStatus.Completed) * 100.0 / allEnrollments.Count).ToString("F1") 
                : "0";

            _logger.LogInformation("Instructor {InstructorId} viewing progress overview. Enrollments: {Count}", 
                userId, enrollments.Count);

            return View(enrollments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading progress overview for instructor {InstructorId}.", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل نظرة عامة على التقدم.");
            
            ViewBag.Courses = new List<object>();
            ViewBag.CourseId = courseId;
            ViewBag.TotalStudents = 0;
            ViewBag.TotalEnrollments = 0;
            ViewBag.CompletedEnrollments = 0;
            ViewBag.AverageProgress = "0";
            ViewBag.CompletionRate = "0";
            
            return View(new List<Domain.Entities.Learning.Enrollment>());
        }
    }

    /// <summary>
    /// تقدم الطلاب حسب الدورة - Course Progress
    /// </summary>
    public async Task<IActionResult> Course(int courseId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("NotFound: Course {CourseId} not found or instructor {InstructorId} unauthorized.", courseId, userId);
                SetErrorMessage("الدورة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            // Authorization check
            if (course.InstructorId != userId)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to view progress for course {CourseId}.", userId, courseId);
                SetErrorMessage("غير مصرح لك بعرض تقدم طلاب هذه الدورة.");
                return Forbid();
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.LessonProgress)
                .Where(e => e.CourseId == courseId)
                .OrderBy(e => e.Student.FirstName)
                .ToListAsync();

            ViewBag.Course = course;
            _logger.LogInformation("Instructor {InstructorId} viewing progress for course {CourseId} with {EnrollmentCount} enrollments.", userId, courseId, enrollments.Count);
            return View(enrollments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course progress for course {CourseId} by instructor {InstructorId}.", courseId, userId);
            SetErrorMessage("حدث خطأ أثناء تحميل تقدم الطلاب.");
            return View(new List<Domain.Entities.Learning.Enrollment>());
        }
    }

    /// <summary>
    /// تقدم طالب محدد - Student Detailed Progress
    /// </summary>
    public async Task<IActionResult> Student(int enrollmentId)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .Include(e => e.LessonProgress)
                    .ThenInclude(lp => lp.Lesson)
                .Include(e => e.QuizAttempts)
                    .ThenInclude(qa => qa.Quiz)
                .Include(e => e.AssignmentSubmissions)
                    .ThenInclude(asub => asub.Assignment)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && 
                    e.Course.InstructorId == userId);

            if (enrollment == null)
            {
                _logger.LogWarning("NotFound: Enrollment {EnrollmentId} not found or instructor {InstructorId} unauthorized.", enrollmentId, userId);
                SetErrorMessage("التسجيل غير موجود أو ليس لديك صلاحية عليه.");
                return NotFound();
            }

            // Authorization check
            if (enrollment.Course?.InstructorId != userId)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to view student progress for enrollment {EnrollmentId}.", userId, enrollmentId);
                SetErrorMessage("غير مصرح لك بعرض تقدم هذا الطالب.");
                return Forbid();
            }

            _logger.LogInformation("Instructor {InstructorId} viewing detailed progress for enrollment {EnrollmentId}.", userId, enrollmentId);
            return View(enrollment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading student progress for enrollment {EnrollmentId} by instructor {InstructorId}.", enrollmentId, userId);
            SetErrorMessage("حدث خطأ أثناء تحميل تقدم الطالب.");
            return NotFound();
        }
    }

    /// <summary>
    /// تصدير التقرير - Export Progress Report
    /// Supports CSV, Excel, and PDF formats
    /// </summary>
    public async Task<IActionResult> Export(int courseId, string format = "csv")
    {
        var userId = _currentUserService.UserId;

        // Validate user authentication
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Export attempted without valid UserId. CourseId: {CourseId}", courseId);
            SetErrorMessage("لم يتم التعرف على المستخدم. يرجى تسجيل الدخول مرة أخرى");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Require valid course selection (prevents 404 when Export is clicked without selecting a course)
        if (courseId <= 0)
        {
            _logger.LogWarning("Export attempted without valid courseId. Instructor: {InstructorId}", userId);
            SetErrorMessage("يرجى اختيار دورة لتصدير تقرير التقدم.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("NotFound: Course {CourseId} not found or instructor {InstructorId} unauthorized for export.", courseId, userId);
                SetErrorMessage("الدورة غير موجودة أو ليس لديك صلاحية عليها.");
                return RedirectToAction(nameof(Index));
            }

            // Authorization check (redundant but explicit for security)
            if (course.InstructorId != userId)
            {
                _logger.LogWarning("SECURITY: Unauthorized export attempt. Instructor {InstructorId} tried to export progress for course {CourseId}.", userId, courseId);
                SetErrorMessage("غير مصرح لك بتصدير تقرير هذه الدورة.");
                return Forbid();
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.LessonProgress)
                .Where(e => e.CourseId == courseId)
                .OrderBy(e => e.Student.FirstName)
                .ThenBy(e => e.Student.LastName)
                .ToListAsync();

            // Define export columns
            var columns = new List<ExportColumnDefinition>
            {
                new() { PropertyName = "StudentFullName", DisplayName = "اسم الطالب", Order = 1, 
                    ValueFormatter = (obj) => obj?.ToString() ?? "" },
                new() { PropertyName = "StudentEmail", DisplayName = "البريد الإلكتروني", Order = 2,
                    ValueFormatter = (obj) => obj?.ToString() ?? "غير متوفر" },
                new() { PropertyName = "EnrolledAt", DisplayName = "تاريخ التسجيل", Order = 3, 
                    Format = "yyyy-MM-dd" },
                new() { PropertyName = "ProgressPercentage", DisplayName = "نسبة الإتمام", Order = 4,
                    ValueFormatter = (obj) => obj != null ? $"{obj:F1}%" : "0%" },
                new() { PropertyName = "Status", DisplayName = "الحالة", Order = 5 },
                new() { PropertyName = "FinalGrade", DisplayName = "التقدير النهائي", Order = 6,
                    ValueFormatter = (obj) => obj != null ? $"{obj:F1}" : "-" },
                new() { PropertyName = "CompletedLessons", DisplayName = "الدروس المكتملة", Order = 7,
                    ValueFormatter = (obj) => obj?.ToString() ?? "0" },
                new() { PropertyName = "LastAccessedAt", DisplayName = "آخر نشاط", Order = 8,
                    Format = "yyyy-MM-dd HH:mm" }
            };

            // Transform data to export DTOs
            var exportData = enrollments.Select(e => new ProgressExportDto
            {
                StudentFullName = $"{e.Student.FirstName} {e.Student.LastName}",
                StudentEmail = e.Student.Email ?? "غير متوفر",
                EnrolledAt = e.EnrolledAt,
                ProgressPercentage = e.ProgressPercentage,
                Status = e.Status,
                FinalGrade = e.FinalGrade,
                CompletedLessons = e.LessonProgress.Count(lp => lp.IsCompleted),
                LastAccessedAt = e.LessonProgress.OrderByDescending(lp => lp.LastAccessedAt)
                    .FirstOrDefault()?.LastAccessedAt
            }).ToList();

            // Parse format
            var exportFormat = format.ToLower() switch
            {
                "csv" => ExportFormat.Csv,
                "excel" or "xlsx" => ExportFormat.Excel,
                "pdf" => ExportFormat.Pdf,
                _ => ExportFormat.Csv
            };

            // Generate export based on format
            byte[] exportBytes;
            
            switch (exportFormat)
            {
                case ExportFormat.Excel:
                    exportBytes = await _exportService.ExportToExcelAsync(exportData, columns, 
                        $"تقدم الطلاب - {course.Title}");
                    _logger.LogInformation(
                        "Instructor {InstructorId} exported progress report for course {CourseId} in Excel format. Records: {Count}", 
                        userId, courseId, enrollments.Count);
                    break;
                    
                case ExportFormat.Pdf:
                    var pdfOptions = new PdfExportOptions
                    {
                        Title = $"تقرير تقدم الطلاب",
                        Subtitle = $"الدورة: {course.Title}",
                        IsRtl = true,
                        IsLandscape = true,
                        IncludeLogo = true
                    };
                    exportBytes = await _exportService.ExportToPdfAsync(exportData, columns, pdfOptions);
                    _logger.LogInformation(
                        "Instructor {InstructorId} exported progress report for course {CourseId} in PDF format. Records: {Count}", 
                        userId, courseId, enrollments.Count);
                    break;
                    
                default: // CSV
                    exportBytes = await _exportService.ExportToCsvAsync(exportData, columns);
                    _logger.LogInformation(
                        "Instructor {InstructorId} exported progress report for course {CourseId} in CSV format. Records: {Count}", 
                        userId, courseId, enrollments.Count);
                    break;
            }

            // Sanitize course title for filename
            var safeTitle = string.Join("_", course.Title.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"progress_report_{safeTitle}";
            
            return _exportService.CreateFileResult(exportBytes, exportFormat, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting progress report for course {CourseId} by instructor {InstructorId}. Format: {Format}", 
                courseId, userId, format);
            SetErrorMessage("حدث خطأ أثناء تصدير التقرير. يرجى المحاولة مرة أخرى");
            return RedirectToAction(nameof(Course), new { courseId });
        }
    }
}

/// <summary>
/// نموذج بيانات التصدير للتقدم - Progress Export DTO
/// </summary>
public class ProgressExportDto
{
    public string StudentFullName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public decimal ProgressPercentage { get; set; }
    public Domain.Enums.EnrollmentStatus Status { get; set; }
    public decimal? FinalGrade { get; set; }
    public int CompletedLessons { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

