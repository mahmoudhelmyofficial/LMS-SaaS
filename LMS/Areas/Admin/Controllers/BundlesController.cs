using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Marketing;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة باقات الدورات - Course Bundles Management Controller
/// </summary>
public class BundlesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;
    private readonly ILogger<BundlesController> _logger;
    private readonly ISystemConfigurationService _configService;

    public BundlesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService,
        ILogger<BundlesController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الباقات - Bundles list
    /// </summary>
    public async Task<IActionResult> Index(bool? isActive, int page = 1)
    {
        try
        {
            var query = _context.CourseBundles
                .Include(b => b.BundleCourses)
                .AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(b => b.IsActive == isActive.Value);
            }

            var pageSize = 20;
            try
            {
                pageSize = await _configService.GetPaginationSizeAsync("bundles", 20);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetPaginationSizeAsync failed for bundles, using default 20");
            }

            var totalCount = await query.CountAsync();
            var bundles = await query
                .OrderBy(b => b.DisplayOrder)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.IsActive = isActive;
            ViewBag.Page = page;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.PageSize = pageSize;

            return View(bundles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bundles list");
            SetErrorMessage("حدث خطأ أثناء تحميل قائمة الباقات", "An error occurred while loading bundles list.");
            ViewBag.IsActive = isActive;
            ViewBag.Page = 1;
            ViewBag.TotalCount = 0;
            ViewBag.TotalPages = 0;
            ViewBag.PageSize = 20;
            return View(new List<CourseBundle>());
        }
    }

    /// <summary>
    /// تفاصيل الباقة - Bundle details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
            .FirstOrDefaultAsync(b => b.Id == id);

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
        await PopulateCourses();
        return View(new BundleCreateViewModel());
    }

    /// <summary>
    /// حفظ الباقة الجديدة - Save new bundle
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BundleCreateViewModel model)
    {
        // Ensure CourseIds is not null and has at least one course
        model.CourseIds ??= new List<int>();
        
        if (!model.CourseIds.Any())
        {
            ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
        }

        if (ModelState.IsValid)
        {
            // Calculate original price
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var originalPrice = courses.Sum(c => c.DiscountPrice ?? c.Price);
            var savingsPercentage = originalPrice > 0 
                ? ((originalPrice - model.Price) / originalPrice) * 100 
                : 0;

            var bundle = new CourseBundle
            {
                Name = model.Name,
                Slug = _slugService.GenerateSlug(model.Name),
                ShortDescription = model.ShortDescription,
                Description = model.Description,
                ThumbnailUrl = model.ThumbnailUrl,
                Price = model.Price,
                OriginalPrice = originalPrice,
                Currency = "EGP",
                SavingsPercentage = savingsPercentage,
                IsActive = model.IsActive,
                IsFeatured = model.IsFeatured,
                DisplayOrder = model.DisplayOrder,
                ValidFrom = model.ValidFrom,
                ValidTo = model.ValidTo,
                MaxSales = model.MaxSales,
                CoursesCount = model.CourseIds.Count,
                CreatedByUserId = _currentUserService.UserId
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

            SetSuccessMessage("تم إنشاء الباقة بنجاح", "Bundle created successfully.");
            return RedirectToAction(nameof(Details), new { id = bundle.Id });
        }

        await PopulateCourses();
        return View(model);
    }

    /// <summary>
    /// تعديل الباقة - Edit bundle
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id);

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

        await PopulateCourses();
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

        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bundle == null)
            return NotFound();

        // Ensure CourseIds is not null and has at least one course
        model.CourseIds ??= new List<int>();
        
        if (!model.CourseIds.Any())
        {
            ModelState.AddModelError("CourseIds", "يرجى اختيار دورة واحدة على الأقل");
        }

        if (ModelState.IsValid)
        {
            // Calculate original price
            var courses = await _context.Courses
                .Where(c => model.CourseIds.Contains(c.Id))
                .ToListAsync();

            var originalPrice = courses.Sum(c => c.DiscountPrice ?? c.Price);
            var savingsPercentage = originalPrice > 0 
                ? ((originalPrice - model.Price) / originalPrice) * 100 
                : 0;

            bundle.Name = model.Name;
            bundle.Slug = _slugService.GenerateSlug(model.Name);
            bundle.ShortDescription = model.ShortDescription;
            bundle.Description = model.Description;
            bundle.ThumbnailUrl = model.ThumbnailUrl;
            bundle.Price = model.Price;
            bundle.OriginalPrice = originalPrice;
            bundle.SavingsPercentage = savingsPercentage;
            bundle.IsActive = model.IsActive;
            bundle.IsFeatured = model.IsFeatured;
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

            SetSuccessMessage("تم تحديث الباقة بنجاح", "Bundle updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateCourses();
        return View(model);
    }

    /// <summary>
    /// حذف الباقة - Delete bundle
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bundle == null)
            return NotFound();

        if (bundle.SalesCount > 0)
        {
            SetErrorMessage("لا يمكن حذف الباقة لأنه تم شراؤها من قبل", "Cannot delete the bundle because it has been purchased.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.BundleCourses.RemoveRange(bundle.BundleCourses);
        _context.CourseBundles.Remove(bundle);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الباقة بنجاح", "Bundle deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// نسخ الباقة - Duplicate bundle
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bundle == null)
            return NotFound();

        // Create a duplicate bundle
        var duplicateBundle = new CourseBundle
        {
            Name = $"{bundle.Name} (نسخة)",
            Slug = _slugService.GenerateSlug($"{bundle.Name}-copy-{DateTime.UtcNow.Ticks}"),
            ShortDescription = bundle.ShortDescription,
            Description = bundle.Description,
            ThumbnailUrl = bundle.ThumbnailUrl,
            Price = bundle.Price,
            OriginalPrice = bundle.OriginalPrice,
            Currency = bundle.Currency,
            SavingsPercentage = bundle.SavingsPercentage,
            IsActive = false, // Start as inactive
            IsFeatured = false,
            DisplayOrder = bundle.DisplayOrder,
            ValidFrom = bundle.ValidFrom,
            ValidTo = bundle.ValidTo,
            MaxSales = bundle.MaxSales,
            CoursesCount = bundle.CoursesCount,
            CreatedByUserId = _currentUserService.UserId
        };

        _context.CourseBundles.Add(duplicateBundle);
        await _context.SaveChangesAsync();

        // Copy bundle courses
        foreach (var bundleCourse in bundle.BundleCourses)
        {
            var duplicateBundleCourse = new BundleCourse
            {
                BundleId = duplicateBundle.Id,
                CourseId = bundleCourse.CourseId,
                DisplayOrder = bundleCourse.DisplayOrder
            };
            _context.BundleCourses.Add(duplicateBundleCourse);
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم نسخ الباقة بنجاح", "Bundle copied successfully.");
        return RedirectToAction(nameof(Edit), new { id = duplicateBundle.Id });
    }

    /// <summary>
    /// إحصائيات الباقة - Bundle analytics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Analytics(int id)
    {
        var bundle = await _context.CourseBundles
            .Include(b => b.BundleCourses)
                .ThenInclude(bc => bc.Course)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bundle == null)
            return NotFound();

        // For now, redirect to details with a message
        // This can be enhanced with actual analytics later
        ViewBag.Bundle = bundle;
        ViewBag.TotalSales = bundle.SalesCount;
        ViewBag.TotalRevenue = bundle.SalesCount * bundle.Price;
        
        return View(bundle);
    }

    private async Task PopulateCourses()
    {
        var courses = await _context.Courses
            .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();
        
        ViewBag.Courses = courses;
        ViewBag.CoursesSelectList = new MultiSelectList(courses, "Id", "Title");
    }
}

