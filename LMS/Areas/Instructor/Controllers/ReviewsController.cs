using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة المراجعات - Reviews Management Controller
/// </summary>
public class ReviewsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ReviewsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة مراجعات الدورات - Course Reviews List
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, int? rating, bool? hasResponse, string? sortBy, string? search, int page = 1)
    {
        try
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID is null or empty in ReviewsController.Index");
                return RedirectToAction("Login", "Account", new { area = "" });
            }
            
            var pageSize = 10;

            var query = _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .Where(r => r.Course.InstructorId == userId && r.IsApproved)
                .AsQueryable();

            if (courseId.HasValue)
            {
                query = query.Where(r => r.CourseId == courseId.Value);
            }

            if (rating.HasValue)
            {
                query = query.Where(r => r.Rating == rating.Value);
            }

            if (hasResponse.HasValue)
            {
                query = query.Where(r => hasResponse.Value 
                    ? !string.IsNullOrEmpty(r.InstructorResponse) 
                    : string.IsNullOrEmpty(r.InstructorResponse));
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Comment.Contains(search) || 
                                         r.Student.FullName.Contains(search) ||
                                         r.Course.Title.Contains(search));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            IQueryable<Domain.Entities.Social.Review> sortedQuery;
            switch (sortBy?.ToLower())
            {
                case "oldest":
                    sortedQuery = query.OrderBy(r => r.CreatedAt);
                    break;
                case "rating":
                    sortedQuery = query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "newest":
                default:
                    sortedQuery = query.OrderByDescending(r => r.CreatedAt);
                    break;
            }

            var reviews = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get all reviews for statistics (before filtering by rating/response)
            var allReviewsQuery = _context.Reviews
                .Where(r => r.Course.InstructorId == userId && r.IsApproved);
            
            if (courseId.HasValue)
            {
                allReviewsQuery = allReviewsQuery.Where(r => r.CourseId == courseId.Value);
            }

            var allReviews = await allReviewsQuery.ToListAsync();
            var totalReviews = allReviews.Count;

            // Calculate rating distribution
            var ratingDistribution = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                var countForRating = allReviews.Count(r => r.Rating == i);
                ratingDistribution[i] = totalReviews > 0 
                    ? (int)Math.Round((double)countForRating * 100 / totalReviews) 
                    : 0;
            }

            // Calculate average rating
            var averageRating = totalReviews > 0 
                ? allReviews.Average(r => r.Rating) 
                : 0;

            ViewBag.RatingDistribution = ratingDistribution;
            ViewBag.TotalReviews = totalReviews;
            ViewBag.AverageRating = averageRating;

            ViewBag.Courses = await _context.Courses
                .Where(c => c.InstructorId == userId && c.AllowReviews)
                .OrderBy(c => c.Title)
                .ToListAsync();

            ViewBag.CourseId = courseId;
            ViewBag.Rating = rating;
            ViewBag.HasResponse = hasResponse;
            ViewBag.SortBy = sortBy;
            ViewBag.SearchTerm = search;
            
            // Pagination data
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalItems = totalCount;
            ViewBag.PageSize = pageSize;

            _logger.LogInformation("Instructor {InstructorId} viewed reviews. Total: {Count}", userId, totalCount);

            return View(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReviewsController.Index");
            SetWarningMessage("تعذر تحميل قائمة المراجعات. يرجى المحاولة مرة أخرى.");
            
            // Return empty view instead of redirecting
            ViewBag.TotalReviews = 0;
            ViewBag.AverageRating = 0.0;
            ViewBag.UnrespondedCount = 0;
            ViewBag.RatingDistribution = new Dictionary<int, int>
            {
                { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
            };
            ViewBag.Courses = new List<object>();
            ViewBag.CourseId = courseId;
            ViewBag.Rating = rating;
            ViewBag.HasResponse = hasResponse;
            ViewBag.Page = page;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;
            ViewBag.PageSize = 10;
            
            return View(new List<Domain.Entities.Social.Review>());
        }
    }

    /// <summary>
    /// تفاصيل المراجعة - Review Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var review = await _context.Reviews
            .Include(r => r.Course)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (review == null)
            return NotFound();

        return View(review);
    }

    /// <summary>
    /// الرد على المراجعة - Respond to Review
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Respond(int id)
    {
        var userId = _currentUserService.UserId;

        var review = await _context.Reviews
            .Include(r => r.Course)
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

        if (review == null)
            return NotFound();

        var viewModel = new ReviewResponseViewModel
        {
            ReviewId = review.Id,
            StudentName = review.Student.FullName,
            CourseTitle = review.Course.Title,
            Rating = review.Rating,
            ReviewComment = review.Comment,
            ExistingResponse = review.InstructorResponse
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ الرد على المراجعة - Save Review Response
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, ReviewResponseViewModel model)
    {
        if (id != model.ReviewId)
            return BadRequest();

        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

            if (review == null)
            {
                _logger.LogWarning("Review {ReviewId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Use BusinessRuleHelper for validation
            var alreadyResponded = !string.IsNullOrEmpty(review.InstructorResponse);
            var (isValid, validationReason) = BusinessRuleHelper.ValidateReviewResponse(
                model.Response,
                alreadyResponded);

            if (!isValid)
            {
                _logger.LogWarning("Review response validation failed: {Reason}", validationReason);
                ModelState.AddModelError(nameof(model.Response), validationReason!);
                return View(model);
            }

            var isUpdate = alreadyResponded;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                review.InstructorResponse = model.Response;
                review.ResponseAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notification to student
                var notification = new Notification
                {
                    UserId = review.StudentId,
                    Title = isUpdate ? "تم تحديث رد المدرس على تقييمك" : "رد المدرس على تقييمك",
                    Message = $"رد المدرس على تقييمك للدورة: {review.Course.Title}",
                    Type = NotificationType.CourseUpdate,
                    ActionUrl = $"/Student/Courses/Details/{review.CourseId}#reviews",
                    ActionText = "عرض الرد",
                    IconClass = "fas fa-reply text-primary",
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Instructor {InstructorId} {Action} to review {ReviewId}",
                userId, isUpdate ? "updated response" : "responded", id);

            SetSuccessMessage(isUpdate ? "تم تحديث ردك بنجاح وإرسال إشعار للطالب" : "تم إضافة ردك بنجاح وإرسال إشعار للطالب");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error responding to review {ReviewId}", id);
            SetErrorMessage("حدث خطأ أثناء حفظ الرد");
            return View(model);
        }
    }

    /// <summary>
    /// حذف الرد - Delete Response
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResponse(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

            if (review == null)
            {
                _logger.LogWarning("Review {ReviewId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            if (string.IsNullOrEmpty(review.InstructorResponse))
            {
                SetWarningMessage("لا يوجد رد لحذفه");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                review.InstructorResponse = null;
                review.ResponseAt = null;

                await _context.SaveChangesAsync();

                // Notify student that response was removed
                var notification = new Notification
                {
                    UserId = review.StudentId,
                    Title = "تم حذف رد المدرس",
                    Message = $"قام المدرس بحذف رده على تقييمك للدورة: {review.Course.Title}",
                    Type = NotificationType.CourseUpdate,
                    ActionUrl = $"/Student/Courses/Details/{review.CourseId}#reviews",
                    ActionText = "عرض التقييم",
                    IconClass = "fas fa-info-circle",
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} deleted response to review {ReviewId}", 
                userId, id);

            SetSuccessMessage("تم حذف الرد بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting response for review {ReviewId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الرد");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات المراجعات - Reviews Statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int? courseId)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Reviews
            .Include(r => r.Course)
            .Where(r => r.Course.InstructorId == userId && r.IsApproved)
            .AsQueryable();

        if (courseId.HasValue)
        {
            query = query.Where(r => r.CourseId == courseId.Value);
        }

        var reviews = await query.ToListAsync();

        var stats = new ReviewStatisticsViewModel
        {
            TotalReviews = reviews.Count,
            AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
            FiveStarCount = reviews.Count(r => r.Rating == 5),
            FourStarCount = reviews.Count(r => r.Rating == 4),
            ThreeStarCount = reviews.Count(r => r.Rating == 3),
            TwoStarCount = reviews.Count(r => r.Rating == 2),
            OneStarCount = reviews.Count(r => r.Rating == 1),
            ResponseRate = reviews.Count > 0 
                ? (reviews.Count(r => !string.IsNullOrEmpty(r.InstructorResponse)) * 100.0 / reviews.Count) 
                : 0
        };

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId && c.AllowReviews)
            .OrderBy(c => c.Title)
            .ToListAsync();

        ViewBag.CourseId = courseId;

        return View(stats);
    }

    /// <summary>
    /// الإبلاغ عن مراجعة - Report a review (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Report(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

            if (review == null)
            {
                _logger.LogWarning("Review {ReviewId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("لم يتم العثور على التقييم");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Review = review;
            return View(new ReviewReportViewModel { ReviewId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report form for review {ReviewId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل نموذج الإبلاغ");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إرسال الإبلاغ عن مراجعة - Submit review report (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int id, ReviewReportViewModel model)
    {
        var userId = _currentUserService.UserId;

        // Validate user
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Report POST: UserId is null or empty");
            SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (id != model.ReviewId)
        {
            _logger.LogWarning("Report POST: ReviewId mismatch. Route: {RouteId}, Model: {ModelId}", id, model.ReviewId);
            SetErrorMessage("خطأ في معرّف التقييم");
            return BadRequest();
        }

        // Validate reason
        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "يجب إدخال سبب الإبلاغ");
        }
        else if (model.Reason.Length < 10)
        {
            ModelState.AddModelError(nameof(model.Reason), "سبب الإبلاغ يجب أن يكون 10 أحرف على الأقل");
        }

        if (!ModelState.IsValid)
        {
            var reviewForView = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);
            ViewBag.Review = reviewForView;
            return View(model);
        }

        try
        {
            var review = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.Course.InstructorId == userId);

            if (review == null)
            {
                _logger.LogWarning("Review {ReviewId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("لم يتم العثور على التقييم");
                return RedirectToAction(nameof(Index));
            }

            // Check if already reported
            if (review.IsReported)
            {
                SetWarningMessage("تم الإبلاغ عن هذا التقييم مسبقاً");
                return RedirectToAction(nameof(Index));
            }

            // Use transaction for data consistency
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Mark review as reported
                review.IsReported = true;
                review.ReportReason = model.Reason?.Trim();
                review.ReportedAt = DateTime.UtcNow;
                review.ReportedById = userId;

                await _context.SaveChangesAsync();

                // Create notification for admin
                var adminNotification = new Notification
                {
                    UserId = null, // Will be sent to admins
                    Title = "إبلاغ عن تقييم",
                    Message = $"قام المدرس بالإبلاغ عن تقييم في الدورة: {review.Course?.Title ?? "غير محدد"}. السبب: {model.Reason}",
                    Type = NotificationType.Warning,
                    ActionUrl = $"/Admin/Reviews/Details/{review.Id}",
                    ActionText = "مراجعة الإبلاغ",
                    IconClass = "fas fa-flag text-danger",
                    IsRead = false
                };

                _context.Notifications.Add(adminNotification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Instructor {InstructorId} reported review {ReviewId}. Reason: {Reason}", 
                userId, id, model.Reason);

            SetSuccessMessage("تم إرسال الإبلاغ بنجاح. سيتم مراجعته من قبل الإدارة");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting review {ReviewId} by instructor {InstructorId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء إرسال الإبلاغ. يرجى المحاولة مرة أخرى");
            return RedirectToAction(nameof(Index));
        }
    }
}

