using LMS.Areas.Admin.ViewModels;
using LMS.Extensions;
using LMS.Data;
using LMS.Domain.Entities.Assessments;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة بنك الأسئلة - Question Bank Management Controller
/// </summary>
public class QuestionBankController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QuestionBankController> _logger;
    private readonly ISystemConfigurationService _configService;
    private readonly ICurrentUserService _currentUserService;

    public QuestionBankController(
        ApplicationDbContext context, 
        ILogger<QuestionBankController> logger,
        ISystemConfigurationService configService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
        _currentUserService = currentUserService;
    }

    #region Question Banks

    /// <summary>
    /// قائمة بنوك الأسئلة - Question banks list
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var pageSize = await _configService.GetPaginationSizeAsync("question_banks", 20);
        var totalCount = await _context.QuestionBanks.CountAsync();
        var banks = await _context.QuestionBanks
            .Include(b => b.QuestionCategory)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        return View(banks);
    }

    /// <summary>
    /// تفاصيل بنك الأسئلة - Question bank details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var bank = await _context.QuestionBanks
            .Include(b => b.QuestionCategory)
            .Include(b => b.Items)
            .Include(b => b.Owner)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bank == null)
            return NotFound();

        return View(bank);
    }

    /// <summary>
    /// إنشاء بنك أسئلة جديد - Create new question bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCategoriesDropdownAsync();
        return View(new QuestionBankViewModel());
    }

    /// <summary>
    /// حفظ بنك الأسئلة الجديد - Save new question bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionBankViewModel model)
    {
        if (ModelState.IsValid)
        {
            var bank = new QuestionBank
            {
                Name = model.Name,
                Description = model.Description,
                CategoryId = model.CategoryId,
                IsPublic = model.IsPublic,
                IsActive = true,
                OwnerId = _currentUserService.UserId ?? throw new InvalidOperationException("User not authenticated")
            };

            _context.QuestionBanks.Add(bank);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء بنك الأسئلة بنجاح", "Question bank created successfully.");
            return RedirectToAction(nameof(Details), new { id = bank.Id });
        }

        await PopulateCategoriesDropdownAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل بنك أسئلة - Edit question bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var bank = await _context.QuestionBanks.FindAsync(id);
        if (bank == null)
            return NotFound();

        var model = new QuestionBankViewModel
        {
            Id = bank.Id,
            Name = bank.Name,
            Description = bank.Description,
            CategoryId = bank.CategoryId,
            IsPublic = bank.IsPublic
        };

        await PopulateCategoriesDropdownAsync();
        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات بنك الأسئلة - Save question bank changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, QuestionBankViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var bank = await _context.QuestionBanks.FindAsync(id);
            if (bank == null)
                return NotFound();

            bank.Name = model.Name;
            bank.Description = model.Description;
            bank.CategoryId = model.CategoryId;
            bank.IsPublic = model.IsPublic;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث بنك الأسئلة بنجاح", "Question bank updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateCategoriesDropdownAsync();
        return View(model);
    }

    /// <summary>
    /// حذف بنك أسئلة - Delete question bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var bank = await _context.QuestionBanks
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bank == null)
            return NotFound();

        _context.QuestionBanks.Remove(bank);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف بنك الأسئلة بنجاح", "Question bank deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// نسخ بنك أسئلة - Duplicate question bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        try
        {
            var bank = await _context.QuestionBanks
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bank == null)
            {
                SetErrorMessage("بنك الأسئلة غير موجود", "Question bank not found.");
                return RedirectToAction(nameof(Index));
            }

            var newBank = new QuestionBank
            {
                Name = $"{bank.Name} - نسخة",
                Description = bank.Description,
                CategoryId = bank.CategoryId,
                IsPublic = bank.IsPublic,
                IsActive = true,
                OwnerId = _currentUserService.UserId ?? throw new InvalidOperationException("User not authenticated")
            };

            _context.QuestionBanks.Add(newBank);
            await _context.SaveChangesAsync();

            // Copy all questions with proper null handling
            if (bank.Items != null && bank.Items.Any())
            {
                foreach (var item in bank.Items)
                {
                    var newItem = new QuestionBankItem
                    {
                        QuestionBankId = newBank.Id,
                        QuestionText = item.QuestionText ?? string.Empty,
                        QuestionType = item.QuestionType,
                        Options = item.Options, // This is a JSON string, not a navigation property
                        CorrectAnswer = item.CorrectAnswer ?? string.Empty,
                        Explanation = item.Explanation,
                        DefaultPoints = item.DefaultPoints,
                        DifficultyLevel = item.DifficultyLevel,
                        Tags = item.Tags,
                        ImageUrl = item.ImageUrl,
                        AudioUrl = item.AudioUrl,
                        DisplayOrder = item.DisplayOrder
                    };
                    _context.QuestionBankItems.Add(newItem);
                }

                await _context.SaveChangesAsync();
            }

            // Update the new bank's question count
            newBank.QuestionsCount = await _context.QuestionBankItems
                .CountAsync(q => q.QuestionBankId == newBank.Id);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم نسخ بنك الأسئلة بنجاح", "Question bank copied successfully.");
            return RedirectToAction(nameof(Edit), new { id = newBank.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating question bank {BankId}", id);
            SetErrorMessage("حدث خطأ أثناء نسخ بنك الأسئلة", "An error occurred while copying the question bank.");
            return RedirectToAction(nameof(Index));
        }
    }

    #endregion

    #region Question Bank Items

    /// <summary>
    /// إضافة سؤال لبنك الأسئلة - Add question to bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddQuestion(int bankId)
    {
        var bank = await _context.QuestionBanks.FindAsync(bankId);
        if (bank == null)
            return NotFound();

        ViewBag.BankName = bank.Name;
        return View(new QuestionBankItemViewModel { BankId = bankId });
    }

    /// <summary>
    /// حفظ السؤال الجديد - Save new question
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(QuestionBankItemViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Serialize options to JSON if provided
            string? optionsJson = null;
            string correctAnswer = "";
            
            if (model.Options != null && model.Options.Any())
            {
                var optionsList = model.Options
                    .OrderBy(o => o.Order)
                    .Select(o => new { text = o.Text, order = o.Order })
                    .ToList();
                optionsJson = System.Text.Json.JsonSerializer.Serialize(optionsList);
                
                // Get correct answer indices
                var correctIndices = model.Options
                    .Where(o => o.IsCorrect)
                    .Select(o => o.Order)
                    .ToList();
                correctAnswer = string.Join(",", correctIndices);
            }

            var item = new QuestionBankItem
            {
                QuestionBankId = model.BankId,
                QuestionText = model.QuestionText,
                QuestionType = model.QuestionType,
                DifficultyLevel = model.DifficultyLevel,
                DefaultPoints = model.Points,
                Explanation = model.Explanation,
                Tags = model.Tags,
                Options = optionsJson,
                CorrectAnswer = correctAnswer
            };

            _context.QuestionBankItems.Add(item);
            await _context.SaveChangesAsync();

            // Update bank questions count
            var bank = await _context.QuestionBanks.FindAsync(model.BankId);
            if (bank != null)
            {
                bank.QuestionsCount = await _context.QuestionBankItems
                    .CountAsync(q => q.QuestionBankId == model.BankId);
                await _context.SaveChangesAsync();
            }

            SetSuccessMessage("تم إضافة السؤال بنجاح", "Question added successfully.");
            return RedirectToAction(nameof(Details), new { id = model.BankId });
        }

        var bankForView = await _context.QuestionBanks.FindAsync(model.BankId);
        ViewBag.BankName = bankForView?.Name;

        return View(model);
    }

    /// <summary>
    /// تعديل سؤال في بنك الأسئلة - Edit question in bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditQuestion(int id)
    {
        var item = await _context.QuestionBankItems
            .Include(i => i.Bank)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound();

        var model = new QuestionBankItemViewModel
        {
            Id = item.Id,
            BankId = item.BankId,
            QuestionText = item.QuestionText,
            QuestionType = item.QuestionType,
            DifficultyLevel = item.DifficultyLevel,
            Points = item.DefaultPoints,
            Explanation = item.Explanation,
            Tags = item.Tags
        };

        ViewBag.BankName = item.Bank.Name;
        return View("AddQuestion", model);
    }

    /// <summary>
    /// حذف سؤال من بنك الأسئلة - Delete question from bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var item = await _context.QuestionBankItems.FindAsync(id);
        if (item == null)
            return NotFound();

        var bankId = item.BankId;

        _context.QuestionBankItems.Remove(item);
        await _context.SaveChangesAsync();

        // Update bank questions count
        var bank = await _context.QuestionBanks.FindAsync(bankId);
        if (bank != null)
        {
            bank.QuestionsCount = await _context.QuestionBankItems
                .CountAsync(q => q.QuestionBankId == bankId);
            await _context.SaveChangesAsync();
        }

        SetSuccessMessage("تم حذف السؤال بنجاح", "Question deleted successfully.");
        return RedirectToAction(nameof(Details), new { id = bankId });
    }

    /// <summary>
    /// نسخ سؤال - Duplicate question
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateQuestion(int id)
    {
        var item = await _context.QuestionBankItems.FindAsync(id);
        if (item == null)
            return NotFound();

        var newItem = new QuestionBankItem
        {
            QuestionBankId = item.QuestionBankId,
            QuestionText = (item.QuestionText ?? string.Empty) + " (نسخة)",
            QuestionType = item.QuestionType,
            Options = item.Options,
            CorrectAnswer = item.CorrectAnswer ?? string.Empty,
            Explanation = item.Explanation,
            DefaultPoints = item.DefaultPoints,
            DifficultyLevel = item.DifficultyLevel,
            Tags = item.Tags,
            ImageUrl = item.ImageUrl,
            AudioUrl = item.AudioUrl,
            DisplayOrder = item.DisplayOrder + 1
        };

        _context.QuestionBankItems.Add(newItem);
        await _context.SaveChangesAsync();

        // Update bank questions count
        var bank = await _context.QuestionBanks.FindAsync(item.QuestionBankId);
        if (bank != null)
        {
            bank.QuestionsCount = await _context.QuestionBankItems
                .CountAsync(q => q.QuestionBankId == item.QuestionBankId);
            await _context.SaveChangesAsync();
        }

        SetSuccessMessage("تم نسخ السؤال بنجاح", "Question copied successfully.");
        return RedirectToAction(nameof(Details), new { id = item.QuestionBankId });
    }

    #endregion

    #region Question Categories

    /// <summary>
    /// تصنيفات الأسئلة - Question categories
    /// </summary>
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.QuestionCategories
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    /// <summary>
    /// إضافة تصنيف جديد - Add new category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("اسم التصنيف مطلوب", "Category name is required.");
            return RedirectToAction(nameof(Categories));
        }

        var category = new QuestionCategory
        {
            Name = name,
            Description = description
        };

        _context.QuestionCategories.Add(category);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إضافة التصنيف بنجاح", "Category added successfully.");
        return RedirectToAction(nameof(Categories));
    }

    /// <summary>
    /// حذف تصنيف - Delete category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.QuestionCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        _context.QuestionCategories.Remove(category);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التصنيف بنجاح", "Category deleted successfully.");
        return RedirectToAction(nameof(Categories));
    }

    /// <summary>
    /// تعديل تصنيف - Edit category
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("اسم التصنيف مطلوب", "Category name is required.");
            return RedirectToAction(nameof(Categories));
        }

        var category = await _context.QuestionCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        category.Name = name;
        category.Description = description;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تحديث التصنيف بنجاح", "Category updated successfully.");
        return RedirectToAction(nameof(Categories));
    }

    #endregion

    #region Private Helpers

    private async Task PopulateCategoriesDropdownAsync()
    {
        ViewBag.Categories = await _context.QuestionCategories
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    #endregion
}

