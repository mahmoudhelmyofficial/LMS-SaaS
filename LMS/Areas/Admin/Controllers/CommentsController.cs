using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة التعليقات - Comments Management Controller
/// </summary>
public class CommentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ApplicationDbContext context,
        ILogger<CommentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التعليقات - Comments List
    /// </summary>
    public async Task<IActionResult> Index(string? entityType, bool? isApproved, string? searchTerm)
    {
        var query = _context.Comments
            .Include(c => c.Author)
            .AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(c => c.EntityType == entityType);
        }

        if (isApproved.HasValue)
        {
            query = query.Where(c => c.IsApproved == isApproved.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(c => c.Content.Contains(searchTerm));
        }

        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.EntityType = entityType;
        ViewBag.IsApproved = isApproved;
        ViewBag.SearchTerm = searchTerm;

        return View(comments);
    }

    /// <summary>
    /// تفاصيل التعليق - Comment Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var comment = await _context.Comments
            .Include(c => c.Author)
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return NotFound();

        // Get replies
        var replies = await _context.Comments
            .Include(c => c.Author)
            .Where(c => c.ParentCommentId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        ViewBag.Replies = replies;

        return View(comment);
    }

    /// <summary>
    /// الموافقة على التعليق - Approve Comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            return NotFound();

        comment.IsApproved = true;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تمت الموافقة على التعليق");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// رفض التعليق - Reject Comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            return NotFound();

        comment.IsApproved = false;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم رفض التعليق");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف التعليق - Delete Comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var comment = await _context.Comments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return NotFound();

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التعليق بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تعديل التعليق - Edit Comment (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var comment = await _context.Comments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return NotFound();

        return View(comment);
    }

    /// <summary>
    /// تعديل التعليق - Edit Comment (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string content)
    {
        var comment = await _context.Comments.FindAsync(id);
        if (comment == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(content))
        {
            SetErrorMessage("محتوى التعليق مطلوب");
            return RedirectToAction(nameof(Edit), new { id });
        }

        comment.Content = content;
        comment.IsEdited = true;
        comment.EditedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعديل التعليق بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// الموافقة المجمعة - Bulk Approve
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(List<int> commentIds)
    {
        if (commentIds == null || !commentIds.Any())
        {
            SetErrorMessage("لم يتم اختيار أي تعليقات");
            return RedirectToAction(nameof(Index));
        }

        var comments = await _context.Comments
            .Where(c => commentIds.Contains(c.Id))
            .ToListAsync();

        foreach (var comment in comments)
        {
            comment.IsApproved = true;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تمت الموافقة على {comments.Count} تعليق");
        return RedirectToAction(nameof(Index));
    }
}

