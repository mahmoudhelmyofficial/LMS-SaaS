using System.Text.RegularExpressions;
using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Entities.Security;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة الدروس - Lessons Controller
/// </summary>
public class LessonsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LessonsController> _logger;

    public LessonsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<LessonsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة جميع الدروس - All lessons list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in LessonsController.Index");
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var query = _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Quizzes)
                .Include(l => l.Assignments)
                .Where(l => l.Module != null && l.Module.Course != null && l.Module.Course.InstructorId == userId);

            if (courseId.HasValue)
            {
                query = query.Where(l => l.Module.CourseId == courseId.Value);
            }

            var totalCount = await query.CountAsync();
            var lessons = await query
                .OrderBy(l => l.Module.CourseId)
                .ThenBy(l => l.Module.OrderIndex)
                .ThenBy(l => l.OrderIndex)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            // Get all lessons for statistics (before pagination)
            var allLessonsQuery = _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Where(l => l.Module != null && l.Module.Course != null && l.Module.Course.InstructorId == userId);
            
            if (courseId.HasValue)
            {
                allLessonsQuery = allLessonsQuery.Where(l => l.Module.CourseId == courseId.Value);
            }
            
            var allLessons = await allLessonsQuery.ToListAsync();
            
            // Calculate statistics by lesson type
            ViewBag.VideoLessons = allLessons.Count(l => l.Type == Domain.Enums.LessonType.Video);
            ViewBag.QuizLessons = allLessons.Count(l => l.Type == Domain.Enums.LessonType.Quiz);
            ViewBag.AssignmentLessons = allLessons.Count(l => l.Type == Domain.Enums.LessonType.Assignment);
            ViewBag.PublishedLessons = allLessons.Count(l => l.IsPublished);
            ViewBag.DraftLessons = allLessons.Count(l => !l.IsPublished);
            ViewBag.PreviewLessons = allLessons.Count(l => l.IsPreviewable);

            // Get modules for filter dropdown
            ViewBag.Modules = await _context.Modules
                .Include(m => m.Course)
                .Where(m => m.Course.InstructorId == userId)
                .OrderBy(m => m.Course.Title)
                .ThenBy(m => m.OrderIndex)
                .Select(m => new { m.Id, m.Title, CourseName = m.Course.Title })
                .ToListAsync();

            // Get instructor's courses for filter
            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.CourseId = courseId;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);
            ViewBag.TotalCount = totalCount;

            _logger.LogInformation("Instructor {InstructorId} viewed lessons list. Total: {Count}", userId, totalCount);

            return View(lessons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LessonsController.Index");
            SetWarningMessage("تعذر تحميل قائمة الدروس. يرجى المحاولة مرة أخرى.");
            
            // Return empty view instead of redirecting
            ViewBag.VideoLessons = 0;
            ViewBag.QuizLessons = 0;
            ViewBag.AssignmentLessons = 0;
            ViewBag.PublishedLessons = 0;
            ViewBag.DraftLessons = 0;
            ViewBag.PreviewLessons = 0;
            ViewBag.Courses = new List<object>();
            ViewBag.Modules = new List<object>();
            ViewBag.CourseId = courseId;
            ViewBag.Page = page;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalCount = 0;
            ViewBag.TotalItems = 0;
            ViewBag.PageSize = 20;
            
            return View(new List<Lesson>());
        }
    }

    /// <summary>
    /// إنشاء درس جديد - Create new lesson
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int moduleId)
    {
        var userId = _currentUserService.UserId;
            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == moduleId && m.Course != null && m.Course.InstructorId == userId);

        if (module == null)
            return NotFound();

        ViewBag.Module = module;
        return View(new LessonCreateViewModel { ModuleId = moduleId });
    }

    /// <summary>
    /// حفظ الدرس الجديد - Save new lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        var module = await _context.Modules
            .Include(m => m.Course)
            .Include(m => m.Lessons)
            .FirstOrDefaultAsync(m => m.Id == model.ModuleId && m.Course.InstructorId == userId);

        if (module == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            var maxOrder = module.Lessons.Any() ? module.Lessons.Max(l => l.OrderIndex) : 0;

            string? videoUrl = null;
            string? videoProvider = null;
            string? videoId = null;
            
            if (!string.IsNullOrWhiteSpace(model.VideoUrl))
            {
                videoUrl = model.VideoUrl.Trim();
                (videoProvider, videoId) = DetectVideoProvider(model.VideoUrl);
            }

            var durationSeconds = model.DurationSeconds;
            if (model.DurationMinutes.HasValue && model.DurationMinutes.Value > 0)
            {
                durationSeconds = model.DurationMinutes.Value * 60;
            }

            var lesson = new Lesson
            {
                ModuleId = model.ModuleId,
                Title = model.Title,
                Description = model.Description,
                Type = model.Type,
                VideoUrl = videoUrl,
                VideoProvider = videoProvider,
                VideoId = videoId,
                HtmlContent = model.HtmlContent,
                FileUrl = model.FileUrl,
                DurationSeconds = durationSeconds,
                IsPreviewable = model.IsPreviewable,
                IsDownloadable = model.IsDownloadable,
                MustComplete = model.MustComplete,
                OrderIndex = maxOrder + 1
            };

            // Handle content drip settings
            if (!string.IsNullOrWhiteSpace(model.ContentDripType) && model.ContentDripType != "Immediate")
            {
                if (Enum.TryParse<ContentDripType>(model.ContentDripType, out var dripType))
                {
                    lesson.ContentDrip = dripType;
                }
                lesson.AvailableAfterDays = model.AvailableAfterDays;
                lesson.AvailableFrom = model.AvailableFrom;
                lesson.PrerequisiteLessonId = model.PrerequisiteLessonId;
            }

            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة الدرس بنجاح");
            return RedirectToAction("Edit", "Courses", new { id = module.CourseId });
        }

        ViewBag.Module = module;
        return View(model);
    }

    /// <summary>
    /// تعديل الدرس - Edit lesson
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        var lesson = await _context.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == id && l.Module.Course.InstructorId == userId);

        if (lesson == null)
            return NotFound();

        var viewModel = new LessonEditViewModel
        {
            Id = lesson.Id,
            ModuleId = lesson.ModuleId,
            Title = lesson.Title,
            Description = lesson.Description,
            Type = lesson.Type,
            VideoUrl = lesson.VideoUrl,
            VideoProvider = lesson.VideoProvider,
            VideoId = lesson.VideoId,
            ExistingVideoUrl = lesson.VideoUrl,
            HtmlContent = lesson.HtmlContent,
            FileUrl = lesson.FileUrl,
            DurationSeconds = lesson.DurationSeconds,
            DurationMinutes = lesson.DurationSeconds > 0 ? lesson.DurationSeconds / 60 : null,
            IsPreviewable = lesson.IsPreviewable,
            IsDownloadable = lesson.IsDownloadable,
            MustComplete = lesson.MustComplete,
            OrderIndex = lesson.OrderIndex,
            AvailableAfterDays = lesson.AvailableAfterDays,
            AvailableFrom = lesson.AvailableFrom,
            CreatedAt = lesson.CreatedAt,
            UpdatedAt = lesson.UpdatedAt
        };

        ViewBag.Module = lesson.Module;
        ViewBag.ExistingResources = lesson.Resources?.ToList() ?? new List<LessonResource>();

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الدرس - Save lesson edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LessonEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == id && l.Module != null && l.Module.Course != null && l.Module.Course.InstructorId == userId);

        if (lesson == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            lesson.Title = model.Title;
            lesson.Description = model.Description;
            lesson.Type = model.Type;
            
            if (!string.IsNullOrWhiteSpace(model.VideoUrl))
            {
                lesson.VideoUrl = model.VideoUrl.Trim();
                var (provider, videoId) = DetectVideoProvider(model.VideoUrl);
                lesson.VideoProvider = provider;
                lesson.VideoId = videoId;
            }
            else
            {
                lesson.VideoUrl = null;
                lesson.VideoProvider = null;
                lesson.VideoId = null;
            }
            
            lesson.HtmlContent = model.HtmlContent;
            lesson.FileUrl = model.FileUrl;
            
            if (model.DurationMinutes.HasValue && model.DurationMinutes.Value > 0)
            {
                lesson.DurationSeconds = model.DurationMinutes.Value * 60;
            }
            else if (model.DurationSeconds > 0)
            {
                lesson.DurationSeconds = model.DurationSeconds;
            }
            
            lesson.IsPreviewable = model.IsPreviewable;
            lesson.IsDownloadable = model.IsDownloadable;
            lesson.MustComplete = model.MustComplete;
            lesson.AvailableAfterDays = model.AvailableAfterDays;
            lesson.AvailableFrom = model.AvailableFrom;

            // Handle content drip settings
            if (!string.IsNullOrWhiteSpace(model.ContentDripType) && model.ContentDripType != "Immediate")
            {
                if (Enum.TryParse<ContentDripType>(model.ContentDripType, out var dripType))
                {
                    lesson.ContentDrip = dripType;
                }
                lesson.AvailableAfterDays = model.AvailableAfterDays;
                lesson.AvailableFrom = model.AvailableFrom;
                lesson.PrerequisiteLessonId = model.PrerequisiteLessonId;
            }
            else
            {
                // Reset content drip if set to Immediate
                lesson.ContentDrip = null;
                lesson.PrerequisiteLessonId = null;
            }

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الدرس بنجاح");
            return RedirectToAction(nameof(Edit), new { id });
        }

        ViewBag.Module = lesson.Module;
        return View(model);
    }

    private static (string provider, string? videoId) DetectVideoProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ("Local", null);
        
        url = url.Trim();
        
        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
        {
            string? videoId = null;
            if (url.Contains("youtu.be/"))
            {
                videoId = url.Split("youtu.be/").LastOrDefault()?.Split('?').FirstOrDefault();
            }
            else if (url.Contains("v="))
            {
                videoId = url.Split("v=").LastOrDefault()?.Split('&').FirstOrDefault();
            }
            else if (url.Contains("/embed/"))
            {
                videoId = url.Split("/embed/").LastOrDefault()?.Split('?').FirstOrDefault();
            }
            return ("YouTube", videoId);
        }
        
        if (url.Contains("vimeo.com"))
        {
            var match = Regex.Match(url, @"vimeo\.com\/(\d+)");
            var videoId = match.Success ? match.Groups[1].Value : null;
            return ("Vimeo", videoId);
        }
        
        return ("Local", null);
    }

    /// <summary>
    /// حذف الدرس - Delete lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        int? courseId = null;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                        .ThenInclude(c => c.Enrollments)
                .Include(l => l.Quizzes)
                .Include(l => l.Assignments)
                .FirstOrDefaultAsync(l => l.Id == id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("Lesson {LessonId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("الدرس غير موجود أو ليس لديك صلاحية الوصول إليه");
                return RedirectToAction("Index", "Courses");
            }

            courseId = lesson.Module.CourseId;

            // Check for student progress
            var hasStudentProgress = await _context.LessonProgresses
                .AnyAsync(lp => lp.LessonId == id);

            var enrollmentCount = lesson.Module.Course.Enrollments?.Count ?? 0;
            var isPublished = lesson.Module.IsPublished;

            // Use BusinessRuleHelper for validation
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteLesson(
                hasStudentProgress,
                enrollmentCount,
                isPublished);

            if (!canDelete)
            {
                _logger.LogWarning("Cannot delete lesson {LessonId}: {Reason}", id, reason);
                SetErrorMessage(reason ?? "لا يمكن حذف هذا الدرس");
                return RedirectToAction("Edit", "Courses", new { id = courseId });
            }

            // Check if lesson has quiz or assignment with attempts
            if (lesson.Quiz != null || lesson.Assignment != null)
            {
                var hasAttempts = false;
                if (lesson.Quiz != null)
                {
                    hasAttempts = await _context.QuizAttempts.AnyAsync(qa => qa.QuizId == lesson.Quiz.Id);
                }
                if (lesson.Assignment != null)
                {
                    hasAttempts = hasAttempts || await _context.AssignmentSubmissions
                        .AnyAsync(s => s.AssignmentId == lesson.Assignment.Id);
                }

                if (hasAttempts)
                {
                    _logger.LogWarning("Cannot delete lesson {LessonId} with quiz/assignment attempts", id);
                    SetErrorMessage("لا يمكن حذف الدرس لأنه يحتوي على اختبار أو تكليف تم حله من قبل الطلاب");
                    return RedirectToAction("Edit", "Courses", new { id = courseId });
                }
            }

            var lessonTitle = lesson.Title;
            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                // ── Quiz related entities ───────────────────────────────────
                if (lesson.Quiz != null)
                {
                    var questions = await _context.Questions
                        .IgnoreQueryFilters()
                        .Where(q => q.QuizId == lesson.Quiz.Id)
                        .ToListAsync();
                    foreach (var question in questions)
                    {
                        var options = await _context.QuestionOptions
                            .Where(o => o.QuestionId == question.Id)
                            .ToListAsync();
                        _context.QuestionOptions.RemoveRange(options);
                    }
                    _context.Questions.RemoveRange(questions);

                    // Delete quiz attempts (if any slipped past the earlier check)
                    var quizAttempts = await _context.QuizAttempts
                        .IgnoreQueryFilters()
                        .Where(qa => qa.QuizId == lesson.Quiz.Id)
                        .ToListAsync();
                    if (quizAttempts.Any())
                        _context.QuizAttempts.RemoveRange(quizAttempts);

                    _context.Quizzes.Remove(lesson.Quiz);
                }

                // ── Assignment related entities ─────────────────────────────
                if (lesson.Assignment != null)
                {
                    var submissions = await _context.AssignmentSubmissions
                        .IgnoreQueryFilters()
                        .Where(s => s.AssignmentId == lesson.Assignment.Id)
                        .ToListAsync();
                    _context.AssignmentSubmissions.RemoveRange(submissions);
                    _context.Assignments.Remove(lesson.Assignment);
                }

                // ── Lesson resources (Cascade FK, but clean explicitly for safety) ──
                var resources = await _context.LessonResources
                    .IgnoreQueryFilters()
                    .Where(r => r.LessonId == id)
                    .ToListAsync();
                _context.LessonResources.RemoveRange(resources);

                // ── Lesson progress records (Restrict FK, BaseEntity = hard delete) ─
                var progressRecords = await _context.LessonProgresses
                    .Where(lp => lp.LessonId == id)
                    .ToListAsync();
                if (progressRecords.Any())
                    _context.LessonProgresses.RemoveRange(progressRecords);

                // ── Comments (Cascade FK) ───────────────────────────────────
                var commentsOnLesson = await _context.Comments
                    .IgnoreQueryFilters()
                    .Where(c => c.LessonId == id)
                    .ToListAsync();
                _context.Comments.RemoveRange(commentsOnLesson);

                // ── Student notes (required FK) ─────────────────────────────
                var studentNotes = await _context.StudentNotes
                    .IgnoreQueryFilters()
                    .Where(n => n.LessonId == id)
                    .ToListAsync();
                _context.StudentNotes.RemoveRange(studentNotes);

                // ── Video chapters (Cascade FK) ─────────────────────────────
                var videoChapters = await _context.VideoChapters
                    .Where(vc => vc.LessonId == id)
                    .ToListAsync();
                if (videoChapters.Any())
                    _context.VideoChapters.RemoveRange(videoChapters);

                // ── Video metadata (Cascade FK) ─────────────────────────────
                var videoMetadata = await _context.VideoMetadata
                    .Where(vm => vm.LessonId == id)
                    .ToListAsync();
                if (videoMetadata.Any())
                    _context.VideoMetadata.RemoveRange(videoMetadata);

                // ── Video security entities (Restrict/NoAction FK) ──────────
                var playbackSessions = await _context.Set<VideoPlaybackSession>()
                    .Where(vps => vps.LessonId == id)
                    .ToListAsync();
                if (playbackSessions.Any())
                    _context.Set<VideoPlaybackSession>().RemoveRange(playbackSessions);

                var signedUrls = await _context.Set<SignedVideoUrl>()
                    .Where(s => s.LessonId == id)
                    .ToListAsync();
                if (signedUrls.Any())
                    _context.Set<SignedVideoUrl>().RemoveRange(signedUrls);

                var accessLogs = await _context.Set<VideoAccessLog>()
                    .Where(v => v.LessonId.HasValue && v.LessonId == id)
                    .ToListAsync();
                if (accessLogs.Any())
                    _context.Set<VideoAccessLog>().RemoveRange(accessLogs);

                var geoRestrictions = await _context.Set<VideoGeoRestriction>()
                    .Where(v => v.LessonId.HasValue && v.LessonId == id)
                    .ToListAsync();
                if (geoRestrictions.Any())
                    _context.Set<VideoGeoRestriction>().RemoveRange(geoRestrictions);

                // ── Discussions (Restrict FK) ───────────────────────────────
                var discussions = await _context.Discussions
                    .IgnoreQueryFilters()
                    .Where(d => d.LessonId == id)
                    .ToListAsync();
                if (discussions.Any())
                    _context.Discussions.RemoveRange(discussions);

                // ── Content drip rules (Restrict FK) ────────────────────────
                var dripRules = await _context.ContentDripRules
                    .Where(c => c.LessonId == id)
                    .ToListAsync();
                if (dripRules.Any())
                    _context.ContentDripRules.RemoveRange(dripRules);

                // ── Nullify optional FK references ──────────────────────────
                var bookmarks = await _context.Bookmarks
                    .Where(b => b.LessonId == id)
                    .ToListAsync();
                bookmarks.ForEach(b => b.LessonId = null);

                var flashcards = await _context.FlashcardDecks
                    .Where(f => f.LessonId == id)
                    .ToListAsync();
                flashcards.ForEach(f => f.LessonId = null);

                var faqs = await _context.Faqs
                    .Where(f => f.LessonId.HasValue && f.LessonId == id)
                    .ToListAsync();
                faqs.ForEach(f => f.LessonId = null);

                var reminders = await _context.StudentReminders
                    .Where(r => r.LessonId.HasValue && r.LessonId == id)
                    .ToListAsync();
                reminders.ForEach(r => r.LessonId = null);

                var calendarEvents = await _context.CalendarEvents
                    .Where(e => e.LessonId.HasValue && e.LessonId == id)
                    .ToListAsync();
                calendarEvents.ForEach(e => e.LessonId = null);

                var enrollments = await _context.Enrollments
                    .Where(e => e.LastLessonId == id)
                    .ToListAsync();
                enrollments.ForEach(e => e.LastLessonId = null);

                // ── Finally, remove the lesson itself ───────────────────────
                _context.Lessons.Remove(lesson);
                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                _logger.LogError("Transaction failed while deleting lesson {LessonId}: {Error}", id, error);
                SetErrorMessage(error ?? "حدث خطأ أثناء حذف الدرس");
                return RedirectToAction("Index", "Modules", new { area = "Instructor", courseId = courseId });
            }

            _logger.LogInformation("Lesson {LessonId} '{Title}' deleted by instructor {InstructorId}", 
                id, lessonTitle, userId);

            SetSuccessMessage($"تم حذف الدرس '{lessonTitle}' بنجاح");
            return RedirectToAction("Index", "Modules", new { area = "Instructor", courseId = courseId });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error deleting lesson {LessonId}: {Message}", id, dbEx.Message);
            SetErrorMessage("لا يمكن حذف الدرس بسبب وجود بيانات مرتبطة به. يرجى التحقق من عدم وجود تقدم طلاب أو محاولات اختبار مرتبطة بهذا الدرس.");
            
            if (courseId.HasValue)
                return RedirectToAction("Index", "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lesson {LessonId}: {Message}", id, ex.Message);
            SetErrorMessage("حدث خطأ أثناء حذف الدرس. يرجى المحاولة مرة أخرى.");
            
            if (courseId.HasValue)
                return RedirectToAction("Index", "Modules", new { area = "Instructor", courseId = courseId.Value });
            
            return RedirectToAction("Index", "Courses");
        }
    }

    /// <summary>
    /// تعديل سريع لعنوان الدرس - Quick edit lesson title via AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEditLesson([FromBody] LessonQuickEditModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Json(new { success = false, message = "عنوان الدرس مطلوب" });

            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("QuickEditLesson: Lesson {LessonId} not found for instructor {InstructorId}", model.Id, userId);
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            lesson.Title = model.Title.Trim();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} quick-edited by instructor {InstructorId}", model.Id, userId);
            return Json(new { success = true, message = "تم تحديث الدرس بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QuickEditLesson for lesson {LessonId}", model.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث الدرس" });
        }
    }

    /// <summary>
    /// نسخ الدرس - Duplicate a lesson
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateLesson([FromBody] LessonDuplicateModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Include(l => l.Quizzes)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(l => l.Assignments)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("DuplicateLesson: Lesson {LessonId} not found for instructor {InstructorId}", model.Id, userId);
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            var maxOrder = await _context.Lessons
                .Where(l => l.ModuleId == lesson.ModuleId)
                .MaxAsync(l => (int?)l.OrderIndex) ?? 0;

            var newLesson = new Lesson
            {
                ModuleId = lesson.ModuleId,
                Title = lesson.Title + " (نسخة)",
                Description = lesson.Description,
                Type = lesson.Type,
                VideoUrl = lesson.VideoUrl,
                VideoProvider = lesson.VideoProvider,
                VideoId = lesson.VideoId,
                HtmlContent = lesson.HtmlContent,
                FileUrl = lesson.FileUrl,
                DurationSeconds = lesson.DurationSeconds,
                IsPreviewable = lesson.IsPreviewable,
                IsDownloadable = lesson.IsDownloadable,
                MustComplete = lesson.MustComplete,
                OrderIndex = maxOrder + 1,
                IsPublished = false // Always start as draft
            };

            _context.Lessons.Add(newLesson);
            await _context.SaveChangesAsync();

            // Duplicate quiz if exists
            if (lesson.Quiz != null)
            {
                var newQuiz = new Domain.Entities.Assessments.Quiz
                {
                    LessonId = newLesson.Id,
                    Title = lesson.Quiz.Title + " (نسخة)",
                    Description = lesson.Quiz.Description,
                    TimeLimitMinutes = lesson.Quiz.TimeLimitMinutes,
                    PassingScore = lesson.Quiz.PassingScore,
                    MaxAttempts = lesson.Quiz.MaxAttempts,
                    ShuffleQuestions = lesson.Quiz.ShuffleQuestions,
                    ShowCorrectAnswers = lesson.Quiz.ShowCorrectAnswers,
                    IsActive = true
                };

                _context.Quizzes.Add(newQuiz);
                await _context.SaveChangesAsync();

                // Duplicate questions and options
                foreach (var question in lesson.Quiz.Questions ?? Enumerable.Empty<Domain.Entities.Assessments.Question>())
                {
                    var newQuestion = new Domain.Entities.Assessments.Question
                    {
                        QuizId = newQuiz.Id,
                        QuestionText = question.QuestionText,
                        Type = question.Type,
                        Points = question.Points,
                        OrderIndex = question.OrderIndex,
                        Explanation = question.Explanation
                    };

                    _context.Questions.Add(newQuestion);
                    await _context.SaveChangesAsync();

                    foreach (var option in question.Options ?? Enumerable.Empty<Domain.Entities.Assessments.QuestionOption>())
                    {
                        var newOption = new Domain.Entities.Assessments.QuestionOption
                        {
                            QuestionId = newQuestion.Id,
                            OptionText = option.OptionText,
                            IsCorrect = option.IsCorrect,
                            OrderIndex = option.OrderIndex
                        };
                        _context.QuestionOptions.Add(newOption);
                    }
                    await _context.SaveChangesAsync();
                }
            }

            // Duplicate assignment if exists
            if (lesson.Assignment != null)
            {
                var newAssignment = new Domain.Entities.Assessments.Assignment
                {
                    LessonId = newLesson.Id,
                    Title = lesson.Assignment.Title + " (نسخة)",
                    Description = lesson.Assignment.Description,
                    Instructions = lesson.Assignment.Instructions,
                    MaxPoints = lesson.Assignment.MaxPoints,
                    DueDate = lesson.Assignment.DueDate,
                    AllowLateSubmission = lesson.Assignment.AllowLateSubmission,
                    MaxFileSizeMB = lesson.Assignment.MaxFileSizeMB,
                    AcceptedFileTypes = lesson.Assignment.AcceptedFileTypes
                };

                _context.Assignments.Add(newAssignment);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Lesson {LessonId} duplicated as {NewLessonId} by instructor {InstructorId}", model.Id, newLesson.Id, userId);
            return Json(new { success = true, message = "تم نسخ الدرس بنجاح", lesson = new { id = newLesson.Id, title = newLesson.Title } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DuplicateLesson for lesson {LessonId}", model.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء نسخ الدرس" });
        }
    }

    /// <summary>
    /// نقل الدرس لوحدة أخرى - Move lesson to different module
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLesson([FromBody] LessonMoveModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.LessonId && l.Module.Course.InstructorId == userId);

            if (lesson == null)
            {
                _logger.LogWarning("MoveLesson: Lesson {LessonId} not found for instructor {InstructorId}", model.LessonId, userId);
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            var targetModule = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.Id == model.TargetModuleId && m.Course.InstructorId == userId);

            if (targetModule == null)
            {
                _logger.LogWarning("MoveLesson: Target module {ModuleId} not found for instructor {InstructorId}", model.TargetModuleId, userId);
                return Json(new { success = false, message = "الوحدة المستهدفة غير موجودة" });
            }

            if (lesson.ModuleId == model.TargetModuleId)
                return Json(new { success = false, message = "الدرس موجود بالفعل في هذه الوحدة" });

            var maxOrder = await _context.Lessons
                .Where(l => l.ModuleId == model.TargetModuleId)
                .MaxAsync(l => (int?)l.OrderIndex) ?? 0;

            lesson.ModuleId = model.TargetModuleId;
            lesson.OrderIndex = maxOrder + 1;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} moved to module {ModuleId} by instructor {InstructorId}", model.LessonId, model.TargetModuleId, userId);
            return Json(new { success = true, message = $"تم نقل الدرس إلى '{targetModule.Title}' بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MoveLesson for lesson {LessonId}", model.LessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء نقل الدرس" });
        }
    }

    /// <summary>
    /// تبديل حالة النشر - Toggle lesson publish status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish([FromBody] LessonDuplicateModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            lesson.IsPublished = !lesson.IsPublished;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} publish toggled to {IsPublished} by instructor {InstructorId}", model.Id, lesson.IsPublished, userId);
            return Json(new { success = true, message = lesson.IsPublished ? "تم نشر الدرس" : "تم إلغاء نشر الدرس", isPublished = lesson.IsPublished });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TogglePublish for lesson {LessonId}", model.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء تغيير حالة النشر" });
        }
    }

    /// <summary>
    /// تبديل المعاينة المجانية - Toggle lesson free preview
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePreview([FromBody] LessonDuplicateModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Module.Course.InstructorId == userId);

            if (lesson == null)
                return Json(new { success = false, message = "الدرس غير موجود" });

            lesson.IsPreviewable = !lesson.IsPreviewable;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Lesson {LessonId} preview toggled to {IsPreviewable} by instructor {InstructorId}", model.Id, lesson.IsPreviewable, userId);
            return Json(new { success = true, message = lesson.IsPreviewable ? "تم تفعيل المعاينة المجانية" : "تم إلغاء المعاينة المجانية", isPreviewable = lesson.IsPreviewable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TogglePreview for lesson {LessonId}", model.Id);
            return Json(new { success = false, message = "حدث خطأ أثناء تغيير حالة المعاينة" });
        }
    }

    /// <summary>
    /// نشر مجمع للدروس - Bulk publish lessons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkPublish([FromBody] LessonBulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي دروس" });

            var lessons = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Where(l => model.Ids.Contains(l.Id) && l.Module.Course.InstructorId == userId)
                .ToListAsync();

            var count = 0;
            foreach (var lesson in lessons)
            {
                if (!lesson.IsPublished)
                {
                    lesson.IsPublished = true;
                    count++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bulk publish: {Count} lessons published by instructor {InstructorId}", count, userId);
            return Json(new { success = true, message = $"تم نشر {count} درس بنجاح", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkPublish lessons");
            return Json(new { success = false, message = "حدث خطأ أثناء نشر الدروس" });
        }
    }

    /// <summary>
    /// إلغاء نشر مجمع للدروس - Bulk unpublish lessons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUnpublish([FromBody] LessonBulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي دروس" });

            var lessons = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Where(l => model.Ids.Contains(l.Id) && l.Module.Course.InstructorId == userId)
                .ToListAsync();

            var count = 0;
            foreach (var lesson in lessons)
            {
                if (lesson.IsPublished)
                {
                    lesson.IsPublished = false;
                    count++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bulk unpublish: {Count} lessons unpublished by instructor {InstructorId}", count, userId);
            return Json(new { success = true, message = $"تم إلغاء نشر {count} درس بنجاح", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkUnpublish lessons");
            return Json(new { success = false, message = "حدث خطأ أثناء إلغاء نشر الدروس" });
        }
    }

    /// <summary>
    /// حذف مجمع للدروس - Bulk delete lessons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete([FromBody] LessonBulkActionModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي دروس" });

            var lessons = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                        .ThenInclude(c => c.Enrollments)
                .Include(l => l.Quizzes)
                .Include(l => l.Assignments)
                .Where(l => model.Ids.Contains(l.Id) && l.Module.Course.InstructorId == userId)
                .ToListAsync();

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var lesson in lessons)
            {
                var hasStudentProgress = await _context.LessonProgresses.AnyAsync(lp => lp.LessonId == lesson.Id);
                var enrollmentCount = lesson.Module.Course.Enrollments?.Count ?? 0;
                var (canDelete, reason) = BusinessRuleHelper.CanDeleteLesson(hasStudentProgress, enrollmentCount, lesson.Module.IsPublished);

                if (!canDelete)
                {
                    errors.Add($"'{lesson.Title}': {reason}");
                    continue;
                }

                // Check for quiz/assignment attempts
                var hasAttempts = false;
                if (lesson.Quiz != null)
                    hasAttempts = await _context.QuizAttempts.AnyAsync(qa => qa.QuizId == lesson.Quiz.Id);
                if (!hasAttempts && lesson.Assignment != null)
                    hasAttempts = await _context.AssignmentSubmissions.AnyAsync(s => s.AssignmentId == lesson.Assignment.Id);

                if (hasAttempts)
                {
                    errors.Add($"'{lesson.Title}': يحتوي على اختبار أو تكليف تم حله");
                    continue;
                }

                // Clean up related data
                var progressRecords = await _context.LessonProgresses.Where(lp => lp.LessonId == lesson.Id).ToListAsync();
                _context.LessonProgresses.RemoveRange(progressRecords);

                var resources = await _context.LessonResources.Where(r => r.LessonId == lesson.Id).ToListAsync();
                _context.LessonResources.RemoveRange(resources);

                if (lesson.Quiz != null)
                {
                    var questions = await _context.Questions.Where(q => q.QuizId == lesson.Quiz.Id).ToListAsync();
                    foreach (var question in questions)
                    {
                        var options = await _context.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync();
                        _context.QuestionOptions.RemoveRange(options);
                    }
                    _context.Questions.RemoveRange(questions);
                    _context.Quizzes.Remove(lesson.Quiz);
                }

                if (lesson.Assignment != null)
                {
                    var submissions = await _context.AssignmentSubmissions.Where(s => s.AssignmentId == lesson.Assignment.Id).ToListAsync();
                    _context.AssignmentSubmissions.RemoveRange(submissions);
                    _context.Assignments.Remove(lesson.Assignment);
                }

                _context.Lessons.Remove(lesson);
                deletedCount++;
            }

            await _context.SaveChangesAsync();

            var message = $"تم حذف {deletedCount} درس بنجاح";
            if (errors.Any())
                message += $". تعذر حذف {errors.Count}: {string.Join("، ", errors)}";

            _logger.LogInformation("Bulk delete: {Count} lessons deleted by instructor {InstructorId}", deletedCount, userId);
            return Json(new { success = true, message, count = deletedCount, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkDelete lessons");
            return Json(new { success = false, message = "حدث خطأ أثناء حذف الدروس" });
        }
    }

    /// <summary>
    /// تعيين/إلغاء المعاينة المجانية بشكل مجمع - Bulk set/unset free preview
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetPreview([FromBody] LessonBulkPreviewModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model.Ids == null || !model.Ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي دروس" });

            var lessons = await _context.Lessons
                .Include(l => l.Module)
                    .ThenInclude(m => m.Course)
                .Where(l => model.Ids.Contains(l.Id) && l.Module.Course.InstructorId == userId)
                .ToListAsync();

            var count = 0;
            foreach (var lesson in lessons)
            {
                if (lesson.IsPreviewable != model.IsPreviewable)
                {
                    lesson.IsPreviewable = model.IsPreviewable;
                    count++;
                }
            }

            await _context.SaveChangesAsync();

            var actionText = model.IsPreviewable ? "تفعيل المعاينة المجانية" : "إلغاء المعاينة المجانية";
            _logger.LogInformation("Bulk set preview ({IsPreviewable}): {Count} lessons updated by instructor {InstructorId}", model.IsPreviewable, count, userId);
            return Json(new { success = true, message = $"تم {actionText} لـ {count} درس بنجاح", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkSetPreview lessons");
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث المعاينة المجانية" });
        }
    }

    /// <summary>
    /// الحصول على وحدات الدورة - Get modules for a course (AJAX helper)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetModules()
    {
        var userId = _currentUserService.UserId;

        try
        {
            var modules = await _context.Modules
                .Include(m => m.Course)
                .Where(m => m.Course.InstructorId == userId)
                .OrderBy(m => m.Course.Title)
                .ThenBy(m => m.OrderIndex)
                .Select(m => new { m.Id, m.Title, CourseName = m.Course.Title })
                .ToListAsync();

            return Json(new { success = true, modules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetModules");
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل الوحدات" });
        }
    }
}
