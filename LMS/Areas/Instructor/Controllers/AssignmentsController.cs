using LMS.Areas.Instructor.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Data.SqlClient;
using System.Net.Sockets;
using System.IO;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة التكليفات - Assignments Controller
/// Refactored to use service layer with proper enterprise patterns
/// Enhanced with comprehensive error handling and validation
/// </summary>
public class AssignmentsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAssignmentService _assignmentService;
    private readonly ISystemConfigurationService _configService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AssignmentsController> _logger;

    // Cache configuration
    private static readonly TimeSpan LessonsCacheExpiration = TimeSpan.FromMinutes(5);
    private const string AvailableLessonsCacheKeyPrefix = "AvailableLessons_";

    public AssignmentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAssignmentService assignmentService,
        ISystemConfigurationService configService,
        IMemoryCache cache,
        ILogger<AssignmentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _assignmentService = assignmentService;
        _configService = configService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التكليفات - Assignments list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, string? search, string? status, string? sortBy, int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var pageSize = await _configService.GetPaginationSizeAsync("assignments", 10);

            var filter = new AssignmentFilterRequest
            {
                CourseId = courseId,
                Search = search,
                Status = status,
                SortBy = sortBy ?? "CreatedAt",
                SortDescending = true,
                Page = page,
                PageSize = pageSize
            };

            var assignmentsResult = await _assignmentService.GetInstructorAssignmentsAsync(userId, filter);
            var statistics = await _assignmentService.GetInstructorAssignmentStatisticsAsync(userId, courseId);

            // Map DTOs to entities for view compatibility
            var assignments = assignmentsResult.Items.Select(dto => new Assignment
            {
                Id = dto.Id,
                LessonId = dto.LessonId,
                Title = dto.Title,
                Description = dto.Description,
                Instructions = dto.Instructions,
                DueDate = dto.DueDate,
                MaxPoints = dto.MaxGrade,
                PassingPoints = dto.PassingScore,
                AllowLateSubmission = dto.AllowLateSubmission,
                LatePenaltyPercentage = dto.LatePenaltyPercentage,
                MaxFileSizeMB = dto.MaxFileSizeMB ?? 10,
                AcceptedFileTypes = dto.AcceptedFileTypes,
                CreatedAt = dto.CreatedAt,
                Lesson = new Domain.Entities.Courses.Lesson
                {
                    Id = dto.LessonId,
                    Title = dto.LessonTitle,
                    Module = new Domain.Entities.Courses.Module
                    {
                        Title = dto.ModuleTitle,
                        Course = new Domain.Entities.Courses.Course
                        {
                            Id = dto.CourseId,
                            Title = dto.CourseTitle
                        }
                    }
                },
                Submissions = new List<AssignmentSubmission>()
            }).ToList();

            // Populate ViewBag with all required data
            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.CourseId = courseId;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.SortBy = sortBy;

            // Statistics for dashboard cards
            ViewBag.TotalAssignments = statistics.TotalAssignments;
            ViewBag.PendingSubmissions = statistics.PendingSubmissions;
            ViewBag.GradedSubmissions = statistics.GradedSubmissions;
            ViewBag.AverageGrade = Math.Round(statistics.AverageGrade, 1);
            ViewBag.SubmissionCounts = statistics.SubmissionCounts;
            ViewBag.PendingCounts = statistics.PendingCounts;
            ViewBag.GradedCounts = statistics.GradedCounts;
            ViewBag.AverageGrades = statistics.AverageGrades;

            // Pagination
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = assignmentsResult.TotalPages;
            ViewBag.TotalItems = assignmentsResult.TotalCount;
            ViewBag.PageSize = pageSize;

            return View(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assignments for instructor");
            SetErrorMessage("حدث خطأ أثناء تحميل التكليفات");
            return View(new List<Assignment>());
        }
    }

    /// <summary>
    /// إنشاء تكليف - Create assignment
    /// Enhanced with comprehensive error handling and validation
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? lessonId)
    {
        var userId = _currentUserService.UserId;
        
        // Enhanced user validation
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "Create assignment accessed without valid user. " +
                "Authenticated: {IsAuthenticated}, UserName: {UserName}, Claims: {Claims}",
                _currentUserService.IsAuthenticated,
                _currentUserService.UserName,
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        _logger.LogInformation(
            "Create assignment page accessed. LessonId: {LessonId}, UserId: {UserId}, HasLessonId: {HasLessonId}",
            lessonId,
            userId,
            lessonId.HasValue);

        try
        {
            // Validate database connection with timeout
            if (!await ValidateDatabaseConnectionAsync())
            {
                _logger.LogError("Database connection validation failed for user {UserId}", userId);
                ViewBag.ErrorMessage = "لا يمكن الاتصال بقاعدة البيانات. يرجى المحاولة لاحقاً";
                ViewBag.ShowRetryButton = true;
                ViewBag.Lessons = new List<dynamic>();
                return View(new AssignmentCreateViewModel());
            }

            // If no lessonId provided, show lesson selection
            if (!lessonId.HasValue)
            {
                var lessonsResult = await GetAvailableLessonsForAssignmentAsync(userId);
                
                if (!lessonsResult.IsSuccess)
                {
                    _logger.LogError(
                        "Failed to load available lessons for user {UserId}: {Error}",
                        userId, lessonsResult.Error);
                    
                    // Instead of redirecting, show error on Create page with retry option
                    ViewBag.ErrorMessage = lessonsResult.Error ?? "حدث خطأ أثناء تحميل قائمة الدروس المتاحة";
                    ViewBag.ShowRetryButton = true;
                    ViewBag.Lessons = new List<dynamic>();
                    return View(new AssignmentCreateViewModel());
                }

                var lessons = lessonsResult.Value?.Cast<dynamic>() ?? new List<dynamic>();
                
                // Log if no lessons available
                if (!lessons.Any())
                {
                    _logger.LogInformation(
                        "No available lessons found for instructor {InstructorId}",
                        userId);
                    ViewBag.InfoMessage = "لا توجد دروس متاحة لإضافة تكليف. جميع الدروس تحتوي على تكليفات بالفعل.";
                }

                ViewBag.Lessons = lessons;
                return View(new AssignmentCreateViewModel());
            }

            // Get lesson with validation
            var lessonResult = await GetLessonWithValidationAsync(lessonId.Value, userId);
            
            if (!lessonResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to load lesson {LessonId} for instructor {InstructorId}: {Error}",
                    lessonId,
                    userId,
                    lessonResult.Error);
                // Stay on the Create page and show error with retry option
                ViewBag.ErrorMessage = lessonResult.Error ?? "الدرس المطلوب غير موجود أو ليس لديك صلاحية للوصول إليه";
                ViewBag.ShowRetryButton = true;
                ViewBag.Lessons = new List<dynamic>();
                return View(new AssignmentCreateViewModel());
            }

            var lesson = lessonResult.Value;
            
            if (lesson == null)
            {
                _logger.LogWarning("Lesson {LessonId} not found for instructor {InstructorId}", lessonId, userId);
                ViewBag.ErrorMessage = "الدرس المطلوب غير موجود أو ليس لديك صلاحية للوصول إليه";
                ViewBag.ShowRetryButton = true;
                ViewBag.Lessons = new List<dynamic>();
                return View(new AssignmentCreateViewModel());
            }

            // Check if lesson already has assignment
            if (lesson.Assignment != null)
            {
                _logger.LogInformation(
                    "Lesson {LessonId} already has assignment {AssignmentId}",
                    lessonId,
                    lesson.Assignment.Id);
                ViewBag.ErrorMessage = "هذا الدرس يحتوي على تكليف بالفعل. يمكنك تعديل التكليف الحالي بدلاً من إنشاء تكليف جديد.";
                ViewBag.Lessons = new List<dynamic>();
                return View(new AssignmentCreateViewModel());
            }

            ViewBag.Lesson = lesson;
            return View(new AssignmentCreateViewModel { LessonId = lessonId.Value });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(
                dbEx,
                "Database error in Create. LessonId: {LessonId}, UserId: {UserId}, Inner: {Inner}",
                lessonId,
                userId,
                dbEx.InnerException?.Message);
            ViewBag.ErrorMessage = "حدث خطأ في قاعدة البيانات. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني";
            ViewBag.ShowRetryButton = true;
            ViewBag.Lessons = new List<dynamic>();
            return View(new AssignmentCreateViewModel());
        }
        catch (InvalidOperationException invEx)
        {
            _logger.LogError(
                invEx,
                "Invalid operation in Create. LessonId: {LessonId}, UserId: {UserId}, Message: {Message}",
                lessonId,
                userId,
                invEx.Message);
            ViewBag.ErrorMessage = "عملية غير صالحة. يرجى التحقق من البيانات والمحاولة مرة أخرى";
            ViewBag.ShowRetryButton = true;
            ViewBag.Lessons = new List<dynamic>();
            return View(new AssignmentCreateViewModel());
        }
        catch (NullReferenceException nullEx)
        {
            _logger.LogError(
                nullEx,
                "Null reference in Create. LessonId: {LessonId}, UserId: {UserId}, Stack: {StackTrace}",
                lessonId,
                userId,
                nullEx.StackTrace);
            ViewBag.ErrorMessage = "بيانات غير مكتملة. يرجى المحاولة مرة أخرى";
            ViewBag.ShowRetryButton = true;
            ViewBag.Lessons = new List<dynamic>();
            return View(new AssignmentCreateViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error in Create. LessonId: {LessonId}, UserId: {UserId}, Exception: {ExceptionType}, Message: {Message}",
                lessonId,
                userId,
                ex.GetType().Name,
                ex.Message);
            ViewBag.ErrorMessage = "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى";
            ViewBag.ShowRetryButton = true;
            ViewBag.Lessons = new List<dynamic>();
            return View(new AssignmentCreateViewModel());
        }
    }

    /// <summary>
    /// حفظ التكليف - Save assignment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssignmentCreateViewModel model)
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
            var request = new CreateAssignmentRequest
            {
                LessonId = model.LessonId,
                Title = model.Title,
                Description = model.Description,
                Instructions = model.Instructions,
                DueDate = model.DueDate,
                MaxGrade = model.MaxGrade,
                PassingScore = model.PassingScore,
                AllowLateSubmissions = model.AllowLateSubmissions,
                LatePenaltyPercentage = model.LatePenaltyPercentage,
                MaxFileSizeMB = model.MaxFileSizeMB,
                AcceptedFileTypes = model.AcceptedFileTypes,
                InstructorId = userId
            };

            var result = await _assignmentService.CreateAssignmentAsync(request);

            if (result.IsSuccess && result.Value != null)
            {
                // Invalidate cache for available lessons since a new assignment was created
                InvalidateAvailableLessonsCache(_currentUserService.UserId!);

                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == model.LessonId);

                SetSuccessMessage("تم إنشاء التكليف بنجاح");
                return RedirectToAction(nameof(Index), new { courseId = lesson?.Module?.CourseId });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء إنشاء التكليف");
            
            var lessonForView = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId);
            ViewBag.Lesson = lessonForView;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assignment for lesson {LessonId}", model.LessonId);
            SetErrorMessage("حدث خطأ أثناء إنشاء التكليف");
            
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId);
            ViewBag.Lesson = lesson;
            return View(model);
        }
    }

    /// <summary>
    /// تعديل التكليف - Edit assignment (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in AssignmentsController.Edit");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var result = await _assignmentService.GetAssignmentByIdAsync(id, userId);

            if (!result.IsSuccess || result.Value == null)
            {
                _logger.LogWarning("Assignment {AssignmentId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            var assignmentDto = result.Value;

            var viewModel = new AssignmentEditViewModel
            {
                Id = assignmentDto.Id,
                LessonId = assignmentDto.LessonId,
                Title = assignmentDto.Title,
                Description = assignmentDto.Description,
                Instructions = assignmentDto.Instructions,
                DueDate = assignmentDto.DueDate,
                MaxGrade = assignmentDto.MaxGrade,
                PassingScore = assignmentDto.PassingScore,
                AllowLateSubmissions = assignmentDto.AllowLateSubmission,
                LatePenaltyPercentage = assignmentDto.LatePenaltyPercentage,
                MaxFileSizeMB = assignmentDto.MaxFileSizeMB ?? 10,
                AcceptedFileTypes = assignmentDto.AcceptedFileTypes,
                SubmissionsCount = assignmentDto.SubmissionCount
            };

            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == assignmentDto.LessonId);

            ViewBag.Lesson = lesson;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AssignmentsController.Edit GET for assignment {AssignmentId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل الصفحة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ تعديلات التكليف - Save assignment edits (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AssignmentEditViewModel model)
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
            var assignmentResult = await _assignmentService.GetAssignmentByIdAsync(id, userId);
            if (assignmentResult.IsSuccess && assignmentResult.Value != null)
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == assignmentResult.Value.LessonId);

                ViewBag.Lesson = lesson;
                model.SubmissionsCount = assignmentResult.Value.SubmissionCount;
            }
            return View(model);
        }

        try
        {
            var request = new UpdateAssignmentRequest
            {
                Id = model.Id,
                LessonId = model.LessonId,
                Title = model.Title,
                Description = model.Description,
                Instructions = model.Instructions,
                DueDate = model.DueDate,
                MaxGrade = model.MaxGrade,
                PassingScore = model.PassingScore,
                AllowLateSubmissions = model.AllowLateSubmissions,
                LatePenaltyPercentage = model.LatePenaltyPercentage,
                MaxFileSizeMB = model.MaxFileSizeMB,
                AcceptedFileTypes = model.AcceptedFileTypes,
                InstructorId = userId
            };

            var result = await _assignmentService.UpdateAssignmentAsync(id, request);

            if (result.IsSuccess && result.Value != null)
            {
                SetSuccessMessage("تم تحديث التكليف بنجاح");
                return RedirectToAction(nameof(Index), new { courseId = result.Value.CourseId });
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء تحديث التكليف");

            var assignmentResult = await _assignmentService.GetAssignmentByIdAsync(id, userId);
            if (assignmentResult.IsSuccess && assignmentResult.Value != null)
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == assignmentResult.Value.LessonId);

                ViewBag.Lesson = lesson;
                model.SubmissionsCount = assignmentResult.Value.SubmissionCount;
            }
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating assignment {AssignmentId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث التكليف");

            var assignmentResult = await _assignmentService.GetAssignmentByIdAsync(id, userId);
            if (assignmentResult.IsSuccess && assignmentResult.Value != null)
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .FirstOrDefaultAsync(l => l.Id == assignmentResult.Value.LessonId);

                ViewBag.Lesson = lesson;
                model.SubmissionsCount = assignmentResult.Value.SubmissionCount;
            }
            return View(model);
        }
    }

    /// <summary>
    /// حذف التكليف - Delete assignment
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

        // Get assignment to get courseId before deletion
        var assignmentResult = await _assignmentService.GetAssignmentByIdAsync(id, userId);
        if (!assignmentResult.IsSuccess || assignmentResult.Value == null)
        {
            return NotFound();
        }

        var courseId = assignmentResult.Value.CourseId;
        var assignmentTitle = assignmentResult.Value.Title;

        var result = await _assignmentService.DeleteAssignmentAsync(id, userId);

        if (result.IsSuccess)
        {
            // Invalidate cache since a lesson is now available for assignment
            InvalidateAvailableLessonsCache(userId);
            
            SetSuccessMessage($"تم حذف التكليف '{assignmentTitle}' بنجاح");
            return RedirectToAction(nameof(Index), new { courseId });
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء حذف التكليف");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// التسليمات المعلقة - Pending submissions
    /// </summary>
    public async Task<IActionResult> Submissions(AssignmentStatus? status, int? assignmentId, string? search, int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var pageSize = await _configService.GetPaginationSizeAsync("submissions", 20);

            var filter = new SubmissionFilterRequest
            {
                AssignmentId = assignmentId,
                Status = status,
                Search = search,
                Page = page,
                PageSize = pageSize,
                SortBy = "SubmittedAt",
                SortDescending = true
            };

            var submissionsResult = await _assignmentService.GetSubmissionsAsync(userId, filter);

            // Get assignment details if assignmentId is provided
            AssignmentDto? assignmentDto = null;
            if (assignmentId.HasValue)
            {
                var assignmentResult = await _assignmentService.GetAssignmentByIdAsync(assignmentId.Value, userId);
                if (assignmentResult.IsSuccess && assignmentResult.Value != null)
                {
                    assignmentDto = assignmentResult.Value;
                }
            }

            // Get statistics for the assignment if provided
            AssignmentDetailStatisticsDto? assignmentStats = null;
            if (assignmentId.HasValue)
            {
                assignmentStats = await _assignmentService.GetAssignmentStatisticsAsync(assignmentId.Value, userId);
            }

            // Map DTOs to ViewModels
            var submissions = submissionsResult.Items.Select(dto => new SubmissionDisplayViewModel
            {
                Id = dto.Id,
                StudentName = dto.StudentName,
                StudentEmail = dto.StudentEmail,
                AssignmentTitle = dto.AssignmentTitle,
                CourseName = dto.CourseName,
                SubmittedAt = dto.SubmittedAt ?? DateTime.UtcNow,
                Status = dto.Status,
                Grade = dto.Grade,
                IsLate = dto.IsLate
            }).ToList();

            // Populate ViewBag with all required data
            ViewBag.AssignmentTitle = assignmentDto?.Title;
            ViewBag.AssignmentId = assignmentId;
            ViewBag.Status = status;
            ViewBag.SearchTerm = filter.Search;
            
            // Statistics
            ViewBag.TotalSubmissions = assignmentStats?.TotalSubmissions ?? submissionsResult.TotalCount;
            ViewBag.PendingCount = assignmentStats?.PendingSubmissions ?? submissions.Count(s => s.Status == AssignmentStatus.Submitted);
            ViewBag.GradedCount = assignmentStats?.GradedSubmissions ?? submissions.Count(s => s.Status == AssignmentStatus.Graded);
            ViewBag.AverageGrade = assignmentStats?.AverageGrade ?? (submissions.Any(s => s.Grade.HasValue) 
                ? Math.Round((decimal)submissions.Where(s => s.Grade.HasValue).Average(s => s.Grade!.Value), 1) 
                : 0);

            // Pagination
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = submissionsResult.TotalPages;
            ViewBag.TotalItems = submissionsResult.TotalCount;
            ViewBag.PageSize = pageSize;

            return View(submissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading submissions for instructor");
            SetErrorMessage("حدث خطأ أثناء تحميل التسليمات");
            return View(new List<SubmissionDisplayViewModel>());
        }
    }

    /// <summary>
    /// تقييم التسليم - Grade submission (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Grade(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _assignmentService.GetSubmissionByIdAsync(id, userId);

        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var submissionDto = result.Value;

        var viewModel = new GradeSubmissionViewModel
        {
            SubmissionId = submissionDto.Id,
            Grade = submissionDto.Grade ?? 0,
            Feedback = submissionDto.Feedback
        };

        // Get submission entity for view
        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == id);

        ViewBag.Submission = submission;
        ViewBag.MaxGrade = submission?.Assignment.MaxPoints ?? 100;

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التقييم - Save grade (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(int id, GradeSubmissionViewModel model)
    {
        if (id != model.SubmissionId)
            return BadRequest();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.Id == id);
            ViewBag.Submission = submission;
            ViewBag.MaxGrade = submission?.Assignment.MaxPoints ?? 100;
            return View(model);
        }

        try
        {
            var request = new GradeSubmissionRequest
            {
                SubmissionId = model.SubmissionId,
                Grade = model.Grade,
                Feedback = model.Feedback
            };

            var result = await _assignmentService.GradeSubmissionAsync(id, request, userId);

            if (result.IsSuccess)
            {
                SetSuccessMessage("تم تقييم التسليم بنجاح وإرسال إشعار للطالب");
                return RedirectToAction(nameof(Submissions));
            }

            SetErrorMessage(result.Error ?? "حدث خطأ أثناء حفظ التقييم. يرجى المحاولة مرة أخرى");
            
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.Id == id);
            ViewBag.Submission = submission;
            ViewBag.MaxGrade = submission?.Assignment.MaxPoints ?? 100;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grading assignment submission {SubmissionId}", id);
            SetErrorMessage("حدث خطأ أثناء حفظ التقييم. يرجى المحاولة مرة أخرى");
            
            var submission = await _context.AssignmentSubmissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.Id == id);
            ViewBag.Submission = submission;
            ViewBag.MaxGrade = submission?.Assignment.MaxPoints ?? 100;
            return View(model);
        }
    }

    /// <summary>
    /// عرض تفاصيل التسليم - View submission details
    /// </summary>
    public async Task<IActionResult> ViewSubmission(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _assignmentService.GetSubmissionByIdAsync(id, userId);

        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        // Get submission entity for view
        var submission = await _context.AssignmentSubmissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Include(s => s.Student)
            .Include(s => s.Grader)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
            return NotFound();

        return View(submission);
    }

    /// <summary>
    /// نسخ التكليف - Duplicate assignment
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

        var result = await _assignmentService.DuplicateAssignmentAsync(id, userId);

        if (result.IsSuccess && result.Value != null)
        {
            SetSuccessMessage("تم نسخ التكليف بنجاح");
            return RedirectToAction(nameof(Index), new { courseId = result.Value.CourseId });
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء نسخ التكليف");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// طلب إعادة التسليم - Request resubmission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestResubmission(int id, string? feedback)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (string.IsNullOrWhiteSpace(feedback))
        {
            SetErrorMessage("يجب تحديد سبب طلب إعادة التسليم");
            return RedirectToAction(nameof(ViewSubmission), new { id });
        }

        var result = await _assignmentService.RequestResubmissionAsync(id, feedback, userId);

        if (result.IsSuccess)
        {
            SetSuccessMessage("تم طلب إعادة التسليم من الطالب وإرسال إشعار");
            return RedirectToAction(nameof(Submissions));
        }

        SetErrorMessage(result.Error ?? "حدث خطأ أثناء معالجة الطلب");
        return RedirectToAction(nameof(Submissions));
    }

    /// <summary>
    /// تفاصيل التكليف - Assignment details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _assignmentService.GetAssignmentByIdAsync(id, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var statistics = await _assignmentService.GetAssignmentStatisticsAsync(id, userId);

        // Get assignment entity for view
        var assignment = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Student)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null)
        {
            return NotFound();
        }

        ViewBag.Assignment = assignment;
        ViewBag.Stats = new
        {
            TotalSubmissions = statistics.TotalSubmissions,
            PendingSubmissions = statistics.PendingSubmissions,
            GradedSubmissions = statistics.GradedSubmissions,
            NotSubmittedCount = statistics.NotSubmittedCount,
            LateSubmissions = statistics.LateSubmissions,
            AverageGrade = statistics.AverageGrade,
            HighestGrade = statistics.HighestGrade,
            LowestGrade = statistics.LowestGrade,
            OnTimeSubmissionRate = statistics.OnTimeSubmissionRate
        };
        ViewBag.RecentSubmissions = assignment.Submissions
            .OrderByDescending(s => s.SubmittedAt)
            .Take(10)
            .ToList();

        return View(assignment);
    }

    /// <summary>
    /// تحليلات التكليف - Assignment analytics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Analytics(int assignmentId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var result = await _assignmentService.GetAssignmentByIdAsync(assignmentId, userId);
        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        var statistics = await _assignmentService.GetAssignmentStatisticsAsync(assignmentId, userId);

        var assignment = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Student)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);

        if (assignment == null)
        {
            return NotFound();
        }

        ViewBag.Assignment = assignment;
        ViewBag.Analytics = new
        {
            TotalSubmissions = statistics.TotalSubmissions,
            PendingSubmissions = statistics.PendingSubmissions,
            GradedSubmissions = statistics.GradedSubmissions,
            NotSubmittedCount = statistics.NotSubmittedCount,
            LateSubmissions = statistics.LateSubmissions,
            AverageGrade = statistics.AverageGrade,
            HighestGrade = statistics.HighestGrade,
            LowestGrade = statistics.LowestGrade,
            OnTimeSubmissionRate = statistics.OnTimeSubmissionRate
        };

        // Grade distribution
        var gradeDistribution = assignment.Submissions
            .Where(s => s.Grade.HasValue)
            .GroupBy(s => Math.Floor((double)s.Grade!.Value / 10) * 10)
            .Select(g => new
            {
                Label = $"{g.Key}-{g.Key + 9}",
                Count = g.Count()
            })
            .OrderBy(x => x.Label)
            .ToList();

        ViewBag.GradeDistribution = gradeDistribution;
        ViewBag.RecentSubmissions = assignment.Submissions
            .OrderByDescending(s => s.SubmittedAt)
            .Take(10)
            .ToList();

        return View();
    }

    #region Helper Methods

    /// <summary>
    /// Validate database connection before operations
    /// Enhanced with timeout and comprehensive validation
    /// </summary>
    private async Task<bool> ValidateDatabaseConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var canConnect = await _context.Database.CanConnectAsync(cts.Token);
            
            if (!canConnect)
            {
                _logger.LogError("Database connection validation failed - CanConnect returned false");
                return false;
            }
            
            // Additional validation: try a simple query
            try
            {
                await _context.Database
                    .ExecuteSqlRawAsync("SELECT 1", cancellationToken: cts.Token);
                return true;
            }
            catch (Exception queryEx)
            {
                _logger.LogError(queryEx, "Database query validation failed");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Database connection validation timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection validation failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Get available lessons for assignment creation with comprehensive error handling
    /// Enhanced with enterprise patterns: retry logic, caching, detailed logging, null safety
    /// </summary>
    private async Task<Result<List<object>>> GetAvailableLessonsForAssignmentAsync(string userId)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetAvailableLessonsForAssignmentAsync called with null/empty userId");
            return Result<List<object>>.Failure("معرف المستخدم غير صالح");
        }

        // Try cache first
        var cacheKey = $"{AvailableLessonsCacheKeyPrefix}{userId}";
        if (_cache.TryGetValue(cacheKey, out List<object>? cachedLessons))
        {
            _logger.LogInformation("Retrieved available lessons from cache for user {UserId}", userId);
            return Result<List<object>>.Success(cachedLessons!);
        }

        const int maxRetries = 3;
        int attempt = 0;
        
        while (attempt < maxRetries)
        {
            try
            {
                attempt++;
                _logger.LogInformation(
                    "Fetching available lessons for instructor {InstructorId}, Attempt {Attempt}/{MaxRetries}",
                    userId, attempt, maxRetries);

                // Use AsNoTracking for read-only query (performance optimization)
                var lessonsQuery = _context.Lessons
                    .AsNoTracking()
                    .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                    .Where(l => l.Module != null &&
                               l.Module.Course != null &&
                               l.Module.Course.InstructorId == userId &&
                               !_context.Assignments.Any(a => a.LessonId == l.Id))
                    .AsQueryable();

                // Execute query with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var lessons = await lessonsQuery
                    .OrderBy(l => l.Module!.Course!.Title)
                    .ThenBy(l => l.Module!.Title)
                    .ThenBy(l => l.OrderIndex)
                    .Select(l => new
                    {
                        l.Id,
                        l.Title,
                        ModuleTitle = l.Module != null ? l.Module.Title : "غير محدد",
                        CourseTitle = l.Module != null && l.Module.Course != null
                            ? l.Module.Course.Title
                            : "غير محدد",
                        CourseId = l.Module != null && l.Module.Course != null
                            ? l.Module.Course.Id
                            : 0
                    })
                    .ToListAsync(cts.Token);

                _logger.LogInformation(
                    "Successfully fetched {Count} available lessons for instructor {InstructorId}",
                    lessons.Count, userId);

                // Handle empty result gracefully
                if (lessons.Count == 0)
                {
                    _logger.LogInformation(
                        "No available lessons found for instructor {InstructorId} (all lessons have assignments)",
                        userId);
                    var emptyResult = new List<object>();
                    
                    // Cache empty result for shorter duration
                    _cache.Set(cacheKey, emptyResult, TimeSpan.FromMinutes(1));
                    return Result<List<object>>.Success(emptyResult);
                }

                var result = lessons.Cast<object>().ToList();
                
                // Cache the result
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = LessonsCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.Normal
                };
                _cache.Set(cacheKey, result, cacheOptions);

                return Result<List<object>>.Success(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError(
                    "Timeout while fetching lessons for instructor {InstructorId}, Attempt {Attempt}",
                    userId, attempt);
                
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogWarning(
                        "Retrying after {Delay}ms due to timeout",
                        delay.TotalMilliseconds);
                    await Task.Delay(delay);
                    continue; // Retry
                }
                
                return Result<List<object>>.Failure(
                    "انتهت مهلة الاتصال بقاعدة البيانات. يرجى المحاولة مرة أخرى");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(
                    dbEx,
                    "Database error fetching available lessons for instructor {InstructorId}, Attempt {Attempt}/{MaxRetries}. " +
                    "InnerException: {InnerException}, StackTrace: {StackTrace}",
                    userId, attempt, maxRetries,
                    dbEx.InnerException?.Message,
                    dbEx.StackTrace);

                // Check if it's a transient error (retryable)
                if (IsTransientDatabaseError(dbEx) && attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    _logger.LogWarning(
                        "Transient database error detected, retrying after {Delay}ms",
                        delay.TotalMilliseconds);
                    await Task.Delay(delay);
                    continue; // Retry
                }

                return Result<List<object>>.Failure(
                    "حدث خطأ في قاعدة البيانات أثناء تحميل قائمة الدروس. يرجى المحاولة لاحقاً");
            }
            catch (InvalidOperationException invEx)
            {
                _logger.LogError(
                    invEx,
                    "Invalid operation while fetching lessons for instructor {InstructorId}. " +
                    "Message: {Message}, StackTrace: {StackTrace}",
                    userId, invEx.Message, invEx.StackTrace);
                
                return Result<List<object>>.Failure(
                    "عملية غير صالحة أثناء تحميل الدروس. يرجى التحقق من البيانات");
            }
            catch (NullReferenceException nullEx)
            {
                _logger.LogError(
                    nullEx,
                    "Null reference exception while fetching lessons for instructor {InstructorId}. " +
                    "StackTrace: {StackTrace}",
                    userId, nullEx.StackTrace);
                
                return Result<List<object>>.Failure(
                    "بيانات غير مكتملة. يرجى التحقق من صحة الدروس والوحدات");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error fetching available lessons for instructor {InstructorId}, Attempt {Attempt}. " +
                    "ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    userId, attempt, ex.GetType().Name, ex.Message, ex.StackTrace);
                
                if (attempt < maxRetries && IsRetryableException(ex))
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogWarning(
                        "Retryable exception detected, retrying after {Delay}ms",
                        delay.TotalMilliseconds);
                    await Task.Delay(delay);
                    continue; // Retry
                }
                
                return Result<List<object>>.Failure(
                    "حدث خطأ غير متوقع أثناء تحميل قائمة الدروس المتاحة. يرجى المحاولة لاحقاً");
            }
        }

        // Should never reach here, but handle gracefully
        return Result<List<object>>.Failure(
            "فشل تحميل قائمة الدروس بعد عدة محاولات. يرجى المحاولة لاحقاً");
    }

    /// <summary>
    /// Check if database error is transient (retryable)
    /// Based on SQL Server transient error numbers
    /// </summary>
    private bool IsTransientDatabaseError(DbUpdateException ex)
    {
        // SQL Server transient error numbers
        var transientErrorNumbers = new[] { 2, 53, 121, 233, 10053, 10054, 10060, 40197, 40501, 40613 };
        
        var sqlException = ex.InnerException as SqlException;
        if (sqlException != null)
        {
            return transientErrorNumbers.Contains(sqlException.Number);
        }
        
        // Check for connection-related exceptions
        return ex.InnerException is SocketException ||
               ex.InnerException is IOException ||
               ex.InnerException?.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
               ex.InnerException?.Message?.Contains("network", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Check if exception is retryable
    /// </summary>
    private bool IsRetryableException(Exception ex)
    {
        return ex is TimeoutException ||
               ex is OperationCanceledException ||
               ex is SocketException ||
               ex is IOException ||
               (ex.InnerException != null && IsRetryableException(ex.InnerException));
    }

    /// <summary>
    /// Invalidate cache for available lessons
    /// Call this when assignments are created/deleted to keep cache fresh
    /// </summary>
    private void InvalidateAvailableLessonsCache(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var cacheKey = $"{AvailableLessonsCacheKeyPrefix}{userId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Invalidated available lessons cache for user {UserId}", userId);
    }

    /// <summary>
    /// Get lesson with validation and proper error handling
    /// Uses Result pattern for consistent error handling
    /// </summary>
    private async Task<Result<Lesson?>> GetLessonWithValidationAsync(int lessonId, string userId)
    {
        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Assignments)
                .FirstOrDefaultAsync(l => l.Id == lessonId &&
                                         l.Module != null &&
                                         l.Module.Course != null &&
                                         l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning(
                    "Lesson {LessonId} not found or access denied for instructor {InstructorId}",
                    lessonId,
                    userId);
                return Result<Lesson?>.Failure("الدرس المطلوب غير موجود أو ليس لديك صلاحية للوصول إليه");
            }

            return Result<Lesson?>.Success(lesson);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(
                dbEx,
                "Database error fetching lesson {LessonId} for instructor {InstructorId}",
                lessonId,
                userId);
            return Result<Lesson?>.Failure("حدث خطأ في قاعدة البيانات أثناء تحميل الدرس");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching lesson {LessonId} for instructor {InstructorId}",
                lessonId,
                userId);
            return Result<Lesson?>.Failure("حدث خطأ أثناء تحميل الدرس");
        }
    }

    /// <summary>
    /// Health check endpoint for assignments functionality
    /// Enhanced with comprehensive diagnostics and monitoring
    /// </summary>
    [HttpGet("health/assignments")]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { status = "unhealthy", reason = "User not authenticated" });
            }

            var healthStatus = new
            {
                status = "healthy",
                userId,
                timestamp = DateTime.UtcNow,
                checks = new Dictionary<string, object>()
            };

            // Database connection check
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var dbConnected = await _context.Database.CanConnectAsync(cts.Token);
                healthStatus.checks["database"] = new
                {
                    connected = dbConnected,
                    status = dbConnected ? "ok" : "failed"
                };
                
                if (!dbConnected)
                {
                    healthStatus = new
                    {
                        status = "unhealthy",
                        userId,
                        timestamp = DateTime.UtcNow,
                        checks = healthStatus.checks
                    };
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Database health check failed");
                healthStatus.checks["database"] = new
                {
                    connected = false,
                    status = "error",
                    error = dbEx.Message
                };
                healthStatus = new
                {
                    status = "unhealthy",
                    userId,
                    timestamp = DateTime.UtcNow,
                    checks = healthStatus.checks
                };
            }

            // Test lesson query
            try
            {
                var lessonsResult = await GetAvailableLessonsForAssignmentAsync(userId);
                healthStatus.checks["lessonsQuery"] = new
                {
                    success = lessonsResult.IsSuccess,
                    status = lessonsResult.IsSuccess ? "ok" : "failed",
                    error = lessonsResult.Error,
                    count = lessonsResult.IsSuccess ? (lessonsResult.Value?.Count ?? 0) : 0
                };
                
                if (!lessonsResult.IsSuccess)
                {
                    healthStatus = new
                    {
                        status = "degraded",
                        userId,
                        timestamp = DateTime.UtcNow,
                        checks = healthStatus.checks
                    };
                }
            }
            catch (Exception queryEx)
            {
                _logger.LogError(queryEx, "Lessons query health check failed");
                healthStatus.checks["lessonsQuery"] = new
                {
                    success = false,
                    status = "error",
                    error = queryEx.Message
                };
                healthStatus = new
                {
                    status = "degraded",
                    userId,
                    timestamp = DateTime.UtcNow,
                    checks = healthStatus.checks
                };
            }

            // Cache check
            var cacheKey = $"{AvailableLessonsCacheKeyPrefix}{userId}";
            var cacheAvailable = _cache.TryGetValue(cacheKey, out _);
            healthStatus.checks["cache"] = new
            {
                available = cacheAvailable,
                status = cacheAvailable ? "ok" : "not_cached"
            };

            // User access check
            try
            {
                var canAccess = await _context.Courses
                    .AnyAsync(c => c.InstructorId == userId);
                healthStatus.checks["userAccess"] = new
                {
                    hasAccess = canAccess,
                    status = canAccess ? "ok" : "no_courses"
                };
            }
            catch (Exception accessEx)
            {
                _logger.LogError(accessEx, "User access check failed");
                healthStatus.checks["userAccess"] = new
                {
                    hasAccess = false,
                    status = "error",
                    error = accessEx.Message
                };
            }

            return Json(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Json(new
            {
                status = "error",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    #endregion
}
