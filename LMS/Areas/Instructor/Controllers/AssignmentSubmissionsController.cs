using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة تسليمات التكليفات - Assignment Submissions Controller
/// </summary>
public class AssignmentSubmissionsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AssignmentSubmissionsController> _logger;

    public AssignmentSubmissionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<AssignmentSubmissionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التسليمات - Submissions List
    /// </summary>
    public async Task<IActionResult> Index(int? assignmentId, int? courseId, AssignmentStatus? status)
    {
        var userId = _currentUserService.UserId;

        var query = _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(s => s.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(s => s.Assignment.Lesson.Module.Course.InstructorId == userId)
            .AsQueryable();

        if (assignmentId.HasValue)
        {
            query = query.Where(s => s.AssignmentId == assignmentId.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(s => s.Assignment.Lesson.Module.CourseId == courseId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var submissions = await query
            .OrderByDescending(s => s.SubmittedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.Assignments = await _context.Assignments
            .Include(a => a.Lesson.Module.Course)
            .Where(a => a.Lesson.Module.Course.InstructorId == userId)
            .OrderBy(a => a.Title)
            .ToListAsync();

        ViewBag.AssignmentId = assignmentId;
        ViewBag.CourseId = courseId;
        ViewBag.Status = status;

        return View(submissions);
    }

    /// <summary>
    /// التسليمات المعلقة - Pending Submissions (Awaiting Grading)
    /// </summary>
    public async Task<IActionResult> Pending(int? courseId, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(s => s.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(s => s.Assignment.Lesson.Module.Course.InstructorId == userId &&
                       s.Status == AssignmentStatus.Submitted)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(s => s.Assignment.Lesson.Module.CourseId == courseId.Value);
        }

        var totalCount = await query.CountAsync();
        var submissions = await query
            .OrderByDescending(s => s.SubmittedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

        _logger.LogInformation("Instructor {InstructorId} viewed pending submissions. Total: {Count}", 
            userId, totalCount);

        return View(submissions);
    }

    /// <summary>
    /// تفاصيل التسليم - Submission Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(s => s.Enrollment)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(s => s.Id == id && 
                s.Assignment.Lesson.Module.Course.InstructorId == userId);

        if (submission == null)
            return NotFound();

        return View(submission);
    }

    /// <summary>
    /// تقييم التسليم - Grade Submission
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Grade(int id)
    {
        var userId = _currentUserService.UserId;

        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(s => s.Enrollment)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(s => s.Id == id && 
                s.Assignment.Lesson.Module.Course.InstructorId == userId);

        if (submission == null)
            return NotFound();

        var viewModel = new AssignmentSubmissionGradeViewModel
        {
            SubmissionId = submission.Id,
            StudentName = submission.Enrollment.Student.FullName,
            AssignmentTitle = submission.Assignment.Title,
            MaxPoints = submission.Assignment.MaxPoints,
            SubmittedAt = submission.SubmittedAt,
            TextSubmission = submission.TextSubmission,
            AttachmentUrls = submission.AttachmentUrls,
            CurrentGrade = submission.Grade,
            CurrentFeedback = submission.Feedback,
            CurrentStatus = submission.Status
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التقييم - Save Grade
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(int id, AssignmentSubmissionGradeViewModel model)
    {
        if (id != model.SubmissionId)
            return BadRequest();

        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Lesson.Module.Course)
                .Include(s => s.Enrollment)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(s => s.Id == id && 
                    s.Assignment.Lesson.Module.Course.InstructorId == userId);

            if (submission == null)
            {
                _logger.LogWarning("Assignment submission {SubmissionId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            // Validate submission can be graded
            if (submission.Status == AssignmentStatus.NotSubmitted)
            {
                _logger.LogWarning("Cannot grade submission {SubmissionId} with status NotSubmitted", id);
                SetErrorMessage("لا يمكن تقييم تكليف لم يتم تسليمه");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Use BusinessRuleHelper for grade validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateAssignmentGrade(
                model.Grade,
                submission.Assignment.MaxPoints,
                model.Status);

            if (!isValid)
            {
                _logger.LogWarning("Grade validation failed for submission {SubmissionId}: {Reason}", 
                    id, validationReason);
                ModelState.AddModelError(nameof(model.Grade), validationReason!);
                return View(model);
            }

            // Additional validation
            if (model.Status == AssignmentStatus.Graded && string.IsNullOrWhiteSpace(model.Feedback))
            {
                SetWarningMessage("يُفضل إضافة ملاحظات للطالب عند التقييم");
            }

            if (model.Status == AssignmentStatus.Resubmit && string.IsNullOrWhiteSpace(model.Feedback))
            {
                ModelState.AddModelError(nameof(model.Feedback), 
                    "يجب إضافة ملاحظات عند طلب إعادة التسليم");
                return View(model);
            }

            var previousGrade = submission.Grade;
            var previousStatus = submission.Status;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                submission.Grade = model.Grade;
                submission.Feedback = model.Feedback;
                submission.Status = model.Status;
                submission.GradedAt = DateTime.UtcNow;
                submission.GradedBy = userId;

                await _context.SaveChangesAsync();

                var student = submission.Enrollment.Student;
                
                // Send email notification to student
                if (student?.Email != null)
                {
                    try
                    {
                        await _emailService.SendAssignmentGradedAsync(
                            student.Email,
                            student.FullName,
                            submission.Assignment.Title,
                            model.Grade,
                            model.Feedback ?? string.Empty
                        );

                        _logger.LogInformation("Sent grading email to student {StudentId} for assignment {AssignmentId}", 
                            student.Id, submission.AssignmentId);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send grading email to {Email}", student.Email);
                        // Don't fail the transaction if email fails
                    }
                }

                // Create in-app notification with appropriate message
                string notificationMessage;
                string iconClass;

                if (model.Status == AssignmentStatus.Graded)
                {
                    var percentage = (model.Grade / submission.Assignment.MaxPoints) * 100;
                    notificationMessage = $"حصلت على {model.Grade} نقطة من {submission.Assignment.MaxPoints} ({percentage:F1}%)";
                    iconClass = percentage >= submission.Assignment.PassingScore 
                        ? "fas fa-check-circle text-success" 
                        : "fas fa-times-circle text-warning";
                }
                else if (model.Status == AssignmentStatus.Resubmit)
                {
                    notificationMessage = "يُرجى مراجعة الملاحظات وإعادة تسليم التكليف";
                    iconClass = "fas fa-redo text-info";
                }
                else
                {
                    notificationMessage = "تم تحديث حالة تكليفك";
                    iconClass = "fas fa-info-circle";
                }

                var notification = new Notification
                {
                    UserId = submission.Enrollment.StudentId,
                    Title = $"تم تقييم تكليفك: {submission.Assignment.Title}",
                    Message = notificationMessage,
                    Type = NotificationType.AssignmentGraded,
                    ActionUrl = $"/Student/Assignments/Details/{submission.Id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = iconClass,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Assignment submission {SubmissionId} graded by instructor {InstructorId}. Grade: {Grade}/{MaxPoints}, Status: {Status}", 
                id, userId, model.Grade, submission.Assignment.MaxPoints, model.Status);

            if (previousGrade.HasValue && previousGrade != model.Grade)
            {
                _logger.LogInformation("Grade changed from {OldGrade} to {NewGrade}", previousGrade, model.Grade);
            }

            SetSuccessMessage("تم حفظ التقييم بنجاح وإرسال الإشعار للطالب");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grading assignment submission {SubmissionId}", id);
            SetErrorMessage("حدث خطأ أثناء حفظ التقييم. يرجى المحاولة مرة أخرى");
            return View(model);
        }
    }

    /// <summary>
    /// تقييم متعدد - Bulk Grade
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BulkGrade(int assignmentId)
    {
        var userId = _currentUserService.UserId;

        var assignment = await _context.Assignments
            .Include(a => a.Lesson.Module.Course)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && 
                a.Lesson.Module.Course.InstructorId == userId);

        if (assignment == null)
            return NotFound();

        var submissions = await _context.AssignmentSubmissions
            .Include(s => s.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(s => s.AssignmentId == assignmentId && 
                s.Status == AssignmentStatus.Submitted)
            .OrderBy(s => s.Enrollment.Student.FirstName)
            .ToListAsync();

        ViewBag.Assignment = assignment;
        ViewBag.Submissions = submissions;

        return View();
    }

    /// <summary>
    /// حفظ التقييم الجماعي - Save Bulk Grades
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkGrade(int assignmentId, Dictionary<int, decimal> grades, Dictionary<int, AssignmentStatus> statuses, string? bulkFeedback)
    {
        var userId = _currentUserService.UserId;

        // Validate assignment belongs to instructor
        var assignment = await _context.Assignments
            .Include(a => a.Lesson.Module.Course)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && 
                a.Lesson.Module.Course.InstructorId == userId);

        if (assignment == null)
        {
            _logger.LogWarning("BulkGrade attempted for unauthorized assignment {AssignmentId} by instructor {InstructorId}", 
                assignmentId, userId);
            return NotFound();
        }

        if (grades == null || !grades.Any())
        {
            SetErrorMessage("لم يتم تحديد أي درجات للحفظ");
            return RedirectToAction(nameof(BulkGrade), new { assignmentId });
        }

        var successCount = 0;
        var errorCount = 0;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                foreach (var (submissionId, grade) in grades)
                {
                    // Skip empty grades
                    if (grade < 0) continue;

                    var submission = await _context.AssignmentSubmissions
                        .Include(s => s.Enrollment)
                            .ThenInclude(e => e.Student)
                        .FirstOrDefaultAsync(s => s.Id == submissionId && 
                            s.AssignmentId == assignmentId);

                    if (submission == null)
                    {
                        _logger.LogWarning("BulkGrade: Submission {SubmissionId} not found for assignment {AssignmentId}", 
                            submissionId, assignmentId);
                        errorCount++;
                        continue;
                    }

                    // Validate grade
                    if (grade < 0 || grade > assignment.MaxPoints)
                    {
                        _logger.LogWarning("BulkGrade: Invalid grade {Grade} for submission {SubmissionId}. Max: {MaxPoints}", 
                            grade, submissionId, assignment.MaxPoints);
                        errorCount++;
                        continue;
                    }

                    // Get status or default to Graded
                    var status = statuses?.GetValueOrDefault(submissionId, AssignmentStatus.Graded) ?? AssignmentStatus.Graded;

                    // Update submission
                    submission.Grade = grade;
                    submission.Status = status;
                    submission.GradedAt = DateTime.UtcNow;
                    submission.GradedBy = userId;

                    // Append bulk feedback if provided
                    if (!string.IsNullOrWhiteSpace(bulkFeedback))
                    {
                        submission.Feedback = string.IsNullOrEmpty(submission.Feedback)
                            ? bulkFeedback
                            : submission.Feedback + "\n\n" + bulkFeedback;
                    }

                    // Create notification for student
                    var percentage = (grade / assignment.MaxPoints) * 100;
                    var notification = new Notification
                    {
                        UserId = submission.Enrollment.StudentId,
                        Title = $"تم تقييم تكليفك: {assignment.Title}",
                        Message = $"حصلت على {grade} نقطة من {assignment.MaxPoints} ({percentage:F1}%)",
                        Type = NotificationType.AssignmentGraded,
                        ActionUrl = $"/Student/Assignments/Details/{submission.Id}",
                        ActionText = "عرض التفاصيل",
                        IconClass = percentage >= (assignment.PassingScore ?? 60) 
                            ? "fas fa-check-circle text-success" 
                            : "fas fa-times-circle text-warning",
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                    successCount++;
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "BulkGrade completed for assignment {AssignmentId} by instructor {InstructorId}. Success: {Success}, Errors: {Errors}", 
                assignmentId, userId, successCount, errorCount);

            if (errorCount > 0)
            {
                SetWarningMessage($"تم حفظ {successCount} تقييم بنجاح. فشل حفظ {errorCount} تقييم.");
            }
            else if (successCount > 0)
            {
                SetSuccessMessage($"تم حفظ {successCount} تقييم بنجاح وإرسال الإشعارات للطلاب");
            }
            else
            {
                SetWarningMessage("لم يتم حفظ أي تقييمات");
            }

            return RedirectToAction(nameof(Index), new { assignmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkGrade for assignment {AssignmentId}", assignmentId);
            SetErrorMessage("حدث خطأ أثناء حفظ التقييمات. يرجى المحاولة مرة أخرى");
            return RedirectToAction(nameof(BulkGrade), new { assignmentId });
        }
    }

    /// <summary>
    /// السماح بإعادة التسليم - Allow Resubmission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AllowResubmission(int id, string? reason)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment.Lesson.Module.Course)
                .Include(s => s.Enrollment)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(s => s.Id == id && 
                    s.Assignment.Lesson.Module.Course.InstructorId == userId);

            if (submission == null)
            {
                _logger.LogWarning("Assignment submission {SubmissionId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            // Validate resubmission is allowed
            if (submission.Status == AssignmentStatus.NotSubmitted)
            {
                SetErrorMessage("لا يمكن طلب إعادة تسليم لتكليف لم يتم تسليمه");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                SetErrorMessage("يجب تحديد سبب طلب إعادة التسليم");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                var previousStatus = submission.Status;
                submission.Status = AssignmentStatus.Resubmit;
                
                // Append reason to feedback
                var resubmitNote = $"\n\n[طلب إعادة تسليم - {DateTime.UtcNow:yyyy-MM-dd HH:mm}]: {reason}";
                submission.Feedback = string.IsNullOrEmpty(submission.Feedback) 
                    ? resubmitNote.Trim()
                    : submission.Feedback + resubmitNote;

                submission.GradedAt = DateTime.UtcNow;
                submission.GradedBy = userId;

                await _context.SaveChangesAsync();

                // Create notification for student
                var notification = new Notification
                {
                    UserId = submission.Enrollment.StudentId,
                    Title = $"طلب إعادة تسليم: {submission.Assignment.Title}",
                    Message = $"يُرجى مراجعة الملاحظات وإعادة تسليم التكليف. السبب: {reason}",
                    Type = NotificationType.AssignmentGraded,
                    ActionUrl = $"/Student/Assignments/Details/{submission.Id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = "fas fa-redo text-info",
                    IsRead = false
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Resubmission allowed for submission {SubmissionId} by instructor {InstructorId}. Reason: {Reason}", 
                id, userId, reason);

            SetSuccessMessage("تم السماح بإعادة التسليم وإرسال إشعار للطالب");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allowing resubmission for submission {SubmissionId}", id);
            SetErrorMessage("حدث خطأ أثناء معالجة الطلب");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات التسليمات - Submissions Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int? courseId)
    {
        var userId = _currentUserService.UserId;

        var query = _context.AssignmentSubmissions
            .Include(s => s.Assignment.Lesson.Module.Course)
            .Where(s => s.Assignment.Lesson.Module.Course.InstructorId == userId)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(s => s.Assignment.Lesson.Module.CourseId == courseId.Value);
        }

        var submissions = await query.ToListAsync();

        var stats = new AssignmentSubmissionStatisticsViewModel
        {
            TotalSubmissions = submissions.Count,
            PendingCount = submissions.Count(s => s.Status == AssignmentStatus.Submitted),
            GradedCount = submissions.Count(s => s.Status == AssignmentStatus.Graded),
            LateCount = submissions.Count(s => s.IsLate),
            AverageGrade = submissions.Where(s => s.Grade.HasValue).Any() 
                ? submissions.Where(s => s.Grade.HasValue).Average(s => s.Grade!.Value) 
                : 0,
            AverageGradingTime = CalculateAverageGradingTime(submissions)
        };

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .ToListAsync();

        ViewBag.CourseId = courseId;

        return View(stats);
    }

    private double CalculateAverageGradingTime(List<Domain.Entities.Assessments.AssignmentSubmission> submissions)
    {
        var gradedSubmissions = submissions.Where(s => s.GradedAt.HasValue).ToList();
        if (!gradedSubmissions.Any()) return 0;

        var totalHours = gradedSubmissions
            .Sum(s => (s.GradedAt!.Value - s.SubmittedAt).TotalHours);

        return totalHours / gradedSubmissions.Count;
    }
}

