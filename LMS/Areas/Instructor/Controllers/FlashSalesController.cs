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
/// إدارة العروض السريعة للمدرس - Instructor Flash Sales Controller
/// </summary>
public class FlashSalesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FlashSalesController> _logger;

    public FlashSalesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FlashSalesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة العروض السريعة - Flash sales list
    /// </summary>
    public async Task<IActionResult> Index(bool? isActive, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.FlashSales
            .Include(f => f.Course)
            .Where(f => f.Course!.InstructorId == userId);

        if (isActive.HasValue)
        {
            query = query.Where(f => f.IsActive == isActive.Value);
        }

        var flashSales = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.IsActive = isActive;
        ViewBag.Page = page;

        return View(flashSales);
    }

    /// <summary>
    /// تفاصيل العرض السريع - Flash sale details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var flashSale = await _context.FlashSales
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (flashSale == null)
            return NotFound();

        return View(flashSale);
    }

    /// <summary>
    /// إنشاء عرض سريع جديد - Create new flash sale
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateInstructorCoursesDropdown();
        return View(new FlashSaleCreateViewModel());
    }

    /// <summary>
    /// حفظ العرض السريع الجديد - Save new flash sale
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FlashSaleCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (ModelState.IsValid)
        {
            // Verify course belongs to instructor
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
            {
                ModelState.AddModelError("CourseId", "الدورة غير موجودة أو لا تملكها");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Validate dates
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError("EndDate", "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Validate discount
            if (model.DiscountPrice >= course.Price)
            {
                ModelState.AddModelError("DiscountPrice", "سعر العرض يجب أن يكون أقل من السعر الأصلي");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Check for overlapping flash sales
            var hasOverlap = await _context.FlashSales
                .AnyAsync(f => f.CourseId == model.CourseId &&
                    f.IsActive &&
                    ((model.StartDate >= f.StartDate && model.StartDate < f.EndDate) ||
                     (model.EndDate > f.StartDate && model.EndDate <= f.EndDate) ||
                     (model.StartDate <= f.StartDate && model.EndDate >= f.EndDate)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "يوجد عرض سريع نشط آخر على هذه الدورة في نفس الفترة");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            var discountPercentage = ((course.Price - model.DiscountPrice) / course.Price) * 100;

            var flashSale = new FlashSale
            {
                CourseId = model.CourseId,
                Name = model.Name,
                Description = model.Description,
                OriginalPrice = course.Price,
                DiscountPrice = model.DiscountPrice,
                DiscountPercentage = discountPercentage,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxQuantity = model.MaxQuantity,
                SoldQuantity = 0,
                IsActive = model.IsActive && model.StartDate <= DateTime.UtcNow && model.EndDate > DateTime.UtcNow,
                ShowTimer = model.ShowTimer,
                Priority = model.Priority
            };

            _context.FlashSales.Add(flashSale);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء العرض السريع بنجاح");
            return RedirectToAction(nameof(Details), new { id = flashSale.Id });
        }

        await PopulateInstructorCoursesDropdown();
        return View(model);
    }

    /// <summary>
    /// تعديل العرض السريع - Edit flash sale
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var flashSale = await _context.FlashSales
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (flashSale == null)
            return NotFound();

        var viewModel = new FlashSaleEditViewModel
        {
            Id = flashSale.Id,
            CourseId = flashSale.CourseId ?? 0,
            Name = flashSale.Name,
            Description = flashSale.Description,
            DiscountPrice = flashSale.DiscountPrice,
            StartDate = flashSale.StartDate,
            EndDate = flashSale.EndDate,
            MaxQuantity = flashSale.MaxQuantity,
            SoldQuantity = flashSale.SoldQuantity,
            IsActive = flashSale.IsActive,
            ShowTimer = flashSale.ShowTimer,
            Priority = flashSale.Priority
        };

        await PopulateInstructorCoursesDropdown();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات العرض السريع - Save flash sale edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FlashSaleEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        var flashSale = await _context.FlashSales
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (flashSale == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Validate dates
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError("EndDate", "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Validate discount
            if (model.DiscountPrice >= flashSale.Course!.Price)
            {
                ModelState.AddModelError("DiscountPrice", "سعر العرض يجب أن يكون أقل من السعر الأصلي");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            // Check for overlapping flash sales (excluding current one)
            var hasOverlap = await _context.FlashSales
                .AnyAsync(f => f.Id != id && 
                    f.CourseId == flashSale.CourseId &&
                    f.IsActive &&
                    ((model.StartDate >= f.StartDate && model.StartDate < f.EndDate) ||
                     (model.EndDate > f.StartDate && model.EndDate <= f.EndDate) ||
                     (model.StartDate <= f.StartDate && model.EndDate >= f.EndDate)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "يوجد عرض سريع نشط آخر على هذه الدورة في نفس الفترة");
                await PopulateInstructorCoursesDropdown();
                return View(model);
            }

            var discountPercentage = ((flashSale.Course.Price - model.DiscountPrice) / flashSale.Course.Price) * 100;

            flashSale.Name = model.Name;
            flashSale.Description = model.Description;
            flashSale.DiscountPrice = model.DiscountPrice;
            flashSale.DiscountPercentage = discountPercentage;
            flashSale.StartDate = model.StartDate;
            flashSale.EndDate = model.EndDate;
            flashSale.MaxQuantity = model.MaxQuantity;
            flashSale.IsActive = model.IsActive && model.StartDate <= DateTime.UtcNow && model.EndDate > DateTime.UtcNow;
            flashSale.ShowTimer = model.ShowTimer;
            flashSale.Priority = model.Priority;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث العرض السريع بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateInstructorCoursesDropdown();
        return View(model);
    }

    /// <summary>
    /// تفعيل/تعطيل العرض - Toggle sale status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var userId = _currentUserService.UserId;

        var flashSale = await _context.FlashSales
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (flashSale == null)
            return NotFound();

        flashSale.IsActive = !flashSale.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(flashSale.IsActive ? "تم تفعيل العرض" : "تم تعطيل العرض");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// حذف العرض السريع - Delete flash sale
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var flashSale = await _context.FlashSales
            .Include(f => f.Course)
            .FirstOrDefaultAsync(f => f.Id == id && f.Course!.InstructorId == userId);

        if (flashSale == null)
            return NotFound();

        if (flashSale.SoldQuantity > 0)
        {
            SetErrorMessage("لا يمكن حذف العرض لأنه تم استخدامه من قبل");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.FlashSales.Remove(flashSale);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف العرض السريع بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات العروض السريعة - Flash sales statistics
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
            var instructorFlashSalesQuery = _context.FlashSales
                .Include(f => f.Course)
                .Where(f => f.Course != null && f.Course.InstructorId == userId);

            var totalSales = await instructorFlashSalesQuery.CountAsync();

            var activeSales = await _context.FlashSales
                .Include(f => f.Course)
                .CountAsync(f => f.Course != null &&
                    f.Course.InstructorId == userId &&
                    f.IsActive &&
                    f.StartDate <= DateTime.UtcNow &&
                    f.EndDate > DateTime.UtcNow);

            var flashSalesData = await instructorFlashSalesQuery
                .Select(f => new { f.UsedCount, f.DiscountPrice })
                .ToListAsync();

            var totalRevenue = flashSalesData.Sum(f => f.UsedCount * f.DiscountPrice);
            var totalSoldQuantity = flashSalesData.Sum(f => f.UsedCount);

            ViewBag.TotalSales = totalSales;
            ViewBag.ActiveSales = activeSales;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalSoldQuantity = totalSoldQuantity;

            _logger.LogInformation("Instructor {InstructorId} viewed flash sales statistics. Total: {TotalSales}, Active: {ActiveSales}", 
                userId, totalSales, activeSales);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading flash sales statistics for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل إحصائيات العروض السريعة.");
            
            // Return view with default values
            ViewBag.TotalSales = 0;
            ViewBag.ActiveSales = 0;
            ViewBag.TotalRevenue = 0m;
            ViewBag.TotalSoldQuantity = 0;
            
            return View();
        }
    }

    private async Task PopulateInstructorCoursesDropdown()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = new SelectList(
            await _context.Courses
                .Where(c => c.InstructorId == userId && 
                    c.Status == CourseStatus.Published && 
                    !c.IsFree)
                .Select(c => new { c.Id, c.Title, c.Price })
                .ToListAsync(),
            "Id", "Title");
    }
}

