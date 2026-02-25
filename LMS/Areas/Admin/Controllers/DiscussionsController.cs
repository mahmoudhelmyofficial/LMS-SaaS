using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المناقشات - Discussions Moderation Controller
/// </summary>
public class DiscussionsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiscussionsController> _logger;

    public DiscussionsController(
        ApplicationDbContext context,
        ILogger<DiscussionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المناقشات - Discussions List
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? isPinned, bool? isLocked, string? searchTerm)
    {
        var query = _context.Discussions
            .Include(d => d.Course)
            .Include(d => d.Author)
            .Include(d => d.Lesson)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(d => d.CourseId == courseId.Value);
        }

        if (isPinned.HasValue)
        {
            query = query.Where(d => d.IsPinned == isPinned.Value);
        }

        if (isLocked.HasValue)
        {
            query = query.Where(d => d.IsLocked == isLocked.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(d => d.Title.Contains(searchTerm) || d.Content.Contains(searchTerm));
        }

        var discussions = await query
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
        ViewBag.CourseId = courseId;
        ViewBag.IsPinned = isPinned;
        ViewBag.IsLocked = isLocked;
        ViewBag.SearchTerm = searchTerm;

        return View(discussions);
    }

    /// <summary>
    /// تفاصيل المناقشة - Discussion Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var discussion = await _context.Discussions
            .Include(d => d.Course)
            .Include(d => d.Author)
            .Include(d => d.Lesson)
            .Include(d => d.Replies)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discussion == null)
            return NotFound();

        return View(discussion);
    }

    /// <summary>
    /// إنشاء مناقشة جديدة - Create new discussion
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new DiscussionCreateViewModel());
    }

    /// <summary>
    /// حفظ المناقشة الجديدة - Save new discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DiscussionCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var discussion = new Discussion
            {
                Title = model.Title,
                Content = model.Content,
                CourseId = model.CourseId,
                LessonId = model.LessonId,
                AuthorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                IsPinned = model.IsPinned,
                IsLocked = model.IsLocked
            };

            _context.Discussions.Add(discussion);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء المناقشة بنجاح");
            return RedirectToAction(nameof(Details), new { id = discussion.Id });
        }

        await PopulateDropdowns(model.CourseId);
        return View(model);
    }

    /// <summary>
    /// تعديل المناقشة - Edit discussion
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var discussion = await _context.Discussions
            .Include(d => d.Course)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discussion == null)
            return NotFound();

        var model = new DiscussionEditViewModel
        {
            Id = discussion.Id,
            Title = discussion.Title,
            Content = discussion.Content,
            CourseId = discussion.CourseId,
            LessonId = discussion.LessonId,
            IsPinned = discussion.IsPinned,
            IsLocked = discussion.IsLocked
        };

        await PopulateDropdowns(discussion.CourseId);
        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات المناقشة - Save discussion edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DiscussionEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var discussion = await _context.Discussions.FindAsync(id);
        if (discussion == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            discussion.Title = model.Title;
            discussion.Content = model.Content;
            discussion.CourseId = model.CourseId;
            discussion.LessonId = model.LessonId;
            discussion.IsPinned = model.IsPinned;
            discussion.IsLocked = model.IsLocked;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث المناقشة بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateDropdowns(model.CourseId);
        return View(model);
    }

    /// <summary>
    /// تثبيت المناقشة - Pin discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(int id)
    {
        try
        {
            var discussion = await _context.Discussions.FindAsync(id);
            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for Pin action", id);
                SetErrorMessage("المناقشة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            discussion.IsPinned = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Discussion {DiscussionId} was pinned", id);
            SetSuccessMessage("تم تثبيت المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pinning discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء تثبيت المناقشة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إلغاء تثبيت المناقشة - Unpin discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpin(int id)
    {
        try
        {
            var discussion = await _context.Discussions.FindAsync(id);
            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for Unpin action", id);
                SetErrorMessage("المناقشة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            discussion.IsPinned = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Discussion {DiscussionId} was unpinned", id);
            SetSuccessMessage("تم إلغاء تثبيت المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpinning discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء إلغاء تثبيت المناقشة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// قفل المناقشة - Lock discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(int id)
    {
        try
        {
            var discussion = await _context.Discussions.FindAsync(id);
            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for Lock action", id);
                SetErrorMessage("المناقشة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            discussion.IsLocked = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Discussion {DiscussionId} was locked", id);
            SetSuccessMessage("تم قفل المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء قفل المناقشة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// فتح المناقشة - Unlock discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id)
    {
        try
        {
            var discussion = await _context.Discussions.FindAsync(id);
            if (discussion == null)
            {
                _logger.LogWarning("Discussion {DiscussionId} not found for Unlock action", id);
                SetErrorMessage("المناقشة غير موجودة");
                return RedirectToAction(nameof(Index));
            }

            discussion.IsLocked = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Discussion {DiscussionId} was unlocked", id);
            SetSuccessMessage("تم فتح المناقشة");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking discussion {DiscussionId}", id);
            SetErrorMessage("حدث خطأ أثناء فتح المناقشة");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تثبيت/إلغاء تثبيت - Toggle Pin
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var discussion = await _context.Discussions.FindAsync(id);
        if (discussion == null)
            return NotFound();

        discussion.IsPinned = !discussion.IsPinned;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(discussion.IsPinned ? "تثبيت" : "إلغاء تثبيت")} المناقشة");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// قفل/فتح المناقشة - Toggle Lock
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        var discussion = await _context.Discussions.FindAsync(id);
        if (discussion == null)
            return NotFound();

        discussion.IsLocked = !discussion.IsLocked;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(discussion.IsLocked ? "قفل" : "فتح")} المناقشة");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف المناقشة - Delete Discussion
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var discussion = await _context.Discussions
            .Include(d => d.Replies)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discussion == null)
            return NotFound();

        var courseId = discussion.CourseId;

        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف المناقشة بنجاح");
        return RedirectToAction(nameof(Index), new { courseId });
    }

    /// <summary>
    /// حذف رد - Delete Reply
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReply(int id, int discussionId)
    {
        var reply = await _context.DiscussionReplies.FindAsync(id);
        if (reply == null)
            return NotFound();

        _context.DiscussionReplies.Remove(reply);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الرد بنجاح");
        return RedirectToAction(nameof(Details), new { id = discussionId });
    }

    #region Private Helpers

    private async Task PopulateDropdowns(int? selectedCourseId = null)
    {
        var courses = await _context.Courses
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Title)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.Courses = new SelectList(courses, "Id", "Title", selectedCourseId);

        if (selectedCourseId.HasValue)
        {
            var lessons = await _context.Lessons
                .Where(l => l.Module!.CourseId == selectedCourseId.Value)
                .OrderBy(l => l.OrderIndex)
                .Select(l => new { l.Id, l.Title })
                .ToListAsync();

            ViewBag.Lessons = new SelectList(lessons, "Id", "Title");
        }
        else
        {
            ViewBag.Lessons = new SelectList(Enumerable.Empty<object>(), "Id", "Title");
        }
    }

    #endregion
}

