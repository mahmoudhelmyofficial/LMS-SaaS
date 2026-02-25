using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Marketing;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة روابط الأفلييت للمدرس - Instructor Affiliate Links Controller
/// </summary>
public class AffiliatesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;
    private readonly ILogger<AffiliatesController> _logger;

    public AffiliatesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService,
        ILogger<AffiliatesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة روابط الأفلييت - Affiliate links list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, bool? isActive, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.AffiliateLinks
            .Include(a => a.Course)
            .Where(a => a.AffiliateUserId == userId);

        if (courseId.HasValue)
        {
            query = query.Where(a => a.CourseId == courseId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("affiliates", 20);
        var links = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.IsActive = isActive;
        ViewBag.Page = page;

        await PopulateInstructorCoursesDropdown();
        return View(links);
    }

    /// <summary>
    /// تفاصيل رابط الأفلييت - Affiliate link details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var link = await _context.AffiliateLinks
            .Include(a => a.Course)
            .Include(a => a.Commissions)
                .ThenInclude(c => c.Payment)
                    .ThenInclude(p => p.Student)
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateUserId == userId);

        if (link == null)
            return NotFound();

        return View(link);
    }

    /// <summary>
    /// إنشاء رابط أفلييت جديد - Create new affiliate link
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateInstructorCoursesDropdown();
        return View(new AffiliateLinkCreateViewModel());
    }

    /// <summary>
    /// حفظ رابط الأفلييت الجديد - Save new affiliate link
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AffiliateLinkCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (ModelState.IsValid)
        {
            // Verify course belongs to instructor if specified
            if (model.CourseId.HasValue)
            {
                var courseExists = await _context.Courses
                    .AnyAsync(c => c.Id == model.CourseId.Value && c.InstructorId == userId);

                if (!courseExists)
                {
                    ModelState.AddModelError("CourseId", "الدورة غير موجودة أو لا تملكها");
                    await PopulateInstructorCoursesDropdown();
                    return View(model);
                }
            }

            // Generate unique affiliate code
            var affiliateCode = GenerateAffiliateCode();
            while (await _context.AffiliateLinks.AnyAsync(a => a.AffiliateCode == affiliateCode))
            {
                affiliateCode = GenerateAffiliateCode();
            }

            var link = new AffiliateLink
            {
                AffiliateUserId = userId,
                CourseId = model.CourseId,
                AffiliateCode = affiliateCode,
                CampaignName = model.CampaignName,
                Description = model.Description,
                CommissionRate = model.CommissionRate,
                CommissionType = model.CommissionType,
                FixedCommission = model.FixedCommission,
                ValidFrom = model.ValidFrom,
                ValidTo = model.ValidTo,
                IsActive = model.IsActive,
                CookieDurationDays = model.CookieDurationDays
            };

            _context.AffiliateLinks.Add(link);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء رابط الأفلييت بنجاح");
            return RedirectToAction(nameof(Details), new { id = link.Id });
        }

        await PopulateInstructorCoursesDropdown();
        return View(model);
    }

    /// <summary>
    /// تعديل رابط الأفلييت - Edit affiliate link
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var link = await _context.AffiliateLinks
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateUserId == userId);

        if (link == null)
            return NotFound();

        var viewModel = new AffiliateLinkEditViewModel
        {
            Id = link.Id,
            CourseId = link.CourseId,
            CampaignName = link.CampaignName,
            Description = link.Description,
            CommissionRate = link.CommissionRate,
            CommissionType = link.CommissionType,
            FixedCommission = link.FixedCommission,
            ValidFrom = link.ValidFrom,
            ValidTo = link.ValidTo,
            IsActive = link.IsActive,
            CookieDurationDays = link.CookieDurationDays
        };

        await PopulateInstructorCoursesDropdown();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات رابط الأفلييت - Save affiliate link edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AffiliateLinkEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        var link = await _context.AffiliateLinks
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateUserId == userId);

        if (link == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Verify course belongs to instructor if specified
            if (model.CourseId.HasValue)
            {
                var courseExists = await _context.Courses
                    .AnyAsync(c => c.Id == model.CourseId.Value && c.InstructorId == userId);

                if (!courseExists)
                {
                    ModelState.AddModelError("CourseId", "الدورة غير موجودة أو لا تملكها");
                    await PopulateInstructorCoursesDropdown();
                    return View(model);
                }
            }

            link.CourseId = model.CourseId;
            link.CampaignName = model.CampaignName;
            link.Description = model.Description;
            link.CommissionRate = model.CommissionRate;
            link.CommissionType = model.CommissionType;
            link.FixedCommission = model.FixedCommission;
            link.ValidFrom = model.ValidFrom;
            link.ValidTo = model.ValidTo;
            link.IsActive = model.IsActive;
            link.CookieDurationDays = model.CookieDurationDays;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث رابط الأفلييت بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateInstructorCoursesDropdown();
        return View(model);
    }

    /// <summary>
    /// تفعيل/تعطيل الرابط - Toggle link status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var userId = _currentUserService.UserId;

        var link = await _context.AffiliateLinks
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateUserId == userId);

        if (link == null)
            return NotFound();

        link.IsActive = !link.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(link.IsActive ? "تم تفعيل الرابط" : "تم تعطيل الرابط");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف رابط الأفلييت - Delete affiliate link
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var link = await _context.AffiliateLinks
            .Include(a => a.Commissions)
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateUserId == userId);

        if (link == null)
            return NotFound();

        if (link.Commissions.Any())
        {
            SetErrorMessage("لا يمكن حذف الرابط لأنه يحتوي على عمولات مسجلة");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.AffiliateLinks.Remove(link);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف رابط الأفلييت بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات الأفلييت - Affiliate statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var userId = _currentUserService.UserId;

        // Validate user
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Statistics: UserId is null or empty");
            SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var now = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

            // Get all affiliate links for the instructor
            var links = await _context.AffiliateLinks
                .Include(a => a.Course)
                .Include(a => a.Commissions)
                .Where(a => a.AffiliateUserId == userId)
                .ToListAsync();

            var totalLinks = links.Count;
            var activeLinks = links.Count(a => a.IsActive);
            var totalClicks = links.Sum(a => a.ClickCount);
            var totalConversions = links.Sum(a => a.ConversionCount);
            var totalEarnings = links.Sum(a => a.TotalEarnings);
            var pendingEarnings = links.Sum(a => a.PendingEarnings);
            var paidEarnings = links.Sum(a => a.PaidEarnings);
            var conversionRate = totalClicks > 0 ? (decimal)totalConversions / totalClicks * 100 : 0;

        ViewBag.TotalLinks = totalLinks;
        ViewBag.ActiveLinks = activeLinks;
        ViewBag.TotalClicks = totalClicks;
        ViewBag.TotalConversions = totalConversions;
        ViewBag.ConversionRate = conversionRate;
        ViewBag.TotalEarnings = totalEarnings;
        ViewBag.PendingEarnings = pendingEarnings;
        ViewBag.PaidEarnings = paidEarnings;
        
        // Calculate change metrics
        var newLinksThisMonth = links.Count(a => a.CreatedAt >= firstDayOfMonth);
        var newLinksLastMonth = links.Count(a => a.CreatedAt >= firstDayOfLastMonth && a.CreatedAt < firstDayOfMonth);
        ViewBag.NewLinksChange = newLinksThisMonth - newLinksLastMonth;
        
        // Get commissions this month
        var commissionsThisMonth = links.SelectMany(a => a.Commissions)
            .Where(c => c.CreatedAt >= firstDayOfMonth)
            .Sum(c => c.CommissionAmount);
        var commissionsLastMonth = links.SelectMany(a => a.Commissions)
            .Where(c => c.CreatedAt >= firstDayOfLastMonth && c.CreatedAt < firstDayOfMonth)
            .Sum(c => c.CommissionAmount);
        
        var earningsChange = commissionsLastMonth > 0 
            ? ((commissionsThisMonth - commissionsLastMonth) / commissionsLastMonth) * 100 
            : (commissionsThisMonth > 0 ? 100 : 0);
        ViewBag.EarningsChange = earningsChange;
        
        // Calculate clicks change
        // Note: Would need click tracking table for accurate data
        ViewBag.ClicksThisMonth = totalClicks; // Simplified
        ViewBag.ConversionsThisMonth = totalConversions; // Simplified
        
        // Top performing links (null-coalesce localization to prevent view errors)
        var topLinksLimit = await _configService.GetTopItemsLimitAsync("analytics_top_courses", Constants.DisplayLimits.TopCoursesOnAnalytics);
        var allCourses = await _configService.GetLocalizationAsync("all_courses", "ar", "جميع الدورات") ?? "جميع الدورات";
        var topLinks = links
            .OrderByDescending(a => a.TotalEarnings)
            .Take(topLinksLimit)
            .Select(a => new {
                a.Id,
                a.CampaignName,
                a.AffiliateCode,
                CourseName = a.Course?.Title ?? allCourses,
                a.ClickCount,
                a.ConversionCount,
                ConversionRate = a.ClickCount > 0 ? (a.ConversionCount * 100.0 / a.ClickCount) : 0,
                a.TotalEarnings
            })
            .ToList();
        ViewBag.TopLinks = topLinks;
        
        // Chart data - Performance over last 6 months (using real historical data)
        var chartLabels = new List<string>();
        var clicksChartData = new List<int>();
        var conversionsChartData = new List<int>();
        var arabicMonths = await _configService.GetMonthNamesAsync("ar") ?? new Dictionary<int, string>();
        
        // Get real historical data from commissions
        var linkIds = links.Select(l => l.Id).ToList();
        var historicalCommissions = await _context.AffiliateCommissions
            .Where(c => linkIds.Contains(c.AffiliateLinkId))
            .GroupBy(c => new { Year = c.CreatedAt.Year, Month = c.CreatedAt.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Conversions = g.Count(),
                // Note: Clicks would need to be tracked separately in a click tracking table
                // For now, we'll use conversions as a proxy or leave clicks at 0
                Clicks = 0 // Would need click tracking table for real data
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();
        
        // Build chart data for last 6 months
        for (int i = Constants.DisplayLimits.MonthlyChartDataPoints - 1; i >= 0; i--)
        {
            var monthDate = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthName = arabicMonths.TryGetValue(monthDate.Month, out var name) ? name : monthDate.ToString("MMMM");
            chartLabels.Add(monthName);
            
            // Find matching historical data
            var monthData = historicalCommissions.FirstOrDefault(h => h.Year == monthDate.Year && h.Month == monthDate.Month);
            clicksChartData.Add(monthData?.Clicks ?? 0);
            conversionsChartData.Add(monthData?.Conversions ?? 0);
        }
        ViewBag.ChartLabels = chartLabels;
        ViewBag.ClicksChartData = clicksChartData;
        ViewBag.ConversionsChartData = conversionsChartData;
        
        // Recent commissions (null-coalesce to prevent string.Format or display errors)
        var recentTransactionsLimit = await _configService.GetTopItemsLimitAsync("earnings_recent_transactions", Constants.DisplayLimits.RecentTransactionsOnEarnings);
        var notSpecified = await _configService.GetLocalizationAsync("not_specified", "ar", "غير محدد") ?? "غير محدد";
        var recentCommissions = links
            .SelectMany(a => a.Commissions.Select(c => new { 
                Link = a, 
                Commission = c 
            }))
            .OrderByDescending(x => x.Commission.CreatedAt)
            .Take(recentTransactionsLimit)
            .Select(x => new {
                CampaignName = x.Link.CampaignName,
                CourseName = x.Link.Course?.Title ?? notSpecified,
                Amount = x.Commission.CommissionAmount,
                Status = x.Commission.Status,
                Date = x.Commission.CreatedAt
            })
            .ToList();
        ViewBag.RecentCommissions = recentCommissions;

            _logger.LogInformation("Instructor {InstructorId} viewed affiliate statistics. Total links: {TotalLinks}, Active: {ActiveLinks}", 
                userId, totalLinks, activeLinks);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading affiliate statistics for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل إحصائيات الأفلييت.");
            
            // Return view with default values
            ViewBag.TotalLinks = 0;
            ViewBag.ActiveLinks = 0;
            ViewBag.TotalClicks = 0;
            ViewBag.TotalConversions = 0;
            ViewBag.ConversionRate = 0m;
            ViewBag.TotalEarnings = 0m;
            ViewBag.PendingEarnings = 0m;
            ViewBag.PaidEarnings = 0m;
            ViewBag.NewLinksChange = 0;
            ViewBag.EarningsChange = 0m;
            ViewBag.ClicksThisMonth = 0;
            ViewBag.ConversionsThisMonth = 0;
            ViewBag.TopLinks = new List<object>();
            ViewBag.ChartLabels = new List<string>();
            ViewBag.ClicksChartData = new List<int>();
            ViewBag.ConversionsChartData = new List<int>();
            ViewBag.RecentCommissions = new List<object>();
            
            return View();
        }
    }

    /// <summary>
    /// عرض العمولات - View commissions
    /// </summary>
    public async Task<IActionResult> Commissions(int? linkId, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.AffiliateCommissions
            .Include(c => c.AffiliateLink)
            .Include(c => c.Payment)
                .ThenInclude(p => p.Student)
            .Include(c => c.Course)
            .Where(c => c.AffiliateLink.AffiliateUserId == userId);

        if (linkId.HasValue)
        {
            query = query.Where(c => c.AffiliateLinkId == linkId.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("affiliates_commissions", 20);
        var commissions = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.LinkId = linkId;
        ViewBag.Page = page;

        return View(commissions);
    }

    private string GenerateAffiliateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 10)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private async Task PopulateInstructorCoursesDropdown()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = new SelectList(
            await _context.Courses
                .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");
    }
}

