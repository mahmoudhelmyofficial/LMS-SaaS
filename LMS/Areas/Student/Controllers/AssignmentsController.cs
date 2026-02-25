using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// تكليفات الطالب - Student Assignments Controller
/// </summary>
public class AssignmentsController : StudentBaseController
{
    private const int MaxAssignmentFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedAssignmentExtensions = { ".pdf", ".doc", ".docx", ".txt", ".zip", ".jpg", ".jpeg", ".png" };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        IInstructorNotificationService instructorNotificationService,
        IFileStorageService fileStorage,
        ILogger<AssignmentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _instructorNotificationService = instructorNotificationService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// تكليفاتي - My assignments
    /// </summary>
    public async Task<IActionResult> Index(string? filter = "all")
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get all assignments from enrolled courses (active enrollments only)
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .ToListAsync();

        var assignments = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(a => a.Lesson != null && a.Lesson.Module != null && enrolledCourseIds.Contains(a.Lesson.Module.CourseId))
            .OrderBy(a => a.DueDate)
            .ToListAsync();

        // Get or create submissions for all assignments
        var existingSubmissions = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(s => s.StudentId == userId)
            .ToListAsync();

        var existingAssignmentIds = existingSubmissions.Select(s => s.AssignmentId).ToHashSet();

        // Create submissions list including assignments without submissions
        var allSubmissions = new List<AssignmentSubmission>(existingSubmissions);

        // Get enrollments for creating virtual submissions (active only)
        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .ToDictionaryAsync(e => e.CourseId, e => e.Id);

        foreach (var assignment in assignments)
        {
            if (assignment.Lesson?.Module == null) continue;
            if (!existingAssignmentIds.Contains(assignment.Id))
            {
                var enrollmentId = enrollments.GetValueOrDefault(assignment.Lesson.Module.CourseId, 0);
                if (enrollmentId == 0) continue;
                var virtualSubmission = new AssignmentSubmission
                {
                    AssignmentId = assignment.Id,
                    Assignment = assignment,
                    StudentId = userId!,
                    Status = AssignmentStatus.Pending,
                    EnrollmentId = enrollmentId
                };
                allSubmissions.Add(virtualSubmission);
            }
        }

        // Apply filter
        var filteredSubmissions = filter?.ToLower() switch
        {
            "pending" => allSubmissions.Where(s => s.Status == AssignmentStatus.Pending).ToList(),
            "submitted" => allSubmissions.Where(s => s.Status == AssignmentStatus.Submitted).ToList(),
            "graded" => allSubmissions.Where(s => s.Status == AssignmentStatus.Graded).ToList(),
            _ => allSubmissions
        };

        ViewBag.Filter = filter;

