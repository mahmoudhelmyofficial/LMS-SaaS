using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Entities.Social;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// مناقشات الدورات - Course Discussions Controller
/// </summary>
public class DiscussionsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DiscussionsController> _logger;

    public DiscussionsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<DiscussionsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المناقشات - Discussions list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? resolved, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Discussions
            .Include(d => d.Course)
            .Include(d => d.Author)
            .Include(d => d.Replies)
            .Where(d => d.Course.InstructorId == userId);

        if (courseId.HasValue)
        {
            query = query.Where(d => d.CourseId == courseId.Value);
        }

        if (resolved.HasValue)
        {
            query = query.Where(d => d.IsResolved == resolved.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        var pageSize = 20;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var discussions = await query
            .OrderByDescending(d => d.LastReplyAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Resolved = resolved;
        ViewBag.Page = page;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalCount;
        ViewBag.PageSize = pageSize;

        return View(discussions);
    }

    /// <summary>
    /// تفاصيل المناقشة - Discussion details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var discussion = await _context.Discussions
            .Include(d => d.Course)
            .Include(d => d.Author)
            .Include(d => d.Lesson)
            .Include(d => d.Replies)
                .ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(d => d.Id == id && d.Course.InstructorId == userId);

        if (discussion == null)
            return NotFound();

        return View(discussion);
    }

    /// <summary>
    /// الرد على المناقشة - Reply to discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string content)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var discussion = await _context.Discussions
                .Include(d => d.Course)
                .Include(d => d.Author)
                .FirstOrDefaultAsync(d => d.Id == id && d.Course.InstructorId == userId);

            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateDiscussionContent(content, true);

            if (!isValid)
            {
                _logger.LogWarning("Discussion reply validation failed: {Reason}", validationReason);
                SetErrorMessage(validationReason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                var reply = new DiscussionReply
                {
                    DiscussionId = id,
                    AuthorId = userId!,
                    Content = content,
                    IsInstructorReply = true
                };

                discussion.LastReplyAt = DateTime.UtcNow;
                discussion.RepliesCount++;

                _context.DiscussionReplies.Add(reply);
                await _context.SaveChangesAsync();

                // Notify discussion author
                if (discussion.AuthorId != userId)
                {
                    var notification = new Notification
                    {
                        UserId = discussion.AuthorId,
                        Title = "رد المدرس على مناقشتك",
                        Message = $"رد المدرس على مناقشتك في دورة: {discussion.Course.Title}",
                        Type = NotificationType.DiscussionReply,
                        ActionUrl = $"/Student/Discussions/Details/{discussion.Id}",
                        ActionText = "عرض الرد",
                        IconClass = "fas fa-reply text-primary",
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }
            });

            _logger.LogInformation(
                "Instructor {InstructorId} replied to discussion {DiscussionId}", 
                userId, id);

            SetSuccessMessage("تم إرسال الرد بنجاح وإشعار صاحب المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء إرسال الرد");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تبديل حالة الحل - Toggle solved status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSolved(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var discussion = await _context.Discussions
                .Include(d => d.Course)
                .Include(d => d.Author)
                .FirstOrDefaultAsync(d => d.Id == id && d.Course.InstructorId == userId);

            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                discussion.IsResolved = !discussion.IsResolved;
                discussion.ResolvedAt = discussion.IsResolved ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync();

                // Notify discussion author
                var notification = new Notification
                {
                    UserId = discussion.AuthorId,
                    Title = discussion.IsResolved ? "تم حل مناقشتك" : "تم إعادة فتح مناقشتك",
                    Message = $"قام المدرس بتحديد مناقشتك كـ{(discussion.IsResolved ? "محلولة" : "غير محلولة")} في دورة: {discussion.Course.Title}",
                    Type = NotificationType.DiscussionReply,
                    ActionUrl = $"/Student/Discussions/Details/{discussion.Id}",
                    ActionText = "عرض المناقشة",
                    IconClass = discussion.IsResolved ? "fas fa-check-circle text-success" : "fas fa-undo text-warning",
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Discussion {DiscussionId} {Action} by instructor {InstructorId}", 
                id, discussion.IsResolved ? "marked as resolved" : "marked as unresolved", userId);

            SetSuccessMessage(discussion.IsResolved ? "تم تحديد المناقشة كمحلولة" : "تم إعادة فتح المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling solved status for discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تحديد كمحلول - Mark as resolved
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkResolved(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var discussion = await _context.Discussions
                .Include(d => d.Course)
                .Include(d => d.Author)
                .FirstOrDefaultAsync(d => d.Id == id && d.Course.InstructorId == userId);

            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            if (discussion.IsResolved)
            {
                SetWarningMessage("المناقشة محلولة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                discussion.IsResolved = true;
                discussion.ResolvedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Notify discussion author
                var notification = new Notification
                {
                    UserId = discussion.AuthorId,
                    Title = "تم حل مناقشتك",
                    Message = $"قام المدرس بتحديد مناقشتك كمحلولة في دورة: {discussion.Course.Title}",
                    Type = NotificationType.DiscussionReply,
                    ActionUrl = $"/Student/Discussions/Details/{discussion.Id}",
                    ActionText = "عرض المناقشة",
                    IconClass = "fas fa-check-circle text-success",
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Discussion {DiscussionId} marked as resolved by instructor {InstructorId}", 
                id, userId);

            SetSuccessMessage("تم تحديد المناقشة كمحلولة وإشعار صاحبها");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking discussion {DiscussionId} as resolved", id);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

