using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Marketing;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة باقات المدرس - Instructor Bundles Controller
/// </summary>
public class BundlesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;
    private readonly ISystemConfigurationService _configService;
    private readonly ICurrencyService _currencyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BundlesController> _logger;

    public BundlesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService,
        ISystemConfigurationService configService,
        ICurrencyService currencyService,
        IMemoryCache cache,
        ILogger<BundlesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
        _configService = configService;
        _currencyService = currencyService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// قائمة باقات المدرس - Instructor's bundles list
    /// </summary>
    public async Task<IActionResult> Index(bool? isActive, int page = 1)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Bundles Index: UserId is null or empty");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);

        try
        {
            var query = _context.CourseBundles
                .Include(b => b.BundleCourses)
                .Where(b => b.CreatedByUserId == userId);

            // Get total counts before filtering
            var allBundlesQuery = _context.CourseBundles.Where(b => b.CreatedByUserId == userId);
            var totalBundles = await allBundlesQuery.CountAsync();
            var activeBundles = await allBundlesQuery.CountAsync(b => b.IsActive);
            var totalSales = await allBundlesQuery.SumAsync(b => b.SalesCount);
            
            // Calculate total revenue from payments
            var bundleIds = await allBundlesQuery.Select(b => b.Id).ToListAsync();
            var totalRevenue = await _context.Payments
                .Where(p => p.BundleId.HasValue && bundleIds.Contains(p.BundleId.Value) && p.Status == Domain.Enums.PaymentStatus.Completed)
                .SumAsync(p => p.TotalAmount);

            if (isActive.HasValue)
            {
                query = query.Where(b => b.IsActive == isActive.Value);
            }

            var totalFilteredCount = await query.CountAsync();
            var bundles = await query
                .OrderBy(b => b.DisplayOrder)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            // Set ViewBag statistics for the view
            ViewBag.TotalBundles = totalBundles;
            ViewBag.ActiveBundles = activeBundles;
            ViewBag.TotalSales = totalSales;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.IsActive = isActive;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalFilteredCount / 20.0);
            ViewBag.TotalItems = totalFilteredCount;

            _logger.LogInformation("Instructor {InstructorId} viewed bundles list. Total: {TotalBundles}, Active: {ActiveBundles}", 
                userId, totalBundles, activeBundles);

            return View(bundles ?? new List<CourseBundle>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bundles for instructor {InstructorId}", userId);
            
            // Set default values to prevent null reference errors in view
            ViewBag.TotalBundles = 0;
            ViewBag.ActiveBundles = 0;
            ViewBag.TotalSales = 0;
            ViewBag.TotalRevenue = 0m;
            ViewBag.IsActive = isActive;
            ViewBag.Page = page;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;
            
            SetErrorMessage("حدث خطأ أثناء تحميل الباقات. يرجى المحاولة مرة أخرى.");
            return View(new List<CourseBundle>());
        }
    }

    /// <summary>
    /// تفاصيل الباقة - Bundle details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;
        
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
            .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

        if (bundle == null)
            return NotFound();

        return View(bundle);
    }

    /// <summary>
    /// إنشاء باقة جديدة - Create new bundle
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        await PopulateInstructorCoursesDropdown();
        return View(new BundleCreateViewModel());
    }

    /// <summary>
    /// حفظ الباقة الجديدة - Save new bundle
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BundleCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (!model.CourseIds.Any())
            {
                ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
            }
            else if (model.CourseIds.Count < 2)
            {
                ModelState.AddModelError("CourseIds", "الباقة يجب أن تحتوي على دورتين على الأقل");
            }

            // Verify all courses belong to the instructor
            var instructorCourseIds = await _context.Courses
                .Where(c => c.InstructorId == userId && model.CourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (instructorCourseIds.Count != model.CourseIds.Count)
            {
                ModelState.AddModelError("CourseIds", "لا يمكنك إضافة دورات لا تملكها");
            }

            if (!ModelState.IsValid)
            {
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Calculate original price
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var originalPrice = courses.Sum(c => c.DiscountPrice ?? c.Price);

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateBundle(
                model.CourseIds.Count,
                model.Price,
                originalPrice,
                model.ValidFrom,
                model.ValidTo,
                model.MaxSales);

            if (!isValid)
            {
                _logger.LogWarning("Bundle validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            var savingsPercentage = originalPrice > 0 
                ? ((originalPrice - model.Price) / originalPrice) * 100 
                : 0;

            // Generate unique slug
            var slug = _slugService.GenerateSlug(model.Name);
            var slugExists = await _context.CourseBundles.AnyAsync(b => b.Slug == slug);
            if (slugExists)
            {
                slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
            }

            CourseBundle bundle = null!;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                bundle = new CourseBundle
                {
                    Name = model.Name,
                    Slug = slug,
                    ShortDescription = model.ShortDescription,
                    Description = model.Description,
                    ThumbnailUrl = model.ThumbnailUrl,
                    Price = model.Price,
                    OriginalPrice = originalPrice,
                    Currency = "EGP",
                    SavingsPercentage = savingsPercentage,
                    IsActive = model.IsActive,
                    IsFeatured = false, // Only admins can feature
                    DisplayOrder = model.DisplayOrder,
                    ValidFrom = model.ValidFrom,
                    ValidTo = model.ValidTo,
                    MaxSales = model.MaxSales,
                    CoursesCount = model.CourseIds.Count,
                    CreatedByUserId = userId
                };

                _context.CourseBundles.Add(bundle);
                await _context.SaveChangesAsync();

                // Add courses to bundle
                foreach (var courseId in model.CourseIds)
                {
                    var bundleCourse = new BundleCourse
                    {
                        BundleId = bundle.Id,
                        CourseId = courseId,
                        DisplayOrder = model.CourseIds.IndexOf(courseId)
                    };
                    _context.BundleCourses.Add(bundleCourse);
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Bundle {BundleId} '{Name}' created by instructor {InstructorId}. Courses: {CourseCount}, Price: {Price}, Savings: {Savings}%", 
                bundle.Id, bundle.Name, userId, model.CourseIds.Count, model.Price, savingsPercentage);

            SetSuccessMessage($"تم إنشاء الباقة بنجاح. الخصم: {savingsPercentage:F1}%");
            return RedirectToAction(nameof(Details), new { id = bundle.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bundle for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الباقة");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الباقة - Edit bundle
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;
        
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

        if (bundle == null)
            return NotFound();

        var viewModel = new BundleEditViewModel
        {
            Id = bundle.Id,
            Name = bundle.Name,
            ShortDescription = bundle.ShortDescription,
            Description = bundle.Description,
            ThumbnailUrl = bundle.ThumbnailUrl,
            Price = bundle.Price,
            IsActive = bundle.IsActive,
            IsFeatured = bundle.IsFeatured,
            DisplayOrder = bundle.DisplayOrder,
            ValidFrom = bundle.ValidFrom,
            ValidTo = bundle.ValidTo,
            MaxSales = bundle.MaxSales,
            SalesCount = bundle.SalesCount,
            CourseIds = bundle.BundleCourses.Select(bc => bc.CourseId).ToList()
        };

        await PopulateInstructorCoursesDropdown();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الباقة - Save bundle edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BundleEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

        if (bundle == null)
            return NotFound();

        try
        {
            if (!model.CourseIds.Any())
            {
                ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
            }

            // Verify all courses belong to the instructor
            var instructorCourseIds = await _context.Courses
                .Where(c => c.InstructorId == userId && model.CourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (instructorCourseIds.Count != model.CourseIds.Count)
            {
                ModelState.AddModelError("CourseIds", "لا يمكنك إضافة دورات لا تملكها");
            }

            if (!ModelState.IsValid)
            {
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Calculate original price
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var originalPrice = courses.Sum(c => c.DiscountPrice ?? c.Price);

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateBundle(
                model.CourseIds.Count,
                model.Price,
                originalPrice,
                model.ValidFrom,
                model.ValidTo,
                model.MaxSales);

            if (!isValid)
            {
                _logger.LogWarning("Bundle {BundleId} validation failed: {Reason}", id, validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            var savingsPercentage = originalPrice > 0 
                ? ((originalPrice - model.Price) / originalPrice) * 100 
                : 0;

            // Check if bundle has sales - warn if changing critical settings
            if (bundle.SalesCount > 0)
            {
                var criticalChanges = new List<string>();
                
                if (bundle.Price != model.Price)
                    criticalChanges.Add("السعر");
                
                if (bundle.CoursesCount != model.CourseIds.Count)
                    criticalChanges.Add("عدد الدورات");

                if (criticalChanges.Any())
                {
                    _logger.LogInformation("Bundle {BundleId} has sales but critical settings changed: {Changes}", 
                        id, string.Join(", ", criticalChanges));
                    SetWarningMessage($"تحذير: الباقة بها مبيعات سابقة. تم تغيير: {string.Join("، ", criticalChanges)}");
                }
            }

            // Generate unique slug if name changed
            var newSlug = _slugService.GenerateSlug(model.Name);
            if (newSlug != bundle.Slug)
            {
                var slugExists = await _context.CourseBundles
                    .AnyAsync(b => b.Slug == newSlug && b.Id != id);
                if (slugExists)
                {
                    newSlug = $"{newSlug}-{Guid.NewGuid().ToString()[..8]}";
                }
            }

            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                bundle.Name = model.Name;
                bundle.Slug = newSlug;
                bundle.ShortDescription = model.ShortDescription;
                bundle.Description = model.Description;
                bundle.ThumbnailUrl = model.ThumbnailUrl;
                bundle.Price = model.Price;
                bundle.OriginalPrice = originalPrice;
                bundle.SavingsPercentage = savingsPercentage;
                bundle.IsActive = model.IsActive;
                bundle.DisplayOrder = model.DisplayOrder;
                bundle.ValidFrom = model.ValidFrom;
                bundle.ValidTo = model.ValidTo;
                bundle.MaxSales = model.MaxSales;
                bundle.CoursesCount = model.CourseIds.Count;

                // Update bundle courses
                _context.BundleCourses.RemoveRange(bundle.BundleCourses);
                
                foreach (var courseId in model.CourseIds)
                {
                    var bundleCourse = new BundleCourse
                    {
                        BundleId = bundle.Id,
                        CourseId = courseId,
                        DisplayOrder = model.CourseIds.IndexOf(courseId)
                    };
                    _context.BundleCourses.Add(bundleCourse);
                }

                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                SetErrorMessage(error ?? "حدث خطأ أثناء تحديث الباقة");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            _logger.LogInformation(
                "Bundle {BundleId} '{Name}' updated by instructor {InstructorId}. Courses: {CourseCount}, Price: {Price}, Savings: {Savings}%", 
                bundle.Id, bundle.Name, userId, model.CourseIds.Count, model.Price, savingsPercentage);

            SetSuccessMessage($"تم تحديث الباقة بنجاح. الخصم: {savingsPercentage:F1}%");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bundle {BundleId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث الباقة");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
    }

    /// <summary>
    /// حذف الباقة - Delete bundle
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var bundle = await _context.CourseBundles
                .Include(b => b.BundleCourses)
                .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

            if (bundle == null)
            {
                _logger.LogWarning("Bundle {BundleId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Use BusinessRuleHelper for validation
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteWithSales(bundle.SalesCount, bundle.IsActive);

            if (!canDelete)
            {
                _logger.LogWarning("Cannot delete bundle {BundleId}: {Reason}", id, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            var bundleName = bundle.Name;
            var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
            {
                _context.BundleCourses.RemoveRange(bundle.BundleCourses);
                _context.CourseBundles.Remove(bundle);
                await _context.SaveChangesAsync();
            }, _logger);

            if (!success)
            {
                SetErrorMessage(error ?? "حدث خطأ أثناء حذف الباقة");
                return RedirectToAction(nameof(Details), new { id });
            }

            _logger.LogInformation("Bundle {BundleId} '{Name}' deleted by instructor {InstructorId}", 
                id, bundleName, userId);

            SetSuccessMessage($"تم حذف الباقة '{bundleName}' بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bundle {BundleId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الباقة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تفعيل/تعطيل الباقة - Toggle bundle status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var bundle = await _context.CourseBundles
                .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

            if (bundle == null)
            {
                _logger.LogWarning("Bundle {BundleId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            bundle.IsActive = !bundle.IsActive;
            await _context.SaveChangesAsync();

            var statusText = bundle.IsActive ? "تفعيل" : "تعطيل";
            _logger.LogInformation("Bundle {BundleId} status toggled to {Status} by instructor {InstructorId}", 
                id, bundle.IsActive, userId);

            SetSuccessMessage($"تم {statusText} الباقة '{bundle.Name}' بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling bundle {BundleId} status", id);
            SetErrorMessage("حدث خطأ أثناء تحديث حالة الباقة");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات الباقة - Bundle statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
        
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
            .FirstOrDefaultAsync(b => b.Id == id && b.CreatedByUserId == userId);

        if (bundle == null)
            return NotFound();
        
        // Get actual bundle purchases from Payment table
        var bundlePayments = await _context.Payments
            .Where(p => p.BundleId == id && p.Status == Domain.Enums.PaymentStatus.Completed)
            .ToListAsync();
        
        var totalSales = bundlePayments.Count;
        var totalRevenue = bundlePayments.Sum(p => p.TotalAmount);
        
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalSales = totalSales;
        
        // Calculate this month vs last month for growth
        var salesThisMonth = bundlePayments.Count(p => p.CompletedAt >= firstDayOfMonth);
        var salesLastMonth = bundlePayments.Count(p => p.CompletedAt >= firstDayOfLastMonth && p.CompletedAt < firstDayOfMonth);
        ViewBag.SalesThisMonth = salesThisMonth;
        
        var revenueThisMonth = bundlePayments.Where(p => p.CompletedAt >= firstDayOfMonth).Sum(p => p.TotalAmount);
        var revenueLastMonth = bundlePayments.Where(p => p.CompletedAt >= firstDayOfLastMonth && p.CompletedAt < firstDayOfMonth).Sum(p => p.TotalAmount);
        
        // Calculate sales and revenue change percentages
        var salesChange = salesLastMonth > 0 
            ? (int)Math.Round(((salesThisMonth - salesLastMonth) * 100.0 / salesLastMonth)) 
            : (salesThisMonth > 0 ? 100 : 0);
        var revenueChange = revenueLastMonth > 0 
            ? (int)Math.Round(((revenueThisMonth - revenueLastMonth) * 100m / revenueLastMonth)) 
            : (revenueThisMonth > 0 ? 100 : 0);
        
        ViewBag.SalesChange = salesChange;
        ViewBag.RevenueChange = revenueChange;
        
        // Get platform commission rate dynamically
        var platformCommissionRate = await GetPlatformCommissionRateAsync(userId, null);
        var platformCommission = totalRevenue * (platformCommissionRate / 100m);
        var netRevenue = totalRevenue - platformCommission;
        
        ViewBag.GrossRevenue = totalRevenue;
        ViewBag.PlatformCommission = platformCommission;
        ViewBag.NetRevenue = netRevenue;
        ViewBag.PlatformCommissionRate = platformCommissionRate;
        
        // Conversion rate - calculate from actual views if available, otherwise use bundle sales count
        // Note: Views tracking would need to be implemented separately
        var conversionRate = totalSales > 0 ? 100.0 : 0; // Simplified - would need view tracking
        ViewBag.ConversionRate = conversionRate;
        ViewBag.ViewsCount = totalSales; // Placeholder until view tracking is implemented
        
        // Per-course breakdown in bundle
        var courseStatsList = new List<dynamic>();
        decimal totalIndividualValue = 0;
        foreach (var bc in bundle.BundleCourses)
        {
            var courseTitle = bc.Course?.Title ?? await _configService.GetLocalizationAsync("not_specified", "ar", "غير محدد");
            var individualValue = bc.Course?.DiscountPrice ?? bc.Course?.Price ?? 0;
            totalIndividualValue += individualValue;
            courseStatsList.Add(new {
                CourseId = bc.CourseId,
                CourseTitle = courseTitle,
                CoursePrice = bc.Course?.Price ?? 0,
                IndividualValue = individualValue
            });
        }
        ViewBag.CourseStats = courseStatsList;
        ViewBag.TotalIndividualValue = totalIndividualValue;
        ViewBag.BundleSavings = totalIndividualValue - bundle.Price;
        
        // Chart data - Sales per month (last 6 months) - REAL DATA
        var chartLabels = new List<string>();
        var salesChartData = new List<int>();
        var revenueChartData = new List<decimal>();
        var arabicMonths = await _configService.GetMonthNamesAsync("ar");
        
        for (int i = Constants.DisplayLimits.MonthlyChartDataPoints - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var monthName = arabicMonths.TryGetValue(monthStart.Month, out var name) ? name : monthStart.ToString("MMMM");
            chartLabels.Add(monthName);
            
            var monthlyPayments = bundlePayments.Where(p => p.CompletedAt >= monthStart && p.CompletedAt < monthEnd).ToList();
            var monthlySales = monthlyPayments.Count;
            var monthlyRevenue = monthlyPayments.Sum(p => p.TotalAmount);
            
            salesChartData.Add(monthlySales);
            revenueChartData.Add(monthlyRevenue / 1000); // In thousands
        }
        ViewBag.ChartLabels = chartLabels;
        ViewBag.SalesChartData = salesChartData;
        ViewBag.RevenueChartData = revenueChartData;
        
        // Sales distribution - calculate from actual payment sources if available
        // For now, try to get from configuration, but calculate from actual data if possible
        var directSales = bundlePayments.Count(p => string.IsNullOrEmpty(p.AffiliateLinkId?.ToString()));
        var referralSales = bundlePayments.Count(p => p.AffiliateLinkId.HasValue);
        var totalSalesForDistribution = totalSales > 0 ? totalSales : 1;
        
        ViewBag.DirectSalesPercent = (directSales * 100 / totalSalesForDistribution).ToString();
        ViewBag.SearchSalesPercent = "0"; // Would need to track search source
        ViewBag.SocialSalesPercent = "0"; // Would need to track social source
        ViewBag.ReferralSalesPercent = (referralSales * 100 / totalSalesForDistribution).ToString();
        
        // Average time to purchase - calculate from actual payment timestamps
        // This would ideally track time from first view to purchase, but we'll use created to completed time
        var avgTimeToPurchase = bundlePayments
            .Where(p => p.CompletedAt.HasValue && p.CreatedAt != null)
            .Select(p => (p.CompletedAt!.Value - p.CreatedAt).TotalDays)
            .DefaultIfEmpty(0)
            .Average();
        
        ViewBag.AvgTimeToPurchase = (decimal)Math.Round(avgTimeToPurchase, 1);

        return View(bundle);
    }
    
    /// <summary>
    /// Get platform commission rate for instructor
    /// </summary>
    private async Task<decimal> GetPlatformCommissionRateAsync(string? instructorId, int? courseId)
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
        if (courseSetting != null) return courseSetting.PlatformRate;

        var instructorSetting = settings.FirstOrDefault(s => s.Type == "instructor" && s.InstructorId == instructorId);
        if (instructorSetting != null) return instructorSetting.PlatformRate;

        var categorySetting = settings.FirstOrDefault(s => s.Type == "category" && s.CategoryId == categoryId);
        if (categorySetting != null) return categorySetting.PlatformRate;

        var globalSetting = settings.FirstOrDefault(s => s.Type == "global");
        return globalSetting?.PlatformRate ?? Constants.Earnings.DefaultPlatformRate; // Default platform rate if no setting found
    }

    private async Task PopulateInstructorCoursesDropdown()
    {
        var userId = _currentUserService.UserId;
        
        ViewBag.Courses = new MultiSelectList(
            await _context.Courses
                .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");
    }
}

