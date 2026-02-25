using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات الضرائب - Tax Settings Controller
/// </summary>
public class TaxSettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TaxSettingsController> _logger;

    public TaxSettingsController(
        ApplicationDbContext context,
        ILogger<TaxSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة إعدادات الضرائب - Tax Settings List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var settings = await _context.TaxSettings
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        return View(settings);
    }

    /// <summary>
    /// إنشاء إعدادات ضريبة جديدة - Create new tax setting
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var model = new TaxSettingCreateViewModel
        {
            TaxType = "VAT",
            TaxRate = 15,
            IsEnabled = true,
            ApplyToDigitalProducts = true,
            ApplyToPhysicalProducts = true,
            ApplyToSubscriptions = true
        };
        return View("CreateTaxSetting", model);
    }

    /// <summary>
    /// حفظ إعدادات الضريبة الجديدة - Save new tax settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaxSettingCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var setting = new TaxSetting
            {
                TaxName = model.TaxName,
                TaxRate = model.TaxRate,
                TaxType = model.TaxType,
                Country = model.Country,
                State = model.State,
                ZipCode = model.ZipCode,
                IsEnabled = model.IsEnabled,
                IsDefault = model.IsDefault,
                ApplyToPhysicalProducts = model.ApplyToPhysicalProducts,
                ApplyToDigitalProducts = model.ApplyToDigitalProducts,
                ApplyToSubscriptions = model.ApplyToSubscriptions,
                TaxIdNumber = model.TaxIdNumber,
                DisplayOrder = model.DisplayOrder,
                Description = model.Description
            };

            // If this is set as default, unset others
            if (setting.IsDefault)
            {
                var existingDefaults = await _context.TaxSettings
                    .Where(t => t.IsDefault)
                    .ToListAsync();
                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }

            _context.TaxSettings.Add(setting);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة إعدادات الضريبة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View("CreateTaxSetting", model);
    }

    /// <summary>
    /// تعديل إعدادات الضريبة - Edit tax settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var setting = await _context.TaxSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        var viewModel = new TaxSettingEditViewModel
        {
            Id = setting.Id,
            TaxName = setting.TaxName,
            TaxRate = setting.TaxRate,
            TaxType = setting.TaxType,
            Country = setting.Country,
            State = setting.State,
            ZipCode = setting.ZipCode,
            IsEnabled = setting.IsEnabled,
            IsDefault = setting.IsDefault,
            ApplyToPhysicalProducts = setting.ApplyToPhysicalProducts,
            ApplyToDigitalProducts = setting.ApplyToDigitalProducts,
            ApplyToSubscriptions = setting.ApplyToSubscriptions,
            TaxIdNumber = setting.TaxIdNumber,
            DisplayOrder = setting.DisplayOrder,
            Description = setting.Description
        };

        return View("EditTaxSetting", viewModel);
    }

    /// <summary>
    /// حفظ تعديلات إعدادات الضريبة - Save tax settings edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaxSettingEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var setting = await _context.TaxSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // If this is set as default, unset others
            if (model.IsDefault && !setting.IsDefault)
            {
                var existingDefaults = await _context.TaxSettings
                    .Where(t => t.IsDefault && t.Id != id)
                    .ToListAsync();
                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }

            setting.TaxName = model.TaxName;
            setting.TaxRate = model.TaxRate;
            setting.TaxType = model.TaxType;
            setting.Country = model.Country;
            setting.State = model.State;
            setting.ZipCode = model.ZipCode;
            setting.IsEnabled = model.IsEnabled;
            setting.IsDefault = model.IsDefault;
            setting.ApplyToPhysicalProducts = model.ApplyToPhysicalProducts;
            setting.ApplyToDigitalProducts = model.ApplyToDigitalProducts;
            setting.ApplyToSubscriptions = model.ApplyToSubscriptions;
            setting.TaxIdNumber = model.TaxIdNumber;
            setting.DisplayOrder = model.DisplayOrder;
            setting.Description = model.Description;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث إعدادات الضريبة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View("EditTaxSetting", model);
    }

    /// <summary>
    /// تبديل حالة الضريبة - Toggle tax status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var setting = await _context.TaxSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        setting.IsEnabled = !setting.IsEnabled;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(setting.IsEnabled ? "تفعيل" : "تعطيل")} الضريبة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف إعدادات الضريبة - Delete tax settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var setting = await _context.TaxSettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        _context.TaxSettings.Remove(setting);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف إعدادات الضريبة بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

