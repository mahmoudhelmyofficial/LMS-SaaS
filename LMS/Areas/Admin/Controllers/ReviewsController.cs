using LMS.Data;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المراجعات - Reviews Management Controller
/// </summary>
public class ReviewsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<ReviewsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المراجعات - Reviews list
    /// </summary>
    public async Task<IActionResult> Index(bool? approved, int? courseId, int page = 1)
    {
        var query = _context.Reviews
            .Include(r => r.Course)
            .Include(r => r.Student)
            .AsQueryable();

        if (approved.HasValue)
        {
            query = query.Where(r => r.IsApproved == approved.Value);
        }

        if (courseId.HasValue)
        {
            query = query.Where(r => r.CourseId == courseId.Value);
        }

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.Approved = approved;
        ViewBag.CourseId = courseId;
        ViewBag.Page = page;

        // Get courses for filter
        ViewBag.Courses = await _context.Courses
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        return View(reviews);
    }

    /// <summary>
    /// المراجعات المعلقة - Pending reviews
    /// </summary>
    public async Task<IActionResult> Pending(int? courseId, int page = 1)
    {
        var query = _context.Reviews
            .Include(r => r.Course)
            .Include(r => r.Student)
            .Where(r => !r.IsApproved && !r.IsRejected)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(r => r.CourseId == courseId.Value);
        }

        var totalCount = await query.CountAsync();
        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

        // Statistics
        ViewBag.PendingCount = totalCount;
        ViewBag.ApprovedCount = await _context.Reviews.CountAsync(r => r.IsApproved);
        ViewBag.RejectedCount = await _context.Reviews.CountAsync(r => r.IsRejected);
        ViewBag.TotalReviews = await _context.Reviews.CountAsync();

        // Get courses for filter
        ViewBag.Courses = await _context.Courses
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        return View(reviews);
    }

    /// <summary>
    /// تفاصيل المراجعة - Review details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var review = await _context.Reviews
            .Include(r => r.Course)
                .ThenInclude(c => c.Instructor)
            .Include(r => r.Student)
            .Include(r => r.HelpfulnessVotes)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        return View(review);
    }

    /// <summary>
    /// الموافقة على المراجعة - Approve review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                _logger.LogWarning("Review not found: {ReviewId}", id);
                return NotFound();
            }

            if (review.IsApproved)
            {
                SetWarningMessage(CultureExtensions.T("المراجعة موافق عليها بالفعل", "Review is already approved."));
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check for spam before approving
            if (await IsSpamContentAsync(review.Comment))
            {
                SetWarningMessage(CultureExtensions.T("تم اكتشاف محتوى مشبوه. يرجى المراجعة يدوياً قبل الموافقة.", "Suspicious content detected. Please review manually before approving."));
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                review.IsApproved = true;
                review.IsRejected = false;
                review.ApprovedAt = DateTime.UtcNow;
                review.ApprovedBy = _currentUserService.UserId;

                // Recalculate course statistics - with null check
                if (review.Course != null)
                {
                    var approvedReviews = await _context.Reviews
                        .Where(r => r.CourseId == review.CourseId && (r.IsApproved || r.Id == id))
                        .ToListAsync();

                    review.Course.TotalReviews = approvedReviews.Count;
                    review.Course.AverageRating = approvedReviews.Any() 
                        ? approvedReviews.Average(r => (decimal)r.Rating) 
                        : 0;
                }

                await _context.SaveChangesAsync();

                // Send notification to student
                if (review.Student?.Email != null && review.Course != null)
                {
                    await _emailService.SendEmailAsync(
                        review.Student.Email,
                        "تمت الموافقة على تقييمك",
                        $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {review.Student.FirstName}</h2>
                            <p>تمت الموافقة على تقييمك لدورة <strong>{review.Course.Title}</strong>.</p>
                            <p>شكراً لمشاركة رأيك مع المجتمع!</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Review {ReviewId} approved by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم الموافقة على المراجعة وإرسال إشعار للطالب", "Review approved and notification sent to student."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving review {ReviewId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء الموافقة على المراجعة", "An error occurred while approving the review."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// رفض المراجعة - Reject review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetErrorMessage(CultureExtensions.T("يجب إدخال سبب الرفض", "Rejection reason is required."));
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Student)
                .Include(r => r.Course)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                _logger.LogWarning("Review not found: {ReviewId}", id);
                return NotFound();
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                review.IsRejected = true;
                review.RejectionReason = reason;
                review.IsApproved = false;
                review.RejectedAt = DateTime.UtcNow;
                review.RejectedBy = _currentUserService.UserId;

                // Recalculate course statistics (excluding this review) - with null check
                if (review.Course != null)
                {
                    var approvedReviews = await _context.Reviews
                        .Where(r => r.CourseId == review.CourseId && r.IsApproved && r.Id != id)
                        .ToListAsync();

                    review.Course.TotalReviews = approvedReviews.Count;
                    review.Course.AverageRating = approvedReviews.Any() 
                        ? approvedReviews.Average(r => (decimal)r.Rating) 
                        : 0;
                }

                await _context.SaveChangesAsync();

                // Send notification to student - with null check for course
                if (review.Student?.Email != null && review.Course != null)
                {
                    await _emailService.SendEmailAsync(
                        review.Student.Email,
                        "بخصوص تقييمك",
                        $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {review.Student.FirstName}</h2>
                            <p>للأسف، لم نتمكن من الموافقة على تقييمك لدورة <strong>{review.Course.Title}</strong>.</p>
                            <p><strong>السبب:</strong> {reason}</p>
                            <p>يرجى مراجعة سياسة المراجعات والتأكد من توافق تقييمك مع المعايير.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Review {ReviewId} rejected by admin {AdminId}. Reason: {Reason}", 
                id, _currentUserService.UserId, reason);

            SetSuccessMessage(CultureExtensions.T("تم رفض المراجعة وإرسال إشعار للطالب", "Review rejected and notification sent to student."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting review {ReviewId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء رفض المراجعة", "An error occurred while rejecting the review."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تثبيت المراجعة - Pin review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.IsPinned = !review.IsPinned;
        await _context.SaveChangesAsync();

        SetSuccessMessage(review.IsPinned ? "تم تثبيت المراجعة" : "تم إلغاء التثبيت");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف المراجعة - Delete review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .Include(r => r.HelpfulnessVotes)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                _logger.LogWarning("Review not found for deletion: {ReviewId}", id);
                return NotFound();
            }

            var courseId = review.CourseId;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Remove helpfulness votes
                _context.ReviewHelpfulness.RemoveRange(review.HelpfulnessVotes);
                
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                // Update course statistics
                var course = await _context.Courses.FindAsync(courseId);
                if (course != null)
                {
                    var totalReviews = await _context.Reviews
                        .Where(r => r.CourseId == courseId && r.IsApproved)
                        .CountAsync();
                    
                    var averageRating = totalReviews > 0
                        ? await _context.Reviews
                            .Where(r => r.CourseId == courseId && r.IsApproved)
                            .AverageAsync(r => (decimal)r.Rating)
                        : 0;

                    course.TotalReviews = totalReviews;
                    course.AverageRating = averageRating;

                    await _context.SaveChangesAsync();
                }
            });

            _logger.LogInformation("Review {ReviewId} deleted by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم حذف المراجعة بنجاح", "Review deleted successfully."));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء حذف المراجعة", "An error occurred while deleting the review."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// اكتشاف الرسائل المشبوهة - Detect suspicious reviews
    /// </summary>
    public async Task<IActionResult> SpamDetection(int page = 1)
    {
        try
        {
            var allReviews = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .Where(r => !r.IsApproved && !r.IsRejected)
                .ToListAsync();

            // Get spam keywords once
            var spamKeywords = await GetSpamKeywordsAsync();

            // Filter reviews that contain spam keywords or patterns
            var suspiciousReviews = allReviews
                .Where(r => ContainsSpamKeywords(r.Comment, spamKeywords) || 
                           HasSuspiciousPattern(r.Comment) ||
                           r.Rating == 1 || r.Rating == 5) // Extreme ratings
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .Select(r => new
                {
                    Review = r,
                    SpamScore = CalculateSpamScoreSync(r.Comment, spamKeywords),
                    HasUrl = Regex.IsMatch(r.Comment ?? "", @"https?://", RegexOptions.IgnoreCase),
                    HasContactInfo = HasContactInfo(r.Comment),
                    IsExtremeRating = r.Rating == 1 || r.Rating == 5
                })
                .ToList();

            ViewBag.Page = page;
            return View(suspiciousReviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading spam detection");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل البيانات", "An error occurred while loading data."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الموافقة المجمعة - Bulk approve reviews
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(int[] ids)
    {
        if (ids == null || !ids.Any())
        {
            SetErrorMessage(CultureExtensions.T("لم يتم تحديد أي مراجعات", "No reviews selected."));
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var reviews = await _context.Reviews
                .Include(r => r.Course)
                .Where(r => ids.Contains(r.Id) && !r.IsApproved)
                .ToListAsync();

            // Get spam keywords once
            var spamKeywords = await GetSpamKeywordsAsync();

            await _context.ExecuteInTransactionAsync(async () =>
            {
                foreach (var review in reviews)
                {
                    // Skip spam content
                    if (ContainsSpamKeywords(review.Comment, spamKeywords))
                    {
                        _logger.LogWarning("Skipping review {ReviewId} due to spam detection", review.Id);
                        continue;
                    }

                    review.IsApproved = true;
                    review.ApprovedAt = DateTime.UtcNow;
                    review.ApprovedBy = _currentUserService.UserId;
                }

                await _context.SaveChangesAsync();

                // Update course statistics for affected courses
                var affectedCourseIds = reviews.Select(r => r.CourseId).Distinct();
                foreach (var courseId in affectedCourseIds)
                {
                    var course = await _context.Courses.FindAsync(courseId);
                    if (course != null)
                    {
                        var approvedReviews = await _context.Reviews
                            .Where(r => r.CourseId == courseId && r.IsApproved)
                            .ToListAsync();

                        course.TotalReviews = approvedReviews.Count;
                        course.AverageRating = approvedReviews.Any() 
                            ? approvedReviews.Average(r => (decimal)r.Rating) 
                            : 0;
                    }
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Bulk approved {Count} reviews by admin {AdminId}", 
                reviews.Count, _currentUserService.UserId);

            SetSuccessMessage(string.Format(CultureExtensions.T("تم الموافقة على {0} مراجعة", "{0} review(s) approved."), reviews.Count));
            return RedirectToAction(nameof(Index), new { approved = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk approve");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء الموافقة المجمعة", "An error occurred while bulk approving."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// الحذف المجمع - Bulk delete reviews
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(int[] ids)
    {
        if (ids == null || !ids.Any())
        {
            SetErrorMessage(CultureExtensions.T("لم يتم تحديد أي مراجعات", "No reviews selected."));
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var reviews = await _context.Reviews
                .Include(r => r.HelpfulnessVotes)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync();

            var reviewCount = reviews.Count;
            var affectedCourseIds = reviews.Select(r => r.CourseId).Distinct().ToList();

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Remove helpfulness votes
                foreach (var review in reviews)
                {
                    _context.ReviewHelpfulness.RemoveRange(review.HelpfulnessVotes);
                }

                _context.Reviews.RemoveRange(reviews);
                await _context.SaveChangesAsync();

                // Update course statistics
                foreach (var courseId in affectedCourseIds)
                {
                    var course = await _context.Courses.FindAsync(courseId);
                    if (course != null)
                    {
                        var approvedReviews = await _context.Reviews
                            .Where(r => r.CourseId == courseId && r.IsApproved)
                            .ToListAsync();

                        course.TotalReviews = approvedReviews.Count;
                        course.AverageRating = approvedReviews.Any() 
                            ? approvedReviews.Average(r => (decimal)r.Rating) 
                            : 0;
                    }
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Bulk deleted {Count} reviews by admin {AdminId}", 
                reviewCount, _currentUserService.UserId);

            SetSuccessMessage(string.Format(CultureExtensions.T("تم حذف {0} مراجعة", "{0} review(s) deleted."), reviewCount));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk delete");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء الحذف المجمع", "An error occurred while bulk deleting."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إحصائيات المراجعات - Review statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var reviewsQuery = _context.Reviews
                .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate);

            var stats = new
            {
                TotalReviews = await reviewsQuery.CountAsync(),
                ApprovedReviews = await reviewsQuery.CountAsync(r => r.IsApproved),
                PendingReviews = await reviewsQuery.CountAsync(r => !r.IsApproved && !r.IsRejected),
                RejectedReviews = await reviewsQuery.CountAsync(r => r.IsRejected),
                AverageRating = await reviewsQuery.AverageAsync(r => (decimal?)r.Rating) ?? 0,
                RatingDistribution = await reviewsQuery
                    .GroupBy(r => r.Rating)
                    .Select(g => new { Rating = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Rating)
                    .ToListAsync(),
                ReviewsByDay = await reviewsQuery
                    .GroupBy(r => r.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync(),
                TopReviewedCourses = await _context.Reviews
                    .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
                    .GroupBy(r => new { r.CourseId, r.Course.Title })
                    .Select(g => new { 
                        CourseId = g.Key.CourseId, 
                        Title = g.Key.Title, 
                        Count = g.Count(),
                        AverageRating = g.Average(r => r.Rating)
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync(),
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading review statistics");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// صفحة الرد على المراجعة - Reply to review page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Reply(int id)
    {
        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                _logger.LogWarning("Review not found for reply: {ReviewId}", id);
                return NotFound();
            }

            ViewBag.Review = review;

            return View(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reply page for review {ReviewId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل الصفحة", "An error occurred while loading the page."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حفظ الرد على المراجعة - Save reply to review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string replyContent)
    {
        if (string.IsNullOrWhiteSpace(replyContent))
        {
            SetErrorMessage(CultureExtensions.T("محتوى الرد مطلوب", "Reply content is required."));
            return RedirectToAction(nameof(Reply), new { id });
        }

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                _logger.LogWarning("Review not found for reply: {ReviewId}", id);
                return NotFound();
            }

            // Save the reply
            review.AdminReply = replyContent;
            review.AdminReplyAt = DateTime.UtcNow;
            review.AdminReplyBy = _currentUserService.UserId;

            await _context.SaveChangesAsync();

            // Send notification email to student
            if (review.Student?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    review.Student.Email,
                    "تم الرد على تقييمك",
                    $@"<html><body dir='rtl'>
                        <h2>عزيزي/عزيزتي {review.Student.FirstName}</h2>
                        <p>قام فريق المنصة بالرد على تقييمك لدورة <strong>{review.Course.Title}</strong>.</p>
                        <div style='background-color: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <strong>الرد:</strong>
                            <p>{replyContent}</p>
                        </div>
                        <p>شكراً لمشاركتك رأيك معنا!</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>",
                    true);
            }

            _logger.LogInformation("Admin {AdminId} replied to review {ReviewId}", 
                _currentUserService.UserId, id);

            SetSuccessMessage(CultureExtensions.T("تم إرسال الرد بنجاح وإشعار الطالب", "Reply sent successfully and student notified."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to review {ReviewId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إرسال الرد", "An error occurred while sending the reply."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    #region Spam Detection Helpers

    /// <summary>
    /// Get spam keywords from database (cached per request)
    /// </summary>
    private async Task<List<string>> GetSpamKeywordsAsync()
    {
        return await _context.SpamKeywords
            .Where(sk => sk.IsActive && !sk.IsDeleted)
            .Select(sk => sk.Keyword.ToLower())
            .ToListAsync();
    }

    /// <summary>
    /// Check if content contains spam keywords (async version)
    /// </summary>
    private async Task<bool> IsSpamContentAsync(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        content = content.ToLower();

        // Get active spam keywords from database
        var spamKeywords = await GetSpamKeywordsAsync();

        // Check for spam keywords
        foreach (var keyword in spamKeywords)
        {
            if (content.Contains(keyword))
            {
                _logger.LogWarning("Spam keyword detected: {Keyword}", keyword);
                return true;
            }
        }

        // Check for excessive links
        var urlCount = Regex.Matches(content, @"https?://").Count;
        if (urlCount > 2)
        {
            _logger.LogWarning("Excessive URLs detected: {Count}", urlCount);
            return true;
        }

        // Check for repeated characters (e.g., "!!!!!")
        if (Regex.IsMatch(content, @"(.)\1{5,}"))
        {
            _logger.LogWarning("Repeated characters detected");
            return true;
        }

        return false;
    }

    private bool HasSuspiciousPattern(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Check for all caps (more than 50% of text)
        var upperCount = content.Count(char.IsUpper);
        var letterCount = content.Count(char.IsLetter);
        if (letterCount > 10 && (double)upperCount / letterCount > 0.5)
            return true;

        // Check for excessive punctuation
        var punctuationCount = content.Count(c => "!?.,;:".Contains(c));
        if (punctuationCount > content.Length * 0.2)
            return true;

        return false;
    }

    private bool HasContactInfo(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Check for phone numbers
        if (Regex.IsMatch(content, @"\d{10,}"))
            return true;

        // Check for email patterns
        if (Regex.IsMatch(content, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"))
            return true;

        // Check for social media handles
        if (Regex.IsMatch(content, @"@\w+|facebook|twitter|instagram|whatsapp", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if content contains any of the provided spam keywords (synchronous helper)
    /// </summary>
    private bool ContainsSpamKeywords(string? content, List<string> spamKeywords)
    {
        if (string.IsNullOrWhiteSpace(content) || !spamKeywords.Any())
            return false;

        content = content.ToLower();

        return spamKeywords.Any(keyword => content.Contains(keyword));
    }

    /// <summary>
    /// Calculate spam score synchronously (for use in LINQ queries)
    /// </summary>
    private int CalculateSpamScoreSync(string? content, List<string> spamKeywords)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        int score = 0;

        // Check each spam indicator
        if (ContainsSpamKeywords(content, spamKeywords)) score += 40;
        if (HasSuspiciousPattern(content)) score += 30;
        if (HasContactInfo(content)) score += 30;

        return score;
    }

    private async Task<int> CalculateSpamScoreAsync(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        int score = 0;

        // Get spam keywords
        var spamKeywords = await GetSpamKeywordsAsync();

        // Check each spam indicator
        if (ContainsSpamKeywords(content, spamKeywords)) score += 40;
        if (HasSuspiciousPattern(content)) score += 30;
        if (HasContactInfo(content)) score += 30;

        return score;
    }

    #endregion
}