        return View(filteredSubmissions.OrderBy(s => s.Assignment?.DueDate).ToList());
    }

    /// <summary>
    /// التكليفات المعلقة - Pending assignments
    /// </summary>
    public async Task<IActionResult> Pending()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get all assignments from enrolled courses (active enrollments only)
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .ToListAsync();

        var assignments = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(a => a.Lesson != null && a.Lesson.Module != null && enrolledCourseIds.Contains(a.Lesson.Module.CourseId))
            .OrderBy(a => a.DueDate)
            .ToListAsync();

        // Get or create submissions for all assignments
        var existingSubmissions = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(s => s.StudentId == userId)
            .ToListAsync();

        var existingAssignmentIds = existingSubmissions.Select(s => s.AssignmentId).ToHashSet();

        // Create submissions list including assignments without submissions
        var allSubmissions = new List<AssignmentSubmission>(existingSubmissions);

        // Get enrollments for creating virtual submissions (active only)
        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .ToDictionaryAsync(e => e.CourseId, e => e.Id);

        foreach (var assignment in assignments)
        {
            if (assignment.Lesson?.Module == null) continue;
            if (!existingAssignmentIds.Contains(assignment.Id))
            {
                var enrollmentId = enrollments.GetValueOrDefault(assignment.Lesson.Module.CourseId, 0);
                if (enrollmentId == 0) continue;
                var virtualSubmission = new AssignmentSubmission
                {
                    AssignmentId = assignment.Id,
                    Assignment = assignment,
                    StudentId = userId!,
                    Status = AssignmentStatus.Pending,
                    EnrollmentId = enrollmentId
                };
                allSubmissions.Add(virtualSubmission);
            }
        }

        // Filter only pending submissions
        var pendingSubmissions = allSubmissions
            .Where(s => s.Status == AssignmentStatus.Pending)
            .OrderBy(s => s.Assignment?.DueDate)
            .ToList();

        ViewBag.Filter = "pending";

        return View("Index", pendingSubmissions);
    }

    /// <summary>
    /// تفاصيل التكليف - Assignment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var assignment = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null)
            return NotFound();

        // Check enrollment (active only)
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == assignment.Lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);

        if (enrollment == null)
            return Forbid();

        // ZTP: Assignment linked to lesson must be accessed from Learning (tutor) only
        if (assignment.LessonId > 0 && !ValidateAndKeepAssignmentFromLessonToken(assignment.LessonId, assignment.Id))
        {
            _logger.LogWarning("Assignment {AssignmentId} Details accessed without lesson context for user {UserId}", id, userId);
            SetErrorMessage("يرجى الوصول إلى التكليف من صفحة الدرس (مشاهدة الدورة).");
            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
        }

        // Get existing submission
        var submission = await _context.AssignmentSubmissions
            .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId);

        ViewBag.Enrollment = enrollment;
        ViewBag.Submission = submission;

        return View(assignment);
    }

    /// <summary>
    /// صفحة تسليم التكليف - Submit assignment form (GET)
    /// </summary>
    public async Task<IActionResult> Submit(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get the submission (which may be a virtual one from Index)
        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(s => s.Id == id && s.StudentId == userId);

        // If no submission exists, this might be a direct access - try getting from assignment ID
        if (submission == null)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();
            if (assignment.Lesson?.Module == null)
            {
                _logger.LogWarning("Assignment {AssignmentId} has missing Lesson or Module", id);
                return NotFound();
            }

            // Check enrollment (active only)
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == assignment.Lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);

            if (enrollment == null)
                return Forbid();

            // ZTP: Assignment linked to lesson must be accessed from Learning (tutor) only
            if (assignment.LessonId > 0 && !ValidateAndKeepAssignmentFromLessonToken(assignment.LessonId, assignment.Id))
            {
                _logger.LogWarning("Assignment {AssignmentId} Submit GET accessed without lesson context for user {UserId}", id, userId);
                SetErrorMessage("يرجى الوصول إلى التكليف من صفحة الدرس (مشاهدة الدورة).");
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
            }

            // Create a virtual submission for the form
            submission = new AssignmentSubmission
            {
                AssignmentId = assignment.Id,
                Assignment = assignment,
                StudentId = userId!,
                Status = AssignmentStatus.Pending,
                EnrollmentId = enrollment.Id
            };
        }
        else if (submission.Assignment?.LessonId > 0 && !ValidateAndKeepAssignmentFromLessonToken(submission.Assignment.LessonId, submission.AssignmentId))
        {
            _logger.LogWarning("Assignment {AssignmentId} Submit GET (existing submission) accessed without lesson context for user {UserId}", submission.AssignmentId, userId);
            SetErrorMessage("يرجى الوصول إلى التكليف من صفحة الدرس (مشاهدة الدورة).");
            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = submission.Assignment.LessonId });
        }

        // Check if already submitted (not draft)
        if (submission.Status != AssignmentStatus.Pending && submission.Status != AssignmentStatus.Draft)
        {
            SetWarningMessage("لقد قمت بتسليم هذا الواجب مسبقاً");
            return RedirectToAction(nameof(Details), new { id = submission.AssignmentId });
        }

        return View(submission);
    }

    /// <summary>
    /// تسليم التكليف - Submit assignment (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Submit")]
    public async Task<IActionResult> SubmitPost(int assignmentId, string content, IFormFileCollection? files, string? fileUrl, bool isDraft = false)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        Assignment? assignment = null;
        try
        {
            assignment = await _context.Assignments
                .Include(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                            .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
                return NotFound();
            if (assignment.Lesson?.Module?.Course == null)
            {
                _logger.LogWarning("Assignment {AssignmentId} has missing Lesson/Module/Course", assignmentId);
                return NotFound();
            }

            // Check enrollment (active only)
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.CourseId == assignment.Lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);

            if (enrollment == null)
                return Forbid();

            // ZTP: Assignment linked to lesson - require token; consume only on final submit (not draft)
            if (assignment.LessonId > 0)
            {
                if (isDraft)
                {
                    if (!ValidateAndKeepAssignmentFromLessonToken(assignment.LessonId, assignmentId))
                    {
                        _logger.LogWarning("Assignment {AssignmentId} SaveDraft accessed without lesson context for user {UserId}", assignmentId, userId);
                        SetErrorMessage("يرجى الوصول إلى التكليف من صفحة الدرس (مشاهدة الدورة).");
                        return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                    }
                }
                else if (!ValidateAndConsumeAssignmentFromLessonToken(assignment.LessonId, assignmentId))
                {
                    _logger.LogWarning("Assignment {AssignmentId} Submit POST accessed without valid lesson context for user {UserId}", assignmentId, userId);
                    SetErrorMessage("يرجى الوصول إلى التكليف من صفحة الدرس (مشاهدة الدورة).");
                    return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                }
            }

            // Resolve file URL from uploaded files (first file only, per existing FileUrl single field)
            var resolvedFileUrl = fileUrl;
            if (files != null && files.Count > 0)
            {
                var file = files[0];
                if (file.Length > 0)
                {
                    if (file.Length > MaxAssignmentFileSizeBytes)
                    {
                        SetErrorMessage("حجم الملف يجب أن لا يتجاوز 10 ميجابايت");
                        if (assignment.LessonId > 0)
                            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                        return RedirectToAction(nameof(Details), new { id = assignmentId });
                    }
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !AllowedAssignmentExtensions.Contains(ext))
                    {
                        SetErrorMessage("نوع الملف غير مدعوم. المسموح: PDF, DOC, DOCX, TXT, ZIP, JPG, PNG");
                        if (assignment.LessonId > 0)
                            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                        return RedirectToAction(nameof(Details), new { id = assignmentId });
                    }
                    try
                    {
                        resolvedFileUrl = await _fileStorage.UploadAsync(file, "assignments");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Assignment file upload failed for user {UserId}", userId);
                        SetErrorMessage("فشل رفع الملف. حاول مرة أخرى.");
                        if (assignment.LessonId > 0)
                            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                        return RedirectToAction(nameof(Details), new { id = assignmentId });
                    }
                }
            }

            // Validation
            if (!isDraft)
            {
                if (string.IsNullOrWhiteSpace(content) && string.IsNullOrEmpty(resolvedFileUrl))
                {
                    SetErrorMessage("يجب إضافة محتوى أو رفع ملف للتسليم");
                    if (assignment.LessonId > 0)
                        return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }

                if (!string.IsNullOrWhiteSpace(content) && content.Length < 10)
                {
                    SetErrorMessage("يجب أن يكون المحتوى 10 أحرف على الأقل");
                    if (assignment.LessonId > 0)
                        return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }
                const int maxContentLength = 50000;
                if (content != null && content.Length > maxContentLength)
                {
                    SetErrorMessage("المحتوى طويل جداً. الحد الأقصى 50000 حرف.");
                    if (assignment.LessonId > 0)
                        return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                    return RedirectToAction(nameof(Details), new { id = assignmentId });
                }
            }

            // Check existing submission
            var existingSubmission = await _context.AssignmentSubmissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == userId);

            if (existingSubmission != null && existingSubmission.Status != AssignmentStatus.Draft && !isDraft)
            {
                SetErrorMessage("لقد قمت بتسليم هذا التكليف من قبل");
                if (assignment.LessonId > 0)
                    return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
                return RedirectToAction(nameof(Details), new { id = assignmentId });
            }

            var isLate = assignment.DueDate.HasValue && DateTime.UtcNow > assignment.DueDate;

            AssignmentSubmission? submission = null;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                if (existingSubmission != null)
                {
                    existingSubmission.Content = content;
                    existingSubmission.FileUrl = resolvedFileUrl;
                    existingSubmission.Status = isDraft ? AssignmentStatus.Draft : AssignmentStatus.Submitted;
                    existingSubmission.SubmittedAt = isDraft ? existingSubmission.SubmittedAt : DateTime.UtcNow;
                    existingSubmission.IsLate = isDraft ? existingSubmission.IsLate : isLate;
                    submission = existingSubmission;
                }
                else
                {
                    submission = new AssignmentSubmission
                    {
                        AssignmentId = assignmentId,
                        StudentId = userId!,
                        EnrollmentId = enrollment.Id,
                        Content = content,
                        FileUrl = resolvedFileUrl,
                        Status = isDraft ? AssignmentStatus.Draft : AssignmentStatus.Submitted,
                        SubmittedAt = DateTime.UtcNow,
                        IsLate = isLate
                    };

                    _context.AssignmentSubmissions.Add(submission);
                }

                if (!isDraft)
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = userId!,
                        ActivityType = "AssignmentSubmission",
                        Description = $"تسليم تكليف: {assignment.Title}",
                        EntityType = "Assignment",
                        EntityId = assignmentId,
                        EntityName = assignment.Title,
                        Timestamp = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                // Send notifications only if not draft
                if (!isDraft)
                {
                    // Send email to instructor
                    if (assignment.Lesson.Module.Course.Instructor?.Email != null)
                    {
                        await _emailService.SendAssignmentSubmittedAsync(
                            assignment.Lesson.Module.Course.Instructor.Email,
                            assignment.Lesson.Module.Course.Instructor.FullName,
                            enrollment.Student.FullName,
                            assignment.Title,
                            assignment.Lesson.Module.Course.Title
                        );

                        _logger.LogInformation("Sent assignment submission email to instructor {InstructorId} for assignment {AssignmentId}", 
                            assignment.Lesson.Module.Course.InstructorId, assignmentId);
                    }

                    // Notify instructor (in-app + RealTime + Web Push) via unified service
                    try
                    {
                        await _instructorNotificationService.NotifyAssignmentSubmittedAsync(
                            assignment.Lesson.Module.Course.InstructorId,
                            submission!.Id,
                            assignment.Title,
                            enrollment.Student?.FullName ?? "طالب");
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "Instructor notification failed for assignment submission {SubmissionId}", submission.Id);
                    }

                    // Send confirmation email to student
                    if (enrollment.Student?.Email != null)
                    {
                        var confirmationSubject = "تأكيد تسليم التكليف";
                        var confirmationBody = $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {enrollment.Student.FullName}</h2>
                            <p>تم تسليم تكليف <strong>{assignment.Title}</strong> بنجاح.</p>
                            <p><strong>الدورة:</strong> {assignment.Lesson.Module.Course.Title}</p>
                            <p><strong>تاريخ التسليم:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm}</p>
                            {(isLate ? "<p style='color: orange;'><strong>⚠ ملاحظة:</strong> التسليم متأخر عن الموعد المحدد.</p>" : "")}
                            <p>سيتم مراجعة التسليم من قبل المدرس قريباً.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>";

                        await _emailService.SendEmailAsync(
                            enrollment.Student.Email,
                            confirmationSubject,
                            confirmationBody,
                            true
                        );
                    }

                    // Create in-app notification for student
                    var studentNotification = new Notification
                    {
                        UserId = userId!,
                        Title = "تم تسليم التكليف بنجاح",
                        Message = $"تم تسليم تكليف {assignment.Title} بنجاح" + 
                                 (isLate ? " (متأخر)" : ". سيتم مراجعته قريباً"),
                        Type = NotificationType.AssignmentSubmitted,
                        ActionUrl = $"/Student/Assignments/Details/{assignmentId}",
                        ActionText = "عرض التفاصيل",
                        IconClass = "fas fa-check-circle",
                        IsRead = false
                    };
                    _context.Notifications.Add(studentNotification);

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Student {StudentId} submitted assignment {AssignmentId}, Late: {IsLate}", 
                        userId, assignmentId, isLate);
                }
                else
                {
                    _logger.LogInformation("Student {StudentId} saved draft for assignment {AssignmentId}", 
                        userId, assignmentId);
                }
            });

            SetSuccessMessage(isDraft ? "تم حفظ المسودة بنجاح" : "تم تسليم التكليف بنجاح. تم إرسال إشعار للمدرس.");
            if (assignment.LessonId > 0)
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
            return RedirectToAction(nameof(Details), new { id = assignmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting assignment {AssignmentId} by student {StudentId}", 
                assignmentId, userId);
            SetErrorMessage("حدث خطأ أثناء تسليم التكليف");
            if (assignment?.LessonId > 0)
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = assignment.LessonId });
            return RedirectToAction(nameof(Details), new { id = assignmentId });
        }
    }

    /// <summary>
    /// عرض التسليم - View submission (redirects to Details with the assignment ID)
    /// </summary>
    public async Task<IActionResult> ViewSubmission(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.Id == id && s.StudentId == userId);

        if (submission == null)
            return NotFound();

        // Redirect to assignment details which handles the submission display
        return RedirectToAction(nameof(Details), new { id = submission.AssignmentId });
    }

    /// <summary>
    /// حفظ المسودة - Save draft (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int assignmentId, string content, IFormFileCollection? files, string? fileUrl)
    {
        return await SubmitPost(assignmentId, content, files, fileUrl, isDraft: true);
    }

    /// <summary>Validate AllowAssignmentFromLesson token without removing (for GET).</summary>
    private bool ValidateAndKeepAssignmentFromLessonToken(int lessonId, int assignmentId)
    {
        var token = HttpContext.Session.GetString("AllowAssignmentFromLesson");
        if (string.IsNullOrEmpty(token)) return false;
        var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var allowedLessonId) ||
            !int.TryParse(parts[1], out var allowedAssignmentId) ||
            !long.TryParse(parts[2], out var expiryTicks) ||
            allowedLessonId != lessonId ||
            allowedAssignmentId != assignmentId ||
            new DateTime(expiryTicks) < DateTime.UtcNow)
            return false;
        return true;
    }

    /// <summary>Validate and consume AllowAssignmentFromLesson token (for POST).</summary>
    private bool ValidateAndConsumeAssignmentFromLessonToken(int lessonId, int assignmentId)
    {
        var token = HttpContext.Session.GetString("AllowAssignmentFromLesson");
        HttpContext.Session.Remove("AllowAssignmentFromLesson");
        if (string.IsNullOrEmpty(token)) return false;
        var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var allowedLessonId) ||
            !int.TryParse(parts[1], out var allowedAssignmentId) ||
            !long.TryParse(parts[2], out var expiryTicks) ||
            allowedLessonId != lessonId ||
            allowedAssignmentId != assignmentId ||
            new DateTime(expiryTicks) < DateTime.UtcNow)
            return false;
        return true;
    }
}

