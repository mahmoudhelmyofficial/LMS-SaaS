using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة قوالب التقارير - Report Templates Controller
/// </summary>
public class ReportTemplatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportTemplatesController> _logger;

    public ReportTemplatesController(
        ApplicationDbContext context,
        ILogger<ReportTemplatesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة القوالب - Templates List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var templates = await _context.ReportTemplates
            .OrderBy(rt => rt.Name)
            .ToListAsync();

        return View(templates);
    }

    /// <summary>
    /// إنشاء قالب جديد - Create Template
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ReportTemplateCreateViewModel());
    }

    /// <summary>
    /// حفظ القالب - Save Template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReportTemplateCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var template = new ReportTemplate
            {
                Name = model.Name,
                Description = model.Description,
                ReportType = model.ReportType,
                FiltersJson = model.FiltersJson,
                ColumnsJson = model.ColumnsJson,
                SortingJson = model.SortingJson,
                IsActive = model.IsActive
            };

            _context.ReportTemplates.Add(template);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء القالب بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل القالب - Edit Template
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var viewModel = new ReportTemplateEditViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            ReportType = template.ReportType,
            FiltersJson = template.FiltersJson,
            ColumnsJson = template.ColumnsJson,
            SortingJson = template.SortingJson,
            IsActive = template.IsActive
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديلات - Save Edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ReportTemplateEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            template.Name = model.Name;
            template.Description = model.Description;
            template.ReportType = model.ReportType;
            template.FiltersJson = model.FiltersJson;
            template.ColumnsJson = model.ColumnsJson;
            template.SortingJson = model.SortingJson;
            template.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القالب بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// حذف القالب - Delete Template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        _context.ReportTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

