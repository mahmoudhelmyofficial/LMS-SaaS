using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة نشاط المستخدمين - User Activity Controller
/// </summary>
public class UserActivityController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserActivityController> _logger;

    public UserActivityController(
        ApplicationDbContext context,
        ILogger<UserActivityController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة النشاط - Activity List
    /// </summary>
    public async Task<IActionResult> Index(string? userId, string? activityType, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.UserActivity
            .Include(ua => ua.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(ua => ua.UserId == userId);
        }

        if (!string.IsNullOrEmpty(activityType))
        {
            query = query.Where(ua => ua.ActivityType == activityType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(ua => ua.ActivityDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ua => ua.ActivityDate <= toDate.Value);
        }

        var activities = await query
            .OrderByDescending(ua => ua.ActivityDate)
            .Take(200)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.ActivityType = activityType;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(activities);
    }

    /// <summary>
    /// إحصائيات النشاط - Activity Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.UserActivity.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(ua => ua.ActivityDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ua => ua.ActivityDate <= toDate.Value);
        }

        var activities = await query.ToListAsync();

        var stats = new
        {
            TotalActivities = activities.Count,
            UniqueUsers = activities.Select(a => a.UserId).Distinct().Count(),
            TotalTimeHours = activities.Sum(a => a.TotalTimeMinutes) / 60.0,
            AvgTimePerUser = activities.Any() ? activities.Average(a => a.TotalTimeMinutes) : 0,
            MostActiveDay = activities.GroupBy(a => a.ActivityDate.DayOfWeek)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key.ToString() ?? "N/A"
        };

        // Generate chart data by day of week
        var dayNames = new[] { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
        var activityByDay = activities
            .GroupBy(a => a.ActivityDate.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var timeByDay = activities
            .GroupBy(a => a.ActivityDate.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.TotalTimeMinutes) / 60.0);

        var chartLabels = new List<string>();
        var activityData = new List<int>();
        var timeData = new List<double>();

        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            chartLabels.Add(dayNames[(int)day]);
            activityData.Add(activityByDay.GetValueOrDefault(day, 0));
            timeData.Add(timeByDay.GetValueOrDefault(day, 0));
        }

        ViewBag.Stats = stats;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.ChartLabels = chartLabels;
        ViewBag.ActivityData = activityData;
        ViewBag.TimeData = timeData;

        return View();
    }
}

