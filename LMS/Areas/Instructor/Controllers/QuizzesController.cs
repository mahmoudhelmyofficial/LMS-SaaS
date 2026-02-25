using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة الاختبارات - Quizzes Controller
/// Refactored to use service layer with proper enterprise patterns
/// </summary>
public class QuizzesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuizService _quizService;
    private readonly ISystemConfigurationService _configService;
    private readonly ILogger<QuizzesController> _logger;

    public QuizzesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IQuizService quizService,
        ISystemConfigurationService configService,
        ILogger<QuizzesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _quizService = quizService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الاختبارات - Quizzes list
    /// Enterprise-level implementation with comprehensive error handling and null safety
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? search, string? type, int page = 1)
    {
        try
        {
            // Enhanced null checks with proper logging
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated access attempt to Quizzes/Index");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Validate page number
            if (page < 1)
            {
                _logger.LogWarning("Invalid page number {Page} provided, defaulting to 1", page);
                page = 1;
            }

            // Get pagination size with error handling
            int pageSize = 12;
            try
            {
                pageSize = await _configService.GetPaginationSizeAsync("quizzes", 12);
                if (pageSize < 1)
                {
                    _logger.LogWarning("Invalid page size {PageSize} returned from config service, using default 12", pageSize);
                    pageSize = 12;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get pagination size from config service, using default 12");
                pageSize = 12;
            }

            var filter = new QuizFilterRequest
            {
                CourseId = courseId,
                Search = search,
                Type = type,
                Page = page,
                PageSize = pageSize,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            // Get quizzes with error handling
            PagedResult<QuizDto> quizzesResult;
            try
            {
                quizzesResult = await _quizService.GetInstructorQuizzesAsync(userId, filter);
                if (quizzesResult == null)
                {
                    _logger.LogWarning("GetInstructorQuizzesAsync returned null for user {UserId}", userId);
                    quizzesResult = PagedResult<QuizDto>.Create(new List<QuizDto>(), 0, page, pageSize);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetInstructorQuizzesAsync for user {UserId}", userId);
                SetErrorMessage("حدث خطأ أثناء تحميل الاختبارات");
                quizzesResult = PagedResult<QuizDto>.Create(new List<QuizDto>(), 0, page, pageSize);
            }

            // Get statistics with error handling
            QuizStatisticsDto statistics;
            try
            {
                statistics = await _quizService.GetInstructorQuizStatisticsAsync(userId, courseId);
                if (statistics == null)
                {
                    _logger.LogWarning("GetInstructorQuizStatisticsAsync returned null for user {UserId}", userId);
                    statistics = new QuizStatisticsDto
                    {
                        TotalQuizzes = 0,
                        TotalQuestions = 0,
                        TotalAttempts = 0,
                        AveragePassRate = 0,
                        AttemptCounts = new Dictionary<int, int>(),
                        PassRates = new Dictionary<int, decimal>()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting quiz statistics, using defaults");
                statistics = new QuizStatisticsDto
                {
                    TotalQuizzes = 0,
                    TotalQuestions = 0,
                    TotalAttempts = 0,
                    AveragePassRate = 0,
                    AttemptCounts = new Dictionary<int, int>(),
                    PassRates = new Dictionary<int, decimal>()
                };
            }

            // Safe mapping with null checks
            var quizzes = quizzesResult?.Items?
                .Where(dto => dto != null)
                .Select(dto => new Quiz
                {
                    Id = dto.Id,
                    LessonId = dto.LessonId,
                    Title = dto.Title ?? string.Empty,
                    Description = dto.Description,
                    Instructions = dto.Instructions,
                    PassingScore = dto.PassingScore,
                    TimeLimitMinutes = dto.TimeLimitMinutes,
                    MaxAttempts = dto.MaxAttempts,
                    ShuffleQuestions = dto.ShuffleQuestions,
                    ShuffleOptions = dto.ShuffleOptions,
                    ShowCorrectAnswers = dto.ShowCorrectAnswers,
                    ShowAnswersAfter = dto.ShowAnswersAfter,
                    ShowScoreImmediately = dto.ShowScoreImmediately,
                    AllowBackNavigation = dto.AllowBackNavigation,
                    OneQuestionPerPage = dto.OneQuestionPerPage,
                    AvailableFrom = dto.AvailableFrom,
                    AvailableUntil = dto.AvailableUntil,
                    CreatedAt = dto.CreatedAt,
                    Lesson = new Domain.Entities.Courses.Lesson
                    {
                        Id = dto.LessonId,
                        Title = dto.LessonTitle ?? string.Empty,
                        Module = new Domain.Entities.Courses.Module
                        {
                            Title = dto.ModuleTitle ?? string.Empty,
                            Course = new Domain.Entities.Courses.Course
                            {
                                Id = dto.CourseId,
                                Title = dto.CourseTitle ?? string.Empty
                            }
                        }
                    },
                    Questions = new List<Question>(),
                    Attempts = new List<QuizAttempt>()
                })
                .ToList() ?? new List<Quiz>();

            // Populate ViewBag with safe defaults
            try
            {
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .Select(c => new { c.Id, c.Title })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading courses for ViewBag");
                ViewBag.Courses = new List<object>();
            }

            ViewBag.CourseId = courseId;
            ViewBag.SearchTerm = search;
            ViewBag.Type = type;

            // Statistics with null checks
            ViewBag.TotalQuizzes = statistics?.TotalQuizzes ?? 0;
            ViewBag.TotalQuestions = statistics?.TotalQuestions ?? 0;
            ViewBag.TotalAttempts = statistics?.TotalAttempts ?? 0;
            ViewBag.AveragePassRate = Math.Round(statistics?.AveragePassRate ?? 0, 1);
            ViewBag.AttemptCounts = statistics?.AttemptCounts ?? new Dictionary<int, int>();
            ViewBag.PassRates = statistics?.PassRates ?? new Dictionary<int, decimal>();
            
            // Question counts dictionary for view compatibility with null safety
            ViewBag.QuestionCounts = quizzesResult?.Items?
                .Where(q => q != null)
                .ToDictionary(q => q.Id, q => q.QuestionCount) 
                ?? new Dictionary<int, int>();

            // Pagination with safe defaults
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = quizzesResult?.TotalPages ?? 1;
            ViewBag.TotalItems = quizzesResult?.TotalCount ?? 0;
            ViewBag.PageSize = pageSize;

            _logger.LogInformation(
                "QuizzesController.Index completed successfully for user {UserId}. Found {Count} quizzes",
                userId, quizzes.Count);

            return View(quizzes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unhandled error in QuizzesController.Index for user {UserId}. CourseId: {CourseId}, Page: {Page}",
                _currentUserService.UserId, courseId, page);
            SetErrorMessage("حدث خطأ أثناء تحميل الاختبارات");
            return View(new List<Quiz>());
        }
    }

    /// <summary>
    /// إنشاء اختبار - Create quiz
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? lessonId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in QuizzesController.Create");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            // If no lessonId provided, show lesson selection
            if (!lessonId.HasValue)
            {
                var lessons = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .Include(l => l.Quizzes)
                    .Where(l => l.Module.Course.InstructorId == userId && !l.Quizzes.Any())
                    .OrderBy(l => l.Module.Course.Title)
                        .ThenBy(l => l.Module.Title)
                        .ThenBy(l => l.OrderIndex)
                    .Select(l => new
                    {
                        l.Id,
                        l.Title,
                        ModuleTitle = l.Module.Title,
                        CourseTitle = l.Module.Course.Title,
                        l.Module.CourseId
                    })
                    .ToListAsync();

                ViewBag.Lessons = lessons;
                return View(new QuizCreateViewModel());
            }

            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Quizzes)
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("Lesson {LessonId} not found for instructor {InstructorId}", lessonId, userId);
                return NotFound();
            }

            if (lesson.Quiz != null)
            {
                SetWarningMessage("هذا الدرس يحتوي على اختبار بالفعل");
                return RedirectToAction(nameof(Edit), new { id = lesson.Quiz.Id });
            }

            ViewBag.Lesson = lesson;
            return View(new QuizCreateViewModel { LessonId = lessonId.Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QuizzesController.Create");
            SetErrorMessage("حدث خطأ أثناء تحميل الصفحة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الاختبار - Save quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuizCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId && l.Module.Course.InstructorId == userId);
            
            ViewBag.Lesson = lesson;
            return View(model);
        }

        try
        {
            var request = new CreateQuizRequest
            {
                LessonId = model.LessonId,
                Title = model.Title,
                Description = model.Description,
                Instructions = model.Instructions,
                PassingScore = model.PassingScore,
                TimeLimitMinutes = model.TimeLimitMinutes,
                MaxAttempts = model.MaxAttempts,
                ShuffleQuestions = model.ShuffleQuestions,
                ShuffleOptions = model.ShuffleOptions,
                ShowCorrectAnswers = model.ShowCorrectAnswers,
                ShowAnswersAfter = "Immediately",
                ShowScoreImmediately = model.ShowScoreImmediately,
                AllowBackNavigation = model.AllowBackNavigation,
                OneQuestionPerPage = model.OneQuestionPerPage,
                InstructorId = userId
            };

            var result = await _quizService.CreateQuizAsync(request);

            if (result.IsSuccess && result.Value != null)
            {
                SetSuccessMessage("تم إنشاء الاختبار بنجاح. يمكنك الآن إضافة الأسئلة");
                return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إنشاء الاختبار");
            
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId);
            ViewBag.Lesson = lesson;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quiz for lesson {LessonId}", model.LessonId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الاختبار");
            
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId);
            ViewBag.Lesson = lesson;
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الاختبار - Edit quiz
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(id, userId);

        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var quizDto = result.Value;
        var questions = await _quizService.GetQuizQuestionsAsync(id, userId);

        var viewModel = new QuizEditViewModel
        {
            Id = quizDto.Id,
            LessonId = quizDto.LessonId,
            Title = quizDto.Title,
            Description = quizDto.Description,
            Instructions = quizDto.Instructions,
            PassingScore = quizDto.PassingScore,
            TimeLimitMinutes = quizDto.TimeLimitMinutes,
            MaxAttempts = quizDto.MaxAttempts,
            ShuffleQuestions = quizDto.ShuffleQuestions,
            ShuffleOptions = quizDto.ShuffleOptions,
            ShowCorrectAnswers = quizDto.ShowCorrectAnswers,
            ShowAnswersAfter = quizDto.ShowAnswersAfter ?? "Immediately",
            ShowScoreImmediately = quizDto.ShowScoreImmediately,
            AllowBackNavigation = quizDto.AllowBackNavigation,
            OneQuestionPerPage = quizDto.OneQuestionPerPage,
            AvailableFrom = quizDto.AvailableFrom,
            AvailableUntil = quizDto.AvailableUntil
        };

        // Map questions to entities for view
        ViewBag.Questions = questions.Select(q => new Question
        {
            Id = q.Id,
            QuizId = q.QuizId,
            QuestionText = q.QuestionText,
            Type = Enum.Parse<QuestionType>(q.Type),
            Points = q.Points,
            OrderIndex = q.OrderIndex,
            Options = q.Options.Select(o => new QuestionOption
            {
                Id = o.Id,
                QuestionId = o.QuestionId,
                OptionText = o.OptionText,
                IsCorrect = o.IsCorrect
            }).ToList()
        }).OrderBy(q => q.OrderIndex).ToList();

        // Get lesson for ViewBag
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == quizDto.LessonId);
        ViewBag.Lesson = lesson;

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الاختبار - Save quiz edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, QuizEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var quizResult = await _quizService.GetQuizByIdAsync(id, userId);
            if (quizResult.IsSuccess && quizResult.Value != null)
            {
                var questions = await _quizService.GetQuizQuestionsAsync(id, userId);
                ViewBag.Questions = questions.Select(q => new Question
                {
                    Id = q.Id,
                    QuizId = q.QuizId,
                    QuestionText = q.QuestionText,
                    Type = Enum.Parse<QuestionType>(q.Type),
                    Points = q.Points,
                    OrderIndex = q.OrderIndex,
                    Options = q.Options.Select(o => new QuestionOption
                    {
                        Id = o.Id,
                        QuestionId = o.QuestionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).OrderBy(q => q.OrderIndex).ToList();

                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == quizResult.Value.LessonId);
                ViewBag.Lesson = lesson;
            }
            return View(model);
        }

        try
        {
            var request = new UpdateQuizRequest
            {
                Id = model.Id,
                LessonId = model.LessonId,
                Title = model.Title,
                Description = model.Description,
                Instructions = model.Instructions,
                PassingScore = model.PassingScore,
                TimeLimitMinutes = model.TimeLimitMinutes,
                MaxAttempts = model.MaxAttempts,
                ShuffleQuestions = model.ShuffleQuestions,
                ShuffleOptions = model.ShuffleOptions,
                ShowCorrectAnswers = model.ShowCorrectAnswers,
                ShowAnswersAfter = model.ShowAnswersAfter,
                ShowScoreImmediately = model.ShowScoreImmediately,
                AllowBackNavigation = model.AllowBackNavigation,
                OneQuestionPerPage = model.OneQuestionPerPage,
                AvailableFrom = model.AvailableFrom,
                AvailableUntil = model.AvailableUntil,
                InstructorId = userId
            };

            var result = await _quizService.UpdateQuizAsync(id, request);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم تحديث الاختبار بنجاح");
                return RedirectToAction(nameof(Edit), new { id });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث الاختبار");
            
            var quizResult = await _quizService.GetQuizByIdAsync(id, userId);
            if (quizResult.IsSuccess && quizResult.Value != null)
            {
                var questions = await _quizService.GetQuizQuestionsAsync(id, userId);
                ViewBag.Questions = questions.Select(q => new Question
                {
                    Id = q.Id,
                    QuizId = q.QuizId,
                    QuestionText = q.QuestionText,
                    Type = Enum.Parse<QuestionType>(q.Type),
                    Points = q.Points,
                    OrderIndex = q.OrderIndex,
                    Options = q.Options.Select(o => new QuestionOption
                    {
                        Id = o.Id,
                        QuestionId = o.QuestionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).OrderBy(q => q.OrderIndex).ToList();

                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == quizResult.Value.LessonId);
                ViewBag.Lesson = lesson;
            }
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quiz {QuizId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الاختبار");
            
            var quizResult = await _quizService.GetQuizByIdAsync(id, userId);
            if (quizResult.IsSuccess && quizResult.Value != null)
            {
                var questions = await _quizService.GetQuizQuestionsAsync(id, userId);
                ViewBag.Questions = questions.Select(q => new Question
                {
                    Id = q.Id,
                    QuizId = q.QuizId,
                    QuestionText = q.QuestionText,
                    Type = Enum.Parse<QuestionType>(q.Type),
                    Points = q.Points,
                    OrderIndex = q.OrderIndex,
                    Options = q.Options.Select(o => new QuestionOption
                    {
                        Id = o.Id,
                        QuestionId = o.QuestionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).OrderBy(q => q.OrderIndex).ToList();

                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == quizResult.Value.LessonId);
                ViewBag.Lesson = lesson;
            }
            return View(model);
        }
    }

    /// <summary>
    /// تفاصيل الاختبار - Quiz details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(id, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var statistics = await _quizService.GetQuizStatisticsAsync(id, userId);

        // Get quiz entity for view
        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
            .Include(q => q.Attempts)
                .ThenInclude(a => a.Student)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz == null)
        {
            return NotFound();
        }

        ViewBag.Quiz = quiz;
        ViewBag.Stats = new
        {
            TotalAttempts = statistics.TotalAttempts,
            CompletedAttempts = statistics.CompletedAttempts,
            InProgressAttempts = statistics.InProgressAttempts,
            AverageScore = statistics.AverageScore,
            PassRate = statistics.PassRate,
            TotalQuestions = statistics.QuestionCount,
            TotalPoints = statistics.TotalPoints
        };
        ViewBag.RecentAttempts = quiz.Attempts
            .OrderByDescending(a => a.StartedAt)
            .Take(10)
            .ToList();

        return View(quiz);
    }

    /// <summary>
    /// نتائج الاختبار - Quiz results
    /// </summary>
    public async Task<IActionResult> Results(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(id, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz == null)
            return NotFound();

        var attempts = await _context.QuizAttempts
            .Include(a => a.Student)
            .Where(a => a.QuizId == id)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync();

        ViewBag.Quiz = quiz;
        return View(attempts);
    }

    /// <summary>
    /// إدارة أسئلة الاختبار - Manage quiz questions
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Questions(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(quizId, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var questions = await _quizService.GetQuizQuestionsAsync(quizId, userId);

        // Map to entities for view
        var questionEntities = questions.Select(q => new Question
        {
            Id = q.Id,
            QuizId = q.QuizId,
            QuestionText = q.QuestionText,
            Type = Enum.Parse<QuestionType>(q.Type),
            Points = q.Points,
            OrderIndex = q.OrderIndex,
            Options = q.Options.Select(o => new QuestionOption
            {
                Id = o.Id,
                QuestionId = o.QuestionId,
                OptionText = o.OptionText,
                IsCorrect = o.IsCorrect
            }).ToList()
        }).OrderBy(q => q.OrderIndex).ToList();

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        ViewBag.Quiz = quiz;
        return View(questionEntities);
    }

    /// <summary>
    /// إضافة سؤال للاختبار - Add question to quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(QuestionCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            SetErrorMessage("يرجى التحقق من صحة البيانات المدخلة");
            return RedirectToAction(nameof(Questions), new { quizId = model.QuizId });
        }

        try
        {
            var request = new CreateQuestionRequest
            {
                QuizId = model.QuizId,
                QuestionText = model.QuestionText,
                QuestionType = model.QuestionType,
                Points = model.Points,
                Options = model.Options.Select(o => new CreateQuestionOptionRequest
                {
                    OptionText = o.Text,
                    IsCorrect = o.IsCorrect
                }).ToList()
            };

            var result = await _quizService.AddQuestionAsync(model.QuizId, request, userId);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم إضافة السؤال بنجاح");
            }
            else
            {
                SetErrorMessage(result.Error ?? "حدث خطأ أثناء إضافة السؤال");
            }

            return RedirectToAction(nameof(Questions), new { quizId = model.QuizId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding question to quiz {QuizId}", model.QuizId);
            SetErrorMessage("حدث خطأ أثناء إضافة السؤال");
            return RedirectToAction(nameof(Questions), new { quizId = model.QuizId });
        }
    }

    /// <summary>
    /// حذف سؤال من الاختبار - Delete question from quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int questionId, int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.DeleteQuestionAsync(questionId, quizId, userId);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم حذف السؤال بنجاح");
        }
        else
        {
            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف السؤال");
        }

        return RedirectToAction(nameof(Questions), new { quizId });
    }

    /// <summary>
    /// محاولات الاختبار - Quiz attempts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Attempts(int quizId, int page = 1)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(quizId, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var pageSize = await _configService.GetPaginationSizeAsync("quiz_attempts", 20);

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
            return NotFound();

        var query = _context.QuizAttempts
            .Include(a => a.Student)
            .Where(a => a.QuizId == quizId);

        var totalCount = await query.CountAsync();
        var attempts = await query
            .OrderByDescending(a => a.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Quiz = quiz;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Get statistics
        var statistics = await _quizService.GetQuizStatisticsAsync(quizId, userId);
        ViewBag.Stats = new
        {
            TotalAttempts = statistics.TotalAttempts,
            CompletedAttempts = statistics.CompletedAttempts,
            AverageScore = statistics.AverageScore,
            PassRate = statistics.PassRate
        };

        return View(attempts);
    }

    /// <summary>
    /// تحليلات الاختبار - Quiz analytics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Analytics(int quizId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.GetQuizByIdAsync(quizId, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var statistics = await _quizService.GetQuizStatisticsAsync(quizId, userId);

        var quiz = await _context.Quizzes
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(q => q.Questions)
            .Include(q => q.Attempts)
                .ThenInclude(a => a.Student)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
            return NotFound();

        ViewBag.Quiz = quiz;
        ViewBag.Analytics = new
        {
            TotalAttempts = statistics.TotalAttempts,
            CompletedAttempts = statistics.CompletedAttempts,
            InProgressAttempts = statistics.InProgressAttempts,
            AverageScore = statistics.AverageScore,
            HighestScore = statistics.HighestScore,
            LowestScore = statistics.LowestScore,
            PassRate = statistics.PassRate,
            AverageCompletionTime = statistics.AverageCompletionTime,
            QuestionCount = statistics.QuestionCount,
            TotalPoints = statistics.TotalPoints
        };
        ViewBag.ScoreDistribution = statistics.ScoreDistribution.Select(s => new
        {
            Label = s.Label,
            Count = s.Count
        }).ToList();
        ViewBag.RecentAttempts = quiz.Attempts
            .OrderByDescending(a => a.StartedAt)
            .Take(10)
            .ToList();

        return View();
    }

    /// <summary>
    /// حذف الاختبار - Delete quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get quiz to get courseId before deletion
        var quizResult = await _quizService.GetQuizByIdAsync(id, userId);
        if (!quizResult.IsSuccess || quizResult.Value == null)
        {
            return NotFound();
        }

        var courseId = quizResult.Value.CourseId;
        var quizTitle = quizResult.Value.Title;

        var result = await _quizService.DeleteQuizAsync(id, userId);

        if (result.IsSuccess)
        {
            SetSuccessMessage($"تم حذف الاختبار '{quizTitle}' بنجاح");
            return RedirectToAction(nameof(Index), new { courseId });
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف الاختبار");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// نسخ الاختبار - Duplicate quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _quizService.DuplicateQuizAsync(id, userId);

        if (result.IsSuccess && result.Value != null)
        {
            var originalQuiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id);

            var questionCount = originalQuiz?.Questions.Count ?? 0;
            SetSuccessMessage($"تم نسخ الاختبار بنجاح. تم إنشاء {questionCount} سؤال");
            return RedirectToAction(nameof(Edit), new { id = result.Value.Id });
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء نسخ الاختبار");
        return RedirectToAction(nameof(Index));
    }
}
