using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة كوبونات المدرس - Instructor Coupons Controller
/// </summary>
public class CouponsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CouponsController> _logger;

    public CouponsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CouponsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة كوبونات المدرس - Instructor's coupons list
    /// </summary>
    public async Task<IActionResult> Index(CouponStatus? status, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Coupons
            .Where(c => c.CreatedByUserId == userId);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var coupons = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
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

        return View(coupons);
    }

    /// <summary>
    /// تفاصيل الكوبون - Coupon details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        var coupon = await _context.Coupons
            .Include(c => c.Usages)
                .ThenInclude(u => u.User)
            .Include(c => c.Usages)
                .ThenInclude(u => u.Payment)
                    .ThenInclude(p => p.Course)
            .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

        if (coupon == null)
            return NotFound();

        return View(coupon);
    }

    /// <summary>
    /// إنشاء كوبون جديد - Create new coupon
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateInstructorCoursesDropdown();
        return View(new CouponCreateViewModel());
    }

    /// <summary>
    /// حفظ الكوبون الجديد - Save new coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CouponCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }

        var userId = _currentUserService.UserId;

        try
        {
            // Normalize code to uppercase
            model.Code = model.Code.ToUpper().Trim();

            // Check if code already exists
            var existingCoupon = await _context.Coupons
                .AnyAsync(c => c.Code == model.Code);

            if (existingCoupon)
            {
                _logger.LogWarning("Coupon code {Code} already exists", model.Code);
                ModelState.AddModelError(nameof(model.Code), "كود الكوبون مستخدم بالفعل");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Use BusinessRuleHelper for comprehensive validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateCoupon(
                model.DiscountType,
                model.DiscountValue,
                model.MaxDiscountAmount,
                model.MinimumPurchaseAmount,
                model.ValidFrom,
                model.ValidTo,
                model.MaxUses,
                model.MaxUsesPerUser);

            if (!isValid)
            {
                _logger.LogWarning("Coupon validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Validate code format
            if (model.Code.Length < 4 || model.Code.Length > 20)
            {
                ModelState.AddModelError(nameof(model.Code), "كود الكوبون يجب أن يكون بين 4 و 20 حرف");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Code, @"^[A-Z0-9]+$"))
            {
                ModelState.AddModelError(nameof(model.Code), "كود الكوبون يجب أن يحتوي على أحرف إنجليزية كبيرة وأرقام فقط");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Validate selected courses belong to instructor
            if (model.SelectedCourseIds?.Any() == true)
            {
                var instructorCourseIds = await _context.Courses
                    .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
                    .Select(c => c.Id)
                    .ToListAsync();

                var invalidCourseIds = model.SelectedCourseIds.Except(instructorCourseIds).ToList();
                if (invalidCourseIds.Any())
                {
                    _logger.LogWarning("Instructor {UserId} attempted to create coupon for courses they don't own: {CourseIds}", 
                        userId, string.Join(", ", invalidCourseIds));
                    ModelState.AddModelError(nameof(model.SelectedCourseIds), "بعض الدورات المحددة غير صالحة");
                    await PopulateInstructorCoursesDropdown();
                    return View(model);
                }
            }

            Coupon? coupon = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Create applicable course IDs JSON if specific courses selected
                string? applicableCourseIds = null;
                if (model.SelectedCourseIds?.Any() == true)
                {
                    applicableCourseIds = System.Text.Json.JsonSerializer.Serialize(model.SelectedCourseIds);
                }

                coupon = new Coupon
                {
                    Code = model.Code,
                    Description = model.Description,
                    DiscountType = model.DiscountType,
                    DiscountValue = model.DiscountValue,
                    MaxDiscountAmount = model.MaxDiscountAmount,
                    MinimumPurchaseAmount = model.MinimumPurchaseAmount,
                    ApplicableCourseIds = applicableCourseIds,
                    MaxUses = model.MaxUses,
                    MaxUsesPerUser = model.MaxUsesPerUser,
                    ValidFrom = model.ValidFrom,
                    ValidTo = model.ValidTo,
                    FirstPurchaseOnly = model.FirstPurchaseOnly,
                    IsActive = model.IsActive,
                    Status = model.IsActive ? CouponStatus.Active : CouponStatus.Inactive,
                    CreatedByUserId = userId,
                    Currency = "EGP",
                    UsedCount = 0
                };

                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Coupon {CouponId} '{Code}' created by instructor {InstructorId}. Type: {DiscountType}, Value: {DiscountValue}", 
                coupon!.Id, coupon.Code, userId, model.DiscountType, model.DiscountValue);

            SetSuccessMessage($"تم إنشاء الكوبون '{model.Code}' بنجاح");
            return RedirectToAction(nameof(Details), new { id = coupon.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating coupon for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء الكوبون. يرجى المحاولة مرة أخرى");
            await PopulateInstructorCoursesDropdown();
            return View(model);
        }
    }

    /// <summary>
    /// تعديل الكوبون - Edit coupon
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

        if (coupon == null)
            return NotFound();

        var selectedCourseIds = new List<int>();
        if (!string.IsNullOrEmpty(coupon.ApplicableCourseIds))
        {
            selectedCourseIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(coupon.ApplicableCourseIds) ?? new List<int>();
        }

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
            UsedCount = coupon.UsedCount,
            SelectedCourseIds = selectedCourseIds
        };

        await PopulateInstructorCoursesDropdown();
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

        var userId = _currentUserService.UserId;
        
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

        if (coupon == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Check if code changed and already exists
            var normalizedCode = model.Code.ToUpper().Trim();
            if (coupon.Code != normalizedCode)
            {
                var existingCoupon = await _context.Coupons
                    .AnyAsync(c => c.Code == normalizedCode && c.Id != id);

                if (existingCoupon)
                {
                    ModelState.AddModelError("Code", "كود الكوبون مستخدم بالفعل");
                    await PopulateInstructorCoursesDropdown();
                    return View(model);
                }
            }

            // Validate dates
            if (model.ValidTo <= model.ValidFrom)
            {
                ModelState.AddModelError("ValidTo", "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Update applicable course IDs
            string? applicableCourseIds = null;
            if (model.SelectedCourseIds?.Any() == true)
            {
                applicableCourseIds = System.Text.Json.JsonSerializer.Serialize(model.SelectedCourseIds);
            }

            coupon.Code = normalizedCode;
            coupon.Description = model.Description;
            coupon.DiscountType = model.DiscountType;
            coupon.DiscountValue = model.DiscountValue;
            coupon.MaxDiscountAmount = model.MaxDiscountAmount;
            coupon.MinimumPurchaseAmount = model.MinimumPurchaseAmount;
            coupon.ApplicableCourseIds = applicableCourseIds;
            coupon.MaxUses = model.MaxUses;
            coupon.MaxUsesPerUser = model.MaxUsesPerUser;
            coupon.ValidFrom = model.ValidFrom;
            coupon.ValidTo = model.ValidTo;
            coupon.FirstPurchaseOnly = model.FirstPurchaseOnly;
            coupon.IsActive = model.IsActive;
            coupon.Status = model.IsActive ? CouponStatus.Active : CouponStatus.Inactive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الكوبون بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateInstructorCoursesDropdown();
        return View(model);
    }

    /// <summary>
    /// تعطيل/تفعيل الكوبون - Toggle coupon status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var userId = _currentUserService.UserId;
        
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

        if (coupon == null)
            return NotFound();

        coupon.IsActive = !coupon.IsActive;
        coupon.Status = coupon.IsActive ? CouponStatus.Active : CouponStatus.Inactive;

        await _context.SaveChangesAsync();

        SetSuccessMessage(coupon.IsActive ? "تم تفعيل الكوبون" : "تم تعطيل الكوبون");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف الكوبون - Delete coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var coupon = await _context.Coupons
                .Include(c => c.Usages)
                .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

            if (coupon == null)
            {
                _logger.LogWarning("Coupon {CouponId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            // Use BusinessRuleHelper for validation
            var usageCount = coupon.Usages.Count;
            var (canDelete, reason) = BusinessRuleHelper.CanDeleteCoupon(usageCount);

            if (!canDelete)
            {
                _logger.LogWarning("Cannot delete coupon {CouponId}: {Reason}", id, reason);
                SetErrorMessage(reason!);
                return RedirectToAction(nameof(Details), new { id });
            }

            // Additional check: prevent deletion if coupon is currently active
            if (coupon.IsActive && coupon.Status == CouponStatus.Active)
            {
                var now = DateTime.UtcNow;
                if (coupon.ValidFrom <= now && coupon.ValidTo >= now)
                {
                    _logger.LogWarning("Cannot delete active coupon {CouponId} within validity period", id);
                    SetErrorMessage("لا يمكن حذف كوبون نشط. قم بتعطيله أولاً");
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var couponCode = coupon.Code;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Coupon {CouponId} '{Code}' deleted by instructor {InstructorId}", 
                id, couponCode, userId);

            SetSuccessMessage($"تم حذف الكوبون '{couponCode}' بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting coupon {CouponId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف الكوبون");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إحصائيات الكوبون - Coupon statistics
    /// </summary>
    public async Task<IActionResult> Statistics(int id)
    {
        var userId = _currentUserService.UserId;
        
        var coupon = await _context.Coupons
            .Include(c => c.Usages)
                .ThenInclude(u => u.User)
            .Include(c => c.Usages)
                .ThenInclude(u => u.Payment)
                    .ThenInclude(p => p.Course)
            .FirstOrDefaultAsync(c => c.Id == id && c.CreatedByUserId == userId);

        if (coupon == null)
            return NotFound();

        return View(coupon);
    }

    /// <summary>
    /// إنشاء كود عشوائي - Generate random code
    /// </summary>
    [HttpGet]
    public IActionResult GenerateCode()
    {
        var code = GenerateRandomCode();
        return Ok(new { code });
    }

    private string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private async Task PopulateInstructorCoursesDropdown()
    {
        var userId = _currentUserService.UserId;
        
        var courses = await _context.Courses
            .Where(c => c.InstructorId == userId && c.Status == CourseStatus.Published)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.Courses = new MultiSelectList(courses, "Id", "Title");
    }
}

