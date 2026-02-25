using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using LMS.Areas.Student.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// المخطط الدراسي الذكي - Smart Study Planner Controller
/// </summary>
public class StudyPlannerController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly ILogger<StudyPlannerController> _logger;

    public StudyPlannerController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningAnalyticsService analyticsService,
        ILogger<StudyPlannerController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// المخطط الدراسي - Study planner dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var userId = _currentUserService.UserId!;

        // Get scheduled study sessions from start of today onward (so "Today's Sessions" includes past sessions today)
        var todayStart = DateTime.UtcNow.Date;
        var sessions = await _context.StudySessions
            .Include(ss => ss.Course)
            .Where(ss => ss.UserId == userId && ss.ScheduledDate >= todayStart)
            .OrderBy(ss => ss.ScheduledDate)
            .ToListAsync();

        // Get study recommendations
        var recommendations = await _analyticsService.GetStudyRecommendationsAsync(userId);

        // Get upcoming deadlines
        var upcomingDeadlines = await GetUpcomingDeadlinesAsync(userId);

        // Get enrolled courses for dropdown
        var enrolledCourses = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
            .Select(e => e.Course)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.Courses = enrolledCourses;

        var viewModel = new StudyPlannerViewModel
        {
            Sessions = sessions,
            Recommendations = recommendations,
            UpcomingDeadlines = upcomingDeadlines
        };

        return View(viewModel);
    }

    /// <summary>
    /// توليد خطة دراسية ذكية - Generate smart study plan
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GeneratePlan()
    {
        var userId = _currentUserService.UserId!;

        // Get active enrollments
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
            .Include(e => e.LessonProgress)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
            .ToListAsync();

        ViewBag.Enrollments = enrollments;

        return View(new GeneratePlanViewModel());
    }

    /// <summary>
    /// إنشاء خطة دراسية تلقائية - Create automatic study plan
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePlan(GeneratePlanViewModel model)
    {
        var userId = _currentUserService.UserId!;

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.Id == model.EnrollmentId && e.StudentId == userId);

            if (enrollment == null)
                return NotFound();

            // Delete existing sessions for this course
            var existingSessions = await _context.StudySessions
                .Where(ss => ss.UserId == userId && ss.CourseId == enrollment.CourseId && ss.ScheduledDate >= DateTime.UtcNow)
                .ToListAsync();

            _context.StudySessions.RemoveRange(existingSessions);

            // Generate sessions based on available time and target date
            var completedLessonIds = enrollment.LessonProgress
                .Where(lp => lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToList();

            var remainingLessons = enrollment.Course.Modules
                .OrderBy(m => m.OrderIndex)
                .SelectMany(m => m.Lessons.OrderBy(l => l.OrderIndex))
                .Where(l => !completedLessonIds.Contains(l.Id))
                .ToList();

            if (remainingLessons.Any() && model.TargetCompletionDate.HasValue)
            {
                var daysAvailable = (model.TargetCompletionDate.Value - DateTime.UtcNow).TotalDays;
                var sessionsNeeded = Math.Ceiling(remainingLessons.Count / (double)model.LessonsPerSession);
                var daysBetweenSessions = daysAvailable / sessionsNeeded;

                var currentDate = DateTime.UtcNow.Date;
                var lessonIndex = 0;

                for (int i = 0; i < sessionsNeeded && lessonIndex < remainingLessons.Count; i++)
                {
                    // Schedule based on available days
                    var sessionDate = currentDate.AddDays(i * daysBetweenSessions);
                    
                    // Apply preferred study time
                    sessionDate = sessionDate.Add(model.PreferredStudyTime);

                    // Skip if day is not in selected days
                    if (!IsDayAvailable(sessionDate.DayOfWeek, model.AvailableDays))
                    {
                        i--; // Try next day
                        continue;
                    }

                    var lessonsForSession = remainingLessons
                        .Skip(lessonIndex)
                        .Take(model.LessonsPerSession)
                        .ToList();

                    if (lessonsForSession.Any())
                    {
                        var session = new StudySession
                        {
                            UserId = userId,
                            CourseId = enrollment.CourseId,
                            Title = $"جلسة دراسة - {enrollment.Course.Title}",
                            Description = $"دروس: {string.Join(", ", lessonsForSession.Select(l => l.Title))}",
                            ScheduledDate = sessionDate,
                            DurationMinutes = model.SessionDurationMinutes,
                            LessonIds = string.Join(",", lessonsForSession.Select(l => l.Id)),
                            IsCompleted = false,
                            ReminderSent = false
                        };

                        _context.StudySessions.Add(session);
                        lessonIndex += model.LessonsPerSession;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Study plan generated for user {UserId}, enrollment {EnrollmentId}, sessions: {Count}", 
                    userId, model.EnrollmentId, sessionsNeeded);

                SetSuccessMessage($"تم إنشاء خطة دراسية تحتوي على {sessionsNeeded} جلسة");
                return RedirectToAction(nameof(Index));
            }

            SetErrorMessage("لا توجد دروس متبقية أو لم يتم تحديد تاريخ الإنجاز");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating study plan for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الخطة الدراسية");
            return View(model);
        }
    }

    /// <summary>
    /// إضافة جلسة دراسية يدوياً - Add manual study session
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddSession([FromBody] AddStudySessionViewModel? model)
    {
        var userId = _currentUserService.UserId!;

        if (model == null)
            return Json(new { success = false, message = "طلب غير صالح. حدّث الصفحة وحاول مرة أخرى." });

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            return Json(new { success = false, message = "يرجى التحقق من البيانات المدخلة", errors = errors });
        }

        try
        {
            if (model.CourseId.HasValue)
            {
                var courseExists = await _context.Courses
                    .AnyAsync(c => c.Id == model.CourseId.Value);
                
                if (!courseExists)
                {
                    return Json(new { success = false, message = "الدورة المحددة غير موجودة" });
                }
            }

            var session = new StudySession
            {
                UserId = userId,
                CourseId = model.CourseId,
                Title = model.Title,
                Description = model.Description,
                ScheduledDate = model.ScheduledDate,
                DurationMinutes = model.DurationMinutes,
                IsCompleted = false,
                ReminderSent = false
            };

            _context.StudySessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Study session created: {SessionId} for user {UserId}", session.Id, userId);

            return Json(new { success = true, sessionId = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding study session for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة الجلسة. يرجى المحاولة مرة أخرى." });
        }
    }

    /// <summary>
    /// تحديد الجلسة كمكتملة - Mark session as completed
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CompleteSession([FromBody] SessionIdRequest? model)
    {
        var userId = _currentUserService.UserId!;

        if (model == null || model.SessionId <= 0)
            return Json(new { success = false, message = "طلب غير صالح. حدّث الصفحة وحاول مرة أخرى." });

        var session = await _context.StudySessions
            .FirstOrDefaultAsync(ss => ss.Id == model.SessionId && ss.UserId == userId);

        if (session == null)
            return Json(new { success = false, message = "الجلسة غير موجودة" });

        session.IsCompleted = true;
        session.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// حذف جلسة - Delete session
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteSession([FromBody] SessionIdRequest? model)
    {
        var userId = _currentUserService.UserId!;

        if (model == null || model.SessionId <= 0)
            return Json(new { success = false, message = "طلب غير صالح. حدّث الصفحة وحاول مرة أخرى." });

        var session = await _context.StudySessions
            .FirstOrDefaultAsync(ss => ss.Id == model.SessionId && ss.UserId == userId);

        if (session == null)
            return Json(new { success = false, message = "الجلسة غير موجودة" });

        _context.StudySessions.Remove(session);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// تعديل جلسة - Edit study session
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> EditSession([FromBody] EditStudySessionViewModel? model)
    {
        var userId = _currentUserService.UserId!;

        if (model == null || model.SessionId <= 0)
            return Json(new { success = false, message = "طلب غير صالح. حدّث الصفحة وحاول مرة أخرى." });

        var session = await _context.StudySessions
            .FirstOrDefaultAsync(ss => ss.Id == model.SessionId && ss.UserId == userId);

        if (session == null)
            return Json(new { success = false, message = "الجلسة غير موجودة" });

        if (session.IsCompleted)
            return Json(new { success = false, message = "لا يمكن تعديل جلسة مكتملة" });

        session.Title = model.Title ?? session.Title;
        session.Description = model.Description;
        session.ScheduledDate = model.ScheduledDate;
        session.DurationMinutes = model.DurationMinutes;
        session.CourseId = model.CourseId;

        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// تقويم الدراسة - Study calendar
    /// </summary>
    public async Task<IActionResult> Calendar(int year = 0, int month = 0)
    {
        var userId = _currentUserService.UserId!;

        if (year == 0) year = DateTime.UtcNow.Year;
        if (month == 0) month = DateTime.UtcNow.Month;

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var sessions = await _context.StudySessions
            .Include(ss => ss.Course)
            .Where(ss => ss.UserId == userId &&
                        ss.ScheduledDate >= startDate &&
                        ss.ScheduledDate <= endDate)
            .OrderBy(ss => ss.ScheduledDate)
            .ToListAsync();

        var calendar = new StudyCalendarViewModel
        {
            Year = year,
            Month = month,
            Sessions = sessions
        };

        return View(calendar);
    }

    /// <summary>
    /// التذكيرات - Reminders
    /// </summary>
    public async Task<IActionResult> Reminders()
    {
        var userId = _currentUserService.UserId!;

        var reminders = await _context.Reminders
            .Include(r => r.Course)
            .Where(r => r.UserId == userId && r.ReminderDate >= DateTime.UtcNow && !r.IsSent)
            .OrderBy(r => r.ReminderDate)
            .ToListAsync();

        return View(reminders);
    }

    /// <summary>
    /// إضافة تذكير - Add reminder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReminder(AddReminderViewModel model)
    {
        var userId = _currentUserService.UserId!;

        if (!ModelState.IsValid)
            return Json(new { success = false, message = "بيانات غير صحيحة" });

        try
        {
            var reminder = new Reminder
            {
                UserId = userId,
                CourseId = model.CourseId,
                Title = model.Title,
                Message = model.Message,
                ReminderDate = model.ReminderDate,
                ReminderType = model.ReminderType,
                IsSent = false
            };

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();

            return Json(new { success = true, reminderId = reminder.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reminder for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء إضافة التذكير" });
        }
    }

    /// <summary>
    /// حذف تذكير - Delete reminder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReminder(int reminderId)
    {
        var userId = _currentUserService.UserId!;

        var reminder = await _context.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == userId);

        if (reminder == null)
            return Json(new { success = false, message = "Reminder not found" });

        _context.Reminders.Remove(reminder);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// اقتراحات الجدول الذكي - Smart schedule suggestions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetSmartScheduleSuggestions(int enrollmentId, DateTime targetDate)
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Modules)
                        .ThenInclude(m => m.Lessons)
                .Include(e => e.LessonProgress)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

            if (enrollment == null)
                return Json(new { success = false, message = "Enrollment not found" });

            // Calculate remaining lessons
            var completedLessonIds = enrollment.LessonProgress
                .Where(lp => lp.IsCompleted)
                .Select(lp => lp.LessonId)
                .ToList();

            var remainingLessons = enrollment.Course.Modules
                .SelectMany(m => m.Lessons)
                .Where(l => !completedLessonIds.Contains(l.Id))
                .Count();

            var daysAvailable = (targetDate - DateTime.UtcNow).TotalDays;

            if (daysAvailable <= 0 || remainingLessons == 0)
                return Json(new { success = false, message = "لا يوجد دروس متبقية أو التاريخ غير صحيح" });

            // Calculate recommendations
            var lessonsPerDay = Math.Ceiling(remainingLessons / daysAvailable);
            var estimatedMinutesPerDay = (int)(lessonsPerDay * 15); // Assuming 15 min per lesson
            var sessionsPerWeek = Math.Ceiling(daysAvailable / 7 * 5); // 5 days per week

            var suggestions = new
            {
                remainingLessons,
                daysAvailable = (int)daysAvailable,
                recommendedLessonsPerDay = lessonsPerDay,
                estimatedMinutesPerDay = estimatedMinutesPerDay,
                recommendedSessionsPerWeek = sessionsPerWeek,
                difficulty = estimatedMinutesPerDay > 120 ? "High" : estimatedMinutesPerDay > 60 ? "Medium" : "Low",
                message = estimatedMinutesPerDay > 120 
                    ? "الجدول صعب جداً. فكر في تمديد المدة أو تقليل عدد الدورات."
                    : "الجدول قابل للتحقيق. حافظ على الانضباط!"
            };

            return Json(new { success = true, suggestions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule suggestions for enrollment {EnrollmentId}", enrollmentId);
            return Json(new { success = false, message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إحصائيات الالتزام - Commitment statistics
    /// </summary>
    public async Task<IActionResult> CommitmentStats()
    {
        var userId = _currentUserService.UserId!;

        var allSessions = await _context.StudySessions
            .Where(ss => ss.UserId == userId)
            .ToListAsync();

        var stats = new CommitmentStatsViewModel
        {
            TotalSessionsScheduled = allSessions.Count,
            SessionsCompleted = allSessions.Count(ss => ss.IsCompleted),
            SessionsMissed = allSessions.Count(ss => !ss.IsCompleted && ss.ScheduledDate < DateTime.UtcNow),
            SessionsUpcoming = allSessions.Count(ss => !ss.IsCompleted && ss.ScheduledDate >= DateTime.UtcNow),
            CompletionRate = allSessions.Count > 0 
                ? (decimal)allSessions.Count(ss => ss.IsCompleted) / allSessions.Count * 100 
                : 0,
            AverageSessionDuration = (int)(allSessions.Where(ss => ss.IsCompleted && ss.ActualDurationMinutes.HasValue)
                .Average(ss => (double?)ss.ActualDurationMinutes) ?? 0),
            CurrentStreak = CalculateStudyStreak(allSessions),
            LongestStreak = CalculateLongestStudyStreak(allSessions)
        };

        return View(stats);
    }

    #region Private Helper Methods

    private async Task<List<DeadlineInfo>> GetUpcomingDeadlinesAsync(string userId)
    {
        var deadlines = new List<DeadlineInfo>();
        var twoWeeksFromNow = DateTime.UtcNow.AddDays(14);

        // Assignment deadlines
        var assignments = await _context.AssignmentSubmissions
            .Include(a => a.Assignment)
                .ThenInclude(a => a.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
            .Where(a => a.StudentId == userId &&
                       a.Status != Domain.Enums.AssignmentStatus.Graded &&
                       a.Assignment.DueDate.HasValue &&
                       a.Assignment.DueDate.Value <= twoWeeksFromNow)
            .ToListAsync();

        deadlines.AddRange(assignments.Select(a => new DeadlineInfo
        {
            Type = "Assignment",
            Title = a.Assignment.Title,
            DueDate = a.Assignment.DueDate!.Value,
            CourseName = a.Assignment.Lesson.Module.Course.Title,
            Priority = CalculateDeadlinePriority(a.Assignment.DueDate.Value)
        }));

        // Learning goals
        var goals = await _context.LearningGoals
            .Include(g => g.Course)
            .Where(g => g.UserId == userId &&
                       !g.IsCompleted &&
                       !g.IsCancelled &&
                       g.TargetDate.HasValue &&
                       g.TargetDate.Value <= twoWeeksFromNow)
            .ToListAsync();

        deadlines.AddRange(goals.Select(g => new DeadlineInfo
        {
            Type = "Goal",
            Title = g.Title,
            DueDate = g.TargetDate!.Value,
            CourseName = g.Course?.Title,
            Priority = CalculateDeadlinePriority(g.TargetDate.Value)
        }));

        return deadlines.OrderBy(d => d.DueDate).ToList();
    }

    private bool IsDayAvailable(DayOfWeek dayOfWeek, string availableDays)
    {
        if (string.IsNullOrEmpty(availableDays))
            return true;

        var days = availableDays.Split(',');
        return days.Contains(dayOfWeek.ToString());
    }

    private int CalculateDeadlinePriority(DateTime dueDate)
    {
        var daysUntilDue = (dueDate - DateTime.UtcNow).TotalDays;
        return daysUntilDue switch
        {
            <= 1 => 1, // Critical
            <= 3 => 2, // High
            <= 7 => 3, // Medium
            _ => 4 // Low
        };
    }

    private int CalculateStudyStreak(List<StudySession> sessions)
    {
        var completedSessions = sessions
            .Where(ss => ss.IsCompleted)
            .OrderByDescending(ss => ss.CompletedAt)
            .ToList();

        if (!completedSessions.Any())
            return 0;

        var streak = 0;
        var today = DateTime.UtcNow.Date;

        for (int i = 0; i < 365; i++)
        {
            var date = today.AddDays(-i);
            if (completedSessions.Any(ss => ss.CompletedAt.HasValue && ss.CompletedAt.Value.Date == date))
                streak++;
            else if (i > 0) // Don't break on first day if no session
                break;
        }

        return streak;
    }

    private int CalculateLongestStudyStreak(List<StudySession> sessions)
    {
        var completedDates = sessions
            .Where(ss => ss.IsCompleted && ss.CompletedAt.HasValue)
            .Select(ss => ss.CompletedAt!.Value.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (!completedDates.Any())
            return 0;

        var longestStreak = 1;
        var currentStreak = 1;

        for (int i = 1; i < completedDates.Count; i++)
        {
            if (completedDates[i] == completedDates[i - 1].AddDays(1))
            {
                currentStreak++;
            }
            else
            {
                longestStreak = Math.Max(longestStreak, currentStreak);
                currentStreak = 1;
            }
        }

        return Math.Max(longestStreak, currentStreak);
    }

    #endregion
}

#region View Models

public class StudyPlannerViewModel
{
    public List<StudySession> Sessions { get; set; } = new();
    public StudyRecommendations Recommendations { get; set; } = new();
    public List<DeadlineInfo> UpcomingDeadlines { get; set; } = new();
}

public class GeneratePlanViewModel
{
    public int EnrollmentId { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public int LessonsPerSession { get; set; } = 2;
    public int SessionDurationMinutes { get; set; } = 60;
    public TimeSpan PreferredStudyTime { get; set; } = new TimeSpan(20, 0, 0); // 8 PM default
    public string AvailableDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday";
}

public class AddStudySessionViewModel
{
    public int? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int DurationMinutes { get; set; } = 60;
}

public class EditStudySessionViewModel
{
    public int SessionId { get; set; }
    public int? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int DurationMinutes { get; set; } = 60;
}

/// <summary>
/// Request model for session id in JSON body (complete/delete session).
/// </summary>
public class SessionIdRequest
{
    public int SessionId { get; set; }
}

public class StudyCalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<StudySession> Sessions { get; set; } = new();
}

// CommitmentStatsViewModel moved to ViewModels/CommitmentViewModels.cs

public class DeadlineInfo
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? CourseName { get; set; }
    public int Priority { get; set; }
}

public class AddReminderViewModel
{
    public int? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ReminderDate { get; set; }
    public string ReminderType { get; set; } = "Study"; // Study, Assignment, Quiz, LiveClass
}

public class GoalWithProgress
{
    public LearningGoal Goal { get; set; } = null!;
    public decimal CurrentProgress { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsAchievable { get; set; }
    public int RecommendedDailyEffort { get; set; }
}

public class GoalProgressInfo
{
    public decimal CurrentProgress { get; set; }
    public decimal TargetValue { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsAchievable { get; set; }
    public int RecommendedDailyEffort { get; set; }
}

public class CreateGoalViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GoalType { get; set; } = "CompleteCourse"; // CompleteCourse, CompleteLessons, StudyMinutes, PassQuizzes
    public int? CourseId { get; set; }
    public decimal TargetValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsPublic { get; set; }
}

#endregion

