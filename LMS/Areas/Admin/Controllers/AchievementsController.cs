using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Gamification;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

public class AchievementsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;

    public AchievementsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var achievements = await _context.Achievements.OrderBy(a => a.AchievementType).ToListAsync();
        return View(achievements);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AchievementCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AchievementCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var achievement = new Achievement
            {
                Name = model.Name,
                Description = model.Description,
                IconUrl = model.IconUrl,
                Points = model.Points,
                AchievementType = model.AchievementType,
                RequiredValue = model.RequiredValue,
                IsActive = model.IsActive
            };

            _context.Achievements.Add(achievement);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء الإنجاز بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        return View(achievement);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        var viewModel = new AchievementEditViewModel
        {
            Id = achievement.Id,
            Name = achievement.Name,
            Description = achievement.Description,
            IconUrl = achievement.IconUrl,
            Points = achievement.Points,
            AchievementType = achievement.AchievementType,
            RequiredValue = achievement.RequiredValue,
            IsActive = achievement.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AchievementEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            achievement.Name = model.Name;
            achievement.Description = model.Description;
            achievement.IconUrl = model.IconUrl;
            achievement.Points = model.Points;
            achievement.AchievementType = model.AchievementType;
            achievement.RequiredValue = model.RequiredValue;
            achievement.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث الإنجاز بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        _context.Achievements.Remove(achievement);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الإنجاز بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> Activate(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        achievement.IsActive = true;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تفعيل الإنجاز بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        achievement.IsActive = false;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعطيل الإنجاز بنجاح");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Duplicate(int id)
    {
        var original = await _context.Achievements.FindAsync(id);
        if (original == null)
            return NotFound();

        var duplicate = new Achievement
        {
            Name = original.Name + " (نسخة)",
            Description = original.Description,
            IconUrl = original.IconUrl,
            Points = original.Points,
            AchievementType = original.AchievementType,
            RequiredValue = original.RequiredValue,
            Rarity = original.Rarity,
            IsActive = false, // Start as inactive
            IsSecret = original.IsSecret,
            IsRepeatable = original.IsRepeatable,
            ShowNotification = original.ShowNotification
        };

        _context.Achievements.Add(duplicate);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم نسخ الإنجاز بنجاح");
        return RedirectToAction(nameof(Edit), new { id = duplicate.Id });
    }

    public async Task<IActionResult> Analytics(int id)
    {
        var achievement = await _context.Achievements.FindAsync(id);
        if (achievement == null)
            return NotFound();

        // For now, redirect to details page - can be enhanced later
        return RedirectToAction(nameof(Details), new { id });
    }
}

