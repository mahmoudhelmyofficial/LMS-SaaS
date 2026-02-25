using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Financial;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات العمولات - Commission Settings Management Controller
/// </summary>
public class CommissionSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CommissionSettingsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public CommissionSettingsController(
        ApplicationDbContext context, 
        ILogger<CommissionSettingsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة إعدادات العمولة - Commission settings list
    /// </summary>
    public async Task<IActionResult> Index(string? type, bool? active, int page = 1)
    {
        var query = _context.CommissionSettings
            .Include(c => c.Category)
            .Include(c => c.Course)
            .Include(c => c.Instructor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(c => c.Type == type);

        if (active.HasValue)
            query = query.Where(c => c.IsActive == active.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("commission_settings", 20);
        var totalCount = await query.CountAsync();
        var settings = await query
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommissionSettingListViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type,
                CategoryName = c.Category != null ? c.Category.Name : null,
                CourseName = c.Course != null ? c.Course.Title : null,
                InstructorName = c.Instructor != null ? $"{c.Instructor.FirstName} {c.Instructor.LastName}" : null,
                PlatformRate = c.PlatformRate,
                InstructorRate = c.InstructorRate,
                HoldPeriodDays = c.HoldPeriodDays,
                IsActive = c.IsActive,
                Priority = c.Priority,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.Active = active;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        ViewBag.TotalSettings = await _context.CommissionSettings.CountAsync();
        ViewBag.ActiveSettings = await _context.CommissionSettings.CountAsync(c => c.IsActive);

        return View(settings);
    }

    /// <summary>
    /// تفاصيل إعدادات العمولة - Commission settings details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var setting = await _context.CommissionSettings
            .Include(c => c.Category)
            .Include(c => c.Course)
            .Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (setting == null)
            return NotFound();

        // Calculate usage statistics
        var usageStats = await GetSettingUsageStats(id);
        ViewBag.TotalEarnings = usageStats.TotalEarnings;
        ViewBag.TotalSales = usageStats.TotalSales;
        ViewBag.AverageOrderValue = usageStats.AverageOrderValue;

        return View(setting);
    }

    /// <summary>
    /// إنشاء إعدادات عمولة جديدة - Create new commission settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();

        var model = new CommissionSettingCreateViewModel
        {
            Type = "Global",
            PlatformRate = 30,
            InstructorRate = 70,
            HoldPeriodDays = 14,
            IsActive = true,
            Priority = 0
        };

        return View(model);
    }

    /// <summary>
    /// حفظ إعدادات العمولة الجديدة - Save new commission settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CommissionSettingCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Validate rates sum to 100
            if (model.PlatformRate + model.InstructorRate != 100)
            {
                ModelState.AddModelError("", "مجموع نسبة المنصة ونسبة المدرس يجب أن يساوي 100%");
                await PopulateDropdownsAsync();
                return View(model);
            }

            // Normalize type for comparison (case-insensitive)
            var typeNormalized = model.Type?.ToLowerInvariant() ?? "global";

            // Validate type-specific requirements
            if (typeNormalized == "category" && !model.CategoryId.HasValue)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "يجب اختيار تصنيف");
                await PopulateDropdownsAsync();
                return View(model);
            }

            if (typeNormalized == "course" && !model.CourseId.HasValue)
            {
                ModelState.AddModelError(nameof(model.CourseId), "يجب اختيار دورة");
                await PopulateDropdownsAsync();
                return View(model);
            }

            if (typeNormalized == "instructor" && string.IsNullOrEmpty(model.InstructorId))
            {
                ModelState.AddModelError(nameof(model.InstructorId), "يجب اختيار مدرس");
                await PopulateDropdownsAsync();
                return View(model);
            }

            // Validate date range
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "تاريخ الانتهاء يجب أن يكون بعد تاريخ البداية");
                await PopulateDropdownsAsync();
                return View(model);
            }

            var setting = new CommissionSetting
            {
                Name = model.Name,
                Type = typeNormalized,
                CategoryId = typeNormalized == "category" ? model.CategoryId : null,
                CourseId = typeNormalized == "course" ? model.CourseId : null,
                InstructorId = typeNormalized == "instructor" ? model.InstructorId : null,
                PlatformRate = model.PlatformRate,
                InstructorRate = model.InstructorRate,
                HoldPeriodDays = model.HoldPeriodDays,
                IsActive = model.IsActive,
                Priority = model.Priority,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Notes = model.Notes
            };

            _context.CommissionSettings.Add(setting);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Commission setting {SettingName} created with ID {SettingId}", model.Name, setting.Id);

            SetSuccessMessage(CultureExtensions.T("تم إنشاء إعدادات العمولة بنجاح", "Commission settings created successfully."));
            return RedirectToAction(nameof(Details), new { id = setting.Id });
        }

        await PopulateDropdownsAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل إعدادات العمولة - Edit commission settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var setting = await _context.CommissionSettings
            .Include(c => c.Category)
            .Include(c => c.Course)
            .Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (setting == null)
            return NotFound();

        await PopulateDropdownsAsync();

        var model = new CommissionSettingEditViewModel
        {
            Id = setting.Id,
            Name = setting.Name,
            Type = setting.Type,
            CategoryId = setting.CategoryId,
            CourseId = setting.CourseId,
            InstructorId = setting.InstructorId,
            PlatformRate = setting.PlatformRate,
            InstructorRate = setting.InstructorRate,
            HoldPeriodDays = setting.HoldPeriodDays,
            IsActive = setting.IsActive,
            Priority = setting.Priority,
            StartDate = setting.StartDate,
            EndDate = setting.EndDate,
            Notes = setting.Notes
        };

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات إعدادات العمولة - Save commission settings changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CommissionSettingEditViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Validate rates sum to 100
            if (model.PlatformRate + model.InstructorRate != 100)
            {
                ModelState.AddModelError("", "مجموع نسبة المنصة ونسبة المدرس يجب أن يساوي 100%");
                await PopulateDropdownsAsync();
                return View(model);
            }

            // Validate date range
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "تاريخ الانتهاء يجب أن يكون بعد تاريخ البداية");
                await PopulateDropdownsAsync();
                return View(model);
            }

            var setting = await _context.CommissionSettings.FindAsync(id);

            if (setting == null)
                return NotFound();

            setting.Name = model.Name;
            setting.PlatformRate = model.PlatformRate;
            setting.InstructorRate = model.InstructorRate;
            setting.HoldPeriodDays = model.HoldPeriodDays;
            setting.IsActive = model.IsActive;
            setting.Priority = model.Priority;
            setting.StartDate = model.StartDate;
            setting.EndDate = model.EndDate;
            setting.Notes = model.Notes;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Commission setting {SettingId} updated", id);

            SetSuccessMessage(CultureExtensions.T("تم تحديث إعدادات العمولة بنجاح", "Commission settings updated successfully."));
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateDropdownsAsync();
        return View(model);
    }

    /// <summary>
    /// حذف إعدادات العمولة - Delete commission settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var setting = await _context.CommissionSettings.FindAsync(id);

        if (setting == null)
            return NotFound();

        // Check if it's the global default setting
        if (setting.Type == "global" && setting.Priority == 0)
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن حذف إعدادات العمولة الافتراضية العامة", "Cannot delete the default global commission settings."));
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.CommissionSettings.Remove(setting);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Commission setting {SettingId} deleted", id);

        SetSuccessMessage(CultureExtensions.T("تم حذف إعدادات العمولة بنجاح", "Commission settings deleted successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var setting = await _context.CommissionSettings.FindAsync(id);

        if (setting == null)
            return NotFound();

        setting.IsActive = !setting.IsActive;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Commission setting {SettingId} {Status}", id, setting.IsActive ? "activated" : "deactivated");

        SetSuccessMessage(setting.IsActive ? CultureExtensions.T("تم تفعيل إعدادات العمولة", "Commission settings enabled.") : CultureExtensions.T("تم تعطيل إعدادات العمولة", "Commission settings disabled."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// معاينة حساب العمولة - Preview commission calculation
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview()
    {
        await PopulateDropdownsAsync();

        var model = new CommissionPreviewViewModel
        {
            SaleAmount = 1000
        };

        return View(model);
    }

    /// <summary>
    /// حساب العمولة - Calculate commission
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(CommissionPreviewViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Find applicable commission setting
            var setting = await FindApplicableCommissionSetting(model.CourseId, model.InstructorId);

            if (setting != null)
            {
                model.PlatformRate = setting.PlatformRate;
                model.InstructorRate = setting.InstructorRate;
                model.HoldPeriodDays = setting.HoldPeriodDays;
                model.PlatformCommission = model.SaleAmount * (setting.PlatformRate / 100);
                model.InstructorEarning = model.SaleAmount * (setting.InstructorRate / 100);
                model.AvailableDate = DateTime.UtcNow.AddDays(setting.HoldPeriodDays);
                model.AppliedSettingName = setting.Name;
                model.AppliedSettingType = setting.Type;
            }
            else
            {
                // Default rates if no setting found
                model.PlatformRate = 30;
                model.InstructorRate = 70;
                model.HoldPeriodDays = 14;
                model.PlatformCommission = model.SaleAmount * 0.30m;
                model.InstructorEarning = model.SaleAmount * 0.70m;
                model.AvailableDate = DateTime.UtcNow.AddDays(14);
                model.AppliedSettingName = "Default Global Setting";
                model.AppliedSettingType = "global";
            }
        }

        await PopulateDropdownsAsync();
        return View(model);
    }

    #region Private Helpers

    private async Task PopulateDropdownsAsync()
    {
        try
        {
            ViewBag.Categories = await _context.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories for dropdown");
            ViewBag.Categories = new List<SelectListItem>();
        }

        try
        {
            ViewBag.Courses = await _context.Courses
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading courses for dropdown");
            ViewBag.Courses = new List<SelectListItem>();
        }

        try
        {
            // Use InstructorProfiles to get instructor users (IsInstructor is [NotMapped] and cannot be translated to SQL)
            ViewBag.Instructors = await _context.InstructorProfiles
                .Where(p => p.User != null && !p.User.IsDeleted)
                .Select(p => new SelectListItem
                {
                    Value = p.UserId,
                    Text = (!string.IsNullOrEmpty(p.User.FirstName) || !string.IsNullOrEmpty(p.User.LastName))
                        ? ((p.User.FirstName ?? "") + " " + (p.User.LastName ?? "")).Trim()
                        : (p.User.Email ?? p.UserId)
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading instructors for dropdown");
            ViewBag.Instructors = new List<SelectListItem>();
        }
    }

    private async Task<CommissionSetting?> FindApplicableCommissionSetting(int? courseId, string? instructorId)
    {
        var now = DateTime.UtcNow;
        int? categoryId = null;

        if (courseId.HasValue)
        {
            var course = await _context.Courses.FindAsync(courseId.Value);
            categoryId = course?.CategoryId;
            instructorId ??= course?.InstructorId;
        }

        // Priority order: Course > Instructor > Category > Global
        var settings = await _context.CommissionSettings
            .Where(c => c.IsActive)
            .Where(c => !c.StartDate.HasValue || c.StartDate <= now)
            .Where(c => !c.EndDate.HasValue || c.EndDate >= now)
            .OrderByDescending(c => c.Priority)
            .ToListAsync();

        // Find most specific setting
        var courseSetting = settings.FirstOrDefault(s => s.Type == "course" && s.CourseId == courseId);
        if (courseSetting != null) return courseSetting;

        var instructorSetting = settings.FirstOrDefault(s => s.Type == "instructor" && s.InstructorId == instructorId);
        if (instructorSetting != null) return instructorSetting;

        var categorySetting = settings.FirstOrDefault(s => s.Type == "category" && s.CategoryId == categoryId);
        if (categorySetting != null) return categorySetting;

        var globalSetting = settings.FirstOrDefault(s => s.Type == "global");
        return globalSetting;
    }

    private async Task<(decimal TotalEarnings, int TotalSales, decimal AverageOrderValue)> GetSettingUsageStats(int settingId)
    {
        // This is a placeholder - you would need to track which setting was used for each earning
        // For now, return zeros
        await Task.CompletedTask;
        return (0, 0, 0);
    }

    #endregion
}

