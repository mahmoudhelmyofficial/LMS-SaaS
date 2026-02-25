using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Assessments;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// بنك الأسئلة للمدرس - Instructor Question Bank Controller
/// </summary>
public class QuestionBankController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QuestionBankController> _logger;

    public QuestionBankController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<QuestionBankController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة بنوك الأسئلة - Question banks list
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = _currentUserService.UserId;

        var banks = await _context.QuestionBanks
            .Include(b => b.QuestionCategory)
            .Include(b => b.Items)
            .Where(b => b.CreatedBy == userId || b.OwnerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        ViewBag.Page = page;
        return View(banks);
    }

    /// <summary>
    /// تفاصيل بنك الأسئلة - Question bank details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .Include(b => b.QuestionCategory)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id && (b.CreatedBy == userId || b.OwnerId == userId));

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
        await PopulateCategoriesDropdown();
        return View(new QuestionBankCreateViewModel());
    }

    /// <summary>
    /// حفظ بنك الأسئلة الجديد - Save new question bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionBankCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;
            
            var bank = new QuestionBank
            {
                Name = model.Name,
                Description = model.Description,
                CategoryId = model.CategoryId,
                IsPublic = false, // Instructor banks are private by default
                OwnerId = userId ?? string.Empty, // Set OwnerId for ownership
                CreatedBy = userId,
                TotalQuestions = 0
            };

            _context.QuestionBanks.Add(bank);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Question bank {BankId} created by instructor {InstructorId}", bank.Id, userId);
            SetSuccessMessage("تم إنشاء بنك الأسئلة بنجاح");
            return RedirectToAction(nameof(Details), new { id = bank.Id });
        }

        await PopulateCategoriesDropdown();
        return View(model);
    }

    /// <summary>
    /// تعديل بنك الأسئلة - Edit question bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == id && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        var viewModel = new QuestionBankEditViewModel
        {
            Id = bank.Id,
            Name = bank.Name,
            Description = bank.Description,
            CategoryId = bank.CategoryId
        };

        await PopulateCategoriesDropdown();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات بنك الأسئلة - Save question bank edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, QuestionBankEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == id && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            bank.Name = model.Name;
            bank.Description = model.Description;
            bank.CategoryId = model.CategoryId;
            bank.UpdatedBy = userId;
            bank.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Question bank {BankId} updated by instructor {InstructorId}", id, userId);
            SetSuccessMessage("تم تحديث بنك الأسئلة بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateCategoriesDropdown();
        return View(model);
    }

    /// <summary>
    /// حذف بنك الأسئلة - Delete question bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
        {
            _logger.LogWarning("NotFound: Question bank {BankId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("بنك الأسئلة غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Remove all questions in the bank
                if (bank.Items != null && bank.Items.Any())
                {
                    _context.QuestionBankItems.RemoveRange(bank.Items);
                }
                _context.QuestionBanks.Remove(bank);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Question bank {BankId} deleted by instructor {InstructorId}", id, userId);
            SetSuccessMessage("تم حذف بنك الأسئلة بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting question bank {BankId} by instructor {InstructorId}", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف بنك الأسئلة.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    #region Question Management

    /// <summary>
    /// إضافة سؤال إلى البنك - Add question to bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddQuestion(int bankId)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == bankId && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        ViewBag.BankId = bankId;
        ViewBag.BankName = bank.Name;

        return View(new QuestionBankQuestionCreateViewModel { QuestionBankId = bankId });
    }

    /// <summary>
    /// حفظ السؤال الجديد - Save new question
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(QuestionBankQuestionCreateViewModel model)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == model.QuestionBankId && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
        {
            _logger.LogWarning("NotFound: Question bank {BankId} not found or instructor {InstructorId} unauthorized.", model.QuestionBankId, userId);
            SetErrorMessage("بنك الأسئلة غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Parse difficulty level to int (1-5)
                var difficultyLevel = model.DifficultyLevel?.ToLower() switch
                {
                    "easy" => 1,
                    "medium" => 3,
                    "hard" => 5,
                    _ => 3
                };

                // Serialize options to JSON
                string? optionsJson = null;
                string correctAnswer = string.Empty;
                
                if ((model.QuestionType == QuestionType.MultipleChoice || 
                    model.QuestionType == QuestionType.TrueFalse) && 
                    model.Options != null && model.Options.Any())
                {
                    optionsJson = System.Text.Json.JsonSerializer.Serialize(model.Options);
                    // Get correct answer(s)
                    var correctOptions = model.Options
                        .Where(o => o.IsCorrect)
                        .Select(o => o.OptionText)
                        .ToList();
                    correctAnswer = string.Join(",", correctOptions);
                }

                // Create QuestionBankItem directly (not Question entity)
                var bankItem = new QuestionBankItem
                {
                    QuestionBankId = model.QuestionBankId,
                    QuestionText = model.QuestionText,
                    QuestionType = model.QuestionType,
                    Options = optionsJson,
                    CorrectAnswer = correctAnswer,
                    DefaultPoints = model.Points,
                    DifficultyLevel = difficultyLevel,
                    Explanation = model.Explanation,
                    Tags = model.Tags,
                    CreatedBy = userId
                };

                _context.QuestionBankItems.Add(bankItem);
                
                // Update bank count
                bank.QuestionsCount++;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Question {QuestionId} added to bank {BankId} by instructor {InstructorId}", 
                    bankItem.Id, model.QuestionBankId, userId);
                SetSuccessMessage("تم إضافة السؤال بنجاح");
                return RedirectToAction(nameof(Details), new { id = model.QuestionBankId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding question to bank {BankId} by instructor {InstructorId}", model.QuestionBankId, userId);
                SetErrorMessage("حدث خطأ أثناء إضافة السؤال.");
            }
        }

        ViewBag.BankId = model.QuestionBankId;
        ViewBag.BankName = bank.Name;
        return View(model);
    }

    /// <summary>
    /// تعديل سؤال - Edit question
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditQuestion(int id, int bankId)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == bankId && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        var bankItem = await _context.QuestionBankItems
            .FirstOrDefaultAsync(bi => bi.Id == id && bi.QuestionBankId == bankId);

        if (bankItem == null)
            return NotFound();

        // Convert difficulty level int to string
        var difficultyString = bankItem.DifficultyLevel switch
        {
            1 or 2 => "Easy",
            3 => "Medium",
            4 or 5 => "Hard",
            _ => "Medium"
        };

        var viewModel = new QuestionBankQuestionEditViewModel
        {
            Id = bankItem.Id,
            QuestionBankId = bankId,
            QuestionText = bankItem.QuestionText,
            QuestionType = bankItem.QuestionType,
            Points = bankItem.DefaultPoints,
            DifficultyLevel = difficultyString,
            Explanation = bankItem.Explanation,
            Tags = bankItem.Tags,
            Options = ParseOptionsFromJson(bankItem.Options)
        };

        ViewBag.BankId = bankId;
        ViewBag.BankName = bank.Name;
        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات السؤال - Save question edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion(int id, QuestionBankQuestionEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == model.QuestionBankId && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        var bankItem = await _context.QuestionBankItems
            .FirstOrDefaultAsync(bi => bi.Id == id && bi.QuestionBankId == model.QuestionBankId);

        if (bankItem == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // Parse difficulty level to int (1-5)
                var difficultyLevel = model.DifficultyLevel?.ToLower() switch
                {
                    "easy" => 1,
                    "medium" => 3,
                    "hard" => 5,
                    _ => 3
                };

                // Update question
                bankItem.QuestionText = model.QuestionText;
                bankItem.QuestionType = model.QuestionType;
                bankItem.DefaultPoints = model.Points;
                bankItem.DifficultyLevel = difficultyLevel;
                bankItem.Explanation = model.Explanation;
                bankItem.Tags = model.Tags;
                bankItem.UpdatedBy = userId;
                bankItem.UpdatedAt = DateTime.UtcNow;

                // Update options as JSON and correct answer
                if (model.QuestionType == QuestionType.MultipleChoice || 
                    model.QuestionType == QuestionType.TrueFalse)
                {
                    if (model.Options != null && model.Options.Any(o => !string.IsNullOrWhiteSpace(o.OptionText)))
                    {
                        // Filter out empty options and re-index
                        var validOptions = model.Options
                            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
                            .Select((o, idx) => new QuestionBankOptionViewModel 
                            { 
                                OptionText = o.OptionText, 
                                IsCorrect = o.IsCorrect, 
                                DisplayOrder = idx 
                            })
                            .ToList();
                        
                        bankItem.Options = System.Text.Json.JsonSerializer.Serialize(validOptions);
                        // Update correct answer(s)
                        var correctOptions = validOptions
                            .Where(o => o.IsCorrect)
                            .Select(o => o.OptionText)
                            .ToList();
                        bankItem.CorrectAnswer = string.Join(",", correctOptions);
                    }
                    else
                    {
                        bankItem.Options = null;
                        bankItem.CorrectAnswer = string.Empty;
                    }
                }
                else
                {
                    // Clear options for non-choice question types
                    bankItem.Options = null;
                    if (model.QuestionType != QuestionType.ShortAnswer && 
                        model.QuestionType != QuestionType.FillInTheBlanks)
                    {
                        bankItem.CorrectAnswer = string.Empty;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Question {QuestionId} in bank {BankId} updated by instructor {InstructorId}", 
                    id, model.QuestionBankId, userId);
                SetSuccessMessage("تم تحديث السؤال بنجاح");
                return RedirectToAction(nameof(Details), new { id = model.QuestionBankId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating question {QuestionId} in bank {BankId} by instructor {InstructorId}", 
                    id, model.QuestionBankId, userId);
                SetErrorMessage("حدث خطأ أثناء تحديث السؤال.");
            }
        }

        ViewBag.BankId = model.QuestionBankId;
        ViewBag.BankName = bank.Name;
        return View(model);
    }

    /// <summary>
    /// حذف سؤال من البنك - Delete question from bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id, int bankId)
    {
        var userId = _currentUserService.UserId;
        
        var bank = await _context.QuestionBanks
            .FirstOrDefaultAsync(b => b.Id == bankId && (b.CreatedBy == userId || b.OwnerId == userId));

        if (bank == null)
            return NotFound();

        var bankItem = await _context.QuestionBankItems
            .FirstOrDefaultAsync(bi => bi.Id == id && bi.QuestionBankId == bankId);

        if (bankItem == null)
            return NotFound();

        try
        {
            // Remove bank item
            _context.QuestionBankItems.Remove(bankItem);
            
            // Update bank count
            if (bank.QuestionsCount > 0)
            {
                bank.QuestionsCount--;
            }
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Question {QuestionId} deleted from bank {BankId} by instructor {InstructorId}", 
                id, bankId, userId);
            SetSuccessMessage("تم حذف السؤال بنجاح");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting question {QuestionId} from bank {BankId} by instructor {InstructorId}", 
                id, bankId, userId);
            SetErrorMessage("حدث خطأ أثناء حذف السؤال.");
        }

        return RedirectToAction(nameof(Details), new { id = bankId });
    }

    #endregion

    #region Question Category Management (On-the-fly)

    /// <summary>
    /// الحصول على التصنيفات الفرعية - Get subcategories (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubcategories(int categoryId)
    {
        var subcategories = await _context.QuestionCategories
            .Where(c => c.ParentId == categoryId)
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name })
            .ToListAsync();

        return Json(subcategories);
    }

    /// <summary>
    /// إضافة تصنيف أسئلة جديد - Add new question category (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory([FromBody] AddQuestionCategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف مطلوب" });
        }

        try
        {
            // Check if category with same name exists
            var exists = await _context.QuestionCategories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentId == null);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف بنفس الاسم" });
            }

            var category = new QuestionCategory
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                Icon = model.Icon ?? "help-circle",
                ParentId = null,
                DisplayOrder = await _context.QuestionCategories.Where(c => c.ParentId == null).CountAsync() + 1
            };

            _context.QuestionCategories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Question category {CategoryId} created by instructor {InstructorId}",
                category.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف بنجاح",
                category = new
                {
                    id = category.Id,
                    name = category.Name
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding question category by instructor {InstructorId}", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف" });
        }
    }

    /// <summary>
    /// إضافة تصنيف فرعي للأسئلة - Add new question subcategory (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubcategory([FromBody] AddQuestionSubcategoryModel model)
    {
        var userId = _currentUserService.UserId;

        if (model.ParentCategoryId <= 0)
        {
            return Json(new { success = false, message = "التصنيف الرئيسي مطلوب" });
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Json(new { success = false, message = "اسم التصنيف الفرعي مطلوب" });
        }

        try
        {
            // Verify parent category exists
            var parentExists = await _context.QuestionCategories
                .AnyAsync(c => c.Id == model.ParentCategoryId);

            if (!parentExists)
            {
                return Json(new { success = false, message = "التصنيف الرئيسي غير موجود" });
            }

            // Check if subcategory with same name exists under parent
            var exists = await _context.QuestionCategories
                .AnyAsync(c => c.Name == model.Name.Trim() && c.ParentId == model.ParentCategoryId);

            if (exists)
            {
                return Json(new { success = false, message = "يوجد تصنيف فرعي بنفس الاسم" });
            }

            var subcategory = new QuestionCategory
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                ParentId = model.ParentCategoryId,
                DisplayOrder = await _context.QuestionCategories.Where(c => c.ParentId == model.ParentCategoryId).CountAsync() + 1
            };

            _context.QuestionCategories.Add(subcategory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Question subcategory {SubcategoryId} created under {ParentId} by instructor {InstructorId}",
                subcategory.Id, model.ParentCategoryId, userId);

            return Json(new
            {
                success = true,
                message = "تم إضافة التصنيف الفرعي بنجاح",
                subcategory = new
                {
                    id = subcategory.Id,
                    name = subcategory.Name,
                    parentId = subcategory.ParentId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding question subcategory by instructor {InstructorId}", userId);
            return Json(new { success = false, message = "فشل إضافة التصنيف الفرعي" });
        }
    }

    #endregion

    #region Helper Methods

    private async Task PopulateCategoriesDropdown()
    {
        ViewBag.Categories = new SelectList(
            await _context.QuestionCategories
                .Where(c => c.ParentId == null)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(),
            "Id", "Name");
    }

    private List<QuestionBankOptionViewModel> ParseOptionsFromJson(string? optionsJson)
    {
        if (string.IsNullOrEmpty(optionsJson))
            return new List<QuestionBankOptionViewModel>();
        
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<List<QuestionBankOptionViewModel>>(optionsJson, options) 
                   ?? new List<QuestionBankOptionViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse options JSON: {Json}", optionsJson);
            return new List<QuestionBankOptionViewModel>();
        }
    }

    #endregion

    #region AJAX Endpoints for Course Builder

    /// <summary>
    /// الحصول على بنوك الأسئلة للقائمة المنسدلة - Get question banks for Select2 dropdown
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBanksForSelect(string? search)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var query = _context.QuestionBanks
                .Where(b => (b.CreatedBy == userId || b.OwnerId == userId || b.IsPublic) && b.IsActive && !b.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.Name.Contains(search));
            }

            var banks = await query
                .OrderByDescending(b => b.CreatedAt)
                .Take(50)
                .Select(b => new
                {
                    id = b.Id,
                    text = b.Name,
                    questionsCount = b.QuestionsCount,
                    description = b.Description
                })
                .ToListAsync();

            return Json(new { results = banks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting question banks for select");
            return Json(new { results = new List<object>() });
        }
    }

    /// <summary>
    /// الحصول على أسئلة بنك محدد - Get questions from a specific bank
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBankQuestions(int bankId, string? type, string? difficulty, string? search, int page = 1)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var bank = await _context.QuestionBanks
                .FirstOrDefaultAsync(b => b.Id == bankId && (b.CreatedBy == userId || b.OwnerId == userId || b.IsPublic) && !b.IsDeleted);

            if (bank == null)
                return Json(new { success = false, message = "بنك الأسئلة غير موجود" });

            var query = _context.QuestionBankItems
                .Where(q => q.QuestionBankId == bankId && !q.IsDeleted);

            if (!string.IsNullOrWhiteSpace(type))
            {
                // Accept both enum name (e.g. SingleChoice) and numeric string (e.g. 1) from frontend
                var qType = (QuestionType?)null;
                if (Enum.TryParse<QuestionType>(type, true, out var parsedByName))
                    qType = parsedByName;
                else if (int.TryParse(type, out var typeNum) && Enum.IsDefined(typeof(QuestionType), typeNum))
                    qType = (QuestionType)typeNum;
                if (qType.HasValue)
                    query = query.Where(q => q.QuestionType == qType.Value);
            }

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                var diffLevel = difficulty.ToLower() switch
                {
                    "easy" => 1,
                    "medium" => 3,
                    "hard" => 5,
                    _ => (int?)null
                };
                if (diffLevel.HasValue)
                    query = query.Where(q => q.DifficultyLevel == diffLevel.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(q => q.QuestionText.Contains(search));

            var total = await query.CountAsync();
            var pageSize = 20;

            var questions = await query
                .OrderBy(q => q.DisplayOrder)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    id = q.Id,
                    questionText = q.QuestionText,
                    questionType = q.QuestionType.ToString(),
                    difficultyLevel = q.DifficultyLevel <= 2 ? "Easy" : q.DifficultyLevel <= 3 ? "Medium" : "Hard",
                    defaultPoints = q.DefaultPoints,
                    tags = q.Tags,
                    timesUsed = q.TimesUsed,
                    correctAnswerRate = q.CorrectAnswerRate
                })
                .ToListAsync();

            return Json(new { success = true, questions, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank questions for bank {BankId}", bankId);
            return Json(new { success = false, message = "حدث خطأ أثناء تحميل الأسئلة" });
        }
    }

    /// <summary>
    /// استيراد أسئلة من بنك الأسئلة إلى اختبار - Import questions from bank to quiz
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportToQuiz([FromBody] ImportFromBankRequest? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null)
                return Json(new { success = false, message = "البيانات المرسلة غير صحيحة" });
            if (model.QuizId <= 0)
                return Json(new { success = false, message = "معرف الاختبار غير صالح" });
            var itemIds = model.QuestionBankItemIds ?? new List<int>();
            if (!itemIds.Any())
                return Json(new { success = false, message = "اختر سؤالاً واحداً على الأقل للاستيراد" });

            // Use the quiz service to import
            var quizService = HttpContext.RequestServices.GetRequiredService<IQuizService>();
            var result = await quizService.ImportQuestionsFromBankAsync(model.QuizId, itemIds, userId!);

            if (result.IsSuccess)
                return Json(new { success = true, message = $"تم استيراد {result.Data?.Count ?? 0} سؤال بنجاح", questions = result.Data });

            return Json(new { success = false, message = result.Error ?? "فشل الاستيراد" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing questions to quiz {QuizId}", model?.QuizId);
            return Json(new { success = false, message = "حدث خطأ أثناء استيراد الأسئلة. يرجى المحاولة مرة أخرى أو التحقق من صلاحيات بنك الأسئلة." });
        }
    }

    /// <summary>
    /// استيراد عشوائي من بنك الأسئلة - Random import from bank
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RandomImportToQuiz([FromBody] RandomImportFromBankRequest? model)
    {
        var userId = _currentUserService.UserId;

        try
        {
            if (model == null || model.QuizId <= 0 || model.QuestionBankId <= 0 || model.Count <= 0)
                return Json(new { success = false, message = "البيانات المرسلة غير صحيحة" });

            var quizService = HttpContext.RequestServices.GetRequiredService<IQuizService>();
            var result = await quizService.RandomImportFromBankAsync(model.QuizId, model.QuestionBankId, model.Count, model.QuestionType, model.DifficultyLevel, userId!);

            if (result.IsSuccess)
                return Json(new { success = true, message = $"تم استيراد {result.Data?.Count ?? 0} سؤال عشوائي بنجاح", questions = result.Data });

            return Json(new { success = false, message = result.Error ?? "فشل الاستيراد العشوائي" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error random importing questions to quiz {QuizId}", model?.QuizId);
            return Json(new { success = false, message = "حدث خطأ أثناء الاستيراد العشوائي" });
        }
    }

    #endregion
}

