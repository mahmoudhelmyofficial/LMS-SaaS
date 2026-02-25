using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// مناقشات الطالب - Student Discussions Controller
/// </summary>
public class DiscussionsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly ILogger<DiscussionsController> _logger;

    public DiscussionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IInstructorNotificationService instructorNotificationService,
        ILogger<DiscussionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _instructorNotificationService = instructorNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة المناقشات - Discussions landing (choose course) or مناقشات الدورة - Course discussions
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int? lessonId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // No course selected: show landing with enrolled courses
        if (!courseId.HasValue || courseId.Value == 0)
        {
            var enrolledCourses = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId)
                .OrderBy(e => e.Course.Title)
                .Select(e => new DiscussionsCourseItemViewModel
                {
                    CourseId = e.CourseId,
                    CourseTitle = e.Course.Title
                })
                .ToListAsync();

            ViewBag.IsLanding = true;
            return View("IndexLanding", enrolledCourses);
        }

        var cid = courseId.Value;

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == cid && e.StudentId == userId);

        if (enrollment == null)
            return Forbid();

        var query = _context.Discussions
            .Include(d => d.Author)
            .Include(d => d.Course)
            .Include(d => d.Lesson)
            .Include(d => d.Replies)
            .Where(d => d.CourseId == cid);

        if (lessonId.HasValue)
        {
            query = query.Where(d => d.LessonId == lessonId.Value);
        }

        var discussions = await query
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastReplyAt ?? d.CreatedAt)
            .Select(d => new DiscussionDisplayViewModel
            {
                Id = d.Id,
                Title = d.Title,
                Content = d.Content,
                AuthorName = d.Author != null ? (d.Author.FirstName + " " + d.Author.LastName) : "مستخدم محذوف",
                AuthorImageUrl = d.Author != null ? d.Author.ProfileImageUrl : null,
                CourseName = d.Course.Title,
                LessonName = d.Lesson != null ? d.Lesson.Title : null,
                RepliesCount = d.RepliesCount,
                ViewCount = d.ViewCount,
                IsResolved = d.IsResolved,
                IsPinned = d.IsPinned,
                CreatedAt = d.CreatedAt,
                LastReplyAt = d.LastReplyAt
            })
            .ToListAsync();

        ViewBag.CourseId = cid;
        ViewBag.LessonId = lessonId;
        ViewBag.CreateModel = new CreateDiscussionViewModel { CourseId = cid, LessonId = lessonId };

        return View(discussions);
    }

    /// <summary>
    /// إنشاء مناقشة - Create discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDiscussionViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == model.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Forbid();

        if (ModelState.IsValid)
        {
            var discussion = new Discussion
            {
                CourseId = model.CourseId,
                LessonId = model.LessonId,
                AuthorId = userId!,
                Title = model.Title,
                Content = model.Content
            };

            _context.Discussions.Add(discussion);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء المناقشة بنجاح");
            return RedirectToAction(nameof(Details), new { id = discussion.Id });
        }

        return RedirectToAction(nameof(Index), new { courseId = model.CourseId, lessonId = model.LessonId });
    }

    /// <summary>
    /// تفاصيل المناقشة - Discussion details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var discussion = await _context.Discussions
            .AsNoTracking()
            .Include(d => d.Course)
            .Include(d => d.Author)
            .Include(d => d.Lesson)
            .Include(d => d.Replies)
                .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discussion == null)
            return NotFound();

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == discussion.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Forbid();

        // Increment view count (separate tracked query to avoid tracking the full graph)
        var discussionToUpdate = await _context.Discussions.FindAsync(id);
        var viewCount = discussion.ViewCount;
        if (discussionToUpdate != null)
        {
            discussionToUpdate.ViewCount++;
            await _context.SaveChangesAsync();
            viewCount = discussionToUpdate.ViewCount;
        }

        var isSaved = false;
        try
        {
            isSaved = await _context.UserSavedDiscussions
                .AnyAsync(s => s.DiscussionId == id && s.UserId == userId);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            _logger.LogWarning(ex, "UserSavedDiscussions table may not exist or be inaccessible for discussion {DiscussionId}. Apply migration AddUserSavedDiscussion if save feature is required.", id);
        }

        var authorName = discussion.Author != null
            ? $"{discussion.Author.FirstName} {discussion.Author.LastName}"
            : "مستخدم محذوف";
        var authorImageUrl = discussion.Author?.ProfileImageUrl;

        var viewModel = new DiscussionDisplayViewModel
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            AuthorName = authorName,
            AuthorImageUrl = authorImageUrl,
            CourseName = discussion.Course?.Title ?? "",
            LessonName = discussion.Lesson?.Title,
            RepliesCount = discussion.RepliesCount,
            ViewCount = viewCount,
            IsResolved = discussion.IsResolved,
            IsPinned = discussion.IsPinned,
            IsSaved = isSaved,
            CreatedAt = discussion.CreatedAt,
            LastReplyAt = discussion.LastReplyAt
        };

        ViewBag.Replies = discussion.Replies
            .Where(r => r.ParentReplyId == null)
            .OrderBy(r => r.CreatedAt)
            .Select(r => MapReply(r, discussion.Replies))
            .ToList();

        ViewBag.ReplyModel = new ReplyDiscussionViewModel { DiscussionId = id };

        return View(viewModel);
    }

    /// <summary>
    /// الرد على المناقشة - Reply to discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(ReplyDiscussionViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var discussion = await _context.Discussions
            .Include(d => d.Course)
            .FirstOrDefaultAsync(d => d.Id == model.DiscussionId);

        if (discussion == null)
            return NotFound();

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == discussion.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Forbid();

        if (ModelState.IsValid)
        {
            var reply = new DiscussionReply
            {
                DiscussionId = model.DiscussionId,
                AuthorId = userId!,
                Content = model.Content,
                ParentReplyId = model.ParentReplyId
            };

            discussion.LastReplyAt = DateTime.UtcNow;
            discussion.RepliesCount++;

            _context.DiscussionReplies.Add(reply);
            await _context.SaveChangesAsync();

            try
            {
                var instructorId = discussion.Course?.InstructorId;
                if (!string.IsNullOrEmpty(instructorId))
                {
                    var studentName = await _context.Users
                        .Where(u => u.Id == userId)
                        .Select(u => u.FullName)
                        .FirstOrDefaultAsync() ?? "طالب";
                    await _instructorNotificationService.NotifyDiscussionReplyAsync(
                        instructorId,
                        discussion.Id,
                        discussion.Course?.Title ?? "الدورة",
                        studentName,
                        discussion.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Instructor notification failed for discussion reply on discussion {DiscussionId}", model.DiscussionId);
            }

            SetSuccessMessage("تم إرسال الرد بنجاح");
        }

        return RedirectToAction(nameof(Details), new { id = model.DiscussionId });
    }

    /// <summary>
    /// إعجاب بالرد - Like a reply
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LikeReply(int replyId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var reply = await _context.DiscussionReplies
            .Include(r => r.Discussion)
            .FirstOrDefaultAsync(r => r.Id == replyId);

        if (reply == null)
            return Json(new { success = false, message = "الرد غير موجود" });

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == reply.Discussion.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Json(new { success = false, message = "غير مصرح" });

        // Increment like count
        reply.UpvoteCount++;
        await _context.SaveChangesAsync();

        return Json(new { success = true, likesCount = reply.UpvoteCount });
    }

    /// <summary>
    /// الإبلاغ عن مناقشة - Report a discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int discussionId, string reason)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var discussion = await _context.Discussions
            .FirstOrDefaultAsync(d => d.Id == discussionId);

        if (discussion == null)
            return Json(new { success = false, message = "المناقشة غير موجودة" });

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == discussion.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Json(new { success = false, message = "غير مصرح" });

        // Log the report (in a real application, you'd save this to a Reports table)
        var reasonSafe = string.IsNullOrEmpty(reason) ? "(no reason given)" : reason;
        _logger.LogWarning("Discussion {DiscussionId} reported by user {UserId}. Reason: {Reason}",
            discussionId, userId, reasonSafe);

        return Json(new { success = true, message = "تم إرسال الإبلاغ بنجاح" });
    }

    /// <summary>
    /// الإبلاغ عن رد - Report a reply
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportReply(int replyId, string reason)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        var reply = await _context.DiscussionReplies
            .Include(r => r.Discussion)
            .FirstOrDefaultAsync(r => r.Id == replyId);

        if (reply == null)
            return Json(new { success = false, message = "الرد غير موجود" });

        // Check enrollment
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == reply.Discussion.CourseId && e.StudentId == userId);

        if (enrollment == null)
            return Json(new { success = false, message = "غير مصرح" });

        // Log the report
        _logger.LogWarning("Reply {ReplyId} reported by user {UserId}. Reason: {Reason}", 
            replyId, userId, reason);

        return Json(new { success = true, message = "تم إرسال الإبلاغ بنجاح" });
    }

    /// <summary>
    /// حفظ/إلغاء حفظ المناقشة - Toggle save (bookmark) discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSave(int discussionId)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });

        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Id == discussionId);
        if (discussion == null)
            return Json(new { success = false, message = "المناقشة غير موجودة" });

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == discussion.CourseId && e.StudentId == userId);
        if (enrollment == null)
            return Json(new { success = false, message = "غير مصرح" });

        try
        {
            var saved = await _context.UserSavedDiscussions
                .FirstOrDefaultAsync(s => s.DiscussionId == discussionId && s.UserId == userId);

            if (saved != null)
            {
                _context.UserSavedDiscussions.Remove(saved);
                await _context.SaveChangesAsync();
                return Json(new { success = true, saved = false, message = "تم إلغاء الحفظ" });
            }

            _context.UserSavedDiscussions.Add(new UserSavedDiscussion
            {
                UserId = userId,
                DiscussionId = discussionId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Json(new { success = true, saved = true, message = "تم حفظ الموضوع" });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            _logger.LogWarning(ex, "UserSavedDiscussions table may not exist for ToggleSave discussion {DiscussionId}. Apply migration AddUserSavedDiscussion.", discussionId);
            return Json(new { success = false, message = "ميزة الحفظ غير متاحة حالياً. يرجى تحديث الموقع لاحقاً." });
        }
    }

    private ReplyDisplayViewModel MapReply(DiscussionReply reply, ICollection<DiscussionReply> allReplies)
    {
        var authorName = reply.Author != null
            ? $"{reply.Author.FirstName} {reply.Author.LastName}"
            : "مستخدم محذوف";
        var authorImageUrl = reply.Author?.ProfileImageUrl;

        return new ReplyDisplayViewModel
        {
            Id = reply.Id,
            Content = reply.Content,
            AuthorName = authorName,
            AuthorImageUrl = authorImageUrl,
            IsInstructorReply = reply.IsInstructorReply,
            IsAcceptedAnswer = reply.IsAcceptedAnswer,
            LikesCount = reply.LikesCount,
            CreatedAt = reply.CreatedAt,
            ChildReplies = allReplies
                .Where(r => r.ParentReplyId == reply.Id)
                .OrderBy(r => r.CreatedAt)
                .Select(r => MapReply(r, allReplies))
                .ToList()
        };
    }
}
