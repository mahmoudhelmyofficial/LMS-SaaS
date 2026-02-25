using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة محاولات الاختبارات - Quiz Attempts Controller
/// </summary>
public class QuizAttemptsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QuizAttemptsController> _logger;
    private readonly IEmailService _emailService;

    public QuizAttemptsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<QuizAttemptsController> logger,
        IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _emailService = emailService;
    }

    /// <summary>
    /// قائمة المحاولات - Quiz Attempts List
    /// </summary>
    public async Task<IActionResult> Index(int? quizId, int? courseId, QuizAttemptStatus? status)
    {
        var userId = _currentUserService.UserId;

        var query = _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(qa => qa.Quiz.Lesson.Module.Course.InstructorId == userId)
            .AsQueryable();

        if (quizId.HasValue)
        {
            query = query.Where(qa => qa.QuizId == quizId.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(qa => qa.Quiz.Lesson.Module.CourseId == courseId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(qa => qa.Status == status.Value);
        }

        var attempts = await query
            .OrderByDescending(qa => qa.StartedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.Quizzes = await _context.Quizzes
            .Include(q => q.Lesson.Module.Course)
            .Where(q => q.Lesson.Module.Course.InstructorId == userId)
            .OrderBy(q => q.Title)
            .ToListAsync();

        ViewBag.QuizId = quizId;
        ViewBag.CourseId = courseId;
        ViewBag.Status = status;

        return View(attempts);
    }

    /// <summary>
    /// تفاصيل المحاولة - Attempt Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Options)
            .Include(qa => qa.Enrollment)
                .ThenInclude(e => e.Student)
            .Include(qa => qa.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(qa => qa.Id == id && 
                qa.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (attempt == null)
            return NotFound();

        return View(attempt);
    }

    /// <summary>
    /// تقييم يدوي - Manual Grading
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Grade(int id)
    {
        var userId = _currentUserService.UserId;

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson.Module.Course)
            .Include(qa => qa.Enrollment)
                .ThenInclude(e => e.Student)
            .Include(qa => qa.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(qa => qa.Id == id && 
                qa.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (attempt == null)
            return NotFound();

        // Get questions that need manual grading (essays, etc.)
        var manualQuestions = attempt.Answers
            .Where(a => a.Question.Type == QuestionType.Essay || 
                       a.Question.Type == QuestionType.FileUpload)
            .ToList();

        var viewModel = new QuizAttemptGradeViewModel
        {
            AttemptId = attempt.Id,
            StudentName = attempt.Enrollment.Student.FullName,
            QuizTitle = attempt.Quiz.Title,
            TotalPoints = attempt.TotalPoints,
            CurrentScore = attempt.Score,
            ManualAnswers = manualQuestions.Select(a => new ManualGradeAnswerViewModel
            {
                AnswerId = a.Id,
                QuestionId = a.QuestionId,
                QuestionText = a.Question.QuestionText,
                QuestionPoints = a.Question.Points,
                StudentAnswer = a.AnswerText,
                AttachmentUrl = a.AttachmentUrl,
                CurrentPoints = a.PointsAwarded,
                CurrentFeedback = a.Feedback
            }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التقييم اليدوي - Save Manual Grade
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(int id, QuizAttemptGradeViewModel model)
    {
        if (id != model.AttemptId)
        {
            _logger.LogWarning("BadRequest: Attempt ID mismatch in Grade action. Route ID: {RouteId}, Model ID: {ModelId}", id, model.AttemptId);
            SetErrorMessage("خطأ في معرّف المحاولة.");
            return BadRequest();
        }

        var userId = _currentUserService.UserId;

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Enrollment)
                .ThenInclude(e => e.Student)
            .Include(qa => qa.Answers)
            .FirstOrDefaultAsync(qa => qa.Id == id && 
                qa.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (attempt == null)
        {
            _logger.LogWarning("NotFound: Quiz attempt {AttemptId} not found or instructor {InstructorId} unauthorized.", id, userId);
            SetErrorMessage("المحاولة غير موجودة أو ليس لديك صلاحية لتقييمها.");
            return NotFound();
        }

        // Authorization check
        if (attempt.Quiz?.Lesson?.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to grade quiz attempt {AttemptId}.", userId, id);
            SetErrorMessage("غير مصرح لك بتقييم هذه المحاولة.");
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            // Validate manual grades
            foreach (var manualAnswer in model.ManualAnswers)
            {
                if (manualAnswer.PointsAwarded < 0 || manualAnswer.PointsAwarded > manualAnswer.QuestionPoints)
                {
                    ModelState.AddModelError(string.Empty, $"النقاط الممنوحة للسؤال يجب أن تكون بين 0 و {manualAnswer.QuestionPoints}");
                    SetErrorMessage($"النقاط الممنوحة يجب أن تكون صالحة.");
                    return View(model);
                }
            }

            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    // Update manual grades for each answer
                    foreach (var manualAnswer in model.ManualAnswers)
                    {
                        var answer = attempt.Answers.FirstOrDefault(a => a.Id == manualAnswer.AnswerId);
                        if (answer != null)
                        {
                            answer.PointsAwarded = manualAnswer.PointsAwarded;
                            answer.Feedback = manualAnswer.Feedback;
                            answer.IsCorrect = manualAnswer.PointsAwarded > 0;
                        }
                    }

                    // Recalculate total score
                    attempt.Score = attempt.Answers.Sum(a => a.PointsAwarded);
                    attempt.Percentage = attempt.TotalPoints > 0 
                        ? (attempt.Score / attempt.TotalPoints) * 100 
                        : 0;
                    attempt.Status = QuizAttemptStatus.Graded;
                    attempt.GradedAt = DateTime.UtcNow;
                    attempt.GradedBy = userId;

                    // Determine pass/fail
                    attempt.IsPassed = attempt.Percentage >= attempt.Quiz.PassingScore;

                    await _context.SaveChangesAsync();

                    // Send notification to student
                    var student = attempt.Enrollment.Student;
                    if (student != null)
                    {
                        // Send email notification
                        if (!string.IsNullOrEmpty(student.Email))
                        {
                            await _emailService.SendQuizGradedAsync(
                                student.Email,
                                student.FullName,
                                attempt.Quiz.Title,
                                attempt.Score,
                                attempt.TotalPoints
                            );
                            _logger.LogInformation("Sent quiz graded email to student {StudentId} for attempt {AttemptId}", student.Id, id);
                        }

                        // Create in-app notification
                        var notification = new Notification
                        {
                            UserId = student.Id,
                            Title = $"تم تقييم اختبارك: {attempt.Quiz.Title}",
                            Message = $"حصلت على {attempt.Score} نقطة من {attempt.TotalPoints} في اختبار {attempt.Quiz.Title}. {(attempt.IsPassed ? "تهانينا! لقد نجحت." : "للأسف، لم تنجح.")}",
                            Type = NotificationType.QuizGraded,
                            ActionUrl = $"/Student/QuizAttempts/Details/{attempt.Id}",
                            ActionText = "عرض النتيجة",
                            IconClass = attempt.IsPassed ? "fas fa-check-circle" : "fas fa-times-circle",
                            IsRead = false
                        };
                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();
                    }
                });

                _logger.LogInformation("Quiz attempt {AttemptId} graded successfully by instructor {InstructorId}. Score: {Score}/{TotalPoints}", id, userId, attempt.Score, attempt.TotalPoints);
                SetSuccessMessage("تم حفظ التقييم بنجاح وإرسال الإشعار للطالب.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grading quiz attempt {AttemptId} by instructor {InstructorId}.", id, userId);
                SetErrorMessage("حدث خطأ أثناء حفظ التقييم.");
            }
        }

        return View(model);
    }

    /// <summary>
    /// إعادة تقييم - Regrade Quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regrade(int id)
    {
        var userId = _currentUserService.UserId;

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Options)
            .Include(qa => qa.Answers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.Options)
            .Include(qa => qa.Enrollment)
                .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(qa => qa.Id == id && 
                qa.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (attempt == null)
        {
            _logger.LogWarning("NotFound: Quiz attempt {AttemptId} not found or instructor {InstructorId} unauthorized for regrade.", id, userId);
            SetErrorMessage("المحاولة غير موجودة أو ليس لديك صلاحية عليها.");
            return NotFound();
        }

        // Authorization check
        if (attempt.Quiz?.Lesson?.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to regrade quiz attempt {AttemptId}.", userId, id);
            SetErrorMessage("غير مصرح لك بإعادة تقييم هذه المحاولة.");
            return Forbid();
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Regrade auto-gradable questions
                foreach (var answer in attempt.Answers.Where(a => 
                    a.Question.Type == QuestionType.MultipleChoice || 
                    a.Question.Type == QuestionType.TrueFalse))
                {
                    var correctOptions = answer.Question.Options.Where(o => o.IsCorrect).ToList();
                    var selectedOptions = !string.IsNullOrEmpty(answer.SelectedOptionIds) 
                        ? answer.SelectedOptionIds.Split(',').Select(int.Parse).ToList() 
                        : new List<int>();

                    // Check if answer is correct
                    var isCorrect = correctOptions.Count == selectedOptions.Count &&
                                   correctOptions.All(o => selectedOptions.Contains(o.Id));

                    answer.IsCorrect = isCorrect;
                    answer.PointsAwarded = isCorrect ? answer.Question.Points : 0;
                }

                // Recalculate score
                attempt.Score = attempt.Answers.Sum(a => a.PointsAwarded);
                attempt.Percentage = attempt.TotalPoints > 0 
                    ? (attempt.Score / attempt.TotalPoints) * 100 
                    : 0;
                attempt.IsPassed = attempt.Percentage >= attempt.Quiz.PassingScore;

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Quiz attempt {AttemptId} regraded successfully by instructor {InstructorId}. New score: {Score}/{TotalPoints}", id, userId, attempt.Score, attempt.TotalPoints);
            SetSuccessMessage("تمت إعادة التقييم بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regrading quiz attempt {AttemptId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء إعادة التقييم.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حذف المحاولة - Delete Attempt
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Answers)
            .FirstOrDefaultAsync(qa => qa.Id == id && 
                qa.Quiz.Lesson.Module.Course.InstructorId == userId);

        if (attempt == null)
        {
            _logger.LogWarning("NotFound: Quiz attempt {AttemptId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("المحاولة غير موجودة أو ليس لديك صلاحية عليها.");
            return NotFound();
        }

        // Authorization check
        if (attempt.Quiz?.Lesson?.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to delete quiz attempt {AttemptId}.", userId, id);
            SetErrorMessage("غير مصرح لك بحذف هذه المحاولة.");
            return Forbid();
        }

        var quizId = attempt.QuizId;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.QuizAttempts.Remove(attempt);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Quiz attempt {AttemptId} deleted successfully by instructor {InstructorId}.", id, userId);
            SetSuccessMessage("تم حذف المحاولة بنجاح");
            return RedirectToAction(nameof(Index), new { quizId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quiz attempt {AttemptId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف المحاولة.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات المحاولات - Attempts Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int? quizId, int? courseId)
    {
        var userId = _currentUserService.UserId;

        var query = _context.QuizAttempts
            .Include(qa => qa.Quiz.Lesson.Module.Course)
            .Where(qa => qa.Quiz.Lesson.Module.Course.InstructorId == userId && 
                        qa.Status == QuizAttemptStatus.Completed)
            .AsQueryable();

        if (quizId.HasValue)
        {
            query = query.Where(qa => qa.QuizId == quizId.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(qa => qa.Quiz.Lesson.Module.CourseId == courseId.Value);
        }

        var attempts = await query.ToListAsync();

        var stats = new QuizAttemptStatisticsViewModel
        {
            TotalAttempts = attempts.Count,
            PassedCount = attempts.Count(a => a.IsPassed),
            FailedCount = attempts.Count(a => !a.IsPassed),
            AverageScore = attempts.Any() ? attempts.Average(a => a.Percentage) : 0,
            AverageTimeMinutes = attempts.Any() 
                ? attempts.Where(a => a.CompletedAt.HasValue)
                    .Average(a => (a.CompletedAt!.Value - a.StartedAt).TotalMinutes) 
                : 0,
            HighestScore = attempts.Any() ? attempts.Max(a => a.Percentage) : 0,
            LowestScore = attempts.Any() ? attempts.Min(a => a.Percentage) : 0
        };

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .ToListAsync();

        ViewBag.Quizzes = await _context.Quizzes
            .Include(q => q.Lesson.Module.Course)
            .Where(q => q.Lesson.Module.Course.InstructorId == userId)
            .ToListAsync();

        ViewBag.QuizId = quizId;
        ViewBag.CourseId = courseId;

        return View(stats);
    }
}

