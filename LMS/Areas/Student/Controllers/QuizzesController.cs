using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// نظام الاختبارات مع التكيف والتحليلات - Quizzes with Adaptive Testing & Analytics
/// </summary>
public class QuizzesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly ILogger<QuizzesController> _logger;

    public QuizzesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningAnalyticsService analyticsService,
        IInstructorNotificationService instructorNotificationService,
        ILogger<QuizzesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _analyticsService = analyticsService;
        _instructorNotificationService = instructorNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الاختبارات المتاحة - List of available quizzes
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get all quizzes from enrolled courses
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .ToListAsync();

        var quizzes = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(q => q.Questions)
            .Where(q => q.Lesson != null && q.Lesson.Module != null && enrolledCourseIds.Contains(q.Lesson.Module.CourseId) && q.IsActive)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        var attempts = await _context.QuizAttempts
            .Where(qa => qa.StudentId == userId)
            .ToListAsync();

        ViewBag.Attempts = attempts;
        return View(quizzes);
    }

    /// <summary>
    /// بدء اختبار جديد - Start a new quiz attempt
    /// </summary>
    public async Task<IActionResult> Start(int quizId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
            {
                SetErrorMessage("الاختبار غير موجود");
                return RedirectToAction("Index");
            }

            // Ensure navigation properties are loaded
            if (quiz.Lesson == null || quiz.Lesson.Module == null || quiz.Lesson.Module.Course == null)
            {
                _logger.LogWarning("Quiz {QuizId} has missing navigation properties", quizId);
                SetErrorMessage("حدث خطأ في تحميل بيانات الاختبار");
                return RedirectToAction("Index");
            }

            // ZTP: Lesson-linked quizzes must be started from tutor (Lesson page) only
            if (quiz.LessonId > 0)
            {
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = quiz.LessonId, quizId = quizId });
            }

            // Check enrollment (active only)
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == quiz.Lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);

            if (enrollment == null)
            {
                SetErrorMessage("يجب أن تكون مسجلاً في الدورة للوصول إلى هذا الاختبار");
                return RedirectToAction("Index");
            }

            // Check if quiz is available
            if (quiz.AvailableFrom.HasValue && quiz.AvailableFrom.Value > DateTime.UtcNow)
            {
                SetErrorMessage($"هذا الاختبار سيكون متاحاً في {quiz.AvailableFrom.Value:yyyy-MM-dd HH:mm}");
                return RedirectToAction("Index");
            }

            if (quiz.AvailableUntil.HasValue && quiz.AvailableUntil.Value < DateTime.UtcNow)
            {
                SetErrorMessage("انتهت فترة توفر هذا الاختبار");
                return RedirectToAction("Index");
            }

            // Check max attempts
            var previousAttempts = await _context.QuizAttempts
                .CountAsync(qa => qa.QuizId == quizId && qa.StudentId == userId);

            if (quiz.MaxAttempts.HasValue && previousAttempts >= quiz.MaxAttempts.Value)
            {
                SetErrorMessage("لقد استنفذت جميع المحاولات المتاحة لهذا الاختبار");
                return RedirectToAction("Index");
            }

            var viewModel = new StartQuizViewModel
            {
                Quiz = quiz,
                CourseName = quiz.Lesson.Module.Course.Title,
                ModuleName = quiz.Lesson.Module.Title,
                LessonName = quiz.Lesson.Title,
                PreviousAttempts = previousAttempts,
                RemainingAttempts = quiz.MaxAttempts.HasValue ? quiz.MaxAttempts.Value - previousAttempts : null
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Start action for quiz {QuizId}", quizId);
            SetErrorMessage("حدث خطأ أثناء تحميل الاختبار");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// أخذ الاختبار - Take the quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TakeQuiz(int quizId)
    {
        try
        {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
            .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
            return NotFound();

        // Verify navigation properties are loaded
        if (quiz.Lesson == null || quiz.Lesson.Module == null)
        {
            _logger.LogError("Quiz {QuizId} has missing navigation properties (Lesson or Module)", quizId);
            return BadRequest("Quiz configuration error");
        }

        // Check enrollment (active only)
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == quiz.Lesson.Module.CourseId && e.StudentId == userId && e.Status == EnrollmentStatus.Active);

        if (enrollment == null)
            return Forbid();

        // ZTP: Quiz linked to lesson must be started from Learning (tutor) only
        if (quiz.LessonId > 0)
        {
            var token = HttpContext.Session.GetString("AllowQuizFromLesson");
            HttpContext.Session.Remove("AllowQuizFromLesson");
            var forbidden = string.IsNullOrEmpty(token);
            if (!forbidden)
            {
                var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
                forbidden = parts.Length != 3 ||
                    !int.TryParse(parts[0], out var allowedLessonId) ||
                    !int.TryParse(parts[1], out var allowedQuizId) ||
                    !long.TryParse(parts[2], out var expiryTicks) ||
                    allowedLessonId != quiz.LessonId ||
                    allowedQuizId != quizId ||
                    new DateTime(expiryTicks) < DateTime.UtcNow;
            }
            if (forbidden)
            {
                _logger.LogWarning("Quiz {QuizId} accessed without lesson context for user {UserId}", quizId, userId);
                SetErrorMessage("يرجى الوصول إلى الاختبار من صفحة الدرس (مشاهدة الدورة).");
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = quiz.LessonId });
            }
        }

        // Create new attempt
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

        // Prepare questions with randomized options
        var questions = quiz.Questions
            .OrderBy(q => quiz.RandomizeQuestions ? Guid.NewGuid().GetHashCode() : q.OrderIndex)
            .Select(q => new QuizQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType.ToString(),
                Points = q.Points,
                ImageUrl = q.ImageUrl,
                Options = q.Options
                    .OrderBy(o => quiz.RandomizeOptions ? Guid.NewGuid().GetHashCode() : o.OrderIndex)
                    .Select(o => new QuizOptionViewModel
                    {
                        Id = o.Id,
                        Text = o.OptionText
                    })
                    .ToList()
            })
            .ToList();

        var proctoring = await _context.ProctoringSettings
            .FirstOrDefaultAsync(p => p.QuizId == quiz.Id);

        var viewModel = new TakeQuizViewModel
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            QuizTitle = quiz.Title,
            TimeLimit = quiz.TimeLimit,
            Questions = questions,
            StartTime = attempt.StartedAt,
            RequiresProctoring = quiz.RequiresProctoring && (proctoring == null || proctoring.IsEnabled),
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

        return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TakeQuiz for quiz {QuizId}", quizId);
            SetErrorMessage("حدث خطأ أثناء تحميل الاختبار");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// إرسال إجابات الاختبار - Submit quiz answers
    /// Supports both model binding and form collection for flexibility
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitQuiz(int attemptId, IFormCollection form)
    {
        QuizAttempt? attempt = null;
        var userId = _currentUserService.UserId;
        try
        {
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(qa => qa.Id == attemptId && qa.StudentId == userId);

        if (attempt == null)
            return NotFound();
        if (attempt.Quiz == null)
        {
            _logger.LogWarning("Quiz attempt {AttemptId} has no Quiz", attemptId);
            return NotFound();
        }

        if (attempt.Status != QuizAttemptStatus.InProgress)
        {
            SetErrorMessage("تم إرسال هذا الاختبار مسبقاً");
            if (attempt.Quiz.LessonId > 0)
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = attempt.Quiz.LessonId });
            return RedirectToAction("AttemptResults", new { attemptId = attempt.Id });
        }

        // Parse answers from form collection (answer_questionId format)
        var answers = new List<AnswerSubmission>();
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("answer_"))
            {
                var questionIdStr = key.Replace("answer_", "");
                if (int.TryParse(questionIdStr, out int questionId))
                {
                    var value = form[key].ToString();
                    int? selectedOptionId = null;
                    string? textAnswer = null;

                    // Check if it's a numeric option ID or text answer
                    if (int.TryParse(value, out int optionId))
                    {
                        selectedOptionId = optionId;
                    }
                    else
                    {
                        textAnswer = value; // For true/false or text answers
                    }

                    answers.Add(new AnswerSubmission
                    {
                        QuestionId = questionId,
                        SelectedOptionId = selectedOptionId,
                        TextAnswer = textAnswer
                    });
                }
            }
        }

        // Calculate score
        decimal totalScore = 0;
        decimal maxScore = 0;
        int correctCount = 0;
        int wrongCount = 0;

        foreach (var answer in answers)
        {
            var question = attempt.Quiz.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
            if (question == null) continue;

            maxScore += question.Points;
            bool isCorrect = false;

            if (answer.SelectedOptionId.HasValue)
            {
                // Multiple choice answer
                var selectedOption = question.Options.FirstOrDefault(o => o.Id == answer.SelectedOptionId);
                isCorrect = selectedOption?.IsCorrect ?? false;
            }
            else if (!string.IsNullOrEmpty(answer.TextAnswer))
            {
                // True/False: determine correctness by matching user answer to the correct option (language-agnostic)
                var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctOption != null)
                {
                    var correctText = correctOption.OptionText?.Trim().ToLowerInvariant() ?? "";
                    var isCorrectOptionTrue = correctText.Contains("صحيح") || correctText.Contains("true") || correctText == "صحيح" || correctText == "true";
                    var userValue = answer.TextAnswer.Trim().ToLowerInvariant();
                    var userSaidTrue = userValue == "true" || userValue == "صحيح";
                    isCorrect = userSaidTrue == isCorrectOptionTrue;
                }
            }

            var quizAnswer = new QuizAnswer
            {
                AttemptId = attempt.Id,
                QuestionId = answer.QuestionId,
                SelectedOptionId = answer.SelectedOptionId,
                IsCorrect = isCorrect,
                PointsAwarded = isCorrect ? question.Points : 0
            };

            _context.QuizAnswers.Add(quizAnswer);

            if (isCorrect)
            {
                totalScore += question.Points;
                correctCount++;
            }
            else
            {
                wrongCount++;
            }
        }

        // Update attempt
        attempt.Score = totalScore;
        attempt.MaxScore = (int)maxScore;
        attempt.PercentageScore = maxScore > 0 ? (totalScore / maxScore) * 100 : 0;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Status = QuizAttemptStatus.Completed;
        attempt.IsPassed = attempt.PercentageScore >= attempt.Quiz.PassingScore;
        attempt.CorrectAnswers = correctCount;
        attempt.WrongAnswers = wrongCount;
        attempt.TimeSpentSeconds = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;

        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId!,
            ActivityType = "QuizAttempt",
            Description = $"إكمال اختبار: {attempt.Quiz.Title} - {attempt.PercentageScore:F0}%",
            EntityType = "Quiz",
            EntityId = attempt.QuizId,
            EntityName = attempt.Quiz.Title,
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            DurationSeconds = attempt.TimeSpentSeconds
        });

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                await _context.SaveChangesAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving quiz attempt {AttemptId} for user {UserId}", attemptId, userId);
            SetErrorMessage("حدث خطأ أثناء حفظ إجابات الاختبار");
            if (attempt.Quiz?.LessonId > 0)
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = attempt.Quiz.LessonId });
            return RedirectToAction("Index");
        }

        try
        {
            var instructorId = attempt.Quiz?.Lesson?.Module?.Course?.InstructorId;
            if (!string.IsNullOrEmpty(instructorId))
            {
                var studentName = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync() ?? "طالب";
                await _instructorNotificationService.NotifyQuizCompletedAsync(
                    instructorId,
                    attempt.Id,
                    attempt.Quiz?.Title ?? "اختبار",
                    studentName,
                    attempt.MaxScore > 0 ? (attempt.Score / attempt.MaxScore) : 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Instructor notification failed for quiz attempt {AttemptId}", attempt.Id);
        }

        SetSuccessMessage(attempt.IsPassed 
            ? $"تهانينا! لقد نجحت في الاختبار بنسبة {attempt.PercentageScore:F0}%" 
            : $"حصلت على {attempt.PercentageScore:F0}% - حاول مرة أخرى للنجاح");

        if (attempt.Quiz?.LessonId > 0)
            return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = attempt.Quiz.LessonId });
        return RedirectToAction("AttemptResults", new { attemptId = attempt.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitQuiz for attempt {AttemptId}, user {UserId}", attemptId, userId);
            SetErrorMessage("حدث خطأ أثناء حفظ إجابات الاختبار");
            if (attempt?.Quiz?.LessonId > 0)
                return RedirectToAction("Lesson", "Learning", new { area = "Student", lessonId = attempt.Quiz.LessonId });
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// جميع نتائج الاختبارات - All quiz results
    /// </summary>
    public async Task<IActionResult> Results()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var completedAttempts = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Module)
                            .ThenInclude(m => m.Course)
                .Where(qa => qa.StudentId == userId && qa.Status == QuizAttemptStatus.Completed)
                .OrderByDescending(qa => qa.SubmittedAt)
                .ToListAsync();

            return View("AllResults", completedAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quiz results");
            SetErrorMessage("حدث خطأ أثناء تحميل نتائج الاختبارات");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// نتائج اختبار معين - Quiz results for specific attempt
    /// </summary>
    public async Task<IActionResult> AttemptResults(int attemptId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(qa => qa.Answers)
            .FirstOrDefaultAsync(qa => qa.Id == attemptId && qa.StudentId == userId);

        if (attempt == null)
            return NotFound();

        var viewModel = new QuizResultsViewModel
        {
            Attempt = attempt,
            CourseName = attempt.Quiz.Lesson.Module.Course.Title,
            QuizTitle = attempt.Quiz.Title,
            TotalQuestions = attempt.Answers.Count,
            CorrectAnswers = attempt.Answers.Count(a => a.IsCorrect == true),
            ShowCorrectAnswers = attempt.Quiz.ShowCorrectAnswers
        };

        return View(viewModel);
    }

    /// <summary>
    /// مراجعة الإجابات - Review answers
    /// </summary>
    public async Task<IActionResult> ReviewAnswers(int attemptId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var attempt = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(qa => qa.Answers)
                .FirstOrDefaultAsync(qa => qa.Id == attemptId && qa.StudentId == userId);

            if (attempt == null)
            {
                SetErrorMessage("المحاولة غير موجودة أو لا تملك صلاحية الوصول إليها");
                return RedirectToAction("Index");
            }

            if (attempt.Quiz == null)
            {
                SetErrorMessage("حدث خطأ في تحميل بيانات الاختبار");
                return RedirectToAction("Index");
            }

            if (!attempt.Quiz.ShowCorrectAnswers)
            {
                SetErrorMessage("لا يسمح بمراجعة الإجابات لهذا الاختبار");
                return RedirectToAction("AttemptResults", new { attemptId });
            }

            var reviewItems = (attempt.Quiz.Questions ?? new List<Question>())
                .OrderBy(q => q.OrderIndex)
                .Select(q =>
                {
                    var answer = attempt.Answers?.FirstOrDefault(a => a.QuestionId == q.Id);
                    return new ReviewAnswerItem
                    {
                        Question = q,
                        SelectedOption = answer != null 
                            ? q.Options?.FirstOrDefault(o => o.Id == answer.SelectedOptionId) 
                            : null,
                        CorrectOption = q.Options?.FirstOrDefault(o => o.IsCorrect),
                        IsCorrect = answer?.IsCorrect ?? false,
                        PointsAwarded = answer?.PointsAwarded ?? 0
                    };
                })
                .ToList();

            var viewModel = new ReviewAnswersViewModel
            {
                Attempt = attempt,
                ReviewItems = reviewItems
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReviewAnswers for attempt {AttemptId}", attemptId);
            SetErrorMessage("حدث خطأ أثناء تحميل مراجعة الإجابات");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// سجل محاولات اختبار معين - Quiz attempt history
    /// </summary>
    public async Task<IActionResult> History(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // First check if the quiz exists
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId);
        if (quiz == null)
        {
            SetErrorMessage("الاختبار غير موجود");
            return RedirectToAction("Index");
        }

        var attempts = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
            .Where(qa => qa.QuizId == quizId && qa.StudentId == userId)
            .OrderByDescending(qa => qa.StartedAt)
            .ToListAsync();

        if (!attempts.Any())
        {
            SetWarningMessage("لا توجد محاولات سابقة لهذا الاختبار");
            return RedirectToAction("Index");
        }

        ViewBag.QuizTitle = quiz.Title;
        ViewBag.QuizId = quizId;
        return View(attempts);
    }

    /// <summary>
    /// لوحة تحليلات الاختبارات - Quiz analytics dashboard
    /// </summary>
    public async Task<IActionResult> MyQuizAnalytics()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var attempts = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(qa => qa.StudentId == userId && qa.Status == QuizAttemptStatus.Completed)
            .OrderByDescending(qa => qa.SubmittedAt)
            .ToListAsync();

        var analytics = new QuizAnalyticsDashboard
        {
            TotalQuizzesTaken = attempts.Count,
            AverageScore = attempts.Any() ? attempts.Average(qa => qa.PercentageScore) : 0,
            PassedQuizzes = attempts.Count(qa => qa.IsPassed),
            FailedQuizzes = attempts.Count(qa => !qa.IsPassed),
            TotalTimeSpentMinutes = attempts.Sum(qa => 
                qa.CompletedAt.HasValue && qa.StartedAt != null 
                    ? (int)(qa.CompletedAt.Value - qa.StartedAt).TotalMinutes 
                    : 0),
            BestPerformance = attempts.Any() ? attempts.Max(qa => qa.PercentageScore) : 0,
            WorstPerformance = attempts.Any() ? attempts.Min(qa => qa.PercentageScore) : 0,
            ImprovementTrend = CalculateImprovementTrend(attempts),
            QuizzesBySubject = attempts
                .GroupBy(qa => qa.Quiz.Lesson.Module.Title)
                .Select(g => new SubjectPerformance
                {
                    SubjectName = g.Key,
                    AverageScore = g.Average(qa => qa.PercentageScore),
                    QuizzesTaken = g.Count(),
                    PassRate = g.Count() > 0 ? (decimal)g.Count(qa => qa.IsPassed) / g.Count() * 100 : 0
                })
                .OrderByDescending(sp => sp.AverageScore)
                .ToList(),
            RecentAttempts = attempts.Take(10).ToList()
        };

        return View(analytics);
    }

    /// <summary>
    /// تحليل محاولة اختبار محددة - Detailed attempt analysis
    /// </summary>
    public async Task<IActionResult> AttemptAnalysis(int attemptId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                SetErrorMessage("يرجى تسجيل الدخول أولاً");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var attempt = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(qa => qa.Answers)
                .FirstOrDefaultAsync(qa => qa.Id == attemptId && qa.StudentId == userId);

            if (attempt == null)
            {
                SetErrorMessage("المحاولة غير موجودة أو لا تملك صلاحية الوصول إليها");
                return RedirectToAction("Index");
            }

            if (attempt.Quiz == null || attempt.Quiz.Questions == null)
            {
                SetErrorMessage("حدث خطأ في تحميل بيانات الاختبار");
                return RedirectToAction("Index");
            }

            var analysis = new AttemptAnalysis
            {
                Attempt = attempt,
                QuestionAnalysis = new List<QuestionAnalysisItem>()
            };

            foreach (var question in attempt.Quiz.Questions)
            {
                var answer = attempt.Answers?.FirstOrDefault(a => a.QuestionId == question.Id);
                var correctOption = question.Options?.FirstOrDefault(o => o.IsCorrect);
                var selectedOption = answer != null 
                    ? question.Options?.FirstOrDefault(o => o.Id == answer.SelectedOptionId) 
                    : null;

                analysis.QuestionAnalysis.Add(new QuestionAnalysisItem
                {
                    Question = question,
                    IsCorrect = answer?.IsCorrect ?? false,
                    SelectedOption = selectedOption,
                    CorrectOption = correctOption,
                    PointsAwarded = answer?.PointsAwarded ?? 0,
                    TimeTakenSeconds = 0 // Could track individual question time
                });
            }

            // Calculate statistics
            analysis.TotalCorrect = analysis.QuestionAnalysis.Count(qa => qa.IsCorrect);
            analysis.TotalIncorrect = analysis.QuestionAnalysis.Count(qa => !qa.IsCorrect);
            analysis.StrengthAreas = analysis.QuestionAnalysis
                .Where(qa => qa.IsCorrect && qa.Question != null)
                .GroupBy(qa => qa.Question.QuestionType)
                .Select(g => g.Key.ToString())
                .ToList();
            analysis.WeaknessAreas = analysis.QuestionAnalysis
                .Where(qa => !qa.IsCorrect && qa.Question != null)
                .GroupBy(qa => qa.Question.QuestionType)
                .Select(g => g.Key.ToString())
                .ToList();

            return View(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AttemptAnalysis for attempt {AttemptId}", attemptId);
            SetErrorMessage("حدث خطأ أثناء تحميل تحليل المحاولة");
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// مقارنة المحاولات - Compare attempts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CompareAttempts(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var attempts = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
            .Where(qa => qa.QuizId == quizId && qa.StudentId == userId && qa.Status == QuizAttemptStatus.Completed)
            .OrderBy(qa => qa.AttemptNumber)
            .ToListAsync();

        if (!attempts.Any())
            return NotFound();

        var comparison = new AttemptsComparison
        {
            QuizTitle = attempts.First().Quiz.Title,
            Attempts = attempts,
            ImprovementRate = attempts.Count > 1 
                ? attempts.Last().PercentageScore - attempts.First().PercentageScore 
                : 0,
            BestAttempt = attempts.OrderByDescending(a => a.PercentageScore).First(),
            AverageScore = attempts.Average(a => a.PercentageScore),
            TotalAttempts = attempts.Count
        };

        return View(comparison);
    }

    /// <summary>
    /// وضع التدريب - Practice mode (unlimited attempts, no scoring)
    /// </summary>
    public async Task<IActionResult> PracticeMode(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
            .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
            return NotFound();

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == quiz.Lesson.Module.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Forbid();

        // Create practice attempt (won't be saved to database)
        var questions = quiz.Questions
            .OrderBy(q => Guid.NewGuid()) // Randomize
            .Select(q => new QuizQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType.ToString(),
                Points = q.Points,
                ImageUrl = q.ImageUrl,
                Options = q.Options
                    .OrderBy(o => Guid.NewGuid())
                    .Select(o => new QuizOptionViewModel
                    {
                        Id = o.Id,
                        Text = o.OptionText
                    })
                    .ToList()
            })
            .ToList();

        var viewModel = new PracticeModeViewModel
        {
            QuizId = quiz.Id,
            QuizTitle = quiz.Title,
            Instructions = "وضع التدريب - يمكنك المحاولة عدة مرات بدون حفظ النتائج",
            Questions = questions,
            ShowAnswersImmediately = true
        };

        return View(viewModel);
    }

    /// <summary>
    /// التحقق من الإجابة الفورية في وضع التدريب - Check answer immediately in practice mode
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckPracticeAnswer(int questionId, int selectedOptionId)
    {
        var question = await _context.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            return NotFound();

        var selectedOption = question.Options.FirstOrDefault(o => o.Id == selectedOptionId);
        var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);

        return Json(new
        {
            correct = selectedOption?.IsCorrect ?? false,
            correctOptionId = correctOption?.Id,
            explanation = question.Explanation ?? "لا يوجد شرح متاح"
        });
    }

    /// <summary>
    /// الاختبار التكيفي - Adaptive quiz (adjusts difficulty based on performance)
    /// </summary>
    public async Task<IActionResult> AdaptiveQuiz(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
            .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
            return NotFound();

        // Get student's previous performance to determine starting difficulty
        var previousAttempts = await _context.QuizAttempts
            .Where(qa => qa.StudentId == userId && qa.EnrollmentId != null)
            .OrderByDescending(qa => qa.SubmittedAt)
            .Take(5)
            .ToListAsync();

        var averageScore = previousAttempts.Any() ? previousAttempts.Average(qa => qa.PercentageScore) : 70;

        // Select questions based on difficulty and performance
        var selectedQuestions = quiz.Questions
            .OrderBy(q => CalculateQuestionDifficulty(q, averageScore))
            .Take(Math.Min(quiz.Questions.Count, 10)) // Limit to 10 questions
            .OrderBy(q => Guid.NewGuid()) // Shuffle selected questions
            .ToList();

        var viewModel = new AdaptiveQuizViewModel
        {
            QuizId = quiz.Id,
            QuizTitle = $"{quiz.Title} (تكيفي)",
            Instructions = "اختبار تكيفي - تتغير صعوبة الأسئلة بناءً على أدائك",
            Questions = selectedQuestions.Select(q => new QuizQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType.ToString(),
                Points = q.Points,
                ImageUrl = q.ImageUrl,
                Options = q.Options.OrderBy(o => Guid.NewGuid()).Select(o => new QuizOptionViewModel
                {
                    Id = o.Id,
                    Text = o.OptionText
                }).ToList()
            }).ToList(),
            CurrentDifficultyLevel = DetermineDifficultyLevel(averageScore)
        };

        return View(viewModel);
    }

    /// <summary>
    /// توصيات المراجعة - Review recommendations based on weak areas
    /// </summary>
    public async Task<IActionResult> ReviewRecommendations()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == userId && e.Status == EnrollmentStatus.Active)
            .ToListAsync();

        var recommendations = new List<ReviewRecommendation>();

        foreach (var enrollment in enrollments)
        {
            var strengths = await _analyticsService.AnalyzeStrengthsWeaknessesAsync(userId, enrollment.Id);
            
            foreach (var weakness in strengths.Weaknesses)
            {
                recommendations.Add(new ReviewRecommendation
                {
                    EnrollmentId = enrollment.Id,
                    Topic = weakness.Name,
                    CurrentScore = weakness.Score,
                    TargetScore = 70,
                    Priority = CalculateReviewPriority(weakness.Score),
                    EstimatedReviewTime = CalculateEstimatedReviewTime(weakness.Score),
                    SuggestedActions = new List<string>
                    {
                        $"راجع دروس {weakness.Name}",
                        "حل تمارين إضافية",
                        "شاهد فيديوهات الشرح مرة أخرى"
                    }
                });
            }
        }

        recommendations = recommendations.OrderBy(r => r.Priority).ToList();

        return View(recommendations);
    }

    /// <summary>
    /// لوحة التحديات - Challenges leaderboard
    /// </summary>
    public async Task<IActionResult> Leaderboard(int? courseId = null, string period = "all")
    {
        var userId = _currentUserService.UserId;
        
        var query = _context.QuizAttempts
            .Include(qa => qa.Student)
            .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(qa => qa.Status == QuizAttemptStatus.Completed);

        if (courseId.HasValue)
        {
            query = query.Where(qa => qa.Quiz.Lesson.Module.CourseId == courseId.Value);
        }

        var leaderboard = await query
            .GroupBy(qa => new { qa.StudentId, qa.Student.FirstName, qa.Student.LastName, qa.Student.ProfileImageUrl })
            .Select(g => new ViewModels.LeaderboardEntry
            {
                StudentId = g.Key.StudentId,
                StudentName = $"{g.Key.FirstName} {g.Key.LastName}",
                ProfilePictureUrl = g.Key.ProfileImageUrl,
                TotalQuizzes = g.Count(),
                AverageScore = g.Average(qa => qa.PercentageScore),
                TotalScore = g.Sum(qa => qa.Score),
                Score = g.Average(qa => qa.PercentageScore),
                PassRate = g.Count() > 0 ? (decimal)g.Count(qa => qa.IsPassed) / g.Count() * 100 : 0,
                CompletedAt = g.Max(qa => qa.SubmittedAt) ?? DateTime.UtcNow
            })
            .OrderByDescending(le => le.TotalScore)
            .ThenByDescending(le => le.AverageScore)
            .Take(50)
            .ToListAsync();

        // Add rank and check current user
        for (int i = 0; i < leaderboard.Count; i++)
        {
            leaderboard[i].Rank = i + 1;
            leaderboard[i].IsCurrentUser = leaderboard[i].StudentId == userId;
        }

        // Create the ViewModel expected by the view
        var viewModel = new ViewModels.QuizLeaderboardViewModel
        {
            Quiz = new ViewModels.QuizDisplayInfo
            {
                Id = courseId ?? 0,
                Title = courseId.HasValue 
                    ? await _context.Courses.Where(c => c.Id == courseId.Value).Select(c => c.Title).FirstOrDefaultAsync() ?? "جميع الاختبارات"
                    : "جميع الاختبارات"
            },
            Entries = leaderboard,
            TopThree = leaderboard.Take(3).ToList(),
            TotalParticipants = leaderboard.Count,
            Period = period,
            CurrentUserEntry = leaderboard.FirstOrDefault(e => e.StudentId == userId)
        };

        ViewBag.CourseId = courseId;
        return View(viewModel);
    }

    /// <summary>
    /// تحدي سريع - Quick challenge (timed quiz with ranking)
    /// </summary>
    public async Task<IActionResult> QuickChallenge(int courseId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get random questions from course
        var lessons = await _context.Lessons
            .Include(l => l.Quizzes)
                .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Options)
            .Where(l => l.Module.CourseId == courseId)
            .ToListAsync();

        var allQuestions = lessons
            .SelectMany(l => l.Quizzes)
            .SelectMany(q => q.Questions)
            .OrderBy(q => Guid.NewGuid())
            .Take(10)
            .ToList();

        if (!allQuestions.Any())
        {
            SetErrorMessage("لا توجد أسئلة متاحة لهذا التحدي");
            return RedirectToAction("Index", "Courses");
        }

        var viewModel = new QuickChallengeViewModel
        {
            CourseId = courseId,
            TimeLimitSeconds = 300, // 5 minutes
            Questions = allQuestions.Select(q => new QuizQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType.ToString(),
                Points = q.Points,
                Options = q.Options.OrderBy(o => Guid.NewGuid()).Select(o => new QuizOptionViewModel
                {
                    Id = o.Id,
                    Text = o.OptionText
                }).ToList()
            }).ToList()
        };

        return View(viewModel);
    }

    #region Proctoring Endpoints

    /// <summary>
    /// بدء جلسة المراقبة - Start proctoring session
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartProctoringSession([FromBody] StartProctoringRequest? model)
    {
        try
        {
            if (model == null) return Json(new { success = false, message = "البيانات غير صحيحة" });

            var userId = _currentUserService.UserId;
            var proctoringService = HttpContext.RequestServices.GetRequiredService<IProctoringService>();

            var result = await proctoringService.CreateSessionAsync(
                model.QuizAttemptId, userId!, model.BrowserInfo, model.IpAddress, model.ScreenResolution);

            if (result.IsSuccess)
                return Json(new { success = true, sessionId = result.Data!.Id });

            return Json(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting proctoring session");
            return Json(new { success = false, message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تسجيل مخالفة - Report proctoring violation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportViolation([FromBody] ReportViolationRequest? model)
    {
        try
        {
            if (model == null) return Json(new { success = false });

            var userId = _currentUserService.UserId;
            var proctoringService = HttpContext.RequestServices.GetRequiredService<IProctoringService>();

            if (!Enum.TryParse<ViolationType>(model.ViolationType, out var violationType))
                return Json(new { success = false });

            var severity = Enum.TryParse<ViolationSeverity>(model.Severity, out var sev) ? sev : ViolationSeverity.Medium;

            await proctoringService.LogViolationAsync(
                model.SessionId, violationType, severity, model.Description, model.ScreenshotUrl, userId!);

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting violation");
            return Json(new { success = false });
        }
    }

    /// <summary>
    /// إنهاء جلسة المراقبة - End proctoring session
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndProctoringSession([FromBody] EndProctoringRequest? model)
    {
        try
        {
            if (model == null) return Json(new { success = false });

            var userId = _currentUserService.UserId;
            var proctoringService = HttpContext.RequestServices.GetRequiredService<IProctoringService>();

            await proctoringService.EndSessionAsync(model.SessionId, userId!);

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending proctoring session");
            return Json(new { success = false });
        }
    }

    /// <summary>
    /// رفع لقطة شاشة المراقبة - Upload proctoring screenshot
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProctoringScreenshot([FromBody] UploadScreenshotRequest? model)
    {
        try
        {
            if (model == null || string.IsNullOrEmpty(model.ImageData))
                return Json(new { success = false, message = "No image data provided" });
            
            var userId = _currentUserService.UserId;
            
            // Save the base64 image to storage
            var fileName = $"proctoring/{userId}/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid()}.png";
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
            
            var directory = Path.GetDirectoryName(uploadsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            // Convert base64 to file
            var imageData = model.ImageData;
            if (imageData.Contains(","))
                imageData = imageData.Split(",")[1];
            
            var imageBytes = Convert.FromBase64String(imageData);
            await System.IO.File.WriteAllBytesAsync(uploadsPath, imageBytes);
            
            var screenshotUrl = $"/uploads/{fileName}";
            
            // Log as screenshot in session if session ID provided
            if (model.SessionId > 0)
            {
                var session = await _context.Set<ProctoringSession>()
                    .FirstOrDefaultAsync(s => s.Id == model.SessionId && s.StudentId == userId);
                if (session != null)
                {
                    session.ScreenshotCount++;
                    await _context.SaveChangesAsync();
                }
            }
            
            return Json(new { success = true, url = screenshotUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading proctoring screenshot");
            return Json(new { success = false, message = "Upload failed" });
        }
    }

    /// <summary>
    /// إرسال تلقائي للاختبار - Auto-submit quiz (proctoring termination)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSubmit([FromBody] AutoSubmitRequest? model)
    {
        try
        {
            if (model == null || model.QuizAttemptId <= 0)
                return Json(new { success = false, message = "Invalid attempt ID" });
            
            var userId = _currentUserService.UserId;
            
            var attempt = await _context.Set<QuizAttempt>()
                .FirstOrDefaultAsync(a => a.Id == model.QuizAttemptId && a.StudentId == userId);
            
            if (attempt == null)
                return Json(new { success = false, message = "Attempt not found" });
            
            // Mark as terminated/submitted
            attempt.Status = QuizAttemptStatus.Submitted;
            attempt.SubmittedAt = DateTime.UtcNow;
            
            // End proctoring session
            if (model.SessionId > 0)
            {
                var proctoringService = HttpContext.RequestServices.GetRequiredService<IProctoringService>();
                await proctoringService.EndSessionAsync(model.SessionId, userId!);
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogWarning("Quiz attempt {AttemptId} auto-submitted due to proctoring violation for student {StudentId}", 
                model.QuizAttemptId, userId);
            
            return Json(new { success = true, message = "تم إرسال الاختبار تلقائياً بسبب المخالفات" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-submitting quiz attempt {AttemptId}", model?.QuizAttemptId);
            return Json(new { success = false, message = "Auto-submit failed" });
        }
    }

    #endregion

    #region Private Helper Methods

    private decimal CalculateImprovementTrend(List<QuizAttempt> attempts)
    {
        if (attempts.Count < 2)
            return 0;

        var recentAttempts = attempts.OrderByDescending(a => a.CompletedAt).Take(5).ToList();
        var olderAttempts = attempts.OrderByDescending(a => a.CompletedAt).Skip(5).Take(5).ToList();

        if (!olderAttempts.Any())
            return 0;

        var recentAvg = recentAttempts.Average(a => a.PercentageScore);
        var olderAvg = olderAttempts.Average(a => a.PercentageScore);

        return recentAvg - olderAvg;
    }

    private double CalculateQuestionDifficulty(Question question, decimal studentAverageScore)
    {
        // Simple difficulty calculation based on question type and student performance
        var baseDifficulty = question.Type switch
        {
            QuestionType.MultipleChoice => 1.0,
            QuestionType.TrueFalse => 0.5,
            QuestionType.ShortAnswer => 1.5,
            QuestionType.Essay => 2.0,
            _ => 1.0
        };

        // Adjust based on student performance
        if (studentAverageScore >= 80)
            return baseDifficulty * 1.2; // Give harder questions to high performers
        if (studentAverageScore < 50)
            return baseDifficulty * 0.8; // Give easier questions to struggling students

        return baseDifficulty;
    }

    private string DetermineDifficultyLevel(decimal averageScore)
    {
        return averageScore switch
        {
            >= 80 => "صعب",
            >= 60 => "متوسط",
            _ => "سهل"
        };
    }

    private int CalculateReviewPriority(decimal currentScore)
    {
        return currentScore switch
        {
            < 40 => 1, // Highest priority
            < 60 => 2,
            < 70 => 3,
            _ => 4 // Lowest priority
        };
    }

    private int CalculateEstimatedReviewTime(decimal currentScore)
    {
        return currentScore switch
        {
            < 40 => 120, // 2 hours
            < 60 => 90, // 1.5 hours
            < 70 => 60, // 1 hour
            _ => 30 // 30 minutes
        };
    }

    #endregion
}

#region View Models

public class QuizAnalyticsDashboard
{
    public int TotalQuizzesTaken { get; set; }
    public decimal AverageScore { get; set; }
    public int PassedQuizzes { get; set; }
    public int FailedQuizzes { get; set; }
    public int TotalTimeSpentMinutes { get; set; }
    public decimal BestPerformance { get; set; }
    public decimal WorstPerformance { get; set; }
    public decimal ImprovementTrend { get; set; }
    public List<SubjectPerformance> QuizzesBySubject { get; set; } = new();
    public List<QuizAttempt> RecentAttempts { get; set; } = new();
}

public class SubjectPerformance
{
    public string SubjectName { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    public int QuizzesTaken { get; set; }
    public decimal PassRate { get; set; }
}

// AttemptAnalysis and QuestionAnalysisItem classes are now in LMS.Areas.Student.ViewModels

public class AttemptsComparison
{
    public string QuizTitle { get; set; } = string.Empty;
    public List<QuizAttempt> Attempts { get; set; } = new();
    public decimal ImprovementRate { get; set; }
    public QuizAttempt BestAttempt { get; set; } = null!;
    public decimal AverageScore { get; set; }
    public int TotalAttempts { get; set; }
}

// PracticeModeViewModel moved to ViewModels folder

// AdaptiveQuizViewModel moved to ViewModels folder

public class ReviewRecommendation
{
    public int EnrollmentId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public decimal CurrentScore { get; set; }
    public decimal TargetScore { get; set; }
    public int Priority { get; set; }
    public int EstimatedReviewTime { get; set; }
    public List<string> SuggestedActions { get; set; } = new();
}

public class LeaderboardEntry
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public int Rank { get; set; }
    public int TotalQuizzes { get; set; }
    public decimal AverageScore { get; set; }
    public decimal TotalScore { get; set; }
    public decimal Score { get; set; }
    public decimal PassRate { get; set; }
    public decimal CompletionTime { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool IsCurrentUser { get; set; }
    public int AttemptsCount { get; set; }
}

// QuickChallengeViewModel moved to ViewModels folder

public class StartProctoringRequest
{
    public int QuizAttemptId { get; set; }
    public string? BrowserInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? ScreenResolution { get; set; }
}

public class ReportViolationRequest
{
    public int SessionId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string? Description { get; set; }
    public string? ScreenshotUrl { get; set; }
}

public class EndProctoringRequest
{
    public int SessionId { get; set; }
}

public class UploadScreenshotRequest
{
    public int SessionId { get; set; }
    public string ImageData { get; set; } = string.Empty;
}

public class AutoSubmitRequest
{
    public int QuizAttemptId { get; set; }
    public int SessionId { get; set; }
    public string? Reason { get; set; }
}

public class StartQuizViewModel
{
    public Quiz Quiz { get; set; } = null!;
    public string CourseName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string LessonName { get; set; } = string.Empty;
    public int PreviousAttempts { get; set; }
    public int? RemainingAttempts { get; set; }
}

public class TakeQuizViewModel
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int? TimeLimit { get; set; }
    public List<QuizQuestionViewModel> Questions { get; set; } = new();
    public DateTime StartTime { get; set; }
    // Proctoring (from Quiz.RequiresProctoring + ProctoringSetting)
    public bool RequiresProctoring { get; set; }
    public bool ProcPreventTabSwitch { get; set; } = true;
    public bool ProcPreventCopyPaste { get; set; } = true;
    public bool ProcDisableRightClick { get; set; } = true;
    public bool ProcRequireFullscreen { get; set; }
    public bool ProcRequireWebcam { get; set; }
    public bool ProcCaptureScreenshots { get; set; }
    public int ProcScreenshotInterval { get; set; } = 60;
    public int ProcMaxWarnings { get; set; } = 3;
    public bool ProcAutoTerminate { get; set; }
}

public class SubmitQuizViewModel
{
    public int AttemptId { get; set; }
    public List<AnswerSubmission> Answers { get; set; } = new();
}

public class AnswerSubmission
{
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
}

public class QuizResultsViewModel
{
    public QuizAttempt Attempt { get; set; } = null!;
    public string CourseName { get; set; } = string.Empty;
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public bool ShowCorrectAnswers { get; set; }
}

public class ReviewAnswersViewModel
{
    public QuizAttempt Attempt { get; set; } = null!;
    public List<ReviewAnswerItem> ReviewItems { get; set; } = new();
}

public class ReviewAnswerItem
{
    public Question Question { get; set; } = null!;
    public QuestionOption? SelectedOption { get; set; }
    public QuestionOption? CorrectOption { get; set; }
    public bool IsCorrect { get; set; }
    public decimal PointsAwarded { get; set; }
}

#endregion

