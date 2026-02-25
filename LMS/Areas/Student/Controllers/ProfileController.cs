using LMS.Data;
using LMS.Domain.Entities.Users;
using LMS.Helpers;
using LMS.Services.Interfaces;
using LMS.Areas.Student.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// الملف الشخصي - Profile Controller with Portfolio & Social Features
/// </summary>
public class ProfileController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILearningAnalyticsService _analyticsService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ILearningAnalyticsService analyticsService,
        IFileStorageService fileStorage,
        ILogger<ProfileController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _analyticsService = analyticsService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// الملف الشخصي - My profile
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        // Get learning stats with error handling
        StudentLearningStats? stats = null;
        try
        {
            stats = await _analyticsService.GetStudentLearningStatsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get learning stats for user {UserId}", userId);
            // Create a default stats object if the service fails
            stats = new StudentLearningStats();
        }

        ViewBag.Stats = stats;

        // Get instructor application status for the "Apply as Instructor" card
        var instructorApplication = await _context.InstructorApplications
            .FirstOrDefaultAsync(a => a.UserId == userId);
        ViewBag.InstructorApplication = instructorApplication;

        // Check if user is already an instructor
        var isAlreadyInstructor = await _userManager.IsInRoleAsync(user, "Instructor");
        ViewBag.IsAlreadyInstructor = isAlreadyInstructor;

        // Load recent activities from database
        try
        {
            var recentActivities = await _context.ActivityLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();
            ViewBag.RecentActivities = recentActivities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load activities for user {UserId}", userId);
            ViewBag.RecentActivities = new List<Domain.Entities.Analytics.ActivityLog>();
        }

        // Load user badges
        try
        {
            var userBadges = await _context.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.UserId == userId)
                .OrderByDescending(ub => ub.AwardedAt)
                .Take(4)
                .ToListAsync();
            ViewBag.UserBadges = userBadges;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load badges for user {UserId}", userId);
            ViewBag.UserBadges = new List<Domain.Entities.Gamification.UserBadge>();
        }

        return View(user);
    }

    /// <summary>
    /// الحصول على النشاطات عبر AJAX - Get activities via AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActivities(int page = 1, int pageSize = 10)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً" });
        }

        try
        {
            var totalCount = await _context.ActivityLogs
                .CountAsync(a => a.UserId == userId);

            var activities = await _context.ActivityLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.ActivityType,
                    a.Description,
                    a.CreatedAt,
                    a.IpAddress,
                    TimeAgo = GetTimeAgo(a.CreatedAt)
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                activities,
                totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                currentPage = page
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activities for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل النشاطات" });
        }
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime;
        
        if (diff.TotalMinutes < 60)
            return $"منذ {(int)diff.TotalMinutes} دقيقة";
        if (diff.TotalHours < 24)
            return $"منذ {(int)diff.TotalHours} ساعة";
        if (diff.TotalDays < 7)
            return $"منذ {(int)diff.TotalDays} يوم";
        
        return dateTime.ToString("dd/MM/yyyy HH:mm");
    }

    /// <summary>
    /// تعديل الملف الشخصي - Edit profile
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
        
        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        // Calculate profile completion percentage
        var completionFields = new List<bool>
        {
            !string.IsNullOrEmpty(user.FirstName),
            !string.IsNullOrEmpty(user.LastName),
            !string.IsNullOrEmpty(user.Email),
            user.EmailConfirmed,
            !string.IsNullOrEmpty(user.ProfilePictureUrl),
            !string.IsNullOrEmpty(user.Bio),
            user.DateOfBirth.HasValue,
            !string.IsNullOrEmpty(user.Country),
            !string.IsNullOrEmpty(user.City),
            !string.IsNullOrEmpty(user.PhoneNumber)
        };
        
        var completionPercentage = (decimal)completionFields.Count(f => f) / completionFields.Count * 100;

        var viewModel = new ProfileEditViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Profile?.Bio,
            DateOfBirth = user.DateOfBirth,
            Country = user.Country,
            City = user.City,
            TimeZone = user.TimeZone ?? "Africa/Cairo",
            Language = user.Language ?? "ar",
            ProfileCompletionPercentage = (int)Math.Round(completionPercentage)
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديلات - Save profile changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            // Update user basic info
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Bio = model.Bio;
            user.DateOfBirth = model.DateOfBirth;
            user.Country = model.Country;
            user.City = model.City;
            user.TimeZone = model.TimeZone;
            user.Language = model.Language;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated profile", userId);
            SetSuccessMessage("تم تحديث الملف الشخصي بنجاح");

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الملف الشخصي");
            return View(model);
        }
    }

    /// <summary>
    /// تحميل صورة الملف الشخصي - Upload profile picture
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "يرجى اختيار صورة" });

        try
        {
            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
                return Json(new { success = false, message = "نوع الملف غير مدعوم" });

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return Json(new { success = false, message = "حجم الصورة يجب أن لا يتجاوز 5 ميجابايت" });

            // Upload file
            var uploadResult = await _fileStorage.UploadFileAsync(file, "profiles");

            if (string.IsNullOrEmpty(uploadResult))
                return Json(new { success = false, message = "فشل رفع الصورة" });

            // Update user profile picture
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.ProfilePictureUrl = uploadResult;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, imageUrl = uploadResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile picture for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء رفع الصورة" });
        }
    }

    /// <summary>
    /// محفظة الإنجازات - Portfolio/Showcase
    /// </summary>
    public async Task<IActionResult> Portfolio()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Get completed courses
        var completedCourses = await _context.Enrollments
            .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
            .Include(e => e.Course)
                .ThenInclude(c => c.Category)
            .Where(e => e.StudentId == userId && e.Status == Domain.Enums.EnrollmentStatus.Completed)
            .OrderByDescending(e => e.CompletedAt)
            .ToListAsync();

        // Get certificates
        var certificates = await _context.Certificates
            .Include(c => c.Course)
            .Where(c => c.StudentId == userId && !c.IsRevoked)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync();

        // Get badges
        var badges = await _context.UserBadges
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == userId)
            .OrderByDescending(ub => ub.AwardedAt)
            .ToListAsync();

        // Get skills (from completed courses categories)
        var skills = completedCourses
            .Where(e => e.Course?.Category != null)
            .GroupBy(e => e.Course.Category!.Name)
            .Select(g => new SkillItem
            {
                SkillName = g.Key,
                CoursesCount = g.Count(),
                Level = DetermineSkillLevel(g.Count())
            })
            .ToList();

        var portfolio = new PortfolioViewModel
        {
            CompletedCourses = completedCourses,
            Certificates = certificates,
            Badges = badges,
            Skills = skills
        };

        return View(portfolio);
    }

    /// <summary>
    /// الملف الشخصي العام - Public profile (shareable)
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Public(string username)
    {
        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.UserName == username);

        if (user == null)
            return NotFound();

        // Check if profile is public
        if (user.Profile?.ProfileVisibility != "Public")
        {
            var currentUserId = _currentUserService.UserId;
            if (currentUserId != user.Id)
            {
                return NotFound("هذا الملف الشخصي خاص");
            }
        }

        var stats = await _analyticsService.GetStudentLearningStatsAsync(user.Id);

        // Get public certificates
        var certificates = await _context.Certificates
            .Include(c => c.Course)
            .Where(c => c.StudentId == user.Id && !c.IsRevoked && c.IsPublic)
            .OrderByDescending(c => c.IssuedDate)
            .Take(6)
            .ToListAsync();

        // Get public badges
        var badges = await _context.UserBadges
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == user.Id)
            .OrderByDescending(ub => ub.AwardedAt)
            .Take(10)
            .ToListAsync();

        var publicProfile = new PublicProfileViewModel
        {
            User = user,
            Stats = stats,
            Certificates = certificates,
            Badges = badges
        };

        return View(publicProfile);
    }

    /// <summary>
    /// تقييم نمط التعلم - Learning style assessment
    /// </summary>
    [HttpGet]
    public IActionResult LearningStyleAssessment()
    {
        return View();
    }

    /// <summary>
    /// حفظ نتيجة تقييم نمط التعلم - Save learning style result
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLearningStyle(string style, Dictionary<string, int> scores)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            if (user.Profile == null)
            {
                user.Profile = new UserProfile { UserId = userId };
                _context.UserProfiles.Add(user.Profile);
            }

            user.Profile.LearningStyle = style;
            user.Profile.LearningStyleAssessmentDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم حفظ نمط التعلم الخاص بك" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving learning style for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء حفظ النتيجة" });
        }
    }

    /// <summary>
    /// إعدادات الإشعارات - Notification preferences
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> NotificationPreferences()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var preferences = await _context.NotificationPreferences
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (preferences == null)
        {
            preferences = new Domain.Entities.Notifications.NotificationPreference
            {
                UserId = userId,
                EmailNotifications = true,
                PushNotifications = true,
                SmsNotifications = false,
                NewCoursesFromInstructors = true,
                CourseUpdates = true,
                AssignmentReminders = true,
                QuizReminders = true,
                CertificateIssued = true,
                BadgeEarned = true,
                WeeklyDigest = true,
                MonthlyReport = true
            };

            _context.NotificationPreferences.Add(preferences);
            await _context.SaveChangesAsync();
        }

        return View(preferences);
    }

    /// <summary>
    /// تحديث إعدادات الإشعارات - Update notification preferences
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotificationPreferences(Domain.Entities.Notifications.NotificationPreference model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var preferences = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (preferences == null)
                return NotFound();

            preferences.EmailNotifications = model.EmailNotifications;
            preferences.PushNotifications = model.PushNotifications;
            preferences.SmsNotifications = model.SmsNotifications;
            preferences.NewCoursesFromInstructors = model.NewCoursesFromInstructors;
            preferences.CourseUpdates = model.CourseUpdates;
            preferences.AssignmentReminders = model.AssignmentReminders;
            preferences.QuizReminders = model.QuizReminders;
            preferences.CertificateIssued = model.CertificateIssued;
            preferences.BadgeEarned = model.BadgeEarned;
            preferences.WeeklyDigest = model.WeeklyDigest;
            preferences.MonthlyReport = model.MonthlyReport;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث إعدادات الإشعارات");
            return RedirectToAction(nameof(NotificationPreferences));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return View(model);
        }
    }

    /// <summary>
    /// الخصوصية - Privacy (redirects to PrivacySettings for backward compatibility)
    /// </summary>
    [HttpGet]
    public IActionResult Privacy()
    {
        return RedirectToAction(nameof(PrivacySettings));
    }

    /// <summary>
    /// إعدادات الخصوصية - Privacy settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PrivacySettings()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var user = await _context.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        var settings = new PrivacySettingsViewModel
        {
            ProfileVisibility = user.Profile?.ProfileVisibility ?? "Private",
            ShowEmailPublicly = user.Profile?.ShowEmailPublicly ?? false,
            ShowProgressPublicly = user.Profile?.ShowProgressPublicly ?? false,
            ShowBadgesPublicly = user.Profile?.ShowBadgesPublicly ?? true,
            ShowCertificatesPublicly = user.Profile?.ShowCertificatesPublicly ?? true,
            AllowMessages = user.Profile?.AllowMessages ?? true
        };

        return View(settings);
    }

    /// <summary>
    /// تحديث إعدادات الخصوصية - Update privacy settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacySettings(PrivacySettingsViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            var allowedVisibility = new[] { "Public", "Private", "StudentsOnly" };
            var visibility = string.IsNullOrEmpty(model.ProfileVisibility) || !allowedVisibility.Contains(model.ProfileVisibility)
                ? "Private"
                : model.ProfileVisibility;

            if (user.Profile == null)
            {
                user.Profile = new UserProfile { UserId = userId };
                _context.UserProfiles.Add(user.Profile);
            }

            user.Profile.ProfileVisibility = visibility;
            user.Profile.ShowEmailPublicly = model.ShowEmailPublicly;
            user.Profile.ShowProgressPublicly = model.ShowProgressPublicly;
            user.Profile.ShowBadgesPublicly = model.ShowBadgesPublicly;
            user.Profile.ShowCertificatesPublicly = model.ShowCertificatesPublicly;
            user.Profile.AllowMessages = model.AllowMessages;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث إعدادات الخصوصية");
            return RedirectToAction(nameof(PrivacySettings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating privacy settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return View(model);
        }
    }

    /// <summary>
    /// تحميل السيرة الذاتية - Upload resume/CV
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadResume(IFormFile file)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
        }

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "يرجى اختيار ملف" });

        try
        {
            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
                return Json(new { success = false, message = "نوع الملف غير مدعوم" });

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
                return Json(new { success = false, message = "حجم الملف يجب أن لا يتجاوز 10 ميجابايت" });

            // Upload file
            var uploadResult = await _fileStorage.UploadFileAsync(file, "resumes");

            if (string.IsNullOrEmpty(uploadResult))
                return Json(new { success = false, message = "فشل رفع الملف" });

            // Update user profile
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null)
            {
                if (user.Profile == null)
                {
                    user.Profile = new UserProfile { UserId = userId };
                    _context.UserProfiles.Add(user.Profile);
                }

                user.Profile.ResumeUrl = uploadResult;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, fileUrl = uploadResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading resume for user {UserId}", userId);
            return Json(new { success = false, message = "حدث خطأ أثناء رفع الملف" });
        }
    }

    /// <summary>
    /// الإعدادات - Redirect to Student Settings hub
    /// </summary>
    [HttpGet]
    public IActionResult Settings()
    {
        return RedirectToAction("Index", "Settings", new { area = "Student" });
    }

    /// <summary>
    /// تحديث الإعدادات - Update settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettings(SettingsViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            var allowedVisibility = new[] { "Public", "Private", "StudentsOnly" };
            var visibility = string.IsNullOrEmpty(model.ProfileVisibility) || !allowedVisibility.Contains(model.ProfileVisibility)
                ? "Private"
                : model.ProfileVisibility;

            var allowedLanguages = new[] { "ar", "en" };
            var allowedTimeZones = new[] { "Africa/Cairo", "Asia/Riyadh", "Asia/Dubai", "Asia/Kuwait", "UTC" };
            var language = allowedLanguages.Contains(model.Language ?? "") ? model.Language! : "ar";
            var timeZone = allowedTimeZones.Contains(model.TimeZone ?? "") ? model.TimeZone! : "Africa/Cairo";

            // Update profile settings
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.TimeZone = timeZone;
            user.Language = language;

            // Update privacy settings
            if (user.Profile == null)
            {
                user.Profile = new UserProfile { UserId = userId };
                _context.UserProfiles.Add(user.Profile);
            }

            user.Profile.ProfileVisibility = visibility;
            user.Profile.ShowProgressPublicly = model.ShowProgressPublicly;
            user.Profile.AllowMessages = model.AllowMessages;

            // Update notification settings
            var notificationPrefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId);

            if (notificationPrefs == null)
            {
                notificationPrefs = new Domain.Entities.Notifications.NotificationPreference { UserId = userId };
                _context.NotificationPreferences.Add(notificationPrefs);
            }

            notificationPrefs.EmailNotifications = model.EmailNotifications;
            notificationPrefs.PushNotifications = model.PushNotifications;
            notificationPrefs.CourseUpdates = model.CourseUpdates;
            notificationPrefs.WeeklyDigest = model.WeeklyDigest;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated settings", userId);
            SetSuccessMessage("تم تحديث الإعدادات بنجاح");
            return RedirectToAction("Index", "Settings", new { area = "Student" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث الإعدادات");
            return RedirectToAction("Index", "Settings", new { area = "Student" });
        }
    }

    /// <summary>
    /// الأمان - Security (redirects to SecuritySettings for backward compatibility)
    /// </summary>
    [HttpGet]
    public IActionResult Security()
    {
        return RedirectToAction(nameof(SecuritySettings));
    }

    /// <summary>
    /// إعدادات الأمان - Security settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SecuritySettings()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("المستخدم غير موجود");
            return RedirectToAction(nameof(Index));
        }

        // Get security information
        var has2FA = await _userManager.GetTwoFactorEnabledAsync(user);
        var hasPassword = await _userManager.HasPasswordAsync(user);
        var loginAttempts = await _context.ActivityLogs
            .Where(a => a.UserId == userId && a.ActivityType == "Login")
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync();

        var viewModel = new SecuritySettingsViewModel
        {
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber ?? "",
            HasPassword = hasPassword,
            TwoFactorEnabled = has2FA,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            RecentLoginAttempts = loginAttempts.Select(log => new LoginAttemptViewModel
            {
                IpAddress = log.IpAddress ?? "غير معروف",
                Timestamp = log.Timestamp,
                Success = true, // From ActivityLog, these are successful logins
                DeviceInfo = log.UserAgent ?? "غير معروف"
            }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// تحديث إعدادات الأمان - Update security settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSecuritySettings(SecuritySettingsUpdateViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("المستخدم غير موجود");
            return RedirectToAction(nameof(SecuritySettings));
        }

        try
        {
            // Update phone number if provided
            if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != user.PhoneNumber)
            {
                user.PhoneNumber = model.PhoneNumber;
                user.PhoneNumberConfirmed = false; // Require re-confirmation
            }

            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Security settings updated for user {UserId}", userId);

            SetSuccessMessage("تم تحديث إعدادات الأمان بنجاح");
            return RedirectToAction(nameof(SecuritySettings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security settings for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث إعدادات الأمان");
            return RedirectToAction(nameof(SecuritySettings));
        }
    }

    /// <summary>
    /// تغيير كلمة المرور - Change password
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage("يرجى التحقق من البيانات المدخلة");
            return RedirectToAction(nameof(SecuritySettings));
        }

        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("المستخدم غير موجود");
            return RedirectToAction(nameof(SecuritySettings));
        }

        try
        {
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Password changed for user {UserId}", userId);

                // Log activity
                _context.ActivityLogs.Add(new Domain.Entities.Analytics.ActivityLog
                {
                    UserId = userId,
                    ActivityType = "PasswordChanged",
                    Description = "تغيير كلمة المرور",
                    CreatedAt = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                await _context.SaveChangesAsync();

                SetSuccessMessage("تم تغيير كلمة المرور بنجاح");
                return RedirectToAction(nameof(SecuritySettings));
            }

            SetErrorMessage(string.Join(", ", result.Errors.Select(e => e.Description)));
            return RedirectToAction(nameof(SecuritySettings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تغيير كلمة المرور");
            return RedirectToAction(nameof(SecuritySettings));
        }
    }

    /// <summary>
    /// تفعيل/تعطيل المصادقة الثنائية - Toggle two-factor authentication
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTwoFactor(bool enable)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            SetErrorMessage("المستخدم غير موجود");
            return RedirectToAction(nameof(SecuritySettings));
        }

        try
        {
            var result = await _userManager.SetTwoFactorEnabledAsync(user, enable);

            if (result.Succeeded)
            {
                var action = enable ? "تم تفعيل" : "تم تعطيل";
                _logger.LogInformation("Two-factor authentication {Action} for user {UserId}", action, userId);

                // Log activity
                _context.ActivityLogs.Add(new Domain.Entities.Analytics.ActivityLog
                {
                    UserId = userId,
                    ActivityType = "TwoFactorToggled",
                    Description = $"{action} المصادقة الثنائية",
                    CreatedAt = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                await _context.SaveChangesAsync();

                SetSuccessMessage($"{action} المصادقة الثنائية بنجاح");
                return RedirectToAction(nameof(SecuritySettings));
            }

            SetErrorMessage("حدث خطأ أثناء تحديث إعدادات المصادقة الثنائية");
            return RedirectToAction(nameof(SecuritySettings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling two-factor for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحديث إعدادات المصادقة الثنائية");
            return RedirectToAction(nameof(SecuritySettings));
        }
    }

    /// <summary>
    /// برنامج الإحالة - Referral program
    /// </summary>
    public async Task<IActionResult> Referrals()
    {
        var userId = _currentUserService.UserId!;
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound();

        // Generate or get referral code
        if (string.IsNullOrEmpty(user.ReferralCode))
        {
            user.ReferralCode = GenerateReferralCode(user.UserName!);
            await _context.SaveChangesAsync();
        }

        // Get referral stats
        var referrals = await _context.Users
            .Where(u => u.ReferredBy == userId)
            .ToListAsync();

        // Get active referrals (users who have enrolled in at least one course)
        var activeReferralsCount = 0;
        foreach (var referral in referrals)
        {
            var hasEnrollment = await _context.Enrollments.AnyAsync(e => e.StudentId == referral.Id);
            if (hasEnrollment) activeReferralsCount++;
        }

        // Get earned points from referrals
        var earnedPoints = await _context.UserPoints
            .Where(p => p.UserId == userId && p.Reason != null && p.Reason.Contains("إحالة"))
            .SumAsync(p => p.Points);

        // Calculate conversion rate
        var conversionRate = referrals.Count > 0 ? (activeReferralsCount * 100 / referrals.Count) : 0;

        var referralLink = Url.Action("Register", "Account", new { area = "Identity", referral = user.ReferralCode }, Request.Scheme);

        var viewModel = new ReferralProgramViewModel
        {
            ReferralCode = user.ReferralCode,
            ReferralLink = referralLink!,
            TotalReferrals = referrals.Count,
            ActiveReferrals = activeReferralsCount,
            EarnedPoints = earnedPoints,
            ConversionRate = conversionRate,
            ReferralsList = referrals.Select(r => new ReferralItem
            {
                Name = $"{r.FirstName} {r.LastName}",
                JoinedDate = r.CreatedAt
            }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// تحميل البيانات الشخصية - Download personal data (GDPR compliance)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadData()
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            // Get user's enrollments
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId)
                .Select(e => new
                {
                    CourseName = e.Course.Title,
                    e.EnrolledAt,
                    e.ProgressPercentage,
                    Status = e.Status.ToString()
                })
                .ToListAsync();

            // Get user's certificates
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Where(c => c.StudentId == userId)
                .Select(c => new
                {
                    CourseName = c.Course.Title,
                    c.IssuedAt,
                    c.CertificateNumber
                })
                .ToListAsync();

            // Get user's notes
            var notes = await _context.StudentNotes
                .Include(n => n.Lesson)
                .Where(n => n.StudentId == userId)
                .Select(n => new
                {
                    LessonName = n.Lesson.Title,
                    n.Content,
                    n.CreatedAt
                })
                .ToListAsync();

            // Compile data export
            var exportData = new
            {
                ExportDate = DateTime.UtcNow,
                PersonalInformation = new
                {
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.PhoneNumber,
                    user.DateOfBirth,
                    user.Country,
                    user.City,
                    user.Bio,
                    user.CreatedAt,
                    user.Language,
                    user.TimeZone
                },
                Enrollments = enrollments,
                Certificates = certificates,
                Notes = notes
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            _logger.LogInformation("User {UserId} downloaded their personal data", userId);

            return File(bytes, "application/json", $"my-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data export for user {UserId}", userId);
            SetErrorMessage("حدث خطأ أثناء تجهيز البيانات للتحميل");
            return RedirectToAction(nameof(PrivacySettings));
        }
    }

    #region Private Helper Methods

    private string DetermineSkillLevel(int coursesCount)
    {
        return coursesCount switch
        {
            >= 5 => "Expert",
            >= 3 => "Advanced",
            >= 2 => "Intermediate",
            _ => "Beginner"
        };
    }

    private string GenerateReferralCode(string username)
    {
        var random = new Random();
        var code = username.Substring(0, Math.Min(4, username.Length)).ToUpper() + 
                   random.Next(1000, 9999);
        return code;
    }

    #endregion
}

#region View Models

public class PortfolioViewModel
{
    public List<Domain.Entities.Learning.Enrollment> CompletedCourses { get; set; } = new();
    public List<Domain.Entities.Certifications.Certificate> Certificates { get; set; } = new();
    public List<Domain.Entities.Gamification.UserBadge> Badges { get; set; } = new();
    public List<SkillItem> Skills { get; set; } = new();
}

public class SkillItem
{
    public string SkillName { get; set; } = string.Empty;
    public int CoursesCount { get; set; }
    public string Level { get; set; } = string.Empty;
}

public class ReferralProgramViewModel
{
    public string ReferralCode { get; set; } = string.Empty;
    public string ReferralLink { get; set; } = string.Empty;
    public int TotalReferrals { get; set; }
    public int ActiveReferrals { get; set; }
    public int EarnedPoints { get; set; }
    public int ConversionRate { get; set; }
    public List<ReferralItem> ReferralsList { get; set; } = new();
}

public class ReferralItem
{
    public string Name { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
}

public class SecuritySettingsUpdateViewModel
{
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// نموذج الإعدادات - Settings view model
/// </summary>
public class SettingsViewModel
{
    // Profile settings
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;
    
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;
    
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;
    
    [Display(Name = "رقم الهاتف")]
    public string? PhoneNumber { get; set; }
    
    [Display(Name = "المنطقة الزمنية")]
    public string TimeZone { get; set; } = "Africa/Cairo";
    
    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";
    
    // Privacy settings
    [Display(Name = "ظهور الملف الشخصي")]
    public string ProfileVisibility { get; set; } = "Private";
    
    [Display(Name = "إظهار التقدم للآخرين")]
    public bool ShowProgressPublicly { get; set; }
    
    [Display(Name = "السماح بالرسائل")]
    public bool AllowMessages { get; set; } = true;
    
    // Notification settings
    [Display(Name = "إشعارات البريد الإلكتروني")]
    public bool EmailNotifications { get; set; } = true;
    
    [Display(Name = "الإشعارات الفورية")]
    public bool PushNotifications { get; set; } = true;
    
    [Display(Name = "تحديثات الدورات")]
    public bool CourseUpdates { get; set; } = true;
    
    [Display(Name = "الملخص الأسبوعي")]
    public bool WeeklyDigest { get; set; } = true;
    
    // Security info (read-only)
    public bool TwoFactorEnabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [StringLength(100, ErrorMessage = "يجب أن تكون {0} على الأقل {2} حرفاً", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور الجديدة")]
    [Compare("NewPassword", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

#endregion

