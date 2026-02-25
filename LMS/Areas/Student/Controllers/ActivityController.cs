using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// سجل النشاط - Activity Log Controller
/// </summary>
public class ActivityController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ActivityController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// سجل النشاط - Activity Log
    /// </summary>
    public async Task<IActionResult> Index(string? activityType, DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;

        var query = _context.ActivityLogs
            .Where(al => al.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(activityType))
        {
            query = query.Where(al => al.ActivityType == activityType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(al => al.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(al => al.Timestamp <= toDate.Value);
        }

        var activities = await query
            .OrderByDescending(al => al.Timestamp)
            .Take(200)
            .ToListAsync();

        ViewBag.ActivityType = activityType;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(activities);
    }

    /// <summary>
    /// ملخص النشاط - Activity Summary
    /// </summary>
    public async Task<IActionResult> Summary(DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;

        var query = _context.ActivityLogs
            .Where(al => al.UserId == userId)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(al => al.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(al => al.Timestamp <= toDate.Value);
        }

        var activities = await query.ToListAsync();

        var summary = new
        {
            TotalActivities = activities.Count,
            LessonViews = activities.Count(a => a.ActivityType == "LessonView"),
            QuizAttempts = activities.Count(a => a.ActivityType == "QuizAttempt"),
            AssignmentSubmissions = activities.Count(a => a.ActivityType == "AssignmentSubmission"),
            Discussions = activities.Count(a => a.ActivityType == "Discussion"),
            Reviews = activities.Count(a => a.ActivityType == "Review"),
            TotalTime = activities.Sum(a => a.DurationSeconds ?? 0) / 3600.0 // hours
        };

        ViewBag.Summary = summary;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View();
    }
}

