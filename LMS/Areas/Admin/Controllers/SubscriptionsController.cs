using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الاشتراكات - Subscriptions Management Controller
/// </summary>
public class SubscriptionsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public SubscriptionsController(
        ApplicationDbContext context,
        ILogger<SubscriptionsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// إنشاء اشتراك جديد - Create new subscription (redirect to CreatePlan)
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        // Redirect to CreatePlan as subscriptions are created through plans
        return RedirectToAction(nameof(CreatePlan));
    }

    /// <summary>
    /// قائمة خطط الاشتراك - Subscription plans list
    /// </summary>
    public async Task<IActionResult> Plans()
    {
        var plans = await _context.SubscriptionPlans
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        // Get total active subscribers count
        ViewBag.TotalSubscribers = await _context.Subscriptions
            .CountAsync(s => s.Status == "Active");

        return View(plans);
    }

    /// <summary>
    /// إنشاء خطة جديدة - Create new plan
    /// </summary>
    [HttpGet]
    public IActionResult CreatePlan()
    {
        return View(new SubscriptionPlanCreateViewModel());
    }

    /// <summary>
    /// حفظ الخطة الجديدة - Save new plan
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(SubscriptionPlanCreateViewModel model)
    {
        // Business validation
        if (model.TrialPeriodDays > model.DurationDays)
            ModelState.AddModelError(nameof(model.TrialPeriodDays), "فترة التجربة لا يمكن أن تتجاوز فترة الاشتراك");

        var existingName = await _context.SubscriptionPlans
            .AnyAsync(p => p.Name.Trim().ToLower() == model.Name.Trim().ToLower());
        if (existingName)
            ModelState.AddModelError(nameof(model.Name), "اسم الخطة موجود مسبقاً");

        if (ModelState.IsValid)
        {
            var plan = new SubscriptionPlan
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                Currency = "EGP",
                DurationDays = model.DurationDays,
                CoursesLimit = model.CoursesLimit,
                AccessToPremiumContent = model.AccessToPremiumContent,
                AccessToLiveClasses = model.AccessToLiveClasses,
                PrioritySupport = model.PrioritySupport,
                CertificatesIncluded = model.CertificatesIncluded,
                TrialPeriodDays = model.TrialPeriodDays,
                IsActive = model.IsActive,
                IsFeatured = model.IsFeatured,
                DisplayOrder = model.DisplayOrder
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم إنشاء خطة الاشتراك بنجاح", "Subscription plan created successfully."));
            return RedirectToAction(nameof(Plans));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل الخطة - Edit plan
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditPlan(int id)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
            return NotFound();

        var activeSubscribers = await _context.Subscriptions
            .CountAsync(s => s.PlanId == id && s.Status == "Active");

        var viewModel = new SubscriptionPlanEditViewModel
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            DurationDays = plan.DurationDays,
            CoursesLimit = plan.CoursesLimit,
            AccessToPremiumContent = plan.AccessToPremiumContent,
            AccessToLiveClasses = plan.AccessToLiveClasses,
            PrioritySupport = plan.PrioritySupport,
            CertificatesIncluded = plan.CertificatesIncluded,
            TrialPeriodDays = plan.TrialPeriodDays,
            IsActive = plan.IsActive,
            IsFeatured = plan.IsFeatured,
            DisplayOrder = plan.DisplayOrder,
            ActiveSubscribersCount = activeSubscribers
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الخطة - Save plan edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPlan(int id, SubscriptionPlanEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
            return NotFound();

        if (model.TrialPeriodDays > model.DurationDays)
            ModelState.AddModelError(nameof(model.TrialPeriodDays), "فترة التجربة لا يمكن أن تتجاوز فترة الاشتراك");

        var existingName = await _context.SubscriptionPlans
            .AnyAsync(p => p.Id != id && p.Name.Trim().ToLower() == model.Name.Trim().ToLower());
        if (existingName)
            ModelState.AddModelError(nameof(model.Name), "اسم الخطة موجود مسبقاً");

        if (ModelState.IsValid)
        {
            plan.Name = model.Name;
            plan.Description = model.Description;
            plan.Price = model.Price;
            plan.DurationDays = model.DurationDays;
            plan.CoursesLimit = model.CoursesLimit;
            plan.AccessToPremiumContent = model.AccessToPremiumContent;
            plan.AccessToLiveClasses = model.AccessToLiveClasses;
            plan.PrioritySupport = model.PrioritySupport;
            plan.CertificatesIncluded = model.CertificatesIncluded;
            plan.TrialPeriodDays = model.TrialPeriodDays;
            plan.IsActive = model.IsActive;
            plan.IsFeatured = model.IsFeatured;
            plan.DisplayOrder = model.DisplayOrder;

            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم تحديث خطة الاشتراك بنجاح", "Subscription plan updated successfully."));
            return RedirectToAction(nameof(Plans));
        }

        return View(model);
    }

    /// <summary>
    /// قائمة الاشتراكات - Subscriptions list
    /// </summary>
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        var query = _context.Subscriptions
            .Include(s => s.User)
            .Include(s => s.Plan)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(s => s.Status == status);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("subscriptions", 20);
        var totalCount = await query.CountAsync();
        var subscriptions = await query
            .OrderByDescending(s => s.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubscriptionDisplayViewModel
            {
                Id = s.Id,
                StudentName = s.User != null 
                    ? $"{s.User.FirstName ?? ""} {s.User.LastName ?? ""}".Trim()
                    : "Unknown User",
                StudentEmail = s.User.Email ?? string.Empty,
                PlanName = s.Plan.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Status = s.Status,
                Price = s.Plan.Price,
                Currency = s.Plan.Currency,
                IsAutoRenew = s.IsAutoRenew
            })
            .ToListAsync();

        // Load subscription plans for the pricing cards section
        var plans = await _context.SubscriptionPlans
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
        
        // Get subscriber counts for each plan
        var planSubscriberCounts = await _context.Subscriptions
            .Where(s => s.Status == "Active")
            .GroupBy(s => s.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count);

        ViewBag.Plans = plans;
        ViewBag.PlanSubscriberCounts = planSubscriberCounts;
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(subscriptions);
    }

    /// <summary>
    /// تفاصيل الاشتراك - Subscription details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.User)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subscription == null)
            return NotFound();

        return View(subscription);
    }

    /// <summary>
    /// إلغاء الاشتراك - Cancel subscription
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        var subscription = await _context.Subscriptions.FindAsync(id);
        if (subscription == null)
            return NotFound();

        subscription.Status = "Cancelled";
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = reason;
        subscription.IsAutoRenew = false;

        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم إلغاء الاشتراك", "Subscription cancelled."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف الخطة - Delete subscription plan
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(int id)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan == null)
            {
                _logger.LogWarning("Subscription plan {PlanId} not found for deletion", id);
                return NotFound();
            }

            // Check if plan has active subscribers
            var activeSubscribers = await _context.Subscriptions
                .CountAsync(s => s.PlanId == id && s.Status == "Active");

            if (activeSubscribers > 0)
            {
                _logger.LogWarning("Cannot delete plan {PlanId} with {Count} active subscribers", id, activeSubscribers);
                SetErrorMessage(string.Format(CultureExtensions.T("لا يمكن حذف هذه الخطة لأنها تحتوي على {0} مشترك نشط. يرجى إلغاء الاشتراكات أولاً أو تعطيل الخطة.", "Cannot delete this plan because it has {0} active subscriber(s). Please cancel subscriptions first or disable the plan."), activeSubscribers));
                return RedirectToAction(nameof(Plans));
            }

            // Check for any subscriptions (including inactive)
            var totalSubscriptions = await _context.Subscriptions.CountAsync(s => s.PlanId == id);
            if (totalSubscriptions > 0)
            {
                // Soft delete - just mark as inactive
                plan.IsActive = false;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Subscription plan {PlanId} deactivated (has {Count} historical subscriptions)", id, totalSubscriptions);
                SetWarningMessage(string.Format(CultureExtensions.T("تم تعطيل الخطة بدلاً من حذفها لأنها تحتوي على {0} اشتراك سابق.", "Plan disabled instead of deleted because it has {0} previous subscription(s)."), totalSubscriptions));
                return RedirectToAction(nameof(Plans));
            }

            _context.SubscriptionPlans.Remove(plan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan {PlanId} deleted successfully", id);
            SetSuccessMessage(CultureExtensions.T("تم حذف خطة الاشتراك بنجاح", "Subscription plan deleted successfully."));
            return RedirectToAction(nameof(Plans));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription plan {PlanId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء حذف الخطة", "An error occurred while deleting the plan."));
            return RedirectToAction(nameof(Plans));
        }
    }
}

