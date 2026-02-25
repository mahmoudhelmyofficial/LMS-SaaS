using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة التصنيفات - Categories Management Controller
/// </summary>
public class CategoriesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ISlugService _slugService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CategoriesController> _logger;
    private readonly ISystemConfigurationService _configService;

    private const int MaxCategoryDepth = 3; // Maximum nesting level

    public CategoriesController(
        ApplicationDbContext context,
        ISlugService slugService,
        ICurrentUserService currentUserService,
        ILogger<CategoriesController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _slugService = slugService;
        _currentUserService = currentUserService;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة التصنيفات - Categories list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        return View(categories);
    }

    /// <summary>
    /// التصنيفات الفرعية - Subcategories list
    /// </summary>
    public async Task<IActionResult> Subcategories(int? parentId)
    {
        var query = _context.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.Courses)
            .Where(c => c.ParentCategoryId != null);

        if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentCategoryId == parentId.Value);
        }

        var subcategories = await query
            .OrderBy(c => c.ParentCategory!.Name)
            .ThenBy(c => c.DisplayOrder)
            .ToListAsync();

        var parentCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.ParentCategories = parentCategories;
        ViewBag.SelectedParentId = parentId;
        ViewBag.TotalSubcategories = subcategories.Count;

        return View(subcategories);
    }

    /// <summary>
    /// تفاصيل التصنيف - Category details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var category = await _context.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .Include(c => c.Courses)
                .ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.LogWarning("Category not found: {CategoryId}", id);
            return NotFound();
        }

        return View(category);
    }

    /// <summary>
    /// إنشاء تصنيف جديد - Create new category
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.ParentCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .ToListAsync();

        return View(new CategoryCreateViewModel());
    }

    /// <summary>
    /// حفظ التصنيف الجديد - Save new category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryCreateViewModel model)
    {
        try
        {
            // Validate category name uniqueness
            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower());

            if (existingCategory != null)
            {
                ModelState.AddModelError("Name", "يوجد تصنيف بنفس الاسم بالفعل");
            }

            // Validate slug uniqueness
            var slug = _slugService.GenerateSlug(model.Name);
            var existingSlug = await _context.Categories
                .AnyAsync(c => c.Slug == slug);

            if (existingSlug)
            {
                // Generate unique slug
                slug = $"{slug}-{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Validate parent category hierarchy
            if (model.ParentCategoryId.HasValue)
            {
                var depth = await GetCategoryDepth(model.ParentCategoryId.Value);
                if (depth >= MaxCategoryDepth)
                {
                    ModelState.AddModelError("ParentCategoryId", 
                        $"لا يمكن إنشاء تصنيف فرعي بعمق أكثر من {MaxCategoryDepth} مستويات");
                }

                // Ensure parent exists and is active
                var parentCategory = await _context.Categories.FindAsync(model.ParentCategoryId.Value);
                if (parentCategory == null)
                {
                    ModelState.AddModelError("ParentCategoryId", "التصنيف الأب غير موجود");
                }
            }

            // Validate display order
            if (model.DisplayOrder < 0)
            {
                ModelState.AddModelError("DisplayOrder", "ترتيب العرض يجب أن يكون رقم موجب");
            }

            if (ModelState.IsValid)
            {
                var category = new Category
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim(),
                    IconUrl = model.IconUrl?.Trim(),
                    ImageUrl = model.ImageUrl?.Trim(),
                    ParentCategoryId = model.ParentCategoryId,
                    DisplayOrder = model.DisplayOrder,
                    IsActive = model.IsActive,
                    IsFeatured = model.IsFeatured,
                    Color = model.Color?.Trim(),
                    Slug = slug
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Category {CategoryId} created by admin {AdminId}", 
                    category.Id, _currentUserService.UserId);

                SetSuccessMessage("تم إنشاء التصنيف بنجاح", "Category created successfully.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .ToListAsync();

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            SetErrorMessage("حدث خطأ أثناء إنشاء التصنيف", "An error occurred while creating the category.");
            
            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .ToListAsync();
            
            return View(model);
        }
    }

    /// <summary>
    /// تعديل التصنيف - Edit category
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound();

        var viewModel = new CategoryEditViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IconUrl = category.IconUrl,
            ImageUrl = category.ImageUrl,
            ParentCategoryId = category.ParentCategoryId,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            IsFeatured = category.IsFeatured,
            Color = category.Color
        };

        ViewBag.ParentCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null && c.Id != id)
            .ToListAsync();

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات التصنيف - Save category edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        try
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                _logger.LogWarning("Category not found: {CategoryId}", id);
                return NotFound();
            }

            // Validate name uniqueness (excluding current category)
            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == model.Name.ToLower() && c.Id != id);

            if (existingCategory != null)
            {
                ModelState.AddModelError("Name", "يوجد تصنيف بنفس الاسم بالفعل");
            }

            // Validate parent category
            if (model.ParentCategoryId.HasValue)
            {
                // Prevent circular reference
                if (model.ParentCategoryId.Value == id)
                {
                    ModelState.AddModelError("ParentCategoryId", "لا يمكن جعل التصنيف أب لنفسه");
                }

                // Prevent making a child as parent
                var isDescendant = await IsDescendant(id, model.ParentCategoryId.Value);
                if (isDescendant)
                {
                    ModelState.AddModelError("ParentCategoryId", 
                        "لا يمكن جعل تصنيف فرعي كأب لتصنيفه الأصلي");
                }

                // Check depth
                var depth = await GetCategoryDepth(model.ParentCategoryId.Value);
                if (depth >= MaxCategoryDepth)
                {
                    ModelState.AddModelError("ParentCategoryId", 
                        $"لا يمكن تعيين تصنيف أب بعمق أكثر من {MaxCategoryDepth} مستويات");
                }
            }

            // Validate display order
            if (model.DisplayOrder < 0)
            {
                ModelState.AddModelError("DisplayOrder", "ترتيب العرض يجب أن يكون رقم موجب");
            }

            if (ModelState.IsValid)
            {
                category.Name = model.Name.Trim();
                category.Description = model.Description?.Trim();
                category.IconUrl = model.IconUrl?.Trim();
                category.ImageUrl = model.ImageUrl?.Trim();
                category.ParentCategoryId = model.ParentCategoryId;
                category.DisplayOrder = model.DisplayOrder;
                category.IsActive = model.IsActive;
                category.IsFeatured = model.IsFeatured;
                category.Color = model.Color?.Trim();

                // Regenerate slug if name changed
                if (category.Name != model.Name)
                {
                    category.Slug = _slugService.GenerateSlug(model.Name);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Category {CategoryId} updated by admin {AdminId}", 
                    id, _currentUserService.UserId);

                SetSuccessMessage("تم تحديث التصنيف بنجاح", "Category updated successfully.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentCategoryId == null && c.Id != id)
                .ToListAsync();

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category {CategoryId}", id);
            SetErrorMessage("حدث خطأ أثناء تحديث التصنيف", "An error occurred while updating the category.");
            
            ViewBag.ParentCategories = await _context.Categories
                .Where(c => c.ParentCategoryId == null && c.Id != id)
                .ToListAsync();
            
            return View(model);
        }
    }

    /// <summary>
    /// حذف التصنيف - Delete category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.Courses)
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                _logger.LogWarning("Category not found for deletion: {CategoryId}", id);
                return NotFound();
            }

            // Check if category has courses
            if (category.Courses.Any())
            {
                var courseCount = category.Courses.Count;
                SetErrorMessage(string.Format(CultureExtensions.T("لا يمكن حذف التصنيف لأنه يحتوي على {0} دورة", "Cannot delete the category because it contains {0} course(s)."), courseCount));
                return RedirectToAction(nameof(Index));
            }

            // Check if category has subcategories
            if (category.SubCategories.Any())
            {
                var subCategoryCount = category.SubCategories.Count;
                SetErrorMessage(string.Format(CultureExtensions.T("لا يمكن حذف التصنيف لأنه يحتوي على {0} تصنيف فرعي", "Cannot delete the category because it contains {0} subcategory(ies)."), subCategoryCount));
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} deleted by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage("تم حذف التصنيف بنجاح", "Category deleted successfully.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف التصنيف", "An error occurred while deleting the category.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تبديل حالة التصنيف - Toggle category status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                _logger.LogWarning("Category not found: {CategoryId}", id);
                return NotFound();
            }

            category.IsActive = !category.IsActive;

            // If deactivating, also deactivate all subcategories
            if (!category.IsActive && category.SubCategories.Any())
            {
                foreach (var subCategory in category.SubCategories)
                {
                    subCategory.IsActive = false;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} status toggled to {Status} by admin {AdminId}", 
                id, category.IsActive, _currentUserService.UserId);

            SetSuccessMessage(category.IsActive ? CultureExtensions.T("تم تفعيل التصنيف", "Category enabled.") : CultureExtensions.T("تم إلغاء تفعيل التصنيف", "Category disabled."));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling category status {CategoryId}", id);
            SetErrorMessage("حدث خطأ أثناء تغيير حالة التصنيف", "An error occurred while changing category status.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// نقل الدورات إلى تصنيف آخر - Move courses to another category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveCourses(int fromCategoryId, int toCategoryId)
    {
        if (fromCategoryId == toCategoryId)
        {
            SetErrorMessage("لا يمكن نقل الدورات إلى نفس التصنيف", "Cannot move courses to the same category.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var fromCategory = await _context.Categories
                .Include(c => c.Courses)
                .FirstOrDefaultAsync(c => c.Id == fromCategoryId);

            var toCategory = await _context.Categories.FindAsync(toCategoryId);

            if (fromCategory == null || toCategory == null)
            {
                SetErrorMessage("التصنيف غير موجود", "Category not found.");
                return RedirectToAction(nameof(Index));
            }

            if (!fromCategory.Courses.Any())
            {
                SetWarningMessage("التصنيف المصدر لا يحتوي على دورات", "Source category has no courses.");
                return RedirectToAction(nameof(Index));
            }

            var courseCount = fromCategory.Courses.Count;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                foreach (var course in fromCategory.Courses)
                {
                    course.CategoryId = toCategoryId;
                }

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("{Count} courses moved from category {FromId} to {ToId} by admin {AdminId}", 
                courseCount, fromCategoryId, toCategoryId, _currentUserService.UserId);

            SetSuccessMessage(string.Format(CultureExtensions.T("تم نقل {0} دورة بنجاح", "{0} course(s) moved successfully."), courseCount));
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving courses from category {FromId} to {ToId}", 
                fromCategoryId, toCategoryId);
            SetErrorMessage("حدث خطأ أثناء نقل الدورات", "An error occurred while moving courses.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إحصائيات التصنيفات - Category statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var stats = new
            {
                TotalCategories = await _context.Categories.CountAsync(),
                ActiveCategories = await _context.Categories.CountAsync(c => c.IsActive),
                InactiveCategories = await _context.Categories.CountAsync(c => !c.IsActive),
                FeaturedCategories = await _context.Categories.CountAsync(c => c.IsFeatured),
                ParentCategories = await _context.Categories.CountAsync(c => c.ParentCategoryId == null),
                SubCategories = await _context.Categories.CountAsync(c => c.ParentCategoryId != null),
                CategoriesWithCourses = await _context.Categories
                    .Include(c => c.Courses)
                    .CountAsync(c => c.Courses.Any()),
                EmptyCategories = await _context.Categories
                    .Include(c => c.Courses)
                    .CountAsync(c => !c.Courses.Any()),
                TopCategories = await _context.Categories
                    .Include(c => c.Courses)
                    .Select(c => new { 
                        Category = c, 
                        CourseCount = c.Courses.Count,
                        ActiveCourseCount = c.Courses.Count(co => co.Status == Domain.Enums.CourseStatus.Published)
                    })
                    .OrderByDescending(x => x.CourseCount)
                    .Take(10)
                    .ToListAsync()
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading category statistics");
            SetErrorMessage("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// عرض الدورات في التصنيف - Show courses in a category
    /// </summary>
    public async Task<IActionResult> Courses(int id, int page = 1)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            _logger.LogWarning("Category not found: {CategoryId}", id);
            return NotFound();
        }

        var pageSize = await _configService.GetPaginationSizeAsync("category_courses", 20);
        var query = _context.Courses
            .Include(c => c.Instructor)
            .Where(c => c.CategoryId == id);

        var totalCourses = await query.CountAsync();
        var courses = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Category = category;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCourses / pageSize);
        ViewBag.TotalCourses = totalCourses;
        ViewBag.PageSize = pageSize;

        return View(courses);
    }

    #region Helper Methods

    /// <summary>
    /// Get category depth in hierarchy
    /// </summary>
    private async Task<int> GetCategoryDepth(int categoryId, int currentDepth = 0)
    {
        var category = await _context.Categories.FindAsync(categoryId);
        if (category == null || !category.ParentCategoryId.HasValue)
            return currentDepth;

        return await GetCategoryDepth(category.ParentCategoryId.Value, currentDepth + 1);
    }

    /// <summary>
    /// Check if a category is a descendant of another
    /// </summary>
    private async Task<bool> IsDescendant(int ancestorId, int descendantId)
    {
        var category = await _context.Categories.FindAsync(descendantId);
        if (category == null)
            return false;

        if (!category.ParentCategoryId.HasValue)
            return false;

        if (category.ParentCategoryId.Value == ancestorId)
            return true;

        return await IsDescendant(ancestorId, category.ParentCategoryId.Value);
    }

    #endregion
}
