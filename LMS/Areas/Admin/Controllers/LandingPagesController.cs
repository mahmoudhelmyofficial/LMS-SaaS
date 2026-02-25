using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Marketing;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

public class LandingPagesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISlugService _slugService;

    public LandingPagesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISlugService slugService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _slugService = slugService;
    }

    public async Task<IActionResult> Index()
    {
        var pages = await _context.LandingPages
            .Include(p => p.Template)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(pages);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateTemplates();
        return View(new LandingPageCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LandingPageCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var page = new LandingPage
            {
                Title = model.Title,
                Description = model.Description,
                Slug = !string.IsNullOrEmpty(model.Slug) ? model.Slug : _slugService.GenerateSlug(model.Title),
                TemplateId = model.TemplateId,
                Status = model.Status,
                IsActive = model.IsActive,
                CreatedById = _currentUserService.UserId
            };

            _context.LandingPages.Add(page);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء الصفحة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        await PopulateTemplates();
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var page = await _context.LandingPages
            .Include(p => p.Template)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (page == null)
            return NotFound();

        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _context.LandingPages.FindAsync(id);
        if (page == null)
            return NotFound();

        var viewModel = new LandingPageEditViewModel
        {
            Id = page.Id,
            Title = page.Title,
            Description = page.Description,
            Slug = page.Slug,
            TemplateId = page.TemplateId,
            Status = page.Status,
            IsActive = page.IsActive
        };

        await PopulateTemplates();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LandingPageEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var page = await _context.LandingPages.FindAsync(id);
        if (page == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            page.Title = model.Title;
            page.Description = model.Description;
            page.Slug = !string.IsNullOrEmpty(model.Slug) ? model.Slug : _slugService.GenerateSlug(model.Title);
            page.TemplateId = model.TemplateId;
            page.Status = model.Status;
            page.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الصفحة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        await PopulateTemplates();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var page = await _context.LandingPages.FindAsync(id);
        if (page == null)
            return NotFound();

        _context.LandingPages.Remove(page);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الصفحة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateTemplates()
    {
        ViewBag.Templates = await _context.LandingPageTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();
    }
}

