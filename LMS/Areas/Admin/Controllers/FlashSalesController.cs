using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Marketing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

public class FlashSalesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;

    public FlashSalesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var sales = await _context.FlashSales
            .Include(f => f.FlashSaleCourses)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
        return View(sales);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCourses();
        return View(new FlashSaleCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FlashSaleCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Validate date range
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
                await PopulateCourses();
                return View(model);
            }

            var sale = new FlashSale
            {
                Name = model.Name,
                Description = model.Description,
                DiscountPercentage = model.DiscountPercentage,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MaxSales = model.MaxSales,
                IsActive = model.IsActive,
                FlashSaleCourses = new List<FlashSaleCourse>()
            };

            _context.FlashSales.Add(sale);
            await _context.SaveChangesAsync();

            // Add selected courses to flash sale
            if (model.SelectedCourses != null && model.SelectedCourses.Any())
            {
                var courses = await _context.Courses
                    .Where(c => model.SelectedCourses.Contains(c.Id))
                    .ToListAsync();

                foreach (var course in courses)
                {
                    var flashSaleCourse = new FlashSaleCourse
                    {
                        FlashSaleId = sale.Id,
                        CourseId = course.Id,
                        OriginalPrice = course.Price,
                        DiscountPrice = course.Price - (course.Price * model.DiscountPercentage / 100)
                    };
                    _context.Set<FlashSaleCourse>().Add(flashSaleCourse);
                }
                await _context.SaveChangesAsync();
            }

            SetSuccessMessage("تم إنشاء العرض بنجاح");
            return RedirectToAction(nameof(Details), new { id = sale.Id });
        }

        await PopulateCourses();
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await _context.FlashSales
            .Include(f => f.FlashSaleCourses)
                .ThenInclude(fsc => fsc.Course)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (sale == null)
            return NotFound();

        return View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sale = await _context.FlashSales
            .Include(f => f.FlashSaleCourses)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (sale == null)
            return NotFound();

        var viewModel = new FlashSaleEditViewModel
        {
            Id = sale.Id,
            Name = sale.Name,
            Description = sale.Description,
            DiscountPercentage = sale.DiscountPercentage,
            StartDate = sale.StartDate,
            EndDate = sale.EndDate,
            MaxSales = sale.MaxSales,
            IsActive = sale.IsActive,
            SalesCount = sale.UsedCount,
            SelectedCourses = sale.FlashSaleCourses?.Select(fsc => fsc.CourseId).ToList() ?? new List<int>()
        };

        await PopulateCourses();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FlashSaleEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var sale = await _context.FlashSales
            .Include(f => f.FlashSaleCourses)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (sale == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // Validate date range
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
                await PopulateCourses();
                return View(model);
            }

            sale.Name = model.Name;
            sale.Description = model.Description;
            sale.DiscountPercentage = model.DiscountPercentage;
            sale.StartDate = model.StartDate;
            sale.EndDate = model.EndDate;
            sale.MaxSales = model.MaxSales;
            sale.IsActive = model.IsActive;

            // Update course associations
            // Remove existing course associations
            if (sale.FlashSaleCourses != null && sale.FlashSaleCourses.Any())
            {
                _context.Set<FlashSaleCourse>().RemoveRange(sale.FlashSaleCourses);
            }

            // Add new course associations
            if (model.SelectedCourses != null && model.SelectedCourses.Any())
            {
                var courses = await _context.Courses
                    .Where(c => model.SelectedCourses.Contains(c.Id))
                    .Select(c => new { c.Id, c.Price })
                    .ToListAsync();

                foreach (var course in courses)
                {
                    var flashSaleCourse = new FlashSaleCourse
                    {
                        FlashSaleId = sale.Id,
                        CourseId = course.Id,
                        OriginalPrice = course.Price,
                        DiscountPrice = course.Price - (course.Price * model.DiscountPercentage / 100)
                    };
                    _context.Set<FlashSaleCourse>().Add(flashSaleCourse);
                }
            }

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث العرض بنجاح");
            return RedirectToAction(nameof(Details), new { id = sale.Id });
        }

        await PopulateCourses();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _context.FlashSales.FindAsync(id);
        if (sale == null)
            return NotFound();

        // Check if flash sale has been used
        if (sale.UsedCount > 0 || sale.CurrentSales > 0)
        {
            SetErrorMessage("لا يمكن حذف عرض تم استخدامه. يمكنك تعطيله بدلاً من ذلك.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.FlashSales.Remove(sale);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف العرض بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var sale = await _context.FlashSales.FindAsync(id);
        if (sale == null)
            return NotFound();

        sale.IsActive = !sale.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(sale.IsActive ? "تم تفعيل العرض بنجاح" : "تم تعطيل العرض بنجاح");
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCourses()
    {
        ViewBag.Courses = new MultiSelectList(
            await _context.Courses
                .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync(),
            "Id", "Title");
    }
}

