using LMS.Data;
using LMS.Domain.Enums;
using LMS.Models;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// التقديم كمدرس - Instructor Application Controller for Students
/// </summary>
public class InstructorApplicationController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly ILogger<InstructorApplicationController> _logger;

    public InstructorApplicationController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPlatformSettingsService platformSettings,
        ILogger<InstructorApplicationController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
        _logger = logger;
    }

    /// <summary>
    /// عرض حالة الطلب - View application status
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Check if instructor application is enabled (with default fallback)
            bool enableInstructorApplication = true;
            try
            {
                enableInstructorApplication = await _platformSettings.GetBoolSettingAsync("EnableInstructorApplication", true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get EnableInstructorApplication setting, using default (true)");
            }

            if (!enableInstructorApplication)
            {
                SetInfoMessage("التقديم كمدرس غير متاح حالياً");
                return RedirectToAction("Index", "Profile");
            }

            var application = await _context.InstructorApplications
                .Include(a => a.Documents)
                .Include(a => a.ReviewedBy)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (application == null)
            {
                // No application yet, redirect to apply
                return RedirectToAction(nameof(Apply));
            }

            // Get support email from platform settings (with default fallback)
            string supportEmail = "support@lms.com";
            try
            {
                supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get SupportEmail setting, using default");
            }
            ViewBag.SupportEmail = supportEmail;

            return View(application);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor application for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة طلب الانضمام كمدرس");
            return RedirectToAction("Index", "Profile");
        }
    }

    /// <summary>
    /// التقديم كمدرس - Apply as instructor
    /// </summary>
    public async Task<IActionResult> Apply()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // Check if instructor application is enabled (with default fallback)
            bool enableInstructorApplication = true;
            try
            {
                enableInstructorApplication = await _platformSettings.GetBoolSettingAsync("EnableInstructorApplication", true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get EnableInstructorApplication setting, using default (true)");
            }

            if (!enableInstructorApplication)
            {
                SetInfoMessage("التقديم كمدرس غير متاح حالياً");
                return RedirectToAction("Index", "Profile");
            }

            // Check if user already has an application
            var existingApplication = await _context.InstructorApplications
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (existingApplication != null)
            {
                SetInfoMessage("لديك طلب انضمام موجود بالفعل");
                return RedirectToAction(nameof(Index));
            }

            // Redirect to the main apply page
            return RedirectToAction("ApplyAsInstructor", "Home", new { area = "" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessing instructor application page for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء الوصول لصفحة التقديم");
            return RedirectToAction("Index", "Profile");
        }
    }

    /// <summary>
    /// تفاصيل الطلب - Application details
    /// </summary>
    public async Task<IActionResult> Details()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var application = await _context.InstructorApplications
                .Include(a => a.Documents)
                .Include(a => a.ReviewedBy)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (application == null)
            {
                SetInfoMessage("لم يتم العثور على طلب انضمام");
                return RedirectToAction(nameof(Apply));
            }

            // Get support email (with default fallback)
            string supportEmail = "support@lms.com";
            try
            {
                supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get SupportEmail setting, using default");
            }
            ViewBag.SupportEmail = supportEmail;

            return View(application);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor application details for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل تفاصيل الطلب");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تعديل الطلب - Edit application (only when Pending, UnderReview, or MoreInfoRequired)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var application = await _context.InstructorApplications
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null || !CanEditApplication(application.Status))
        {
            SetInfoMessage("لا يمكن تعديل الطلب في الحالة الحالية");
            return RedirectToAction(nameof(Index));
        }

        var names = application.FullName?.Split(' ', 2) ?? Array.Empty<string>();
        var model = new InstructorApplicationViewModel
        {
            FirstName = names.Length > 0 ? names[0] : application.FullName ?? string.Empty,
            LastName = names.Length > 1 ? names[1] : string.Empty,
            Email = application.Email,
            PhoneNumber = application.Phone,
            Country = application.Country,
            City = application.City,
            Education = application.Education,
            Specialization = application.Specialization,
            YearsOfExperience = application.YearsOfExperience,
            Bio = application.Bio,
            WhyTeach = application.WhyTeach,
            ProposedTopics = application.ProposedTopics,
            LinkedInUrl = application.LinkedInUrl,
            WebsiteUrl = application.WebsiteUrl,
            YouTubeUrl = application.YouTubeUrl,
            SampleVideoUrl = application.SampleVideoUrl,
            AcceptTerms = true
        };
        ViewBag.ApplicationId = application.Id;
        return View(model);
    }

    /// <summary>
    /// حفظ التعديلات - Save application edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InstructorApplicationViewModel model)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var application = await _context.InstructorApplications
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null || !CanEditApplication(application.Status))
        {
            SetInfoMessage("لا يمكن تعديل الطلب في الحالة الحالية");
            return RedirectToAction(nameof(Index));
        }

        // For edit we don't require password or accept terms
        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");
        ModelState.Remove("AcceptTerms");
        if (!ModelState.IsValid)
        {
            ViewBag.ApplicationId = application.Id;
            return View(model);
        }

        application.FullName = $"{model.FirstName?.Trim()} {model.LastName?.Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(application.FullName))
            application.FullName = model.FirstName ?? model.LastName ?? string.Empty;
        application.Phone = model.PhoneNumber;
        application.Country = model.Country;
        application.City = model.City;
        application.Education = model.Education;
        application.Specialization = model.Specialization ?? string.Empty;
        application.YearsOfExperience = model.YearsOfExperience;
        application.Bio = model.Bio ?? string.Empty;
        application.WhyTeach = model.WhyTeach;
        application.ProposedTopics = model.ProposedTopics;
        application.LinkedInUrl = model.LinkedInUrl;
        application.WebsiteUrl = model.WebsiteUrl;
        application.YouTubeUrl = model.YouTubeUrl;
        application.SampleVideoUrl = model.SampleVideoUrl;
        application.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Instructor application {ApplicationId} updated by user {UserId}", application.Id, userId);
        SetSuccessMessage("تم حفظ التعديلات بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إلغاء الطلب - Cancel application
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var application = await _context.InstructorApplications
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null || !CanEditApplication(application.Status))
        {
            SetInfoMessage("لا يمكن إلغاء الطلب في الحالة الحالية");
            return RedirectToAction(nameof(Index));
        }

        application.Status = InstructorApplicationStatus.Cancelled;
        application.UpdatedAt = DateTime.UtcNow;
        application.ReviewNotes = (application.ReviewNotes ?? "") + " [ملغي من قبل مقدم الطلب]";
        await _context.SaveChangesAsync();
        _logger.LogInformation("Instructor application {ApplicationId} cancelled by user {UserId}", application.Id, userId);
        SetSuccessMessage("تم إلغاء الطلب بنجاح");
        return RedirectToAction(nameof(Index));
    }

    private static bool CanEditApplication(InstructorApplicationStatus status)
    {
        return status == InstructorApplicationStatus.Pending
            || status == InstructorApplicationStatus.UnderReview
            || status == InstructorApplicationStatus.MoreInfoRequired;
    }

    /// <summary>
    /// سجل الطلبات - Application history
    /// </summary>
    public async Task<IActionResult> History()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var applications = await _context.InstructorApplications
                .Include(a => a.ReviewedBy)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(applications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructor application history for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل سجل الطلبات");
            return RedirectToAction(nameof(Index));
        }
    }
}






