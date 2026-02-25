using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة قوالب الرسائل النصية - SMS Templates Controller
/// </summary>
public class SmsTemplatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SmsTemplatesController> _logger;

    public SmsTemplatesController(
        ApplicationDbContext context,
        ILogger<SmsTemplatesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة القوالب - Templates List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var templates = await _context.SmsTemplates
            .OrderBy(t => t.TemplateName)
            .ToListAsync();

        return View(templates);
    }

    /// <summary>
    /// إنشاء قالب جديد - Create Template
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new SmsTemplateCreateViewModel());
    }

    /// <summary>
    /// حفظ القالب الجديد - Save Template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SmsTemplateCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var template = new SmsTemplate
            {
                TemplateName = model.TemplateName,
                TemplateKey = model.TemplateKey,
                MessageContent = model.MessageContent,
                Language = model.Language,
                IsActive = model.IsActive,
                Description = model.Description
            };

            _context.SmsTemplates.Add(template);
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
        var template = await _context.SmsTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var viewModel = new SmsTemplateEditViewModel
        {
            Id = template.Id,
            TemplateName = template.TemplateName,
            TemplateKey = template.TemplateKey,
            MessageContent = template.MessageContent,
            Language = template.Language,
            IsActive = template.IsActive,
            Description = template.Description
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديلات - Save Edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SmsTemplateEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var template = await _context.SmsTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            template.TemplateName = model.TemplateName;
            template.TemplateKey = model.TemplateKey;
            template.MessageContent = model.MessageContent;
            template.Language = model.Language;
            template.IsActive = model.IsActive;
            template.Description = model.Description;

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
        var template = await _context.SmsTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        _context.SmsTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

