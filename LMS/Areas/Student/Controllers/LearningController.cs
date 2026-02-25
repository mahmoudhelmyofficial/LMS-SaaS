using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Entities.Certifications;
using LMS.Domain.Entities.Gamification;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Helpers;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التعلم - Learning Controller (Watching lessons, tracking progress)
/// </summary>
public class LearningController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<LearningController> _logger;

    public LearningController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<LearningController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// مشاهدة الدرس - Watch lesson (with smart resume)
    /// Supports: quiz/assignment in-tutor (no redirect). Optional: quizId, assignmentId, attemptId, mode (take).
    /// </summary>
    public async Task<IActionResult> Lesson(int lessonId, int? quizId, int? assignmentId, int? attemptId, string? mode)
    {
        try
        {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
            .Include(l => l.Resources)
            .Include(l => l.Quizzes)
                .ThenInclude(q => q.Questions)
            .Include(l => l.Assignments)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
            return NotFound();

        var course = lesson.Module.Course;
        bool isCourseInstructor = course.InstructorId == userId;
        bool isCoInstructor = false;
        if (!isCourseInstructor)
            isCoInstructor = await _context.CourseInstructors
                .AnyAsync(ci => ci.CourseId == course.Id && ci.InstructorId == userId && !ci.IsDeleted && ci.IsActive);
        bool isAdmin = User.IsInRole("Admin");
        bool isInstructorAccess = isCourseInstructor || isCoInstructor || isAdmin;

        var enrollment = await _context.Enrollments
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.CourseId == course.Id && e.StudentId == userId);

        if (enrollment == null && !lesson.IsFreePreview && !lesson.IsPreviewable && !isInstructorAccess)
            return Forbid();

        LessonProgress? progress = null;
        if (enrollment != null)
        {
            progress = enrollment.LessonProgress.FirstOrDefault(lp => lp.LessonId == lessonId);
            if (progress == null)
            {
                progress = new LessonProgress
                {
                    EnrollmentId = enrollment.Id,
                    LessonId = lessonId,
                    StartedAt = DateTime.UtcNow,
                    WatchedSeconds = 0
                };
                _context.LessonProgress.Add(progress);
                await _context.SaveChangesAsync();
            }
            enrollment.LastAccessedAt = DateTime.UtcNow;
            enrollment.LastLessonId = lessonId;
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                ActivityType = "LessonView",
                Description = $"مشاهدة درس: {lesson.Title}",
                EntityType = "Lesson",
                EntityId = lessonId,
                EntityName = lesson.Title,
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Student {StudentId} accessed lesson {LessonId}", userId, lessonId);
        }
        else if (isInstructorAccess)
            _logger.LogInformation("Instructor/Admin {UserId} accessed lesson {LessonId}", userId, lessonId);

        ViewBag.ResumePosition = progress?.WatchedSeconds ?? 0;
        ViewBag.Enrollment = enrollment;
        ViewBag.Progress = progress;
        ViewBag.IsInstructorAccess = isInstructorAccess;
        ViewBag.Course = course;

        var allLessons = course.Modules
            .OrderBy(m => m.OrderIndex)
            .SelectMany(m => m.Lessons.OrderBy(l => l.OrderIndex))
            .ToList();
        var currentIndex = allLessons.FindIndex(l => l.Id == lessonId);
        ViewBag.PreviousLesson = currentIndex > 0 ? allLessons[currentIndex - 1] : null;
        ViewBag.NextLesson = currentIndex < allLessons.Count - 1 ? allLessons[currentIndex + 1] : null;

        // --- In-tutor: Take Quiz (attemptId + mode=take) ---
        if (attemptId.HasValue && string.Equals(mode, "take", StringComparison.OrdinalIgnoreCase) && enrollment != null && enrollment.Status == EnrollmentStatus.Active)
        {
            var attempt = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(qa => qa.Id == attemptId.Value && qa.StudentId == userId);

            if (attempt != null && attempt.Quiz?.LessonId == lessonId && attempt.Status == QuizAttemptStatus.InProgress)
            {
                var q = attempt.Quiz;
                var questions = q.Questions
                    .OrderBy(x => q.RandomizeQuestions ? Guid.NewGuid().GetHashCode() : x.OrderIndex)
                    .Select(x => new QuizQuestionViewModel
                    {
                        Id = x.Id,
                        QuestionText = x.QuestionText,
                        QuestionType = x.QuestionType.ToString(),
                        Points = x.Points,
                        ImageUrl = x.ImageUrl,
                        Options = x.Options
                            .OrderBy(o => q.RandomizeOptions ? Guid.NewGuid().GetHashCode() : o.OrderIndex)
                            .Select(o => new QuizOptionViewModel { Id = o.Id, Text = o.OptionText })
                            .ToList()
                    })
                    .ToList();

                var proctoring = await _context.ProctoringSettings
                    .FirstOrDefaultAsync(p => p.QuizId == q.Id);

                ViewBag.TutorPanel = "takequiz";
                ViewBag.TakeQuizModel = new TakeQuizViewModel
                {
                    AttemptId = attempt.Id,
                    QuizId = q.Id,
                    QuizTitle = q.Title,
                    TimeLimit = q.TimeLimit,
                    Questions = questions,
                    StartTime = attempt.StartedAt,
                    RequiresProctoring = q.RequiresProctoring && (proctoring == null || proctoring.IsEnabled),
                    ProcPreventTabSwitch = proctoring?.PreventTabSwitch ?? true,
                    ProcPreventCopyPaste = proctoring?.PreventCopyPaste ?? true,
                    ProcDisableRightClick = proctoring?.DisableRightClick ?? true,
                    ProcRequireFullscreen = proctoring?.RequireFullscreen ?? false,
                    ProcRequireWebcam = proctoring?.RequireWebcam ?? false,
                    ProcCaptureScreenshots = proctoring?.CaptureScreenshots ?? false,
                    ProcScreenshotInterval = proctoring?.ScreenshotInterval ?? 60,
                    ProcMaxWarnings = proctoring?.MaxTabSwitchWarnings ?? 3,
                    ProcAutoTerminate = proctoring?.AutoTerminate ?? false
                };
                ViewBag.LessonId = lessonId;
                return View(lesson);
            }
        }

        // --- In-tutor: Quiz Start (quizId only) ---
        if (quizId.HasValue && enrollment != null && enrollment.Status == EnrollmentStatus.Active)
        {
            var quiz = lesson.Quizzes?.FirstOrDefault(q => q.Id == quizId.Value && q.IsActive);
            if (quiz != null)
            {
                if (quiz.AvailableFrom.HasValue && quiz.AvailableFrom.Value > DateTime.UtcNow)
                {
                    SetErrorMessage($"هذا الاختبار متاح في {quiz.AvailableFrom.Value:yyyy-MM-dd HH:mm}");
                    return RedirectToAction(nameof(Lesson), new { lessonId });
                }
                if (quiz.AvailableUntil.HasValue && quiz.AvailableUntil.Value < DateTime.UtcNow)
                {
                    SetErrorMessage("انتهت فترة توفر هذا الاختبار");
                    return RedirectToAction(nameof(Lesson), new { lessonId });
                }
                var previousAttempts = await _context.QuizAttempts
                    .CountAsync(qa => qa.QuizId == quiz.Id && qa.StudentId == userId);
                if (quiz.MaxAttempts.HasValue && previousAttempts >= quiz.MaxAttempts.Value)
                {
                    SetErrorMessage("لا توجد محاولات متبقية لهذا الاختبار");
                    return RedirectToAction(nameof(Lesson), new { lessonId });
                }

                ViewBag.TutorPanel = "quizstart";
                ViewBag.QuizStartModel = new StartQuizViewModel
                {
                    Quiz = quiz,
                    CourseName = course.Title,
                    ModuleName = lesson.Module.Title,
                    LessonName = lesson.Title,
                    PreviousAttempts = previousAttempts,
                    RemainingAttempts = quiz.MaxAttempts.HasValue ? quiz.MaxAttempts.Value - previousAttempts : null
                };
                ViewBag.LessonId = lessonId;
                return View(lesson);
            }
        }

        // --- In-tutor: Assignment (assignmentId) ---
        if (assignmentId.HasValue && enrollment != null && enrollment.Status == EnrollmentStatus.Active)
        {
            var assignment = lesson.Assignments?.FirstOrDefault(a => a.Id == assignmentId.Value);
            if (assignment != null)
            {
                var expiry = DateTime.UtcNow.AddMinutes(30).Ticks;
                HttpContext.Session.SetString("AllowAssignmentFromLesson", $"{lessonId}:{assignment.Id}:{expiry}");

                var submission = await _context.AssignmentSubmissions
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Lesson)
                            .ThenInclude(l => l.Module)
                                .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(s => s.AssignmentId == assignment.Id && s.StudentId == userId);

                if (submission == null)
                {
                    submission = new AssignmentSubmission
                    {
                        AssignmentId = assignment.Id,
                        Assignment = assignment,
                        StudentId = userId,
                        Status = AssignmentStatus.Pending,
                        EnrollmentId = enrollment.Id
                    };
                }

                if (submission.Status != AssignmentStatus.Pending && submission.Status != AssignmentStatus.Draft)
                {
                    SetWarningMessage("لقد قمت بتسليم هذا الواجب مسبقاً");
                    return RedirectToAction(nameof(Lesson), new { lessonId });
                }

                if (assignment.Lesson == null)
                    assignment.Lesson = lesson;

                ViewBag.TutorPanel = "assignment";
                ViewBag.AssignmentSubmission = submission;
                ViewBag.LessonId = lessonId;
                return View(lesson);
            }
        }

        ViewBag.TutorPanel = "lesson";
        return View(lesson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lesson {LessonId}", lessonId);
            SetErrorMessage("حدث خطأ في تحميل الدرس");
            return RedirectToAction("Index", "Courses", new { area = "Student" });
        }
    }

    /// <summary>
    /// Start quiz from tutor (POST). Creates attempt and redirects to take-quiz inside Lesson.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartQuizInLesson(int lessonId, int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .Include(l => l.Quizzes)
                .FirstOrDefaultAsync(l => l.Id == lessonId);
            if (lesson == null) return NotFound();

            var quiz = lesson.Quizzes?.FirstOrDefault(q => q.Id == quizId && q.IsActive);
            if (quiz == null) return NotFound();

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);
            if (enrollment == null) return Forbid();

            if (quiz.AvailableFrom.HasValue && quiz.AvailableFrom.Value > DateTime.UtcNow)
            {
                SetErrorMessage("هذا الاختبار غير متاح حالياً");
                return RedirectToAction(nameof(Lesson), new { lessonId });
            }
            if (quiz.AvailableUntil.HasValue && quiz.AvailableUntil.Value < DateTime.UtcNow)
            {
                SetErrorMessage("انتهت فترة توفر هذا الاختبار");
                return RedirectToAction(nameof(Lesson), new { lessonId });
            }
            if (quiz.MaxAttempts.HasValue)
            {
                var count = await _context.QuizAttempts
                    .CountAsync(qa => qa.QuizId == quizId && qa.StudentId == userId);
                if (count >= quiz.MaxAttempts.Value)
                {
                    SetErrorMessage("لا توجد محاولات متبقية");
                    return RedirectToAction(nameof(Lesson), new { lessonId });
                }
            }

            var attemptNumber = await _context.QuizAttempts
                .CountAsync(qa => qa.QuizId == quizId && qa.StudentId == userId) + 1;
            var attempt = new QuizAttempt
            {
                QuizId = quizId,
                StudentId = userId,
                EnrollmentId = enrollment.Id,
                StartedAt = DateTime.UtcNow,
                AttemptNumber = attemptNumber,
                Status = QuizAttemptStatus.InProgress
            };
            _context.QuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {UserId} started quiz {QuizId} from lesson {LessonId} (in-tutor)", userId, quizId, lessonId);
            return RedirectToAction(nameof(Lesson), new { lessonId, attemptId = attempt.Id, mode = "take" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in StartQuizInLesson for lesson {LessonId}, quiz {QuizId}, user {UserId}", lessonId, quizId, userId);
            SetErrorMessage("حدث خطأ أثناء بدء الاختبار");
            return RedirectToAction(nameof(Lesson), new { lessonId });
        }
    }

    /// <summary>
    /// تحديث التقدم - Update progress
    /// Handles enrolled students; instructors/admins skip progress tracking.
    /// CSRF token must be sent in X-CSRF-TOKEN header (matches Program.cs AddAntiforgery).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressRequest request)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        try
        {
            // Check lesson and access first (before transaction)
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                        .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(l => l.Id == request.LessonId);

            if (lesson == null)
                return NotFound();

            var course = lesson.Module.Course;

            // Check if user is instructor/co-instructor/admin (skip progress tracking for them)
            bool isCourseInstructor = course.InstructorId == userId;
            bool isCoInstructor = !isCourseInstructor && await _context.CourseInstructors
                .AnyAsync(ci => ci.CourseId == course.Id && ci.InstructorId == userId && !ci.IsDeleted && ci.IsActive);
            bool isAdmin = User.IsInRole("Admin");
            bool isInstructorAccess = isCourseInstructor || isCoInstructor || isAdmin;

            // For instructors/admins, return success without tracking progress
            if (isInstructorAccess)
            {
                return Ok(new 
                { 
                    success = true, 
                    progressPercentage = 0,
                    courseCompleted = false,
                    lessonCompleted = false,
                    instructorPreview = true
                });
            }

            // Use execution strategy for retry-safe operations
            var strategy = _context.Database.CreateExecutionStrategy();
            
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {

                var enrollment = await _context.Enrollments
                    .Include(e => e.LessonProgress)
                    .Include(e => e.Student)
                    .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

                if (enrollment == null)
                    return NotFound();

                var progress = enrollment.LessonProgress.FirstOrDefault(lp => lp.LessonId == request.LessonId);
                var isFirstCompletion = false;
                
                if (progress == null)
                {
                    progress = new LessonProgress
                    {
                        EnrollmentId = enrollment.Id,
                        LessonId = request.LessonId,
                        StartedAt = DateTime.UtcNow
                    };
                    _context.LessonProgress.Add(progress);
                }

                progress.WatchedSeconds = request.WatchedSeconds;
                progress.LastWatchedAt = DateTime.UtcNow;

                if (request.Completed && !progress.IsCompleted)
                {
                    isFirstCompletion = true;
                    progress.IsCompleted = true;
                    progress.CompletedAt = DateTime.UtcNow;

                    // Log lesson completion for account activity
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = userId!,
                        ActivityType = "LessonComplete",
                        Description = $"إكمال درس: {lesson.Title}",
                        EntityType = "Lesson",
                        EntityId = request.LessonId,
                        EntityName = lesson.Title,
                        Timestamp = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        DurationSeconds = (int?)request.WatchedSeconds
                    });

                    // Update enrollment progress with division by zero protection
                    var totalLessons = await _context.Lessons
                        .CountAsync(l => l.Module.CourseId == lesson.Module.CourseId);

                    var completedLessons = enrollment.LessonProgress.Count(lp => lp.IsCompleted) + 1;
                    var oldProgress = enrollment.ProgressPercentage;
                    enrollment.ProgressPercentage = totalLessons > 0 
                        ? (int)((decimal)completedLessons / totalLessons * 100) 
                        : 0;

                    // Award points for lesson completion (gamification) with idempotency check
                    if (isFirstCompletion)
                    {
                        // Idempotency check: Ensure we haven't already awarded points for this lesson
                        var existingTransaction = await _context.PointTransactions
                            .AnyAsync(pt => pt.UserId == userId && 
                                          pt.Type == "lesson_completion" && 
                                          pt.RelatedEntityType == "Lesson" && 
                                          pt.RelatedEntityId == request.LessonId);

                        if (!existingTransaction)
                        {
                            var student = await _context.Users.FindAsync(userId);
                            if (student != null)
                            {
                                const int pointsPerLesson = 10;
                                student.Points += pointsPerLesson;

                                // Create point transaction
                                var pointTransaction = new PointTransaction
                                {
                                    UserId = userId!,
                                    Points = pointsPerLesson,
                                    Type = "lesson_completion",
                                    Description = $"إكمال درس: {lesson.Title}",
                                    RelatedEntityType = "Lesson",
                                    RelatedEntityId = request.LessonId
                                };
                                _context.PointTransactions.Add(pointTransaction);

                                _logger.LogInformation("Awarded {Points} points to student {StudentId} for completing lesson {LessonId}", 
                                    pointsPerLesson, userId, request.LessonId);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Points already awarded to student {StudentId} for lesson {LessonId}, skipping duplicate", 
                                userId, request.LessonId);
                        }
                    }

                    // Check for course completion
                    if (enrollment.ProgressPercentage >= 100 && enrollment.Status != EnrollmentStatus.Completed)
                    {
                        enrollment.Status = EnrollmentStatus.Completed;
                        enrollment.CompletedAt = DateTime.UtcNow;

                        // Award completion points with idempotency check
                        var existingCourseCompletion = await _context.PointTransactions
                            .AnyAsync(pt => pt.UserId == userId && 
                                          pt.Type == "course_completion" && 
                                          pt.RelatedEntityType == "Course" && 
                                          pt.RelatedEntityId == lesson.Module.CourseId);

                        if (!existingCourseCompletion)
                        {
                            var student = await _context.Users.FindAsync(userId);
                            if (student != null)
                            {
                                const int completionPoints = 100;
                                student.Points += completionPoints;

                                var pointTransaction = new PointTransaction
                                {
                                    UserId = userId!,
                                    Points = completionPoints,
                                    Type = "course_completion",
                                    Description = $"إكمال دورة: {lesson.Module.Course.Title}",
                                    RelatedEntityType = "Course",
                                    RelatedEntityId = lesson.Module.CourseId
                                };
                                _context.PointTransactions.Add(pointTransaction);
                            }
                        }

                        // Generate certificate if course has certificates enabled
                        var certificate = await GenerateCertificateForCompletionAsync(enrollment, userId!, lesson.Module.Course);
                        if (certificate != null)
                        {
                            _logger.LogInformation("Certificate {CertificateId} generated for student {StudentId} completing course {CourseId}", 
                                certificate.Id, userId, lesson.Module.CourseId);
                        }
                        else if (!lesson.Module.Course.HasCertificate)
                        {
                            // Course completed but no certificate enabled - still send congratulations
                            if (enrollment.Student?.Email != null)
                            {
                                await _emailService.SendEmailAsync(
                                    enrollment.Student.Email,
                                    $"تهانينا! لقد أتممت دورة {lesson.Module.Course.Title}",
                                    $@"<html><body dir='rtl'>
                                        <h2>تهانينا! 🎉</h2>
                                        <p>لقد أتممت دورة <strong>{lesson.Module.Course.Title}</strong> بنجاح.</p>
                                        <p>نفخر بإنجازك ونتمنى لك التوفيق في مسيرتك التعليمية!</p>
                                        <br/>
                                        <p>فريق منصة LMS</p>
                                    </body></html>",
                                    true
                                );
                            }

                            // Create notification
                            var notification = new Notification
                            {
                                UserId = userId!,
                                Title = "تهانينا! لقد أتممت الدورة 🎉",
                                Message = $"لقد أكملت دورة {lesson.Module.Course.Title} بنجاح!",
                                Type = NotificationType.CourseCompleted,
                                ActionUrl = $"/Student/Courses/Details/{enrollment.CourseId}",
                                ActionText = "عرض الدورة",
                                IconClass = "fas fa-check-circle",
                                IsRead = false
                            };
                            _context.Notifications.Add(notification);
                        }

                        _logger.LogInformation("Student {StudentId} completed course {CourseId}", 
                            userId, lesson.Module.CourseId);
                    }
                }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new 
                    { 
                        success = true, 
                        progressPercentage = enrollment.ProgressPercentage,
                        courseCompleted = enrollment.Status == EnrollmentStatus.Completed,
                        lessonCompleted = isFirstCompletion
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for lesson {LessonId}, student {StudentId}", 
                request.LessonId, userId);
            return BadRequest(new { success = false, message = "حدث خطأ أثناء تحديث التقدم" });
        }
    }

    /// <summary>
    /// إنشاء شهادة تلقائياً عند إكمال الدورة - Auto-generate certificate on course completion
    /// </summary>
    private async Task<Certificate?> GenerateCertificateForCompletionAsync(
        Enrollment enrollment, string userId, Course course)
    {
        // 1. Check if course has certificates enabled
        if (!course.HasCertificate)
            return null;

        // 2. Idempotency: check for existing certificate
        var alreadyExists = await _context.Certificates
            .AnyAsync(c => c.StudentId == userId && c.CourseId == course.Id);
        if (alreadyExists)
            return null;

        // 3. Fetch default active template (fallback chain)
        var template = await _context.CertificateTemplates
            .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);
        if (template == null)
        {
            template = await _context.CertificateTemplates
                .FirstOrDefaultAsync(t => t.IsActive);
        }
        var templateId = template?.Id ?? 1;
        if (template == null)
        {
            _logger.LogWarning("No active certificate template found. Using fallback TemplateId=1 for course {CourseId}", course.Id);
        }

        // 4. Build student name
        var studentName = ((enrollment.Student?.FirstName ?? "") + " " + (enrollment.Student?.LastName ?? "")).Trim();
        if (string.IsNullOrEmpty(studentName))
            studentName = "طالب";

        // 5. Build instructor name
        var instructorName = ((course.Instructor?.FirstName ?? "") + " " + (course.Instructor?.LastName ?? "")).Trim();

        // 6. Create the certificate entity
        var verificationCode = CertificateHelper.GenerateVerificationCode();
        var certificate = new Certificate
        {
            StudentId = userId,
            CourseId = course.Id,
            EnrollmentId = enrollment.Id,
            TemplateId = templateId,
            CertificateNumber = CertificateHelper.GenerateCertificateNumber(),
            StudentName = studentName,
            CourseName = course.Title,
            InstructorName = string.IsNullOrEmpty(instructorName) ? null : instructorName,
            VerificationCode = verificationCode,
            CompletionDate = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
            IssuedBy = "System",
            Grade = enrollment.FinalGrade ?? 0,
            LearningHours = enrollment.TotalWatchTimeMinutes > 0 ? enrollment.TotalWatchTimeMinutes / 60 : 0,
            IsRevoked = false,
            ExpiryDate = null
        };

        // 8. Save to get the certificate ID
        _context.Certificates.Add(certificate);
        await _context.SaveChangesAsync();

        // 9. Build verification URL and QR code
        var verificationUrl = Url.Action("Verify", "Certificates",
            new { area = "Student", code = certificate.VerificationCode }, Request.Scheme);
        certificate.VerificationUrl = verificationUrl;
        certificate.QRCodeUrl = CertificateHelper.GenerateQRCodeDataUrl(verificationUrl ?? verificationCode);

        // 11. Update enrollment flags
        enrollment.CertificateIssued = true;
        enrollment.CertificateIssuedAt = DateTime.UtcNow;
        enrollment.CertificateId = certificate.Id;

        // 12. Send email notification
        if (enrollment.Student?.Email != null)
        {
            var certificateUrl = Url.Action("Details", "Certificates",
                new { area = "Student", id = certificate.Id }, Request.Scheme);

            await _emailService.SendCourseCompletionEmailAsync(
                enrollment.Student.Email,
                course.Title,
                certificateUrl ?? ""
            );
        }

        // 13. Create notification
        var notification = new Notification
        {
            UserId = userId,
            Title = "تهانينا! لقد أتممت الدورة 🎉",
            Message = $"لقد أكملت دورة {course.Title} بنجاح. شهادتك جاهزة للتحميل!",
            Type = NotificationType.CourseCompleted,
            ActionUrl = $"/Student/Certificates/Details/{certificate.Id}",
            ActionText = "عرض الشهادة",
            IconClass = "fas fa-certificate",
            IsRead = false
        };
        _context.Notifications.Add(notification);

        // 14. Log the event
        _logger.LogInformation(
            "Certificate {CertificateId} (Number: {CertificateNumber}) auto-generated for student {StudentId} completing course {CourseId}",
            certificate.Id, certificate.CertificateNumber, userId, course.Id);

        // 15. Return the certificate
        return certificate;
    }

    /// <summary>
    /// تمييز الدرس كمكتمل - Mark lesson as complete (JSON API)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkComplete(int lessonId)
    {
        _logger.LogInformation("MarkComplete called for lessonId: {LessonId}", lessonId);
        
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, redirect = Url.Action("Login", "Account", new { area = "" }) });
        }

        try
        {
            // Step 1: Get the lesson and course info
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            var courseId = lesson.Module.CourseId;

            // Step 2: Get enrollment
            var enrollment = await _context.Enrollments
                .Include(e => e.LessonProgress)
                .Include(e => e.Student)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == userId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "يجب أن تكون مسجلاً في الدورة" });
            }

            // Step 3: Get or create progress
            var progress = await _context.LessonProgress
                .FirstOrDefaultAsync(lp => lp.EnrollmentId == enrollment.Id && lp.LessonId == lessonId);

            if (progress == null)
            {
                progress = new LessonProgress
                {
                    EnrollmentId = enrollment.Id,
                    LessonId = lessonId,
                    StartedAt = DateTime.UtcNow,
                    WatchedSeconds = lesson.DurationMinutes * 60
                };
                _context.LessonProgress.Add(progress);
            }

            // Step 4: Mark as completed if not already
            bool wasCompleted = progress.IsCompleted;
            if (!progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
                progress.LastWatchedAt = DateTime.UtcNow;

                // Update progress percentage
                var totalLessons = await _context.Lessons.CountAsync(l => l.Module.CourseId == courseId);
                var completedLessons = await _context.LessonProgress
                    .CountAsync(lp => lp.Enrollment.CourseId == courseId && 
                                      lp.Enrollment.StudentId == userId && 
                                      lp.IsCompleted);
                completedLessons++; // Include current lesson
                
                enrollment.ProgressPercentage = totalLessons > 0 
                    ? Math.Min(100, (int)((decimal)completedLessons / totalLessons * 100))
                    : 0;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Lesson {LessonId} marked as complete. Progress: {Progress}%", 
                    lessonId, enrollment.ProgressPercentage);
            }

            // Step 4b: Check for course completion and generate certificate
            if (enrollment.ProgressPercentage >= 100 && enrollment.Status != EnrollmentStatus.Completed)
            {
                enrollment.Status = EnrollmentStatus.Completed;
                enrollment.CompletedAt = DateTime.UtcNow;

                var certificate = await GenerateCertificateForCompletionAsync(enrollment, userId!, enrollment.Course);
                if (certificate == null && !enrollment.Course.HasCertificate)
                {
                    // Course completed but no certificate - send congratulations notification
                    var notification = new Notification
                    {
                        UserId = userId!,
                        Title = "تهانينا! لقد أتممت الدورة 🎉",
                        Message = $"لقد أكملت دورة {enrollment.Course.Title} بنجاح!",
                        Type = NotificationType.CourseCompleted,
                        ActionUrl = $"/Student/Courses/Details/{enrollment.CourseId}",
                        ActionText = "عرض الدورة",
                        IconClass = "fas fa-check-circle",
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();
            }

            // Step 5: Find next lesson
            var nextLessonId = await _context.Lessons
                .Where(l => l.Module.CourseId == courseId)
                .OrderBy(l => l.Module.OrderIndex)
                .ThenBy(l => l.OrderIndex)
                .Where(l => l.Module.OrderIndex > lesson.Module.OrderIndex || 
                           (l.Module.OrderIndex == lesson.Module.OrderIndex && l.OrderIndex > lesson.OrderIndex))
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();

            // Step 6: Return JSON response with redirect URL
            string redirectUrl;
            string message;
            
            if (nextLessonId.HasValue)
            {
                redirectUrl = Url.Action(nameof(Lesson), new { lessonId = nextLessonId.Value })!;
                message = wasCompleted ? "تم تمييز هذا الدرس كمكتمل مسبقاً" : "تم تمييز الدرس كمكتمل ✓";
            }
            else if (enrollment.ProgressPercentage >= 100)
            {
                redirectUrl = Url.Action("Details", "Courses", new { id = courseId })!;
                message = "مبروك! لقد أكملت الدورة بنجاح 🎉";
            }
            else
            {
                // Stay on current lesson (no more lessons)
                redirectUrl = Url.Action(nameof(Lesson), new { lessonId })!;
                message = wasCompleted ? "تم تمييز هذا الدرس كمكتمل مسبقاً" : "أحسنت! لقد أكملت هذا الدرس ✓";
            }

            _logger.LogInformation("MarkComplete success. NextLesson: {NextLessonId}, Redirect: {Redirect}", 
                nextLessonId, redirectUrl);

            return Json(new { 
                success = true, 
                message = message,
                redirect = redirectUrl,
                nextLessonId = nextLessonId,
                progress = enrollment.ProgressPercentage,
                courseCompleted = enrollment.ProgressPercentage >= 100
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking lesson {LessonId} as complete", lessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء حفظ التقدم" });
        }
    }

    /// <summary>
    /// إضافة ملاحظة - Add note
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int lessonId, string content, int? timestamp)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
            return NotFound();

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return NotFound();

        var note = new StudentNote
        {
            EnrollmentId = enrollment.Id,
            LessonId = lessonId,
            Content = content,
            Timestamp = timestamp
        };

        _context.StudentNotes.Add(note);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, noteId = note.Id });
    }

    /// <summary>
    /// إضافة للمفضلة - Add bookmark
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBookmark(int lessonId, int? timestamp, string? title)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

            if (enrollment == null)
                return Json(new { success = false, message = "يجب التسجيل في الدورة أولاً" });

            var bookmark = new Bookmark
            {
                UserId = userId,
                BookmarkType = "Lesson",
                LessonId = lessonId,
                EnrollmentId = enrollment.Id,
                Timestamp = timestamp,
                Title = title ?? lesson.Title
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Bookmark added for lesson {LessonId} by user {UserId}", lessonId, userId);

            return Json(new { success = true, bookmarkId = bookmark.Id, message = "تم حفظ الإشارة المرجعية" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bookmark for lesson {LessonId}", lessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء حفظ الإشارة المرجعية" });
        }
    }

    /// <summary>
    /// تبديل حالة الإشارة المرجعية - Toggle bookmark
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBookmark(int lessonId, int? timestamp, string? title)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

            if (enrollment == null)
                return Json(new { success = false, message = "يجب التسجيل في الدورة أولاً" });

            // Check if bookmark exists
            var existingBookmark = await _context.Bookmarks
                .FirstOrDefaultAsync(b => b.EnrollmentId == enrollment.Id && b.LessonId == lessonId);

            if (existingBookmark != null)
            {
                // Remove existing bookmark
                _context.Bookmarks.Remove(existingBookmark);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bookmark removed for lesson {LessonId} by user {UserId}", lessonId, userId);

                return Json(new { 
                    success = true, 
                    isBookmarked = false, 
                    message = "تم إزالة الإشارة المرجعية" 
                });
            }
            else
            {
                // Add new bookmark
                var bookmark = new Bookmark
                {
                    UserId = userId,
                    BookmarkType = "Lesson",
                    LessonId = lessonId,
                    EnrollmentId = enrollment.Id,
                    Timestamp = timestamp,
                    Title = title ?? lesson.Title
                };

                _context.Bookmarks.Add(bookmark);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bookmark added for lesson {LessonId} by user {UserId}", lessonId, userId);

                return Json(new { 
                    success = true, 
                    isBookmarked = true, 
                    bookmarkId = bookmark.Id, 
                    message = "تم حفظ الإشارة المرجعية" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling bookmark for lesson {LessonId}", lessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء معالجة الإشارة المرجعية" });
        }
    }

    /// <summary>
    /// حذف إشارة مرجعية - Remove bookmark
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBookmark(int bookmarkId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        try
        {
            var bookmark = await _context.Bookmarks
                .FirstOrDefaultAsync(b => b.Id == bookmarkId);

            if (bookmark == null)
            {
                return Json(new { success = false, message = "الإشارة المرجعية غير موجودة" });
            }

            // Allow remove if owner by UserId, or by Enrollment (backward compat when UserId was not set)
            var isOwnerByUser = bookmark.UserId == userId;
            var isOwnerByEnrollment = false;
            if (!isOwnerByUser && bookmark.EnrollmentId.HasValue)
            {
                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.Id == bookmark.EnrollmentId.Value && e.StudentId == userId);
                isOwnerByEnrollment = enrollment != null;
            }
            if (!isOwnerByUser && !isOwnerByEnrollment)
            {
                return Json(new { success = false, message = "الإشارة المرجعية غير موجودة" });
            }

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Bookmark {BookmarkId} removed by user {UserId}", bookmarkId, userId);

            return Json(new { success = true, message = "تم حذف الإشارة المرجعية" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing bookmark {BookmarkId}", bookmarkId);
            return Json(new { success = false, message = "حدث خطأ أثناء حذف الإشارة المرجعية" });
        }
    }

    /// <summary>
    /// الحصول على حالة الإشارة المرجعية - Get bookmark status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBookmarkStatus(int lessonId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, isBookmarked = false });
        }

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return Json(new { success = false, isBookmarked = false });

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

            if (enrollment == null)
                return Json(new { success = true, isBookmarked = false });

            var isBookmarked = await _context.Bookmarks
                .AnyAsync(b => b.EnrollmentId == enrollment.Id && b.LessonId == lessonId);

            return Json(new { success = true, isBookmarked });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookmark status for lesson {LessonId}", lessonId);
            return Json(new { success = false, isBookmarked = false });
        }
    }

    /// <summary>
    /// بدء اختبار من الدرس (رابط خارجي فقط - للدرس استخدم StartQuizInLesson POST) - Start quiz from lesson (external link only; in-lesson uses StartQuizInLesson).
    /// Sets one-time session token and redirects to Quizzes/Start; Start then redirects lesson-linked quizzes to Lesson.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartQuizFromLesson(int lessonId, int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .Include(l => l.Quizzes)
            .FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson == null) return NotFound();

        var quiz = lesson.Quizzes?.FirstOrDefault(q => q.Id == quizId && q.IsActive);
        if (quiz == null) return NotFound();

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);
        if (enrollment == null) return Forbid();

        var expiry = DateTime.UtcNow.AddMinutes(5).Ticks;
        HttpContext.Session.SetString("AllowQuizFromLesson", $"{lessonId}:{quizId}:{expiry}");
        _logger.LogInformation("Student {UserId} started quiz {QuizId} from lesson {LessonId}", userId, quizId, lessonId);
        return RedirectToAction("Start", "Quizzes", new { area = "Student", quizId });
    }

    /// <summary>
    /// بدء تكليف من الدرس (ZTP - من Tutor فقط) - Start assignment from lesson (access only from Learning)
    /// Sets one-time session token and redirects to Assignments/Submit; Submit validates token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartAssignmentFromLesson(int lessonId, int assignmentId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account", new { area = "" });

        var lesson = await _context.Lessons
            .Include(l => l.Module)
            .Include(l => l.Assignments)
            .FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson == null) return NotFound();

        var assignment = lesson.Assignments?.FirstOrDefault(a => a.Id == assignmentId);
        if (assignment == null) return NotFound();

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);
        if (enrollment == null) return Forbid();

        var expiry = DateTime.UtcNow.AddMinutes(30).Ticks;
        HttpContext.Session.SetString("AllowAssignmentFromLesson", $"{lessonId}:{assignmentId}:{expiry}");
        _logger.LogInformation("Student {UserId} started assignment {AssignmentId} from lesson {LessonId}", userId, assignmentId, lessonId);
        return RedirectToAction("Submit", "Assignments", new { area = "Student", id = assignmentId });
    }

    /// <summary>
    /// المتابعة/استئناف التعلم - Continue learning (smart resume)
    /// </summary>
    public async Task<IActionResult> Continue(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(e => e.LessonProgress)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

        if (enrollment == null)
            return NotFound();

        // Smart resume: Last accessed lesson or next incomplete lesson
        int? targetLessonId = null;

        if (enrollment.LastLessonId.HasValue)
        {
            // Check if last lesson is completed
            var lastLessonProgress = enrollment.LessonProgress
                .FirstOrDefault(lp => lp.LessonId == enrollment.LastLessonId);

            if (lastLessonProgress != null && !lastLessonProgress.IsCompleted)
            {
                // Resume incomplete lesson
                targetLessonId = enrollment.LastLessonId;
            }
        }

        if (!targetLessonId.HasValue)
        {
            // Find next incomplete lesson
            var completedLessonIds = enrollment.LessonProgress
                .Where(lp => lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToList();

            var allLessons = enrollment.Course.Modules
                .OrderBy(m => m.OrderIndex)
                .SelectMany(m => m.Lessons.OrderBy(l => l.OrderIndex))
                .ToList();

            var nextLesson = allLessons.FirstOrDefault(l => !completedLessonIds.Contains(l.Id));
            targetLessonId = nextLesson?.Id;
        }

        if (targetLessonId.HasValue)
        {
            return RedirectToAction(nameof(Lesson), new { lessonId = targetLessonId.Value });
        }

        // Course completed
        SetSuccessMessage("مبروك! لقد أنهيت جميع دروس هذه الدورة 🎉");
        return RedirectToAction("Details", "Courses", new { id = enrollmentId });
    }
}

/// <summary>
/// طلب تحديث التقدم - Update Progress Request
/// </summary>
public class UpdateProgressRequest
{
    public int LessonId { get; set; }
    public int WatchedSeconds { get; set; }
    public bool Completed { get; set; }
}

