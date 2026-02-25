using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة القيود الجغرافية - Country Restrictions Controller
/// </summary>
public class CountryRestrictionsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CountryRestrictionsController> _logger;

    public CountryRestrictionsController(
        ApplicationDbContext context,
        ILogger<CountryRestrictionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة القيود - Restrictions List
    /// </summary>
    public async Task<IActionResult> Index(bool? isBlocked)
    {
        var query = _context.CountryRestrictions.AsQueryable();

        if (isBlocked.HasValue)
        {
            query = query.Where(cr => cr.IsBlocked == isBlocked.Value);
        }

        var restrictions = await query
            .OrderBy(cr => cr.CountryName)
            .ToListAsync();

        ViewBag.IsBlocked = isBlocked;

        return View(restrictions);
    }

    /// <summary>
    /// إنشاء قيد جديد - Create Restriction
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CountryRestrictionCreateViewModel());
    }

    /// <summary>
    /// حفظ القيد الجديد - Save Restriction
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CountryRestrictionCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var restriction = new CountryRestriction
            {
                CountryCode = model.CountryCode,
                CountryName = model.CountryName,
                IsBlocked = model.IsBlocked,
                Reason = model.Reason
            };

            _context.CountryRestrictions.Add(restriction);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة القيد بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل القيد - Edit Restriction
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        var viewModel = new CountryRestrictionEditViewModel
        {
            Id = restriction.Id,
            CountryCode = restriction.CountryCode,
            CountryName = restriction.CountryName,
            IsBlocked = restriction.IsBlocked,
            Reason = restriction.Reason
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديلات - Save Edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CountryRestrictionEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            restriction.CountryCode = model.CountryCode;
            restriction.CountryName = model.CountryName;
            restriction.IsBlocked = model.IsBlocked;
            restriction.Reason = model.Reason;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القيد بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// حذف القيد - Delete Restriction
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        _context.CountryRestrictions.Remove(restriction);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القيد بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل الحظر - Toggle Block
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(int id)
    {
        var restriction = await _context.CountryRestrictions.FindAsync(id);
        if (restriction == null)
            return NotFound();

        restriction.IsBlocked = !restriction.IsBlocked;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(restriction.IsBlocked ? "حظر" : "إلغاء حظر")} الدولة");
        return RedirectToAction(nameof(Index));
    }
}

