using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الدورات - Courses Management Controller
/// </summary>
public class CoursesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CoursesController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;

    public CoursesController(
        ApplicationDbContext context, 
        ILogger<CoursesController> logger,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الدورات - Courses list
    /// </summary>
    public async Task<IActionResult> Index(string? searchTerm, CourseStatus? status, int page = 1)
    {
        var query = _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(c => c.Title.Contains(searchTerm));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("courses", 20);
        var totalCount = await query.CountAsync();
        var courses = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(courses);
    }

    /// <summary>
    /// تفاصيل الدورة - Course details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Instructor)
                .ThenInclude(i => i.InstructorProfile)
            .Include(c => c.Category)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
            return NotFound();

        return View(course);
    }

    /// <summary>
    /// صفحة الموافقة على الدورة (GET) - Redirect to details page
    /// If someone accesses /Admin/Courses/Approve/27 directly via URL, redirect them to the details page
    /// </summary>
    [HttpGet]
    public IActionResult Approve(int id)
    {
        _logger.LogWarning("GET request to Approve action for course {CourseId}. Redirecting to Details.", id);
        SetWarningMessage("يرجى استخدام زر الموافقة من صفحة تفاصيل الدورة");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// الموافقة على الدورة - Approve course (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Approve")]
    public async Task<IActionResult> ApprovePost(int id)
    {
        _logger.LogInformation("Starting approval process for course {CourseId}", id);
        
        // Step 1: Load course
        Course? course;
        try
        {
            course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for approval: {CourseId}", id);
                SetErrorMessage("الدورة غير موجودة");
                return RedirectToAction(nameof(Index));
            }
            
            _logger.LogInformation("Course {CourseId} loaded successfully. Title: {Title}, Status: {Status}", 
                id, course.Title, course.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course {CourseId} from database", id);
            SetErrorMessage("حدث خطأ أثناء تحميل بيانات الدورة");
            return RedirectToAction(nameof(Index));
        }

        // Step 2: Validate business rules
        try
        {
            var moduleCount = course.Modules?.Count ?? 0;
            var lessonCount = course.Modules?.Sum(m => m.Lessons?.Count ?? 0) ?? 0;
            var hasThumbnail = !string.IsNullOrEmpty(course.ThumbnailUrl);
            var hasPrice = course.Price > 0;

            _logger.LogInformation("Course {CourseId} validation: Modules={Modules}, Lessons={Lessons}, HasThumbnail={HasThumbnail}, Price={Price}, IsFree={IsFree}", 
                id, moduleCount, lessonCount, hasThumbnail, course.Price, course.IsFree);

            var (canPublish, reason) = BusinessRuleHelper.CanPublishCourse(
                moduleCount, 
                lessonCount, 
                hasThumbnail, 
                hasPrice, 
                course.IsFree);

            if (!canPublish)
            {
                _logger.LogWarning("Course {CourseId} cannot be published: {Reason}", id, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check current status
            if (course.Status != CourseStatus.PendingReview)
            {
                _logger.LogWarning("Course {CourseId} is not pending review. Current status: {Status}", id, course.Status);
                SetWarningMessage($"الدورة ليست في حالة انتظار المراجعة. الحالة الحالية: {course.Status}");
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء التحقق من صحة بيانات الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Step 3: Update course status (WITHOUT transaction - simpler and more reliable)
        try
        {
            course.Status = CourseStatus.Published;
            course.PublishedAt = DateTime.UtcNow;
            course.ApprovedBy = _currentUserService.UserId;

            _logger.LogInformation("Saving course {CourseId} with new status Published, ApprovedBy={ApprovedBy}", 
                id, course.ApprovedBy);
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Course {CourseId} saved successfully to database", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error while saving course {CourseId}. Exception: {Message}", id, ex.Message);
            
            // Log inner exception if exists
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner exception while saving course {CourseId}", id);
            }
            
            SetErrorMessage($"حدث خطأ في قاعدة البيانات أثناء حفظ الدورة: {ex.Message}");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Step 4: Send email notification (separate from main flow - failure here doesn't affect approval)
        try
        {
            if (!string.IsNullOrEmpty(course.Instructor?.Email))
            {
                _logger.LogInformation("Sending approval email to instructor {Email} for course {CourseId}", 
                    course.Instructor.Email, id);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            course.Instructor.Email,
                            "تم نشر دورتك بنجاح",
                            $@"<html><body dir='rtl'>
                                <h2>مبروك! تم نشر دورتك</h2>
                                <p>تم الموافقة على دورة <strong>{course.Title}</strong> ونشرها على المنصة.</p>
                                <p>يمكن للطلاب الآن التسجيل في الدورة.</p>
                                <br/>
                                <p>فريق منصة LMS</p>
                            </body></html>",
                            true);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Background email task failed for course {CourseId}", id);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // Email errors should never affect the approval result
            _logger.LogWarning(ex, "Non-critical error in email notification setup for course {CourseId}", id);
        }

        _logger.LogInformation("Course {CourseId} approved and published successfully by admin {AdminId}", 
            id, _currentUserService.UserId);

        SetSuccessMessage("تم الموافقة على الدورة ونشرها بنجاح!");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// رفض الدورة - Reject course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetErrorMessage("يجب إدخال سبب الرفض");
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for rejection: {CourseId}", id);
                return NotFound();
            }

            // Check current status
            if (course.Status != CourseStatus.PendingReview)
            {
                _logger.LogWarning("Course {CourseId} is not pending review. Current status: {Status}", id, course.Status);
                SetWarningMessage("الدورة ليست في حالة انتظار المراجعة");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Update course status
                course.Status = CourseStatus.Rejected;
                course.RejectionReason = reason;
                course.RejectedBy = _currentUserService.UserId;
                course.RejectedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notification email to instructor
                if (course.Instructor?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        course.Instructor.Email,
                        "تم رفض دورتك",
                        $@"<html><body dir='rtl'>
                            <h2>تم رفض دورتك</h2>
                            <p>للأسف، تم رفض دورة <strong>{course.Title}</strong>.</p>
                            <p><strong>السبب:</strong> {reason}</p>
                            <p>يرجى مراجعة المحتوى وإجراء التعديلات اللازمة ثم إعادة إرسالها للمراجعة.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Course {CourseId} rejected by admin {AdminId}. Reason: {Reason}", 
                id, _currentUserService.UserId, reason);

            SetSuccessMessage("تم رفض الدورة وإرسال إشعار للمدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء رفض الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تعليق الدورة - Suspend course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? reason)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments.Where(e => e.Status == EnrollmentStatus.Active))
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for suspension: {CourseId}", id);
                return NotFound();
            }

            // Check if course is already suspended
            if (course.Status == CourseStatus.Suspended)
            {
                SetWarningMessage("الدورة معلقة بالفعل");
                return RedirectToAction(nameof(Details), new { id });
            }

            var enrolledStudentEmails = course.Enrollments
                .Where(e => !string.IsNullOrEmpty(e.Student?.Email))
                .Select(e => e.Student!.Email!)
                .Distinct()
                .ToList();

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Store previous status for potential restoration
                course.PreviousStatus = course.Status;
                course.Status = CourseStatus.Suspended;
                course.SuspendedAt = DateTime.UtcNow;
                course.SuspendedBy = _currentUserService.UserId;
                course.SuspensionReason = reason;

                await _context.SaveChangesAsync();

                // Notify instructor
                if (course.Instructor?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        course.Instructor.Email,
                        "تم تعليق دورتك",
                        $@"<html><body dir='rtl'>
                            <h2>تم تعليق دورتك</h2>
                            <p>تم تعليق دورة <strong>{course.Title}</strong> مؤقتاً.</p>
                            {(!string.IsNullOrEmpty(reason) ? $"<p><strong>السبب:</strong> {reason}</p>" : "")}
                            <p>لن يتمكن الطلاب الجدد من التسجيل حتى يتم إعادة تفعيل الدورة.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }

                // Notify enrolled students
                if (enrolledStudentEmails.Any())
                {
                    await _emailService.SendEmailAsync(
                        enrolledStudentEmails,
                        $"تحديث بخصوص دورة {course.Title}",
                        $@"<html><body dir='rtl'>
                            <h2>تحديث مهم</h2>
                            <p>تم تعليق دورة <strong>{course.Title}</strong> مؤقتاً.</p>
                            <p>يمكنك الاستمرار في الوصول إلى المحتوى الذي تم الاشتراك فيه.</p>
                            <p>سيتم إعلامك عند إعادة تفعيل الدورة.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Course {CourseId} suspended by admin {AdminId}. Active enrollments: {Count}", 
                id, _currentUserService.UserId, course.Enrollments.Count);

            SetSuccessMessage($"تم تعليق الدورة. تم إرسال إشعارات إلى المدرس و {enrolledStudentEmails.Count} طالب.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تعليق الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إعادة تفعيل الدورة - Reactivate suspended course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for reactivation: {CourseId}", id);
                return NotFound();
            }

            if (course.Status != CourseStatus.Suspended)
            {
                SetWarningMessage("الدورة ليست معلقة");
                return RedirectToAction(nameof(Details), new { id });
            }

            // Restore to previous status or default to Published
            course.Status = course.PreviousStatus ?? CourseStatus.Published;
            course.SuspendedAt = null;
            course.SuspendedBy = null;
            course.SuspensionReason = null;
            course.PreviousStatus = null;

            await _context.SaveChangesAsync();

            // Notify instructor
            if (course.Instructor?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    course.Instructor.Email,
                    "تم إعادة تفعيل دورتك",
                    $@"<html><body dir='rtl'>
                        <h2>تم إعادة تفعيل دورتك</h2>
                        <p>تم إعادة تفعيل دورة <strong>{course.Title}</strong>.</p>
                        <p>يمكن للطلاب الآن التسجيل في الدورة مرة أخرى.</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>",
                    true);
            }

            _logger.LogInformation("Course {CourseId} reactivated by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage("تم إعادة تفعيل الدورة بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء إعادة تفعيل الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// الدورات المعلقة للمراجعة - Pending courses for review
    /// </summary>
    public async Task<IActionResult> Pending(int page = 1)
    {
        var query = _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .Where(c => c.Status == CourseStatus.PendingReview);

        var pageSize = await _configService.GetPaginationSizeAsync("courses_pending", 20);
        var totalPending = await query.CountAsync();
        var courses = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPending = totalPending;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalPending / pageSize);
        ViewBag.PageSize = pageSize;

        return View(courses);
    }

    /// <summary>
    /// الدورات المميزة - Featured courses
    /// </summary>
    public async Task<IActionResult> Featured(int page = 1)
    {
        var query = _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .Where(c => c.IsFeatured && c.Status == CourseStatus.Published);

        var pageSize = await _configService.GetPaginationSizeAsync("courses_featured", 20);
        var totalFeatured = await query.CountAsync();
        var courses = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalFeatured = totalFeatured;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalFeatured / pageSize);
        ViewBag.PageSize = pageSize;

        return View(courses);
    }

    /// <summary>
    /// تبديل حالة الدورة المميزة - Toggle featured status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeatured(int id)
    {
        try
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            course.IsFeatured = !course.IsFeatured;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Course {CourseId} featured status changed to {IsFeatured}", id, course.IsFeatured);
            SetSuccessMessage(course.IsFeatured ? "تم تمييز الدورة بنجاح" : "تم إلغاء تمييز الدورة");

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling featured status for course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تعديل الدورة - Edit course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Category)
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for editing: {CourseId}", id);
                return NotFound();
            }

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Course = course;

            return View(course);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course edit form {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل نموذج التعديل");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حفظ تعديلات الدورة - Save course edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseEditViewModel model)
    {
        try
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var course = await _context.Courses
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for editing: {CourseId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                course.Title = model.Title;
                course.Subtitle = model.Subtitle;
                course.Description = model.Description;
                course.ShortDescription = model.ShortDescription;
                course.CategoryId = model.CategoryId;
                course.Level = model.Level;
                course.Language = model.Language;
                course.Price = model.Price;
                course.DiscountPrice = model.DiscountPrice;
                course.IsFree = model.IsFree;
                course.Status = model.Status;
                course.UpdatedAt = DateTime.UtcNow;
                // Note: Requirements and WhatYouWillLearn are collections, handled separately

                await _context.SaveChangesAsync();

                _logger.LogInformation("Course {CourseId} updated successfully", id);
                SetSuccessMessage("تم تحديث الدورة بنجاح");
                return RedirectToAction(nameof(Details), new { id });
            }

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Course = course;

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// طلاب الدورة - Course Students
    /// </summary>
    public async Task<IActionResult> Students(int id, int page = 1)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found: {CourseId}", id);
                return NotFound();
            }

            var query = _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.CourseId == id);

            var pageSize = await _configService.GetPaginationSizeAsync("course_students", 20);
            var totalStudents = await query.CountAsync();
            var enrollments = await query
                .OrderByDescending(e => e.EnrolledAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Course = course;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalStudents / pageSize);
            ViewBag.TotalStudents = totalStudents;
            ViewBag.PageSize = pageSize;

            return View(enrollments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course students {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل طلاب الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// صفحة تأكيد نسخ الدورة - Duplicate course confirmation page (GET)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Duplicate(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Modules)
                .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null)
        {
            _logger.LogWarning("Course not found for duplication: {CourseId}", id);
            return NotFound();
        }

        ViewBag.Course = course;
        ViewBag.ModulesCount = course.Modules.Count;
        ViewBag.LessonsCount = course.Modules.Sum(m => m.Lessons.Count);

        return View(course);
    }

    /// <summary>
    /// تنفيذ نسخ الدورة - Execute duplicate course (POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("DuplicateConfirm")]
    public async Task<IActionResult> DuplicatePost(int id)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Resources)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Assignments)
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Lessons)
                        .ThenInclude(l => l.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for duplication: {CourseId}", id);
                return NotFound();
            }

            Course? duplicateCourse = null;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Create duplicate course
                duplicateCourse = new Course
                {
                    Title = $"{course.Title} (نسخة)",
                    Description = course.Description,
                    ShortDescription = course.ShortDescription,
                    CategoryId = course.CategoryId,
                    InstructorId = course.InstructorId,
                    Level = course.Level,
                    Language = course.Language,
                    Price = course.Price,
                    IsFree = course.IsFree,
                    Requirements = course.Requirements,
                    WhatYouWillLearn = course.WhatYouWillLearn,
                    Status = CourseStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Courses.Add(duplicateCourse);
                await _context.SaveChangesAsync();

                // Duplicate modules and lessons
                foreach (var module in course.Modules.OrderBy(m => m.OrderIndex))
                {
                    var duplicateModule = new Module
                    {
                        CourseId = duplicateCourse.Id,
                        Title = module.Title,
                        Description = module.Description,
                        OrderIndex = module.OrderIndex,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Modules.Add(duplicateModule);
                    await _context.SaveChangesAsync();

                    foreach (var lesson in module.Lessons.OrderBy(l => l.OrderIndex))
                    {
                        var duplicateLesson = new Lesson
                        {
                            ModuleId = duplicateModule.Id,
                            Title = lesson.Title,
                            Description = lesson.Description,
                            Content = lesson.Content,
                            VideoUrl = lesson.VideoUrl,
                            Duration = lesson.Duration,
                            OrderIndex = lesson.OrderIndex,
                            IsFree = lesson.IsFree,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Lessons.Add(duplicateLesson);
                    }
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Course {CourseId} duplicated to {NewCourseId}", id, duplicateCourse!.Id);
            SetSuccessMessage("تم نسخ الدورة بنجاح");
            return RedirectToAction(nameof(Edit), new { id = duplicateCourse.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء نسخ الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إنشاء دورة جديدة - Create new course
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        try
        {
            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var instructors = await _context.InstructorProfiles
                .Include(p => p.User)
                .Where(p => p.IsApproved && !p.IsSuspended)
                .OrderBy(p => p.User.FirstName)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Instructors = instructors;

            return View(new CourseCreateViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course create form");
            SetErrorMessage("حدث خطأ أثناء تحميل نموذج الإنشاء");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الدورة الجديدة - Save new course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCreateViewModel model)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var course = new Course
                {
                    Title = model.Title,
                    Description = model.Description,
                    ShortDescription = model.ShortDescription,
                    CategoryId = model.CategoryId,
                    InstructorId = model.InstructorId,
                    Level = model.Level,
                    Language = model.Language,
                    Price = model.Price,
                    IsFree = model.IsFree,
                    Status = CourseStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Course {CourseId} created successfully by admin", course.Id);
                SetSuccessMessage("تم إنشاء الدورة بنجاح");
                return RedirectToAction(nameof(Edit), new { id = course.Id });
            }

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var instructors = await _context.InstructorProfiles
                .Include(p => p.User)
                .Where(p => p.IsApproved && !p.IsSuspended)
                .OrderBy(p => p.User.FirstName)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Instructors = instructors;

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating course");
            SetErrorMessage("حدث خطأ أثناء إنشاء الدورة");

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var instructors = await _context.InstructorProfiles
                .Include(p => p.User)
                .Where(p => p.IsApproved && !p.IsSuspended)
                .OrderBy(p => p.User.FirstName)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Instructors = instructors;

            return View(model);
        }
    }

    /// <summary>
    /// مراجعات الدورة - Course reviews
    /// </summary>
    public async Task<IActionResult> Reviews(int id, int page = 1)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for reviews: {CourseId}", id);
                return NotFound();
            }

            var query = _context.Reviews
                .Include(r => r.Student)
                .Where(r => r.CourseId == id);

            var pageSize = await _configService.GetPaginationSizeAsync("course_reviews", 20);
            var totalReviews = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Statistics
            var approvedReviews = await _context.Reviews.CountAsync(r => r.CourseId == id && r.IsApproved);
            var pendingReviews = await _context.Reviews.CountAsync(r => r.CourseId == id && !r.IsApproved && !r.IsRejected);
            var averageRating = totalReviews > 0 
                ? await _context.Reviews.Where(r => r.CourseId == id && r.IsApproved).AverageAsync(r => (decimal?)r.Rating) ?? 0
                : 0;

            ViewBag.Course = course;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalReviews / pageSize);
            ViewBag.TotalReviews = totalReviews;
            ViewBag.ApprovedReviews = approvedReviews;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.AverageRating = averageRating;
            ViewBag.PageSize = pageSize;

            return View(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course reviews {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل مراجعات الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حذف الدورة - Delete course
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Modules)
                .Include(c => c.Reviews)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
            {
                _logger.LogWarning("Course not found for deletion: {CourseId}", id);
                return NotFound();
            }

            // Validate if course can be deleted
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteCourse(
                course.Enrollments.Count, 
                course.Status);

            if (!canDelete)
            {
                _logger.LogWarning("Course {CourseId} cannot be deleted: {Reason}", id, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Soft delete (if ISoftDelete is implemented) or hard delete
                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Course {CourseId} deleted by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage("تم حذف الدورة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course {CourseId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الدورة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

/// <summary>
/// نموذج تعديل الدورة - Course Edit ViewModel
/// </summary>
public class CourseEditViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public int CategoryId { get; set; }
    public CourseLevel Level { get; set; }
    public string Language { get; set; } = "ar";
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public bool IsFree { get; set; }
    public CourseStatus Status { get; set; }
    // Note: Requirements and WhatYouWillLearn are collections in the entity, handled separately
}

/// <summary>
/// نموذج إنشاء دورة - Course Create ViewModel
/// </summary>
public class CourseCreateViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public int CategoryId { get; set; }
    public string InstructorId { get; set; } = string.Empty;
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;
    public string Language { get; set; } = "ar";
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    // Note: Requirements and WhatYouWillLearn are collections in the entity, can be added after course creation
}

