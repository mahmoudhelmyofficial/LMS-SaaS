using LMS.Data;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة طلاب المدرس - Instructor Students Controller
/// Enterprise-level controller using service layer for business logic
/// </summary>
public class StudentsController : InstructorBaseController
{
    private readonly IStudentService _studentService;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        IStudentService studentService,
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<StudentsController> logger)
    {
        _studentService = studentService;
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الطلاب - Students list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? searchTerm, int page = 1)
    {
        var userId = _currentUserService.UserId;

        // Validate that we have a valid user ID
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Instructor Students page accessed without valid UserId");
            SetErrorMessage("لم يتم التعرف على المستخدم. يرجى تسجيل الدخول مرة أخرى");
            InitializeEmptyViewBag(courseId, searchTerm);
            return View(new List<Domain.Entities.Learning.Enrollment>());
        }

        try
        {
            // Get courses for dropdown (cached)
            var coursesResult = await _studentService.GetInstructorCoursesAsync(userId);
            var courses = coursesResult.IsSuccess ? coursesResult.Value ?? new List<CourseDropdownDto>() : new List<CourseDropdownDto>();
            ViewBag.Courses = courses;

            // Get statistics (cached)
            var statsResult = await _studentService.GetStudentStatisticsAsync(userId, courseId);
            var stats = statsResult.IsSuccess ? statsResult.Value ?? new StudentStatisticsDto() : new StudentStatisticsDto();
            
            ViewBag.TotalStudents = stats.TotalStudents;
            ViewBag.ActiveStudents = stats.ActiveStudents;
            ViewBag.ActiveStudentChange = stats.ActiveStudentChange;
            ViewBag.CompletionRate = stats.CompletionRate;
            ViewBag.AverageProgress = stats.AverageProgress;

            // Get students list
            var filter = new StudentFilterRequest
            {
                CourseId = courseId,
                SearchTerm = searchTerm,
                Page = page,
                PageSize = 20,
                SortBy = "EnrolledAt",
                SortDescending = true
            };

            var studentsResult = await _studentService.GetStudentsAsync(userId, filter);
            
            if (studentsResult.IsFailure)
            {
                SetErrorMessage(studentsResult.Error ?? "حدث خطأ أثناء تحميل قائمة الطلاب");
                InitializeEmptyViewBag(courseId, searchTerm);
                return View(new List<Domain.Entities.Learning.Enrollment>());
            }

            var studentsList = studentsResult.Value ?? new StudentListResult();
            
            // Fetch actual entities for view compatibility
            // The service provides the business logic and filtering, we fetch entities for the view
            var enrollmentIds = studentsList.Enrollments.Select(e => e.Id).ToList();
            var enrollments = enrollmentIds.Any()
                ? await _context.Enrollments
                    .AsNoTracking()
                    .Include(e => e.Student)
                    .Include(e => e.Course)
                    .Where(e => enrollmentIds.Contains(e.Id))
                    .OrderByDescending(e => e.EnrolledAt)
                    .ToListAsync()
                : new List<Domain.Entities.Learning.Enrollment>();

            ViewBag.CourseId = courseId;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Page = studentsList.Page;
            ViewBag.TotalCount = studentsList.TotalCount;
            ViewBag.TotalPages = studentsList.TotalPages;

            _logger.LogInformation("Instructor {InstructorId} viewed students list. Total: {Count}", 
                userId, studentsList.TotalCount);

            return View(enrollments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading students list for instructor {InstructorId}: {Message}", 
                userId, ex.Message);
            
            SetErrorMessage("حدث خطأ أثناء تحميل قائمة الطلاب. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني إذا استمرت المشكلة.");
            
            InitializeEmptyViewBag(courseId, searchTerm);
            
            // Try to get courses for dropdown even on error
            try
            {
                var coursesResult = await _studentService.GetInstructorCoursesAsync(userId);
                if (coursesResult.IsSuccess && coursesResult.Value != null)
                {
                    ViewBag.Courses = coursesResult.Value;
                }
            }
            catch
            {
                ViewBag.Courses = new List<CourseDropdownDto>();
            }
            
            return View(new List<Domain.Entities.Learning.Enrollment>());
        }
    }

    /// <summary>
    /// Initialize empty ViewBag properties
    /// </summary>
    private void InitializeEmptyViewBag(int? courseId, string? searchTerm)
    {
        ViewBag.Courses = new List<CourseDropdownDto>();
        ViewBag.TotalStudents = 0;
        ViewBag.ActiveStudents = 0;
        ViewBag.ActiveStudentChange = 0.0;
        ViewBag.CompletionRate = "0";
        ViewBag.AverageProgress = "0";
        ViewBag.CourseId = courseId;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Page = 1;
        ViewBag.TotalCount = 0;
        ViewBag.TotalPages = 0;
    }


    /// <summary>
    /// تفاصيل الطالب - Student details
    /// </summary>
    public async Task<IActionResult> Details(int enrollmentId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return NotFound();
        }

        try
        {
            var detailsResult = await _studentService.GetEnrollmentDetailsAsync(enrollmentId, userId);
            
            if (detailsResult.IsFailure)
            {
                SetErrorMessage(detailsResult.Error ?? "حدث خطأ أثناء تحميل تفاصيل الطالب");
                return RedirectToAction(nameof(Index));
            }

            // Fetch full entity for view
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Include(e => e.LessonProgress)
                    .ThenInclude(lp => lp.Lesson)
                .Include(e => e.QuizAttempts)
                    .ThenInclude(qa => qa.Quiz)
                .Include(e => e.AssignmentSubmissions)
                    .ThenInclude(s => s.Assignment)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.Course != null && e.Course.InstructorId == userId);

            if (enrollment == null)
            {
                _logger.LogWarning("Enrollment {EnrollmentId} not found for instructor {InstructorId}", 
                    enrollmentId, userId);
                return NotFound();
            }

            var details = detailsResult.Value!;
            ViewBag.CompletedLessons = details.CompletedLessons;
            ViewBag.TotalLessons = details.TotalLessons;
            ViewBag.CompletionPercentage = details.CompletionPercentage;

            _logger.LogInformation("Instructor {InstructorId} viewed details for enrollment {EnrollmentId}", 
                userId, enrollmentId);

            return View(enrollment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading student details for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الطالب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تقدم الطالب - Student progress
    /// </summary>
    public async Task<IActionResult> Progress(int? enrollmentId, int? courseId)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // If no enrollmentId provided, show list of students to select from
            if (!enrollmentId.HasValue)
            {
                _logger.LogInformation("Instructor {InstructorId} accessed Progress without enrollmentId, showing student list", userId);
                
                // Get courses for filter
                var coursesResult = await _studentService.GetInstructorCoursesAsync(userId);
                ViewBag.Courses = coursesResult.IsSuccess && coursesResult.Value != null 
                    ? coursesResult.Value 
                    : new List<CourseDropdownDto>();
                ViewBag.CourseId = courseId;

                // Get enrollments for selection
                var filter = new StudentFilterRequest
                {
                    CourseId = courseId,
                    Page = 1,
                    PageSize = 100,
                    SortBy = "EnrolledAt",
                    SortDescending = true
                };

                var studentsResult = await _studentService.GetStudentsAsync(userId, filter);
                if (studentsResult.IsSuccess && studentsResult.Value != null)
                {
                    var enrollmentIds = studentsResult.Value.Enrollments.Select(e => e.Id).ToList();
                    var enrollments = enrollmentIds.Any()
                        ? await _context.Enrollments
                            .AsNoTracking()
                            .Include(e => e.Student)
                            .Include(e => e.Course)
                            .Where(e => enrollmentIds.Contains(e.Id))
                            .OrderByDescending(e => e.EnrolledAt)
                            .ToListAsync()
                        : new List<Domain.Entities.Learning.Enrollment>();
                    
                    return View("ProgressList", enrollments);
                }
                
                return View("ProgressList", new List<Domain.Entities.Learning.Enrollment>());
            }

            // Get progress details from service
            var progressResult = await _studentService.GetStudentProgressAsync(enrollmentId.Value, userId);
            
            if (progressResult.IsFailure)
            {
                SetErrorMessage(progressResult.Error ?? "حدث خطأ أثناء تحميل تقدم الطالب");
                return RedirectToAction(nameof(Progress));
            }

            // Fetch full entity for view
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId.Value && e.Course != null && e.Course.InstructorId == userId);

            if (enrollment == null)
            {
                _logger.LogWarning("Enrollment {EnrollmentId} not found for instructor {InstructorId}", 
                    enrollmentId, userId);
                SetErrorMessage("لم يتم العثور على بيانات الطالب");
                return RedirectToAction(nameof(Progress));
            }

            var progress = progressResult.Value!;
            ViewBag.TotalLessons = progress.TotalLessons;
            ViewBag.CompletedLessons = progress.CompletedLessons;
            ViewBag.InProgressLessons = progress.InProgressLessons;
            ViewBag.NotStartedLessons = progress.NotStartedLessons;
            ViewBag.CompletionPercentage = progress.CompletionPercentage;

            _logger.LogInformation("Instructor {InstructorId} viewed progress for enrollment {EnrollmentId}", 
                userId, enrollmentId);

            return View(enrollment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading student progress for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage("حدث خطأ أثناء تحميل تقدم الطالب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إرسال رسالة جماعية للطلاب - Send bulk message to students
    /// Redirects to Messages/BulkMessage with the appropriate course filter
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SendBulkMessage(int? courseId, string? studentIds)
    {
        _logger.LogInformation("Redirecting to BulkMessage from Students page. CourseId: {CourseId}", courseId);
        
        // Redirect to the Messages BulkMessage action with the course filter
        return RedirectToAction("BulkMessage", "Messages", new { courseId = courseId });
    }

    /// <summary>
    /// إرسال رسالة لطالب - Send message to a specific student
    /// Redirects to Messages/Compose with pre-selected student
    /// </summary>
    public IActionResult SendMessage(string studentId)
    {
        if (string.IsNullOrEmpty(studentId))
        {
            SetErrorMessage("لم يتم تحديد الطالب");
            return RedirectToAction(nameof(Index));
        }

        _logger.LogInformation("Redirecting to Compose message for student {StudentId}", studentId);
        return RedirectToAction("Compose", "Messages", new { receiverId = studentId });
    }

    /// <summary>
    /// إعادة تعيين تقدم الطالب - Reset student progress
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetProgress(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _studentService.ResetStudentProgressAsync(id, userId);
            
            if (result.IsFailure)
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء إعادة تعيين تقدم الطالب");
                return RedirectToAction(nameof(Index));
            }

            // Get student name for success message
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            var studentName = enrollment?.Student?.FullName ?? "الطالب";
            SetSuccessMessage($"تم إعادة تعيين تقدم الطالب {studentName} بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting progress for enrollment {EnrollmentId}", id);
            SetErrorMessage("حدث خطأ أثناء إعادة تعيين تقدم الطالب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إيقاف الطالب - Suspend student enrollment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _studentService.SuspendStudentAsync(id, userId);
            
            if (result.IsFailure)
            {
                if (result.Error?.Contains("موقوف بالفعل") == true)
                {
                    SetWarningMessage(result.Error);
                }
                else
                {
                    SetErrorMessage(result.Error ?? "حدث خطأ أثناء إيقاف الطالب");
                }
                return RedirectToAction(nameof(Index));
            }

            // Get student name for success message
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            var studentName = enrollment?.Student?.FullName ?? "الطالب";
            SetSuccessMessage($"تم إيقاف الطالب {studentName} من الدورة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending enrollment {EnrollmentId}", id);
            SetErrorMessage("حدث خطأ أثناء إيقاف الطالب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تفعيل الطالب - Reactivate suspended student
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _studentService.ReactivateStudentAsync(id, userId);
            
            if (result.IsFailure)
            {
                if (result.Error?.Contains("غير موقوف") == true)
                {
                    SetWarningMessage(result.Error);
                }
                else
                {
                    SetErrorMessage(result.Error ?? "حدث خطأ أثناء تفعيل الطالب");
                }
                return RedirectToAction(nameof(Index));
            }

            // Get student name for success message
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            var studentName = enrollment?.Student?.FullName ?? "الطالب";
            SetSuccessMessage($"تم تفعيل الطالب {studentName} في الدورة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating enrollment {EnrollmentId}", id);
            SetErrorMessage("حدث خطأ أثناء تفعيل الطالب");
            return RedirectToAction(nameof(Index));
        }
    }
}

