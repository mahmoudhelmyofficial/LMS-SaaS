using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// المنتديات - Forums Controller
/// يعرض جميع المناقشات المتاحة للطالب من الدورات المسجل بها
/// </summary>
public class ForumsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ForumsController> _logger;

    public ForumsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ForumsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المنتديات - Forums list (all discussions from enrolled courses)
    /// </summary>
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var pageSize = 15;

        // Get enrolled course IDs
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == userId)
            .Select(e => e.CourseId)
            .ToListAsync();

        // Get discussions from enrolled courses
        var query = _context.Discussions
            .Include(d => d.Author)
            .Include(d => d.Course)
            .Include(d => d.Lesson)
            .Where(d => enrolledCourseIds.Contains(d.CourseId));

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => d.Title.Contains(search) || d.Content.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var discussions = await query
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastReplyAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new ForumDiscussionViewModel
            {
                Id = d.Id,
                Title = d.Title,
                ContentPreview = d.Content.Length > 150 ? d.Content.Substring(0, 150) + "..." : d.Content,
                AuthorName = d.Author.FirstName + " " + d.Author.LastName,
                AuthorImageUrl = d.Author.ProfilePictureUrl,
                CourseName = d.Course.Title,
                CourseId = d.CourseId,
                LessonName = d.Lesson != null ? d.Lesson.Title : null,
                RepliesCount = d.RepliesCount,
                ViewCount = d.ViewCount,
                IsResolved = d.IsResolved,
                IsPinned = d.IsPinned,
                CreatedAt = d.CreatedAt,
                LastReplyAt = d.LastReplyAt
            })
            .ToListAsync();

        // Get courses for filter dropdown
        var courses = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId)
            .Select(e => new { e.Course.Id, e.Course.Title })
            .ToListAsync();

        ViewBag.Courses = courses;
        ViewBag.Search = search;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalCount = totalCount;

        return View(discussions);
    }
}

#region View Models

public class ForumDiscussionViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorImageUrl { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string? LessonName { get; set; }
    public int RepliesCount { get; set; }
    public int ViewCount { get; set; }
    public bool IsResolved { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReplyAt { get; set; }
}

#endregion

