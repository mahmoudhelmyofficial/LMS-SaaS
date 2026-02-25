using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة تعليقات المدرس - Instructor Comments Controller
/// </summary>
public class CommentsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService,
        ILogger<CommentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التعليقات على دورات المدرس - Comments on instructor's courses
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? replied, bool? pinned, string? status, string? sortBy, string? search, int page = 1)
    {
        var userId = _currentUserService.UserId;

        // Validate that we have a valid user ID
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Instructor Comments page accessed without valid UserId");
            SetErrorMessage("لم يتم التعرف على المستخدم. يرجى تسجيل الدخول مرة أخرى");
            
            // Initialize ViewBag properties to prevent null reference errors in view
            ViewBag.Courses = new List<object>();
            ViewBag.TotalComments = 0;
            ViewBag.RepliedComments = 0;
            ViewBag.PendingComments = 0;
            ViewBag.ReplyRate = 0;
            ViewBag.CourseId = courseId;
            ViewBag.Page = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalItems = 0;
            
            return View(new List<Comment>());
        }

        try
        {
            // Get comments on instructor's courses (include Author for search; User is NotMapped alias)
            var query = _context.Comments
                .Include(c => c.Author)
                .Include(c => c.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .Where(c => c.Lesson != null && 
                            c.Lesson.Module != null && 
                            c.Lesson.Module.Course != null &&
                            c.Lesson.Module.Course.InstructorId == userId);

            if (courseId.HasValue)
            {
                query = query.Where(c => c.Lesson != null && 
                                        c.Lesson.Module != null && 
                                        c.Lesson.Module.CourseId == courseId.Value);
                _logger.LogInformation("Instructor {InstructorId} viewing comments for course {CourseId}.", userId, courseId.Value);
            }
            else
            {
                _logger.LogInformation("Instructor {InstructorId} viewing all comments.", userId);
            }

            // Apply replied filter
            if (replied.HasValue)
            {
                query = query.Where(c => replied.Value 
                    ? !string.IsNullOrEmpty(c.InstructorReply) 
                    : string.IsNullOrEmpty(c.InstructorReply));
            }

            // Apply pinned filter
            if (pinned.HasValue && pinned.Value)
            {
                query = query.Where(c => c.IsPinned);
            }

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status.ToLower())
                {
                    case "pending":
                        query = query.Where(c => string.IsNullOrEmpty(c.InstructorReply));
                        break;
                    case "replied":
                        query = query.Where(c => !string.IsNullOrEmpty(c.InstructorReply));
                        break;
                    case "pinned":
                        query = query.Where(c => c.IsPinned);
                        break;
                }
            }

            // Apply search filter (use Author.FirstName/LastName - FullName is NotMapped and cannot be translated to SQL)
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Content.Contains(search) ||
                                         (c.Author != null && (c.Author.FirstName.Contains(search) || c.Author.LastName.Contains(search))) ||
                                         (c.Lesson != null && c.Lesson.Title.Contains(search)));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Get pagination size
            var pageSize = await _configService.GetPaginationSizeAsync("comments", 20);
            page = Math.Max(1, page); // Ensure page is at least 1

            // Apply sorting
            IQueryable<Comment> sortedQuery;
            switch (sortBy?.ToLower())
            {
                case "oldest":
                    sortedQuery = query.OrderBy(c => c.CreatedAt);
                    break;
                case "mostlikes":
                    sortedQuery = query.OrderByDescending(c => c.LikesCount).ThenByDescending(c => c.CreatedAt);
                    break;
                case "newest":
                default:
                    sortedQuery = query.OrderByDescending(c => c.CreatedAt);
                    break;
            }

            // Apply pagination
            var comments = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get instructor courses for filter
            var courses = await _context.Courses
                .Where(c => c.InstructorId == userId)
                .Select(c => new { c.Id, c.Title })
                .OrderBy(c => c.Title)
                .ToListAsync();

            // Calculate statistics for stat cards
            var allCommentsQuery = _context.Comments
                .Include(c => c.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .Where(c => c.Lesson != null && 
                            c.Lesson.Module != null && 
                            c.Lesson.Module.Course != null &&
                            c.Lesson.Module.Course.InstructorId == userId);

            var allComments = await allCommentsQuery.ToListAsync();
            
            var totalComments = allComments.Count;
            var repliedComments = allComments.Count(c => !string.IsNullOrEmpty(c.InstructorReply));
            var pendingComments = totalComments - repliedComments;
            var replyRate = totalComments > 0 ? (decimal)repliedComments / totalComments * 100 : 0;

            // Set ViewBag properties
            ViewBag.Courses = courses;
            ViewBag.TotalComments = totalComments;
            ViewBag.RepliedComments = repliedComments;
            ViewBag.PendingComments = pendingComments;
            ViewBag.ReplyRate = replyRate;
            ViewBag.CourseId = courseId;
            ViewBag.Replied = replied;
            ViewBag.Pinned = pinned;
            ViewBag.Status = status;
            ViewBag.SortBy = sortBy;
            ViewBag.SearchTerm = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalItems = totalCount;

            _logger.LogInformation("Instructor {InstructorId} viewed comments. Total: {Count}, Page: {Page}", 
                userId, totalCount, page);

            return View(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comments for instructor {InstructorId}: {Message}. StackTrace: {StackTrace}", 
                userId, ex.Message, ex.StackTrace);
            
            // Log inner exception if exists
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
            }
            
            SetErrorMessage("حدث خطأ أثناء تحميل التعليقات. يرجى المحاولة مرة أخرى أو الاتصال بالدعم الفني إذا استمرت المشكلة.");
            
            // Initialize ViewBag properties to prevent null reference errors in view
            try
            {
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .Select(c => new { c.Id, c.Title })
                    .ToListAsync();
            }
            catch
            {
                ViewBag.Courses = new List<object>();
            }
            
            ViewBag.TotalComments = 0;
            ViewBag.RepliedComments = 0;
            ViewBag.PendingComments = 0;
            ViewBag.ReplyRate = 0;
            ViewBag.CourseId = courseId;
            ViewBag.Page = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalItems = 0;
            
            return View(new List<Comment>());
        }
    }

    /// <summary>
    /// تفاصيل التعليق - Comment details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var comment = await _context.Comments
            .Include(c => c.Author)
            .Include(c => c.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(c => c.Id == id && 
                c.Lesson != null && 
                c.Lesson.Module != null && 
                c.Lesson.Module.Course != null &&
                c.Lesson.Module.Course.InstructorId == userId);

        if (comment == null)
            return NotFound();

        return View(comment);
    }

    /// <summary>
    /// الرد على تعليق - Reply to comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string replyText)
    {
        var userId = _currentUserService.UserId;

        var comment = await _context.Comments
            .Include(c => c.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id && 
                c.Lesson != null && 
                c.Lesson.Module != null && 
                c.Lesson.Module.Course != null &&
                c.Lesson.Module.Course.InstructorId == userId);

        if (comment == null)
        {
            _logger.LogWarning("NotFound: Comment {CommentId} not found or instructor {InstructorId} unauthorized.", id, userId);
            SetErrorMessage("التعليق غير موجود أو ليس لديك صلاحية للرد عليه.");
            return NotFound();
        }

        // Authorization check
        if (comment.Lesson?.Module?.Course?.InstructorId != userId)
        {
            _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to reply to comment {CommentId}.", userId, id);
            SetErrorMessage("غير مصرح لك بالرد على هذا التعليق.");
            return Forbid();
        }

        var (isValid, reason) = BusinessRuleHelper.ValidateComment(replyText);
        if (!isValid)
        {
            SetErrorMessage(reason!);
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                comment.InstructorReply = replyText;
                comment.InstructorReplyAt = DateTime.UtcNow;
                comment.InstructorId = userId;

                await _context.SaveChangesAsync();

                // Create in-app notification for the comment author
                if (comment.UserId != userId) // Don't notify instructor if they are the author
                {
                    var notification = new Domain.Entities.Notifications.Notification
                    {
                        UserId = comment.UserId,
                        Title = string.Format(await _configService.GetLocalizationAsync("notification_comment_reply_title", "ar", "رد جديد على تعليقك في درس {0}"), comment.Lesson?.Title ?? ""),
                        Message = string.Format(await _configService.GetLocalizationAsync("notification_comment_reply_message", "ar", "قام المدرس بالرد على تعليقك: \"{0}\""), replyText.Length > 50 ? replyText.Substring(0, 50) + "..." : replyText),
                        Type = Domain.Enums.NotificationType.Social,
                        ActionUrl = $"/Student/Lessons/Details/{comment.LessonId}#comment-{comment.Id}",
                        ActionText = "عرض الرد",
                        IconClass = "fas fa-reply",
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }
            });

            _logger.LogInformation("Instructor {InstructorId} replied to comment {CommentId} successfully.", userId, id);
            SetSuccessMessage("تم إضافة الرد بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to comment {CommentId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حفظ الرد.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تعديل الرد - Edit reply
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditReply(int id, string replyText)
    {
        var userId = _currentUserService.UserId;

        var comment = await _context.Comments
            .Include(c => c.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(c => c.Id == id && 
                c.Lesson != null && 
                c.Lesson.Module != null && 
                c.Lesson.Module.Course != null &&
                c.Lesson.Module.Course.InstructorId == userId &&
                c.InstructorId == userId);

        if (comment == null)
        {
            _logger.LogWarning("NotFound: Comment {CommentId} not found or instructor {InstructorId} unauthorized to edit reply.", id, userId);
            SetErrorMessage("التعليق غير موجود أو ليس لديك صلاحية لتعديل الرد.");
            return NotFound();
        }

        var (isValid, reason) = BusinessRuleHelper.ValidateComment(replyText);
        if (!isValid)
        {
            SetErrorMessage(reason!);
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                comment.InstructorReply = replyText;
                comment.InstructorReplyAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} edited reply for comment {CommentId}.", userId, id);
            SetSuccessMessage("تم تحديث الرد بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing reply for comment {CommentId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الرد.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حذف الرد - Delete reply
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReply(int id)
    {
        var userId = _currentUserService.UserId;

        var comment = await _context.Comments
            .Include(c => c.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(c => c.Id == id && 
                c.Lesson != null && 
                c.Lesson.Module != null && 
                c.Lesson.Module.Course != null &&
                c.Lesson.Module.Course.InstructorId == userId &&
                c.InstructorId == userId);

        if (comment == null)
        {
            _logger.LogWarning("NotFound: Comment {CommentId} not found or instructor {InstructorId} unauthorized to delete reply.", id, userId);
            SetErrorMessage("التعليق غير موجود أو ليس لديك صلاحية لحذف الرد.");
            return NotFound();
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                comment.InstructorReply = null;
                comment.InstructorReplyAt = null;
                comment.InstructorId = null;

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} deleted reply for comment {CommentId}.", userId, id);
            SetSuccessMessage("تم حذف الرد بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reply for comment {CommentId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف الرد.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تثبيت/إلغاء تثبيت التعليق - Pin/Unpin comment
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var userId = _currentUserService.UserId;

        var comment = await _context.Comments
            .Include(c => c.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(c => c.Id == id && 
                c.Lesson != null && 
                c.Lesson.Module != null && 
                c.Lesson.Module.Course != null &&
                c.Lesson.Module.Course.InstructorId == userId);

        if (comment == null)
        {
            _logger.LogWarning("NotFound: Comment {CommentId} not found or instructor {InstructorId} unauthorized.", id, userId);
            SetErrorMessage("التعليق غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                comment.IsPinned = !comment.IsPinned;
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} {Action} comment {CommentId}.", userId, comment.IsPinned ? "pinned" : "unpinned", id);
            SetSuccessMessage(comment.IsPinned ? "تم تثبيت التعليق" : "تم إلغاء تثبيت التعليق");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling pin for comment {CommentId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة التثبيت.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات التعليقات - Comments statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
        var sevenDaysAgo = now.AddDays(-7);

        try
        {
            // Base query for instructor's comments
            var commentsQuery = _context.Comments
                .Include(c => c.Author)
                .Include(c => c.Lesson)
                    .ThenInclude(l => l.Module)
                        .ThenInclude(m => m.Course)
                .Where(c => c.Lesson != null && 
                            c.Lesson.Module != null && 
                            c.Lesson.Module.Course != null &&
                            c.Lesson.Module.Course.InstructorId == userId);

            var allComments = await commentsQuery.ToListAsync();
            
            var totalComments = allComments.Count;
            var repliedComments = allComments.Count(c => c.InstructorReply != null);
            var pendingComments = allComments.Count(c => c.InstructorReply == null);
            
            // Comments this month
            var commentsThisMonth = allComments.Count(c => c.CreatedAt >= firstDayOfMonth);
            var commentsLastMonth = allComments.Count(c => c.CreatedAt >= firstDayOfLastMonth && c.CreatedAt < firstDayOfMonth);
            var commentChangeThisMonth = commentsThisMonth - commentsLastMonth;
            
            ViewBag.TotalComments = totalComments;
            ViewBag.RepliedComments = repliedComments;
            ViewBag.PendingComments = pendingComments;
            ViewBag.ReplyRate = totalComments > 0 ? (decimal)repliedComments / totalComments * 100 : 0;
            ViewBag.CommentsThisMonth = commentsThisMonth;
            ViewBag.CommentChangeThisMonth = commentChangeThisMonth;
            
            // Response time analysis (for comments with replies)
            var repliedWithTime = allComments
                .Where(c => c.InstructorReply != null && c.InstructorReplyAt.HasValue)
                .Select(c => (c.InstructorReplyAt!.Value - c.CreatedAt).TotalHours)
                .ToList();
            
            ViewBag.AverageResponseTime = repliedWithTime.Any() ? repliedWithTime.Average() : 0;
            ViewBag.FastestResponseTime = repliedWithTime.Any() ? repliedWithTime.Min() : 0;
            ViewBag.SlowestResponseTime = repliedWithTime.Any() ? repliedWithTime.Max() : 0;
            
            // Response time comparison with last month
            var repliedThisMonth = allComments
                .Where(c => c.InstructorReplyAt >= firstDayOfMonth)
                .Select(c => (c.InstructorReplyAt!.Value - c.CreatedAt).TotalHours)
                .ToList();
            var repliedLastMonth = allComments
                .Where(c => c.InstructorReplyAt >= firstDayOfLastMonth && c.InstructorReplyAt < firstDayOfMonth)
                .Select(c => (c.InstructorReplyAt!.Value - c.CreatedAt).TotalHours)
                .ToList();
            
            var avgThisMonth = repliedThisMonth.Any() ? repliedThisMonth.Average() : 0;
            var avgLastMonth = repliedLastMonth.Any() ? repliedLastMonth.Average() : 0;
            var responseTimeImprovement = avgLastMonth > 0 
                ? ((avgLastMonth - avgThisMonth) / avgLastMonth) * 100 
                : 0;
            ViewBag.ResponseTimeImprovement = responseTimeImprovement;
            
            // Comments change percentage
            var commentsChangePercent = commentsLastMonth > 0 
                ? ((commentsThisMonth - commentsLastMonth) * 100.0 / commentsLastMonth) 
                : (commentsThisMonth > 0 ? 100 : 0);
            ViewBag.CommentsChangePercent = commentsChangePercent;
            
            // Most active courses by comments
            var courseComments = allComments
                .Where(c => c.Lesson != null && c.Lesson.Module != null && c.Lesson.Module.Course != null)
                .GroupBy(c => new { c.Lesson!.Module!.CourseId, c.Lesson!.Module!.Course!.Title })
                .Select(g => new {
                    CourseId = g.Key.CourseId,
                    CourseTitle = g.Key.Title,
                    TotalComments = g.Count(),
                    RepliedComments = g.Count(c => c.InstructorReply != null),
                    ReplyRate = g.Count() > 0 ? (g.Count(c => c.InstructorReply != null) * 100.0 / g.Count()) : 0,
                    AvgResponseTime = g.Where(c => c.InstructorReplyAt.HasValue)
                        .Select(c => (c.InstructorReplyAt!.Value - c.CreatedAt).TotalHours)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderByDescending(x => x.TotalComments)
                .Take(await _configService.GetTopItemsLimitAsync("analytics_top_courses", Constants.DisplayLimits.TopCoursesOnAnalytics))
                .ToList();
            ViewBag.TopCoursesByComments = courseComments;
            
            // Most active students by comments
            var studentComments = allComments
                .Where(c => c.Author != null)
                .GroupBy(c => new { c.AuthorId, c.Author!.FirstName, c.Author.LastName })
                .Select(g => new {
                    StudentId = g.Key.AuthorId,
                    StudentName = $"{g.Key.FirstName} {g.Key.LastName}",
                    CommentCount = g.Count()
                })
                .OrderByDescending(x => x.CommentCount)
                .Take(await _configService.GetTopItemsLimitAsync("analytics_top_courses", Constants.DisplayLimits.TopCoursesOnAnalytics))
                .ToList();
            ViewBag.TopStudentsByComments = studentComments;
            var maxStudentComments = studentComments.Any() ? studentComments.Max(s => s.CommentCount) : 1;
            ViewBag.MaxStudentComments = maxStudentComments;
            
            // Performance score calculation (based on reply rate and response time)
            var replyRateScore = totalComments > 0 ? (repliedComments * Constants.PerformanceThresholds.ReplyRateScoreMax / totalComments) : 0;
            var avgResponseHours = repliedWithTime.Any() ? repliedWithTime.Average() : 0;
            var responseTimeScore = avgResponseHours <= Constants.PerformanceThresholds.ResponseTimeExcellent ? Constants.PerformanceThresholds.ResponseTimeScoreExcellent : 
                                    avgResponseHours <= Constants.PerformanceThresholds.ResponseTimeVeryGood ? Constants.PerformanceThresholds.ResponseTimeScoreVeryGood : 
                                    avgResponseHours <= Constants.PerformanceThresholds.ResponseTimeGood ? Constants.PerformanceThresholds.ResponseTimeScoreGood : 
                                    avgResponseHours <= Constants.PerformanceThresholds.ResponseTimeFair ? Constants.PerformanceThresholds.ResponseTimeScoreFair : 
                                    Constants.PerformanceThresholds.ResponseTimeScorePoor;
            var performanceScore = (int)(replyRateScore + responseTimeScore);
            ViewBag.PerformanceScore = performanceScore;
            
            // Pinned comments count
            var pinnedComments = allComments.Count(c => c.IsPinned);
            ViewBag.PinnedComments = pinnedComments;
            
            // Comments today
            var commentsToday = allComments.Count(c => c.CreatedAt.Date == now.Date);
            ViewBag.CommentsToday = commentsToday;
            
            // Average likes (if applicable)
            var avgLikes = allComments.Any() ? allComments.Average(c => c.LikesCount) : 0;
            ViewBag.AverageLikes = avgLikes;
            
            // Chart data - Comments per day (last 7 days)
            var chartLabels = new List<string>();
            var commentsChartData = new List<int>();
            var repliesChartData = new List<int>();
            var dayNames = await _configService.GetDayNamesAsync("ar");
            
            for (int i = Constants.DisplayLimits.ChartDataPoints - 1; i >= 0; i--)
            {
                var date = now.Date.AddDays(-i);
                var dayName = dayNames.TryGetValue(date.DayOfWeek, out var name) ? name : date.DayOfWeek.ToString();
                chartLabels.Add(dayName);
                commentsChartData.Add(allComments.Count(c => c.CreatedAt.Date == date));
                repliesChartData.Add(allComments.Count(c => c.InstructorReplyAt?.Date == date));
            }
            ViewBag.ChartLabels = chartLabels;
            ViewBag.CommentsChartData = commentsChartData;
            ViewBag.RepliesChartData = repliesChartData;
            
            // Response time distribution
            var responseDistribution = new int[Constants.ResponseTimeDistribution.BucketCount]; // <1h, 1-3h, 3-6h, 6-12h, >12h
            foreach (var hours in repliedWithTime)
            {
                if (hours < Constants.ResponseTimeDistribution.Bucket1Max) responseDistribution[0]++;
                else if (hours < Constants.ResponseTimeDistribution.Bucket2Max) responseDistribution[1]++;
                else if (hours < Constants.ResponseTimeDistribution.Bucket3Max) responseDistribution[2]++;
                else if (hours < Constants.ResponseTimeDistribution.Bucket4Max) responseDistribution[3]++;
                else responseDistribution[4]++;
            }
            ViewBag.ResponseDistribution = responseDistribution.ToList();
            
            // Most active time of day for comments
            var hourGroups = allComments
                .GroupBy(c => c.CreatedAt.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            
            if (hourGroups != null && hourGroups.Any())
            {
                var peakHour = hourGroups.Key;
                var peakHourEnd = (peakHour + 2) % 24;
                ViewBag.PeakActivityTime = $"{peakHour}-{peakHourEnd} {(peakHour >= 12 ? "مساءً" : "صباحاً")}";
            }
            else
            {
                // Default to evening if no comments yet
                ViewBag.PeakActivityTime = await _configService.GetLocalizationAsync("performance_peak_activity_default", "ar", "8-10 مساءً");
            }

            _logger.LogInformation("Instructor {InstructorId} viewed comment statistics.", userId);
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comment statistics for instructor {InstructorId}.", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل إحصائيات التعليقات.");
            ViewBag.TotalComments = 0;
            ViewBag.RepliedComments = 0;
            ViewBag.PendingComments = 0;
            ViewBag.ReplyRate = 0;
            return View();
        }
    }
}

