using LMS.Data;
using LMS.Domain.Entities.Users;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المدرسين - Instructors Management Controller
/// </summary>
public class InstructorsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InstructorsController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemConfigurationService _configService;
    private readonly IMemoryCache _cache;

    public InstructorsController(
        ApplicationDbContext context,
        ILogger<InstructorsController> logger,
        IEmailService emailService,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ISystemConfigurationService configService,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _configService = configService;
        _cache = cache;
    }

    /// <summary>
    /// قائمة المدرسين - Instructors list
    /// </summary>
    public async Task<IActionResult> Index(bool? approved, int page = 1)
    {
        var query = _context.InstructorProfiles
            .Include(p => p.User)
            .AsQueryable();

        if (approved.HasValue)
        {
            query = query.Where(p => p.IsApproved == approved.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("instructors", 20);
        var totalInstructors = await query.CountAsync();
        var instructors = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Approved = approved;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalInstructors / pageSize);
        ViewBag.TotalItems = totalInstructors;
        ViewBag.PageSize = pageSize;

        return View(instructors);
    }

    /// <summary>
    /// طلبات الانضمام كمدرس - Instructor applications
    /// </summary>
    public async Task<IActionResult> Applications(InstructorApplicationStatus? status, int page = 1)
    {
        var query = _context.InstructorApplications
            .Include(a => a.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("instructor_applications", 20);
        var totalApplications = await query.CountAsync();
        var applications = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalApplications / pageSize);
        ViewBag.TotalItems = totalApplications;
        ViewBag.PageSize = pageSize;

        return View(applications);
    }

    /// <summary>
    /// تفاصيل طلب الانضمام - Application details
    /// </summary>
    public async Task<IActionResult> ApplicationDetails(int id)
    {
        var application = await _context.InstructorApplications
            .Include(a => a.User)
                .ThenInclude(u => u.InstructorProfile)
            .Include(a => a.Documents)
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null)
            return NotFound();

        return View(application);
    }

    /// <summary>
    /// وضع علامة قيد المراجعة - Mark application as under review
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnderReview(int id)
    {
        try
        {
            var application = await _context.InstructorApplications
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                _logger.LogWarning("Instructor application not found: {ApplicationId}", id);
                return NotFound();
            }

            if (application.Status != InstructorApplicationStatus.Pending)
            {
                SetWarningMessage("الطلب ليس في حالة الانتظار");
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            application.Status = InstructorApplicationStatus.UnderReview;
            application.ReviewedById = _currentUserService.UserId;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Application {ApplicationId} marked as under review by {ReviewerId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage("تم وضع علامة قيد المراجعة على الطلب");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking application {ApplicationId} as under review", id);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة الطلب");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
    }

    /// <summary>
    /// طلب معلومات إضافية - Request more information from applicant
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMoreInfo(int id, string requestedInfo)
    {
        if (string.IsNullOrWhiteSpace(requestedInfo))
        {
            SetErrorMessage("يجب إدخال المعلومات المطلوبة");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        try
        {
            var application = await _context.InstructorApplications
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                _logger.LogWarning("Instructor application not found: {ApplicationId}", id);
                return NotFound();
            }

            if (application.Status != InstructorApplicationStatus.Pending && 
                application.Status != InstructorApplicationStatus.UnderReview)
            {
                SetWarningMessage("لا يمكن طلب معلومات إضافية لهذا الطلب");
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            application.Status = InstructorApplicationStatus.MoreInfoRequired;
            application.ReviewNotes = requestedInfo;
            application.ReviewedById = _currentUserService.UserId;
            application.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send email notification to applicant
            if (application.User?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    application.User.Email,
                    "مطلوب معلومات إضافية لطلب الانضمام كمدرس",
                    $@"<html><body dir='rtl'>
                        <h2>عزيزي/عزيزتي {application.User.FirstName}</h2>
                        <p>شكراً لتقديمك طلب الانضمام كمدرس على منصة LMS.</p>
                        <p>نحتاج منك معلومات إضافية لاستكمال مراجعة طلبك:</p>
                        <div style='background: #f5f5f5; padding: 15px; border-radius: 8px; margin: 15px 0;'>
                            <p><strong>المعلومات المطلوبة:</strong></p>
                            <p>{requestedInfo}</p>
                        </div>
                        <p>يرجى تحديث طلبك أو الرد على هذا البريد بالمعلومات المطلوبة.</p>
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>",
                    true);
            }

            _logger.LogInformation("More info requested for application {ApplicationId}. Request: {RequestedInfo}", 
                id, requestedInfo);

            SetSuccessMessage("تم إرسال طلب المعلومات الإضافية للمتقدم");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting more info for application {ApplicationId}", id);
            SetErrorMessage("حدث خطأ أثناء إرسال طلب المعلومات");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
    }

    /// <summary>
    /// الموافقة على طلب الانضمام - Approve application
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveApplication(int id, decimal? customCommissionRate)
    {
        try
        {
            var application = await _context.InstructorApplications
                .Include(a => a.User)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                _logger.LogWarning("Instructor application not found: {ApplicationId}", id);
                return NotFound();
            }

            // Allow approval from Pending, UnderReview, or MoreInfoRequired statuses
            if (application.Status != InstructorApplicationStatus.Pending &&
                application.Status != InstructorApplicationStatus.UnderReview &&
                application.Status != InstructorApplicationStatus.MoreInfoRequired)
            {
                _logger.LogWarning("Application {ApplicationId} cannot be approved. Status: {Status}", 
                    id, application.Status);
                SetWarningMessage("لا يمكن الموافقة على هذا الطلب في حالته الحالية");
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            // Validate commission rate
            var commissionRate = customCommissionRate ?? 70.00m;
            if (commissionRate < BusinessRuleHelper.MinimumCommissionRate || 
                commissionRate > BusinessRuleHelper.MaximumCommissionRate)
            {
                ModelState.AddModelError("", $"معدل العمولة يجب أن يكون بين {BusinessRuleHelper.MinimumCommissionRate}% و {BusinessRuleHelper.MaximumCommissionRate}%");
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Update application status
                application.Status = InstructorApplicationStatus.Approved;
                application.ReviewedAt = DateTime.UtcNow;
                application.ReviewedById = _currentUserService.UserId;

                // Create or update instructor profile from application (profile may exist if created by dashboard)
                var instructorProfile = await _context.InstructorProfiles
                    .FirstOrDefaultAsync(ip => ip.UserId == application.UserId);

                if (instructorProfile != null)
                {
                    instructorProfile.Bio = application.Bio;
                    instructorProfile.Expertise = application.Specialization;
                    instructorProfile.Specializations = application.Specialization;
                    instructorProfile.YearsOfExperience = application.YearsOfExperience;
                    instructorProfile.IsApproved = true;
                    instructorProfile.ApprovedAt = DateTime.UtcNow;
                    instructorProfile.ApprovedBy = _currentUserService.UserId;
                    instructorProfile.CommissionRate = commissionRate;
                    instructorProfile.MinimumWithdrawal = BusinessRuleHelper.MinimumWithdrawalAmount;
                    instructorProfile.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    instructorProfile = new InstructorProfile
                    {
                        UserId = application.UserId,
                        Bio = application.Bio,
                        Expertise = application.Specialization,
                        Specializations = application.Specialization,
                        YearsOfExperience = application.YearsOfExperience,
                        IsApproved = true,
                        ApprovedAt = DateTime.UtcNow,
                        ApprovedBy = _currentUserService.UserId,
                        CommissionRate = commissionRate,
                        MinimumWithdrawal = BusinessRuleHelper.MinimumWithdrawalAmount,
                        TotalEarnings = 0,
                        AvailableBalance = 0,
                        PendingBalance = 0,
                        TotalWithdrawn = 0
                    };
                    _context.InstructorProfiles.Add(instructorProfile);
                }

                // Add user to Instructor role
                var user = await _userManager.FindByIdAsync(application.UserId);
                if (user != null)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, "Instructor");
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Failed to add Instructor role to user {UserId}", application.UserId);
                        throw new InvalidOperationException("فشل إضافة دور المدرس");
                    }
                }

                await _context.SaveChangesAsync();

                // Send approval email
                if (application.User?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        application.User.Email,
                        "مبروك! تم قبولك كمدرس",
                        $@"<html><body dir='rtl'>
                            <h2>مبروك {application.User.FirstName}!</h2>
                            <p>تم الموافقة على طلبك للانضمام كمدرس على منصة LMS.</p>
                            <p><strong>معدل العمولة:</strong> {commissionRate}%</p>
                            <p>يمكنك الآن البدء في إنشاء الدورات ونشرها على المنصة.</p>
                            <p>نتطلع إلى دوراتك المميزة!</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Instructor application {ApplicationId} approved for user {UserId} with commission {Commission}%", 
                id, application.UserId, commissionRate);

            SetSuccessMessage($"تم الموافقة على طلب المدرس بمعدل عمولة {commissionRate}%. تم إرسال إشعار بالبريد الإلكتروني.");
            return RedirectToAction(nameof(Applications));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving instructor application {ApplicationId}", id);
            SetErrorMessage("حدث خطأ أثناء الموافقة على الطلب");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
    }

    /// <summary>
    /// رفض طلب الانضمام - Reject application
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectApplication(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetErrorMessage("يجب إدخال سبب الرفض");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        try
        {
            var application = await _context.InstructorApplications
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                _logger.LogWarning("Instructor application not found: {ApplicationId}", id);
                return NotFound();
            }

            // Allow rejection from Pending, UnderReview, or MoreInfoRequired statuses
            if (application.Status != InstructorApplicationStatus.Pending &&
                application.Status != InstructorApplicationStatus.UnderReview &&
                application.Status != InstructorApplicationStatus.MoreInfoRequired)
            {
                _logger.LogWarning("Application {ApplicationId} cannot be rejected. Status: {Status}", 
                    id, application.Status);
                SetWarningMessage("لا يمكن رفض هذا الطلب في حالته الحالية");
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                application.Status = InstructorApplicationStatus.Rejected;
                application.ReviewedAt = DateTime.UtcNow;
                application.ReviewedById = _currentUserService.UserId;
                application.RejectionReason = reason;

                await _context.SaveChangesAsync();

                // Send rejection email
                if (application.User?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        application.User.Email,
                        "بخصوص طلب الانضمام كمدرس",
                        $@"<html><body dir='rtl'>
                            <h2>عزيزي/عزيزتي {application.User.FirstName}</h2>
                            <p>نشكرك على اهتمامك بالانضمام كمدرس على منصة LMS.</p>
                            <p>للأسف، لم نتمكن من الموافقة على طلبك في الوقت الحالي.</p>
                            <p><strong>السبب:</strong> {reason}</p>
                            <p>يمكنك تحسين ملفك الشخصي وإعادة التقديم مرة أخرى.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Instructor application {ApplicationId} rejected. Reason: {Reason}", 
                id, reason);

            SetSuccessMessage("تم رفض طلب المدرس وإرسال إشعار");
            return RedirectToAction(nameof(Applications));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting instructor application {ApplicationId}", id);
            SetErrorMessage("حدث خطأ أثناء رفض الطلب");
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }
    }

    /// <summary>
    /// تفاصيل المدرس - Instructor details (by User ID)
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                    .ThenInclude(u => u.CoursesCreated)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor profile not found for user: {UserId}", id);
                return NotFound();
            }

            // Load additional statistics
            var totalStudents = await _context.Enrollments
                .Where(e => e.Course.InstructorId == instructor.UserId)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();

            var completedCourses = await _context.Enrollments
                .Where(e => e.Course.InstructorId == instructor.UserId && 
                           e.Status == EnrollmentStatus.Completed)
                .CountAsync();

            ViewBag.TotalStudents = totalStudents;
            ViewBag.CompletedEnrollments = completedCourses;

            return View(instructor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor details {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل المدرس");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تعديل معدل العمولة - Update commission rate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCommission(int id, decimal commissionRate, string? reason)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor profile not found: {InstructorId}", id);
                return NotFound();
            }

            // Store userId for redirects (Details action expects UserId)
            var userId = instructor.UserId;

            // Validate commission rate
            if (commissionRate < BusinessRuleHelper.MinimumCommissionRate || 
                commissionRate > BusinessRuleHelper.MaximumCommissionRate)
            {
                SetErrorMessage($"معدل العمولة يجب أن يكون بين {BusinessRuleHelper.MinimumCommissionRate}% و {BusinessRuleHelper.MaximumCommissionRate}%");
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            var oldRate = instructor.CommissionRate;
            instructor.CommissionRate = commissionRate;

            await _context.SaveChangesAsync();

            // Send notification email
            if (instructor.User?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    instructor.User.Email,
                    "تحديث معدل العمولة",
                    $@"<html><body dir='rtl'>
                        <h2>عزيزي/عزيزتي {instructor.User.FirstName}</h2>
                        <p>تم تحديث معدل العمولة الخاص بك على المنصة.</p>
                        <p><strong>المعدل السابق:</strong> {oldRate}%</p>
                        <p><strong>المعدل الجديد:</strong> {commissionRate}%</p>
                        {(!string.IsNullOrEmpty(reason) ? $"<p><strong>السبب:</strong> {reason}</p>" : "")}
                        <br/>
                        <p>فريق منصة LMS</p>
                    </body></html>",
                    true);
            }

            _logger.LogInformation("Commission rate updated for instructor {InstructorId} from {OldRate}% to {NewRate}%", 
                id, oldRate, commissionRate);

            SetSuccessMessage($"تم تحديث معدل العمولة من {oldRate}% إلى {commissionRate}%");
            return RedirectToAction(nameof(Details), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating commission for instructor {InstructorId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث معدل العمولة");
            // Get userId for redirect if instructor was loaded
            var instructor = await _context.InstructorProfiles.FirstOrDefaultAsync(p => p.Id == id);
            var userId = instructor?.UserId ?? string.Empty;
            return RedirectToAction(nameof(Details), new { id = userId });
        }
    }

    /// <summary>
    /// تعليق حساب المدرس - Suspend instructor account
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string reason)
    {
        var instructor = await _context.InstructorProfiles
            .Include(p => p.User)
                .ThenInclude(u => u.CoursesCreated)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (instructor == null)
        {
            _logger.LogWarning("Instructor profile not found: {InstructorId}", id);
            return NotFound();
        }

        // Store userId for redirects (Details action expects UserId)
        var userId = instructor.UserId;

        if (string.IsNullOrWhiteSpace(reason))
        {
            SetErrorMessage("يجب إدخال سبب التعليق");
            return RedirectToAction(nameof(Details), new { id = userId });
        }

        try
        {
            var publishedCourses = instructor.User.CoursesCreated
                .Where(c => c.Status == CourseStatus.Published)
                .ToList();

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Suspend instructor
                instructor.IsApproved = false;
                instructor.IsSuspended = true;
                instructor.SuspendedAt = DateTime.UtcNow;
                instructor.SuspensionReason = reason;

                // Suspend all published courses
                foreach (var course in publishedCourses)
                {
                    course.PreviousStatus = course.Status;
                    course.Status = CourseStatus.Suspended;
                    course.SuspendedAt = DateTime.UtcNow;
                    course.SuspensionReason = $"تعليق حساب المدرس: {reason}";
                }

                await _context.SaveChangesAsync();

                // Send notification email
                if (instructor.User?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        instructor.User.Email,
                        "تعليق حساب المدرس",
                        $@"<html><body dir='rtl'>
                            <h2>إشعار مهم</h2>
                            <p>عزيزي/عزيزتي {instructor.User.FirstName},</p>
                            <p>تم تعليق حسابك كمدرس على منصة LMS.</p>
                            <p><strong>السبب:</strong> {reason}</p>
                            <p>تم تعليق جميع دوراتك ({publishedCourses.Count} دورة) مؤقتاً.</p>
                            <p>للاستفسار، يرجى التواصل مع الإدارة.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Instructor {InstructorId} suspended. {CoursesCount} courses affected", 
                id, publishedCourses.Count);

            SetSuccessMessage($"تم تعليق حساب المدرس و {publishedCourses.Count} دورة");
            return RedirectToAction(nameof(Details), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending instructor {InstructorId}", id);
            SetErrorMessage("حدث خطأ أثناء تعليق حساب المدرس");
            return RedirectToAction(nameof(Details), new { id = userId });
        }
    }

    /// <summary>
    /// إعادة تفعيل حساب المدرس - Reactivate instructor account
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                    .ThenInclude(u => u.CoursesCreated)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor profile not found: {InstructorId}", id);
                return NotFound();
            }

            // Store userId for redirects (Details action expects UserId)
            var userId = instructor.UserId;

            if (!instructor.IsSuspended)
            {
                SetWarningMessage("حساب المدرس ليس معلقاً");
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            var suspendedCourses = instructor.User.CoursesCreated
                .Where(c => c.Status == CourseStatus.Suspended && c.PreviousStatus.HasValue)
                .ToList();

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Reactivate instructor
                instructor.IsApproved = true;
                instructor.IsSuspended = false;
                instructor.SuspendedAt = null;
                instructor.SuspensionReason = null;

                // Restore courses
                foreach (var course in suspendedCourses)
                {
                    course.Status = course.PreviousStatus!.Value;
                    course.PreviousStatus = null;
                    course.SuspendedAt = null;
                    course.SuspensionReason = null;
                }

                await _context.SaveChangesAsync();

                // Send notification email
                if (instructor.User?.Email != null)
                {
                    await _emailService.SendEmailAsync(
                        instructor.User.Email,
                        "تم إعادة تفعيل حسابك",
                        $@"<html><body dir='rtl'>
                            <h2>مرحباً {instructor.User.FirstName}</h2>
                            <p>تم إعادة تفعيل حسابك كمدرس على منصة LMS.</p>
                            <p>تم استعادة {suspendedCourses.Count} دورة.</p>
                            <p>يمكنك الآن الاستمرار في إدارة دوراتك.</p>
                            <br/>
                            <p>فريق منصة LMS</p>
                        </body></html>",
                        true);
                }
            });

            _logger.LogInformation("Instructor {InstructorId} reactivated. {CoursesCount} courses restored", 
                id, suspendedCourses.Count);

            SetSuccessMessage($"تم إعادة تفعيل حساب المدرس و {suspendedCourses.Count} دورة");
            return RedirectToAction(nameof(Details), new { id = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating instructor {InstructorId}", id);
            SetErrorMessage("حدث خطأ أثناء إعادة تفعيل حساب المدرس");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// دورات المدرس - Instructor Courses
    /// </summary>
    public async Task<IActionResult> Courses(string id, int page = 1)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            var query = _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Modules)
                .Include(c => c.Enrollments)
                .Where(c => c.InstructorId == id);

            var totalCourses = await query.CountAsync();
            var courses = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Instructor = instructor;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCourses / 20);

            return View(courses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor courses {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل دورات المدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// أرباح المدرس - Instructor Earnings
    /// </summary>
    public async Task<IActionResult> Earnings(string id, DateTime? from, DateTime? to, int page = 1)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            from ??= DateTime.UtcNow.AddMonths(-3);
            to ??= DateTime.UtcNow;

            var query = _context.InstructorEarnings
                .Include(e => e.Course)
                .Include(e => e.Enrollment)
                    .ThenInclude(e => e.Student)
                .Where(e => e.InstructorId == id && 
                           e.EarnedAt >= from && 
                           e.EarnedAt <= to);

            var totalEarnings = await query.CountAsync();
            var earnings = await query
                .OrderByDescending(e => e.EarnedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            var totalAmount = await query.SumAsync(e => e.NetAmount);
            var totalGross = await query.SumAsync(e => e.GrossAmount);

            ViewBag.Instructor = instructor;
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalEarnings / 20);
            ViewBag.TotalNetAmount = totalAmount;
            ViewBag.TotalGrossAmount = totalGross;

            return View(earnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor earnings {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل أرباح المدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تقييمات المدرس - Instructor Reviews
    /// </summary>
    public async Task<IActionResult> Reviews(string id, int page = 1)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            var query = _context.Reviews
                .Include(r => r.Student)
                .Include(r => r.Course)
                .Where(r => r.Course.InstructorId == id);

            var totalReviews = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            var averageRating = totalReviews > 0 
                ? await query.AverageAsync(r => r.Rating) 
                : 0;

            ViewBag.Instructor = instructor;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalReviews / 20);
            ViewBag.AverageRating = averageRating;
            ViewBag.TotalReviews = totalReviews;

            return View(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor reviews {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل تقييمات المدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// طلاب المدرس - Instructor Students
    /// </summary>
    public async Task<IActionResult> Students(string id, int page = 1)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            var query = _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == id)
                .GroupBy(e => e.StudentId)
                .Select(g => new
                {
                    Student = g.FirstOrDefault()!.Student,
                    EnrollmentsCount = g.Count(),
                    CompletedCount = g.Count(e => e.Status == EnrollmentStatus.Completed),
                    TotalProgress = g.Average(e => (double)e.ProgressPercentage)
                });

            var totalStudents = await query.CountAsync();
            var students = await query
                .OrderByDescending(s => s.EnrollmentsCount)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Instructor = instructor;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalStudents / 20);
            ViewBag.TotalStudents = totalStudents;

            return View(students);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor students {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل طلاب المدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تعديل المدرس - Edit Instructor
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            var viewModel = new InstructorEditViewModel
            {
                UserId = instructor.UserId,
                Bio = instructor.Bio,
                Expertise = instructor.Expertise,
                YearsOfExperience = instructor.YearsOfExperience,
                CommissionRate = instructor.CommissionRate,
                IsApproved = instructor.IsApproved,
                IsSuspended = instructor.IsSuspended
            };

            ViewBag.Instructor = instructor;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor edit form {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل نموذج التعديل");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// حفظ تعديلات المدرس - Save Instructor Edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, InstructorEditViewModel model)
    {
        try
        {
            if (id != model.UserId)
            {
                return BadRequest();
            }

            var instructor = await _context.InstructorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (instructor == null)
            {
                _logger.LogWarning("Instructor not found: {UserId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Validate commission rate
                if (model.CommissionRate < BusinessRuleHelper.MinimumCommissionRate || 
                    model.CommissionRate > BusinessRuleHelper.MaximumCommissionRate)
                {
                    ModelState.AddModelError("CommissionRate", 
                        $"معدل العمولة يجب أن يكون بين {BusinessRuleHelper.MinimumCommissionRate}% و {BusinessRuleHelper.MaximumCommissionRate}%");
                    ViewBag.Instructor = instructor;
                    return View(model);
                }

                instructor.Bio = model.Bio;
                instructor.Expertise = model.Expertise;
                instructor.YearsOfExperience = model.YearsOfExperience;
                instructor.CommissionRate = model.CommissionRate;

                // Persist approval and suspension (match ApproveApplication / Suspend / Reactivate logic)
                if (instructor.IsApproved != model.IsApproved)
                {
                    instructor.IsApproved = model.IsApproved;
                    if (model.IsApproved)
                    {
                        instructor.ApprovedAt = DateTime.UtcNow;
                        instructor.ApprovedBy = _currentUserService.UserId;
                    }
                    else
                    {
                        instructor.ApprovedAt = null;
                        instructor.ApprovedBy = null;
                    }
                }
                if (instructor.IsSuspended != model.IsSuspended)
                {
                    instructor.IsSuspended = model.IsSuspended;
                    if (model.IsSuspended)
                    {
                        instructor.SuspendedAt = DateTime.UtcNow;
                        instructor.SuspensionReason ??= "تعليق من لوحة التحكم";
                    }
                    else
                    {
                        instructor.SuspendedAt = null;
                        instructor.SuspensionReason = null;
                    }
                }

                instructor.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Invalidate instructor profile cache so they see updated status immediately
                _cache.Remove($"instructor_profile_{id}");

                _logger.LogInformation("Instructor {UserId} updated successfully", id);
                SetSuccessMessage("تم تحديث معلومات المدرس بنجاح");
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Instructor = instructor;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating instructor {UserId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث المدرس");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// عدد طلبات الانضمام المعلقة - Get pending applications count (API)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingCount()
    {
        try
        {
            var count = await _context.InstructorApplications
                .CountAsync(a => a.Status == InstructorApplicationStatus.Pending);
            return Json(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending applications count");
            return Json(new { count = 0 });
        }
    }

    /// <summary>
    /// إحصائيات المدرسين - Instructor statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        int totalInstructors = 0, approvedInstructors = 0, suspendedInstructors = 0;
        int pendingApplications = 0, totalCourses = 0;
        decimal totalRevenue = 0m, totalInstructorEarnings = 0m, averageCommissionRate = 70m;
        int[] commissionDistribution = new[] { 0, 0, 0, 0, 0 };
        List<object> topEarners = new();

        try
        {
            totalInstructors = await _context.InstructorProfiles.CountAsync();
            approvedInstructors = await _context.InstructorProfiles.CountAsync(p => p.IsApproved);
            suspendedInstructors = await _context.InstructorProfiles.CountAsync(p => p.IsSuspended);
            pendingApplications = await _context.InstructorApplications
                .CountAsync(a => a.Status == InstructorApplicationStatus.Pending);
            totalCourses = await _context.Courses.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading instructor statistics counts, using defaults");
        }

        try
        {
            totalRevenue = await _context.InstructorEarnings
                .Select(e => (decimal?)e.GrossAmount)
                .SumAsync() ?? 0m;
            totalInstructorEarnings = await _context.InstructorEarnings
                .Select(e => (decimal?)e.NetAmount)
                .SumAsync() ?? 0m;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading instructor earnings for statistics, using zero");
        }

        try
        {
            if (totalInstructors > 0)
            {
                averageCommissionRate = await _context.InstructorProfiles
                    .Select(p => (decimal?)p.CommissionRate)
                    .AverageAsync() ?? 70m;
            }
            var profiles = await _context.InstructorProfiles
                .Select(p => p.CommissionRate)
                .ToListAsync();
            commissionDistribution = new[]
            {
                profiles.Count(r => r >= 50 && r < 60),
                profiles.Count(r => r >= 60 && r < 70),
                profiles.Count(r => r >= 70 && r < 80),
                profiles.Count(r => r >= 80 && r < 90),
                profiles.Count(r => r >= 90 && r <= 95)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading commission distribution for statistics");
        }

        try
        {
            var topEarnersQuery = await _context.InstructorProfiles
                .AsNoTracking()
                .Where(p => p.TotalEarnings > 0)
                .OrderByDescending(p => p.TotalEarnings)
                .Take(10)
                .Select(p => new { p.UserId, p.TotalEarnings, p.CommissionRate })
                .ToListAsync();

            var topEarnerUserIds = topEarnersQuery.Select(e => e.UserId).ToList();
            if (topEarnerUserIds.Count > 0)
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => topEarnerUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.LastName })
                    .ToDictionaryAsync(u => u.Id);

                var courseCounts = await _context.Courses
                    .Where(c => topEarnerUserIds.Contains(c.InstructorId))
                    .GroupBy(c => c.InstructorId)
                    .Select(g => new { InstructorId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.InstructorId, x => x.Count);

                topEarners = topEarnersQuery.Select(p => new
                {
                    Name = users.TryGetValue(p.UserId, out var user)
                        ? ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim()
                        : "مدرس",
                    p.TotalEarnings,
                    p.CommissionRate,
                    CoursesCount = courseCounts.TryGetValue(p.UserId, out var count) ? count : 0
                }).Cast<object>().ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading top earners for statistics, using empty list");
        }

        var stats = new
        {
            TotalInstructors = totalInstructors,
            ApprovedInstructors = approvedInstructors,
            SuspendedInstructors = suspendedInstructors,
            PendingApplications = pendingApplications,
            TotalCourses = totalCourses,
            TotalRevenue = totalRevenue,
            TotalInstructorEarnings = totalInstructorEarnings,
            AverageCommissionRate = averageCommissionRate,
            CommissionDistribution = commissionDistribution,
            TopEarners = topEarners
        };

        return View(stats);
    }
}

/// <summary>
/// نموذج تعديل المدرس - Instructor Edit ViewModel
/// </summary>
public class InstructorEditViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string? Expertise { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal CommissionRate { get; set; } = 70.00m;
    public bool IsApproved { get; set; }
    public bool IsSuspended { get; set; }
}

