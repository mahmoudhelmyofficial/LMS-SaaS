using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الكوبونات - Coupons Management Controller
/// </summary>
public class CouponsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CouponsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public CouponsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CouponsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الكوبونات - Coupons list
    /// </summary>
    public async Task<IActionResult> Index(CouponStatus? status, int page = 1)
    {
        var query = _context.Coupons.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("coupons", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var coupons = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CouponDisplayViewModel
            {
                Id = c.Id,
                Code = c.Code,
                Description = c.Description,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                UsedCount = c.UsedCount,
                MaxUses = c.MaxUses,
                ValidFrom = c.ValidFrom,
                ValidTo = c.ValidTo,
                IsActive = c.IsActive,
                Status = c.Status
            })
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        
        // Calculate total discount given
        ViewBag.TotalDiscountGiven = await _context.CouponUsages.SumAsync(u => u.DiscountAmount);

        return View(coupons);
    }

    /// <summary>
    /// تفاصيل الكوبون - Coupon details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var coupon = await _context.Coupons
            .Include(c => c.Usages)
                .ThenInclude(u => u.User)
            .Include(c => c.Usages)
                .ThenInclude(u => u.Payment)
                    .ThenInclude(p => p.Course)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (coupon == null)
            return NotFound();

        return View(coupon);
    }

    /// <summary>
    /// إنشاء كوبون جديد - Create new coupon
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CouponCreateViewModel());
    }

    /// <summary>
    /// حفظ الكوبون الجديد - Save new coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CouponCreateViewModel model)
    {
        try
        {
            if (ModelState.IsValid)
            {
                // Validate coupon code format
                if (string.IsNullOrWhiteSpace(model.Code) || model.Code.Length < 3)
                {
                    ModelState.AddModelError("Code", "كود الكوبون يجب أن يكون 3 أحرف على الأقل");
                    return View(model);
                }

                // Check if code already exists
                var existingCoupon = await _context.Coupons
                    .AnyAsync(c => c.Code == model.Code.ToUpper());

                if (existingCoupon)
                {
                    _logger.LogWarning("Coupon code already exists: {Code}", model.Code);
                    ModelState.AddModelError("Code", "كود الكوبون مستخدم بالفعل");
                    return View(model);
                }

                // Validate dates
                var (isValidDates, dateError) = BusinessRuleHelper.ValidateCouponDates(
                    model.ValidFrom, 
                    model.ValidTo);

                if (!isValidDates)
                {
                    ModelState.AddModelError("ValidTo", dateError!);
                    return View(model);
                }

                // Validate discount value based on type
                if (model.DiscountType == DiscountType.Percentage)
                {
                    if (!ValidationHelper.IsValidPercentage(model.DiscountValue))
                    {
                        ModelState.AddModelError("DiscountValue", "نسبة الخصم يجب أن تكون بين 0 و 100");
                        return View(model);
                    }
                }
                else // Fixed amount
                {
                    if (!ValidationHelper.IsValidAmount(model.DiscountValue))
                    {
                        ModelState.AddModelError("DiscountValue", "قيمة الخصم غير صحيحة");
                        return View(model);
                    }
                    
                    // Fixed discount shouldn't exceed max discount amount
                    if (model.MaxDiscountAmount.HasValue && model.DiscountValue > model.MaxDiscountAmount.Value)
                    {
                        ModelState.AddModelError("DiscountValue", 
                            "قيمة الخصم لا يمكن أن تتجاوز الحد الأقصى للخصم");
                        return View(model);
                    }
                }

                // Validate minimum purchase amount
                if (model.MinimumPurchaseAmount.HasValue && model.MinimumPurchaseAmount.Value < 0)
                {
                    ModelState.AddModelError("MinimumPurchaseAmount", "الحد الأدنى للشراء يجب أن يكون رقم موجب");
                    return View(model);
                }

                // Validate max uses
                if (model.MaxUses.HasValue && model.MaxUses.Value < 1)
                {
                    ModelState.AddModelError("MaxUses", "عدد الاستخدامات يجب أن يكون 1 على الأقل");
                    return View(model);
                }

                if (model.MaxUsesPerUser < 1)
                {
                    ModelState.AddModelError("MaxUsesPerUser", "عدد الاستخدامات لكل مستخدم يجب أن يكون 1 على الأقل");
                    return View(model);
                }

                var coupon = new Coupon
                {
                    Code = model.Code.ToUpper().Trim(),
                    Description = model.Description?.Trim(),
                    DiscountType = model.DiscountType,
                    DiscountValue = model.DiscountValue,
                    MaxDiscountAmount = model.MaxDiscountAmount,
                    MinimumPurchaseAmount = model.MinimumPurchaseAmount,
                    MaxUses = model.MaxUses,
                    MaxUsesPerUser = model.MaxUsesPerUser,
                    ValidFrom = model.ValidFrom,
                    ValidTo = model.ValidTo,
                    FirstPurchaseOnly = model.FirstPurchaseOnly,
                    IsActive = model.IsActive,
                    Status = model.IsActive ? CouponStatus.Active : CouponStatus.Inactive,
                    CreatedByUserId = _currentUserService.UserId,
                    Currency = model.Currency ?? "EGP",
                    UsedCount = 0
                };

                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Coupon {Code} created by admin {AdminId}", 
                    coupon.Code, _currentUserService.UserId);

                SetSuccessMessage(CultureExtensions.T("تم إنشاء الكوبون بنجاح", "Coupon created successfully."));
                return RedirectToAction(nameof(Details), new { id = coupon.Id });
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating coupon");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إنشاء الكوبون", "An error occurred while creating the coupon."));
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الكوبون - Edit coupon
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
            return NotFound();

        var viewModel = new CouponEditViewModel
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Description = coupon.Description,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            MinimumPurchaseAmount = coupon.MinimumPurchaseAmount,
            MaxUses = coupon.MaxUses,
            MaxUsesPerUser = coupon.MaxUsesPerUser,
            ValidFrom = coupon.ValidFrom,
            ValidTo = coupon.ValidTo,
            FirstPurchaseOnly = coupon.FirstPurchaseOnly,
            IsActive = coupon.IsActive,
            UsedCount = coupon.UsedCount
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات الكوبون - Save coupon edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CouponEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        try
        {
            var coupon = await _context.Coupons
                .Include(c => c.Usages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coupon == null)
            {
                _logger.LogWarning("Coupon not found for editing: {CouponId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Prevent editing if coupon has been used and trying to change critical fields
                if (coupon.UsedCount > 0)
                {
                    // Check if trying to reduce max uses below current usage
                    if (model.MaxUses.HasValue && model.MaxUses.Value < coupon.UsedCount)
                    {
                        ModelState.AddModelError("MaxUses", 
                            $"لا يمكن تقليل عدد الاستخدامات إلى أقل من العدد المستخدم ({coupon.UsedCount})");
                        return View(model);
                    }

                    // Prevent changing discount type
                    if (coupon.DiscountType != model.DiscountType)
                    {
                        ModelState.AddModelError("DiscountType", 
                            "لا يمكن تغيير نوع الخصم بعد استخدام الكوبون");
                        return View(model);
                    }

                    // Prevent increasing discount value
                    if (model.DiscountValue > coupon.DiscountValue)
                    {
                        ModelState.AddModelError("DiscountValue", 
                            "لا يمكن زيادة قيمة الخصم بعد استخدام الكوبون");
                        return View(model);
                    }
                }

                // Check if code changed and already exists
                if (coupon.Code != model.Code.ToUpper())
                {
                    var existingCoupon = await _context.Coupons
                        .AnyAsync(c => c.Code == model.Code.ToUpper() && c.Id != id);

                    if (existingCoupon)
                    {
                        _logger.LogWarning("Coupon code already exists: {Code}", model.Code);
                        ModelState.AddModelError("Code", "كود الكوبون مستخدم بالفعل");
                        return View(model);
                    }
                }

                // Validate dates
                var (isValidDates, dateError) = BusinessRuleHelper.ValidateCouponDates(
                    model.ValidFrom, 
                    model.ValidTo);

                if (!isValidDates)
                {
                    ModelState.AddModelError("ValidTo", dateError!);
                    return View(model);
                }

                // Validate discount value
                if (model.DiscountType == DiscountType.Percentage)
                {
                    if (!ValidationHelper.IsValidPercentage(model.DiscountValue))
                    {
                        ModelState.AddModelError("DiscountValue", "نسبة الخصم يجب أن تكون بين 0 و 100");
                        return View(model);
                    }
                }
                else
                {
                    if (!ValidationHelper.IsValidAmount(model.DiscountValue))
                    {
                        ModelState.AddModelError("DiscountValue", "قيمة الخصم غير صحيحة");
                        return View(model);
                    }
                    
                    if (model.MaxDiscountAmount.HasValue && model.DiscountValue > model.MaxDiscountAmount.Value)
                    {
                        ModelState.AddModelError("DiscountValue", 
                            "قيمة الخصم لا يمكن أن تتجاوز الحد الأقصى للخصم");
                        return View(model);
                    }
                }

                // Update coupon
                coupon.Code = model.Code.ToUpper().Trim();
                coupon.Description = model.Description?.Trim();
                coupon.DiscountType = model.DiscountType;
                coupon.DiscountValue = model.DiscountValue;
                coupon.MaxDiscountAmount = model.MaxDiscountAmount;
                coupon.MinimumPurchaseAmount = model.MinimumPurchaseAmount;
                coupon.MaxUses = model.MaxUses;
                coupon.MaxUsesPerUser = model.MaxUsesPerUser;
                coupon.ValidFrom = model.ValidFrom;
                coupon.ValidTo = model.ValidTo;
                coupon.FirstPurchaseOnly = model.FirstPurchaseOnly;
                coupon.IsActive = model.IsActive;
                coupon.Status = model.IsActive ? CouponStatus.Active : CouponStatus.Inactive;

                // Auto-expire if past valid date
                if (DateTime.UtcNow > coupon.ValidTo)
                {
                    coupon.IsActive = false;
                    coupon.Status = CouponStatus.Expired;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Coupon {Code} updated by admin {AdminId}", 
                    coupon.Code, _currentUserService.UserId);

                SetSuccessMessage(CultureExtensions.T("تم تحديث الكوبون بنجاح", "Coupon updated successfully."));
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing coupon {CouponId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحديث الكوبون", "An error occurred while updating the coupon."));
            return View(model);
        }
    }

    /// <summary>
    /// تعطيل/تفعيل الكوبون - Toggle coupon status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
            return NotFound();

        coupon.IsActive = !coupon.IsActive;
        coupon.Status = coupon.IsActive ? CouponStatus.Active : CouponStatus.Inactive;

        await _context.SaveChangesAsync();

        SetSuccessMessage(coupon.IsActive ? CultureExtensions.T("تم تفعيل الكوبون", "Coupon enabled.") : CultureExtensions.T("تم تعطيل الكوبون", "Coupon disabled."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف الكوبون - Delete coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var coupon = await _context.Coupons
                .Include(c => c.Usages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (coupon == null)
            {
                _logger.LogWarning("Coupon not found for deletion: {CouponId}", id);
                return NotFound();
            }

            // Validate if coupon can be deleted
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteCoupon(coupon.Usages.Count);

            if (!canDelete)
            {
                _logger.LogWarning("Coupon {Code} cannot be deleted: {Reason}", coupon.Code, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            var couponCode = coupon.Code;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Remove coupon
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Coupon {Code} deleted by admin {AdminId}", 
                couponCode, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم حذف الكوبون بنجاح", "Coupon deleted successfully."));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting coupon {CouponId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء حذف الكوبون", "An error occurred while deleting the coupon."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إنشاء كود عشوائي - Generate random code (POST only for CSRF protection)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GenerateCode()
    {
        var code = GenerateRandomCode();
        return Ok(new { code });
    }

    /// <summary>
    /// تفعيل الكوبونات المنتهية - Expire old coupons (scheduled task endpoint)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExpireOldCoupons()
    {
        try
        {
            var expiredCoupons = await _context.Coupons
                .Where(c => c.IsActive && c.ValidTo < DateTime.UtcNow)
                .ToListAsync();

            foreach (var coupon in expiredCoupons)
            {
                coupon.IsActive = false;
                coupon.Status = CouponStatus.Expired;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Expired {Count} coupons", expiredCoupons.Count);
            SetSuccessMessage(string.Format(CultureExtensions.T("تم تعطيل {0} كوبون منتهي الصلاحية", "{0} expired coupon(s) disabled."), expiredCoupons.Count));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error expiring coupons");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تعطيل الكوبونات", "An error occurred while disabling coupons."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تفعيل كوبونات متعددة - Bulk activate coupons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkActivate(int[] ids)
    {
        if (ids == null || !ids.Any())
        {
            SetErrorMessage(CultureExtensions.T("لم يتم تحديد أي كوبونات", "No coupons selected."));
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var coupons = await _context.Coupons
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            var activatedCount = 0;
            foreach (var coupon in coupons)
            {
                // Only activate if not expired
                if (coupon.ValidTo >= DateTime.UtcNow)
                {
                    coupon.IsActive = true;
                    coupon.Status = CouponStatus.Active;
                    activatedCount++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bulk activated {Count} coupons by admin {AdminId}", 
                activatedCount, _currentUserService.UserId);

            SetSuccessMessage(string.Format(CultureExtensions.T("تم تفعيل {0} كوبون", "{0} coupon(s) enabled."), activatedCount));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk activate");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء التفعيل", "An error occurred while enabling."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تعطيل كوبونات متعددة - Bulk deactivate coupons
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDeactivate(int[] ids)
    {
        if (ids == null || !ids.Any())
        {
            SetErrorMessage(CultureExtensions.T("لم يتم تحديد أي كوبونات", "No coupons selected."));
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var coupons = await _context.Coupons
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            foreach (var coupon in coupons)
            {
                coupon.IsActive = false;
                coupon.Status = CouponStatus.Inactive;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bulk deactivated {Count} coupons by admin {AdminId}", 
                coupons.Count, _currentUserService.UserId);

            SetSuccessMessage(string.Format(CultureExtensions.T("تم تعطيل {0} كوبون", "{0} coupon(s) disabled."), coupons.Count));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk deactivate");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء التعطيل", "An error occurred while disabling."));
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إحصائيات الكوبونات - Coupon statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var stats = new
            {
                TotalCoupons = await _context.Coupons.CountAsync(),
                ActiveCoupons = await _context.Coupons.CountAsync(c => c.IsActive),
                ExpiredCoupons = await _context.Coupons.CountAsync(c => c.ValidTo < DateTime.UtcNow),
                UsedCoupons = await _context.Coupons.CountAsync(c => c.UsedCount > 0),
                TotalUsageCount = await _context.CouponUsages.CountAsync(),
                TotalDiscountGiven = await _context.CouponUsages.SumAsync(u => u.DiscountAmount),
                TopCoupons = await _context.Coupons
                    .OrderByDescending(c => c.UsedCount)
                    .Take(10)
                    .Select(c => new { c.Id, c.Code, c.UsedCount, c.DiscountValue, c.DiscountType })
                    .ToListAsync()
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading coupon statistics");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics."));
            return RedirectToAction(nameof(Index));
        }
    }

    private string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

