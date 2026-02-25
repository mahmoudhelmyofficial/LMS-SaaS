using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Entities.Social;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// مراجعات الدورات - Course Reviews Controller
/// </summary>
public class ReviewsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInstructorNotificationService _instructorNotificationService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IInstructorNotificationService instructorNotificationService,
        ILogger<ReviewsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _instructorNotificationService = instructorNotificationService;
        _logger = logger;
    }

    /// <summary>
    /// مراجعاتي - My reviews
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var reviews = await _context.Reviews
            .Include(r => r.Course)
            .Where(r => r.StudentId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDisplayViewModel
            {
                Id = r.Id,
                CourseId = r.CourseId,
                CourseName = r.Course.Title,
                StudentName = $"{r.Student.FirstName} {r.Student.LastName}",
                StudentImageUrl = r.Student.ProfileImageUrl,
                Rating = r.Rating,
                Title = r.Title,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                IsApproved = r.IsApproved,
                HelpfulCount = r.HelpfulCount,
                NotHelpfulCount = r.NotHelpfulCount,
                InstructorResponse = r.InstructorResponse,
                ResponseAt = r.ResponseAt,
                IsVerifiedPurchase = r.IsVerifiedPurchase,
                IsPinned = r.IsPinned
            })
            .ToListAsync();

        return View(reviews);
    }

    /// <summary>
    /// إنشاء مراجعة - Create review
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Check if enrolled
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == userId);

        if (enrollment == null)
        {
            SetErrorMessage("يجب أن تكون مسجلاً في الدورة لتقييمها");
            return RedirectToAction("Details", "Courses", new { area = "Student", id = courseId });
        }

        // Check if already reviewed
        var existingReview = await _context.Reviews
            .AnyAsync(r => r.CourseId == courseId && r.StudentId == userId);

        if (existingReview)
        {
            SetErrorMessage("لقد قمت بتقييم هذه الدورة من قبل");
            return RedirectToAction(nameof(Edit), new { courseId });
        }

        var course = await _context.Courses
            .Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == courseId);
        ViewBag.Course = course;

        return View(new CreateReviewViewModel { CourseId = courseId });
    }

    /// <summary>
    /// حفظ المراجعة - Save review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReviewViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Check if enrolled
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == model.CourseId && e.StudentId == userId);

        if (enrollment == null)
        {
            SetErrorMessage("يجب أن تكون مسجلاً في الدورة لتقييمها");
            return RedirectToAction("Details", "Courses", new { area = "Student", id = model.CourseId });
        }

        // Check if already reviewed
        var existingReview = await _context.Reviews
            .AnyAsync(r => r.CourseId == model.CourseId && r.StudentId == userId);

        if (existingReview)
        {
            SetErrorMessage("لقد قمت بتقييم هذه الدورة من قبل");
            return RedirectToAction(nameof(Edit), new { courseId = model.CourseId });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var course = await _context.Courses
                    .Include(c => c.Instructor)
                    .FirstOrDefaultAsync(c => c.Id == model.CourseId);
                
                if (course == null)
                    return NotFound();

                // Validation: Check minimum completion requirement (25%)
                if (enrollment.ProgressPercentage < 25)
                {
                    SetErrorMessage("يجب إكمال 25% على الأقل من الدورة قبل التقييم");
                    return RedirectToAction("Details", "Courses", new { area = "Student", id = model.CourseId });
                }

                // Validation: Comment length
                if (string.IsNullOrWhiteSpace(model.Comment) || model.Comment.Length < 10)
                {
                    ModelState.AddModelError(nameof(model.Comment), "يجب أن يكون التعليق 10 أحرف على الأقل");
                    var courseData1 = await _context.Courses.FindAsync(model.CourseId);
                    ViewBag.Course = courseData1;
                    return View(model);
                }

                // Spam detection
                var spamCheckResult = DetectSpam(model.Comment, model.Title);
                if (spamCheckResult.IsSpam)
                {
                    SetErrorMessage($"تم رفض التقييم: {spamCheckResult.Reason}");
                    var courseData2 = await _context.Courses.FindAsync(model.CourseId);
                    ViewBag.Course = courseData2;
                    return View(model);
                }

                // Validation: Extreme rating without justification
                if ((model.Rating == 1 || model.Rating == 5) && model.Comment.Length < 50)
                {
                    ModelState.AddModelError(nameof(model.Comment), 
                        "التقييم المتطرف (1 أو 5 نجوم) يتطلب تعليق تفصيلي (50 حرف على الأقل)");
                    var courseData3 = await _context.Courses.FindAsync(model.CourseId);
                    ViewBag.Course = courseData3;
                    return View(model);
                }

                // Create the review
                var review = new Review
                {
                    CourseId = model.CourseId,
                    StudentId = userId!,
                    Rating = model.Rating,
                    Title = model.Title,
                    Comment = model.Comment,
                    IsApproved = false, // Requires admin approval
                    IsVerifiedPurchase = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New review {ReviewId} created for course {CourseId} by student {StudentId}, Rating: {Rating}", 
                    review.Id, model.CourseId, userId, model.Rating);

                // Notify instructor about new review (unified path: DB + SignalR + Web Push)
                try
                {
                    if (!string.IsNullOrEmpty(course.InstructorId))
                    {
                        var student = await _context.Users.FindAsync(userId);
                        var studentName = student?.FullName ?? student?.FirstName ?? "طالب";
                        _ = await _instructorNotificationService.NotifyNewReviewAsync(
                            course.InstructorId,
                            course.Id,
                            course.Title,
                            studentName,
                            model.Rating,
                            model.Comment);
                    }
                }
                catch (Exception notifyEx)
                {
                    _logger.LogWarning(notifyEx, "Failed to create notification for review {ReviewId}", review.Id);
                }

                SetSuccessMessage("تم إرسال تقييمك بنجاح. سيظهر بعد الموافقة عليه من قبل الإدارة");
                return RedirectToAction("Details", "Courses", new { area = "Student", id = model.CourseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for course {CourseId} by student {StudentId}", 
                    model.CourseId, userId);
                SetErrorMessage("حدث خطأ أثناء إرسال التقييم");
                
                var courseErrorData = await _context.Courses.FindAsync(model.CourseId);
                ViewBag.Course = courseErrorData;
                return View(model);
            }
        }

        var courseData = await _context.Courses.FindAsync(model.CourseId);
        ViewBag.Course = courseData;

        return View(model);
    }

    /// <summary>
    /// تعديل المراجعة - Edit review
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int courseId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var review = await _context.Reviews
            .Include(r => r.Course)
            .FirstOrDefaultAsync(r => r.CourseId == courseId && r.StudentId == userId);

        if (review == null)
            return NotFound();

        var viewModel = new EditReviewViewModel
        {
            Id = review.Id,
            CourseId = review.CourseId,
            Rating = review.Rating,
            Title = review.Title,
            Comment = review.Comment
        };

        ViewBag.Course = review.Course;

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات المراجعة - Save review edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditReviewViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (review == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            review.Rating = model.Rating;
            review.Title = model.Title;
            review.Comment = model.Comment;
            review.IsApproved = false; // Requires re-approval after edit

            await _context.SaveChangesAsync();

            // Update course statistics
            var course = await _context.Courses.FindAsync(review.CourseId);
            if (course != null)
            {
                var totalReviews = await _context.Reviews
                    .Where(r => r.CourseId == review.CourseId && r.IsApproved)
                    .CountAsync();
                
                var averageRating = totalReviews > 0
                    ? await _context.Reviews
                        .Where(r => r.CourseId == review.CourseId && r.IsApproved)
                        .AverageAsync(r => (decimal)r.Rating)
                    : 0;

                course.TotalReviews = totalReviews;
                course.AverageRating = averageRating;

                await _context.SaveChangesAsync();
            }

            SetSuccessMessage("تم تحديث تقييمك بنجاح");
            return RedirectToAction("Details", "Courses", new { area = "Student", id = review.CourseId });
        }

        var courseData = await _context.Courses.FindAsync(review.CourseId);
        ViewBag.Course = courseData;

        return View(model);
    }

    /// <summary>
    /// حذف المراجعة - Delete review
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

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == userId);

        if (review == null)
            return NotFound();

        var courseId = review.CourseId;

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

        SetSuccessMessage("تم حذف تقييمك بنجاح");
        return RedirectToAction("Details", "Courses", new { area = "Student", id = courseId });
    }

    /// <summary>
    /// التصويت على مفيد - Vote helpful
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoteHelpful(int reviewId, bool helpful)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        // Check if already voted
        var existingVote = await _context.ReviewHelpfulness
            .FirstOrDefaultAsync(v => v.ReviewId == reviewId && v.UserId == userId);

        if (existingVote != null)
        {
            // Update vote
            existingVote.IsHelpful = helpful;
        }
        else
        {
            // Create new vote
            var vote = new ReviewHelpfulness
            {
                ReviewId = reviewId,
                UserId = userId!,
                IsHelpful = helpful
            };
            _context.ReviewHelpfulness.Add(vote);
        }

        // Update review counts
        var review = await _context.Reviews.FindAsync(reviewId);
        if (review != null)
        {
            review.HelpfulCount = await _context.ReviewHelpfulness
                .CountAsync(v => v.ReviewId == reviewId && v.IsHelpful);
            review.NotHelpfulCount = await _context.ReviewHelpfulness
                .CountAsync(v => v.ReviewId == reviewId && !v.IsHelpful);
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, helpfulCount = review?.HelpfulCount, notHelpfulCount = review?.NotHelpfulCount });
    }

    #region Spam Detection Helpers

    /// <summary>
    /// Detect spam in review content
    /// </summary>
    private (bool IsSpam, string Reason) DetectSpam(string comment, string? title)
    {
        var content = (comment + " " + (title ?? "")).ToLower();

        // Check for spam keywords
        var spamKeywords = new[]
        {
            "buy now", "click here", "limited time", "act now", "call now",
            "اشتري الآن", "اضغط هنا", "عرض محدود", "تواصل معي", "رقم هاتف",
            "واتساب", "telegram", "instagram", "facebook.com", "اشترك",
            "free money", "make money", "get rich", "weight loss", "viagra"
        };

        foreach (var keyword in spamKeywords)
        {
            if (content.Contains(keyword))
            {
                return (true, "يحتوي التعليق على محتوى غير مسموح");
            }
        }

        // Check for excessive URLs
        var urlPattern = @"(http|https|www\.|\.com|\.net|\.org)";
        var urlMatches = Regex.Matches(content, urlPattern, RegexOptions.IgnoreCase);
        if (urlMatches.Count > 1)
        {
            return (true, "يحتوي التعليق على روابط متعددة");
        }

        // Check for phone numbers
        var phonePattern = @"(\+?\d{10,}|0\d{10}|\d{3}[-\s]?\d{3}[-\s]?\d{4})";
        if (Regex.IsMatch(content, phonePattern))
        {
            return (true, "يحتوي التعليق على أرقام هواتف");
        }

        // Check for email addresses
        var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
        if (Regex.IsMatch(content, emailPattern))
        {
            return (true, "يحتوي التعليق على عنوان بريد إلكتروني");
        }

        // Check for repeated characters (like "aaaaa" or "!!!!!")
        var repeatedCharsPattern = @"(.)\1{5,}";
        if (Regex.IsMatch(comment, repeatedCharsPattern))
        {
            return (true, "يحتوي التعليق على أحرف مكررة بشكل مفرط");
        }

        // Check for all caps (shouting)
        if (comment.Length > 20 && comment == comment.ToUpper() && Regex.IsMatch(comment, @"[A-Z]{20,}"))
        {
            return (true, "يرجى عدم استخدام الأحرف الكبيرة فقط");
        }

        // Check for excessive special characters
        var specialCharsCount = comment.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        if (specialCharsCount > comment.Length * 0.3)
        {
            return (true, "يحتوي التعليق على رموز خاصة بشكل مفرط");
        }

        return (false, string.Empty);
    }

    #endregion
}

