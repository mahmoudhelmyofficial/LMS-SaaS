using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التعليقات للطلاب - Student Comments Controller
/// </summary>
public class CommentsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IInstructorNotificationService instructorNotificationService,
        ILogger<CommentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _instructorNotificationService = instructorNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// تعليقاتي - My Comments
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var comments = await _context.Comments
            .Include(c => c.ParentComment)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(comments);
    }

    /// <summary>
    /// إضافة تعليق - Add Comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CommentCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (ModelState.IsValid)
        {
            var comment = new Comment
            {
                UserId = userId!,
                EntityType = model.EntityType,
                EntityId = model.EntityId,
                Content = model.Content,
                ParentCommentId = model.ParentCommentId,
                IsApproved = false // Needs moderation
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Notify instructor when student comments on a lesson (unified path: DB + SignalR + Web Push)
            if (string.Equals(model.EntityType, "Lesson", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var lesson = await _context.Lessons
                        .Include(l => l.Module)
                        .ThenInclude(m => m.Course)
                        .FirstOrDefaultAsync(l => l.Id == model.EntityId);
                    if (lesson?.Module?.Course != null && !string.IsNullOrEmpty(lesson.Module.Course.InstructorId))
                    {
                        var student = await _context.Users.FindAsync(userId);
                        var studentName = student?.FullName ?? student?.FirstName ?? "طالب";
                        _ = await _instructorNotificationService.NotifyNewCommentOnLessonAsync(
                            lesson.Module.Course.InstructorId,
                            comment.Id,
                            lesson.Module.Course.Id,
                            lesson.Module.Course.Title,
                            lesson.Title,
                            studentName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to notify instructor for new comment {CommentId}", comment.Id);
                }
            }

            SetSuccessMessage("تم إضافة تعليقك بنجاح. سيتم مراجعته قريباً");
            
            // Redirect back to the entity (lesson, discussion, etc.)
            return Redirect(model.ReturnUrl ?? Url.Action(nameof(Index))!);
        }

        SetErrorMessage("حدث خطأ في إضافة التعليق");
        return Redirect(model.ReturnUrl ?? Url.Action(nameof(Index))!);
    }

    /// <summary>
    /// تعديل التعليق - Edit Comment
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (comment == null)
            return NotFound();

        var viewModel = new CommentEditViewModel
        {
            Id = comment.Id,
            Content = comment.Content
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديل - Save Edit
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CommentEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (comment == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            comment.Content = model.Content;
            comment.IsEdited = true;
            comment.EditedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تعديل تعليقك بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// حذف التعليق - Delete Comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (comment == null)
            return NotFound();

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف تعليقك بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إضافة تعليق عبر API - Add comment via API (for lesson page)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Json(new { success = false, message = "محتوى التعليق مطلوب" });
        }

        try
        {
            var lesson = await _context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == request.LessonId);

            if (lesson == null)
            {
                return Json(new { success = false, message = "الدرس غير موجود" });
            }

            // Verify enrollment
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == lesson.Module.CourseId && e.StudentId == userId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "يجب أن تكون مسجلاً في الدورة" });
            }

            var comment = new Comment
            {
                AuthorId = userId,
                LessonId = request.LessonId,
                EntityType = "Lesson",
                EntityId = request.LessonId,
                Content = request.Content,
                IsApproved = true // Auto-approve for now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Student {StudentId} added comment to lesson {LessonId}", userId, request.LessonId);

            return Json(new { success = true, commentId = comment.Id, message = "تم إضافة التعليق بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment for lesson {LessonId} by student {StudentId}", request.LessonId, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة التعليق" });
        }
    }

    /// <summary>
    /// جلب تعليقات الدرس - Get lesson comments via API
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetComments(int lessonId)
    {
        try
        {
            var comments = await _context.Comments
                .Include(c => c.Author)
                .Where(c => c.LessonId == lessonId && c.IsApproved && !c.IsHidden)
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    AuthorName = c.Author.FirstName + " " + c.Author.LastName,
                    AuthorAvatar = c.Author.ProfileImageUrl ?? "/assets/images/avatar/default.png",
                    c.LikeCount,
                    c.IsPinned,
                    c.InstructorReply,
                    InstructorReplyAt = c.InstructorReplyAt.HasValue ? c.InstructorReplyAt.Value.ToString("yyyy-MM-dd HH:mm") : null,
                    CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, comments });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for lesson {LessonId}", lessonId);
            return Json(new { success = false, message = "حدث خطأ أثناء جلب التعليقات" });
        }
    }
}

/// <summary>
/// طلب إضافة تعليق - Add comment request model
/// </summary>
public class AddCommentRequest
{
    public int LessonId { get; set; }
    public string Content { get; set; } = string.Empty;
}

