using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Gamification;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

public class BadgesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;

    public BadgesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var badges = await _context.Badges.OrderBy(b => b.RequiredPoints).ToListAsync();
        return View(badges);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new BadgeCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BadgeCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var badge = new Badge
            {
                Name = model.Name,
                Description = model.Description,
                IconUrl = model.IconUrl,
                Rarity = model.Rarity,
                RequiredPoints = model.RequiredPoints,
                IsActive = model.IsActive
            };

            _context.Badges.Add(badge);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء الشارة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
            return NotFound();

        var viewModel = new BadgeEditViewModel
        {
            Id = badge.Id,
            Name = badge.Name,
            Description = badge.Description,
            IconUrl = badge.IconUrl,
            Rarity = badge.Rarity,
            RequiredPoints = badge.RequiredPoints,
            IsActive = badge.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BadgeEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            badge.Name = model.Name;
            badge.Description = model.Description;
            badge.IconUrl = model.IconUrl;
            badge.Rarity = model.Rarity;
            badge.RequiredPoints = model.RequiredPoints;
            badge.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الشارة بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
            return NotFound();

        return View(badge);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
            return NotFound();

        _context.Badges.Remove(badge);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الشارة بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var badge = await _context.Badges.FindAsync(id);
        if (badge == null)
            return NotFound();

        badge.IsActive = !badge.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(badge.IsActive ? "تم تفعيل الشارة بنجاح" : "تم تعطيل الشارة بنجاح");
        return RedirectToAction(nameof(Details), new { id });
    }
}

