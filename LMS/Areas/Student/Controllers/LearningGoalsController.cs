using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// أهداف التعلم والمخطط الدراسي - Learning Goals & Study Planner Controller
/// </summary>
public class LearningGoalsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly ILogger<LearningGoalsController> _logger;

    public LearningGoalsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningAnalyticsService analyticsService,
        ILogger<LearningGoalsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الأهداف - Goals list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Prevent caching so after remove, refresh shows current DB state
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var userId = _currentUserService.UserId!;

        var goals = await _context.LearningGoals
            .Include(g => g.Course)
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        // Calculate progress for each goal
        var goalsWithProgress = new List<GoalWithProgress>();

        foreach (var goal in goals)
        {
            var progress = await CalculateGoalProgressAsync(goal);
            goalsWithProgress.Add(new GoalWithProgress
            {
                Goal = goal,
                CurrentProgress = progress.CurrentProgress,
                ProgressPercentage = progress.ProgressPercentage,
                DaysRemaining = progress.DaysRemaining,
                IsAchievable = progress.IsAchievable,
                RecommendedDailyEffort = progress.RecommendedDailyEffort
            });
        }

        ViewBag.ActiveGoals = goalsWithProgress.Count(g => !g.Goal.IsCompleted && !g.Goal.IsCancelled);
        ViewBag.CompletedGoals = goalsWithProgress.Count(g => g.Goal.IsCompleted);

        return View(goalsWithProgress);
    }

    /// <summary>
    /// إنشاء هدف جديد - Create new goal
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _currentUserService.UserId!;

        // Get user's active enrollments for goal templates
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
            .ToListAsync();

        ViewBag.Enrollments = enrollments;

        return View(new CreateGoalViewModel());
    }

    /// <summary>
    /// حفظ الهدف الجديد - Save new goal
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateGoalViewModel model)
    {
        var userId = _currentUserService.UserId!;

        if (!ModelState.IsValid)
        {
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
                .ToListAsync();
            ViewBag.Enrollments = enrollments;
            return View(model);
        }

        try
        {
            var goal = new LearningGoal
            {
                UserId = userId,
                Title = model.Title,
                Description = model.Description,
                GoalType = model.GoalType,
                CourseId = model.CourseId,
                TargetValue = model.TargetValue,
                CurrentValue = 0,
                TargetDate = model.TargetDate,
                IsPublic = model.IsPublic,
                IsCompleted = false,
                IsCancelled = false
            };

            _context.LearningGoals.Add(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Learning goal created for user {UserId}: {GoalTitle}", userId, goal.Title);
            SetSuccessMessage("تم إنشاء الهدف بنجاح");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating learning goal for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الهدف");
            return View(model);
        }
    }

    /// <summary>
    /// تفاصيل الهدف - Goal details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId!;

        var goal = await _context.LearningGoals
            .Include(g => g.Course)
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

        if (goal == null)
            return NotFound();

        var progress = await CalculateGoalProgressAsync(goal);

        ViewBag.Progress = progress;

        return View(goal);
    }

    /// <summary>
    /// تعديل الهدف - Edit goal
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId!;

        var goal = await _context.LearningGoals
            .Include(g => g.Course)
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

        if (goal == null)
            return NotFound();

        if (goal.IsCompleted || goal.IsCancelled)
        {
            SetErrorMessage("لا يمكن تعديل هدف مكتمل أو ملغى");
            return RedirectToAction(nameof(Index));
        }

        var model = new EditGoalViewModel
        {
            Id = goal.Id,
            Title = goal.Title,
            Description = goal.Description,
            GoalType = goal.GoalType,
            CourseId = goal.CourseId,
            TargetValue = goal.TargetValue,
            TargetDate = goal.TargetDate,
            IsPublic = goal.IsPublic
        };

        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
            .ToListAsync();

        ViewBag.Enrollments = enrollments;

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات الهدف - Save goal changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditGoalViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId!;

        if (!ModelState.IsValid)
        {
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
                .ToListAsync();
            ViewBag.Enrollments = enrollments;
            return View(model);
        }

        try
        {
            var goal = await _context.LearningGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null)
                return NotFound();

            if (goal.IsCompleted || goal.IsCancelled)
            {
                SetErrorMessage("لا يمكن تعديل هدف مكتمل أو ملغى");
                return RedirectToAction(nameof(Index));
            }

            goal.Title = model.Title;
            goal.Description = model.Description;
            goal.GoalType = model.GoalType;
            goal.CourseId = model.CourseId;
            goal.TargetValue = model.TargetValue;
            goal.TargetDate = model.TargetDate;
            goal.IsPublic = model.IsPublic;
            goal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Learning goal {GoalId} updated by user {UserId}", id, userId);
            SetSuccessMessage("تم تحديث الهدف بنجاح");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating learning goal {GoalId} for user {UserId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الهدف");

            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Active)
                .ToListAsync();
            ViewBag.Enrollments = enrollments;

            return View(model);
        }
    }

    /// <summary>
    /// تحديث التقدم - Update progress
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress(int goalId)
    {
        var userId = _currentUserService.UserId!;

        try
        {
            var goal = await _context.LearningGoals
                .Include(g => g.Course)
                .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

            if (goal == null)
                return Json(new { success = false, message = "Goal not found" });

            // Update current value based on goal type
            switch (goal.GoalType)
            {
                case "CompleteCourse":
                    var enrollment = await _context.Enrollments
                        .FirstOrDefaultAsync(e => e.StudentId == userId && e.CourseId == goal.CourseId);
                    if (enrollment != null)
                    {
                        goal.CurrentValue = enrollment.ProgressPercentage;
                    }
                    break;

                case "CompleteLessons":
                    var completedLessons = await _context.LessonProgress
                        .CountAsync(lp => lp.Enrollment.StudentId == userId && 
                                         lp.Enrollment.CourseId == goal.CourseId && 
                                         lp.IsCompleted);
                    goal.CurrentValue = completedLessons;
                    break;

                case "StudyMinutes":
                    var totalMinutes = await _context.Enrollments
                        .Where(e => e.StudentId == userId)
                        .SumAsync(e => e.TotalWatchTimeMinutes);
                    goal.CurrentValue = totalMinutes;
                    break;
            }

            // Check if goal is completed
            if (goal.CurrentValue >= goal.TargetValue && !goal.IsCompleted)
            {
                goal.IsCompleted = true;
                goal.CompletedAt = DateTime.UtcNow;
                
                // Award points for goal completion
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    const int goalCompletionPoints = 50;
                    user.Points += goalCompletionPoints;

                    var pointTransaction = new Domain.Entities.Gamification.PointTransaction
                    {
                        UserId = userId,
                        Points = goalCompletionPoints,
                        Type = "goal_completion",
                        Description = $"إكمال هدف: {goal.Title}",
                        RelatedEntityType = "LearningGoal",
                        RelatedEntityId = goal.Id
                    };
                    _context.PointTransactions.Add(pointTransaction);
                }

                _logger.LogInformation("Learning goal {GoalId} completed by user {UserId}", goalId, userId);
            }

            await _context.SaveChangesAsync();

            var progressData = await CalculateGoalProgressAsync(goal);

            return Json(new
            {
                success = true,
                currentValue = goal.CurrentValue,
                targetValue = goal.TargetValue,
                progressPercentage = progressData.ProgressPercentage,
                isCompleted = goal.IsCompleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal progress for goal {GoalId}", goalId);
            return Json(new { success = false, message = "Error updating progress" });
        }
    }

    /// <summary>
    /// إلغاء الهدف - Cancel goal
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _currentUserService.UserId!;

        var goal = await _context.LearningGoals
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

        if (goal == null)
            return NotFound();

        goal.IsCancelled = true;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إلغاء الهدف");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الهدف - Delete goal (form submission)
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

        try
        {
            var goal = await _context.LearningGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null)
            {
                SetErrorMessage("الهدف غير موجود أو لا تملك صلاحية حذفه");
                return RedirectToAction(nameof(Index));
            }

            _context.LearningGoals.Remove(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Learning goal {GoalId} deleted by user {UserId}", id, userId);
            SetSuccessMessage("تم حذف الهدف بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting learning goal {GoalId} for user {UserId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف الهدف");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف الهدف عبر AJAX - Delete goal via AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        try
        {
            var goal = await _context.LearningGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

            if (goal == null)
            {
                return Json(new { success = false, message = "الهدف غير موجود أو لا تملك صلاحية حذفه" });
            }

            _context.LearningGoals.Remove(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Learning goal {GoalId} removed via AJAX by user {UserId}", id, userId);
            // Ensure response is not cached
            Response.Headers["Cache-Control"] = "no-store";

            return Json(new { success = true, message = "تم حذف الهدف بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing learning goal {GoalId} for user {UserId}", id, userId);
            return Json(new { success = false, message = "حدث خطأ أثناء حذف الهدف" });
        }
    }

    #region Private Helper Methods

    private async Task<GoalProgressInfo> CalculateGoalProgressAsync(LearningGoal goal)
    {
        var progress = new GoalProgressInfo
        {
            CurrentProgress = goal.CurrentValue,
            TargetValue = goal.TargetValue,
            ProgressPercentage = goal.TargetValue > 0 
                ? (goal.CurrentValue / goal.TargetValue * 100) 
                : 0
        };

        if (goal.TargetDate.HasValue)
        {
            progress.DaysRemaining = (int)(goal.TargetDate.Value - DateTime.UtcNow).TotalDays;
            
            var remainingProgress = goal.TargetValue - goal.CurrentValue;
            if (progress.DaysRemaining > 0 && remainingProgress > 0)
            {
                progress.IsAchievable = true;
                progress.RecommendedDailyEffort = (int)Math.Ceiling(remainingProgress / progress.DaysRemaining);
            }
            else if (progress.DaysRemaining <= 0)
            {
                progress.IsAchievable = false;
            }
        }

        return progress;
    }

    #endregion
}

public class EditGoalViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GoalType { get; set; } = "CompleteCourse";
    public int? CourseId { get; set; }
    public decimal TargetValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsPublic { get; set; }
}

