using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الأسئلة الشائعة - FAQ Management Controller
/// </summary>
public class FaqController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FaqController> _logger;
    private readonly ISystemConfigurationService _configService;

    public FaqController(
        ApplicationDbContext context, 
        ILogger<FaqController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الأسئلة الشائعة - FAQ list
    /// </summary>
    public async Task<IActionResult> Index(string? category, bool? published, int page = 1)
    {
        var query = _context.Faqs
            .Include(f => f.Course)
            .Include(f => f.Lesson)
            .Include(f => f.Author)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(f => f.Category == category);

        if (published.HasValue)
            query = query.Where(f => f.IsPublished == published.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("faqs", 20);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var faqs = await query
            .OrderBy(f => f.DisplayOrder)
            .ThenByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Category = category;
        ViewBag.Published = published;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;

        // Get distinct categories with counts
        var allFaqs = await _context.Faqs.ToListAsync();
        var categoryCounts = allFaqs
            .Where(f => !string.IsNullOrEmpty(f.Category))
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key!, g => g.Count());
        
        ViewBag.Categories = categoryCounts.Keys.ToList();
        ViewBag.CategoryCounts = categoryCounts;

        // Statistics
        var mostViewedCount = allFaqs.Any() ? allFaqs.Max(f => f.ViewCount) : 0;
        var helpfulRate = allFaqs.Any() && allFaqs.Sum(f => f.HelpfulCount + f.NotHelpfulCount) > 0
            ? (allFaqs.Sum(f => f.HelpfulCount) * 100 / (allFaqs.Sum(f => f.HelpfulCount) + allFaqs.Sum(f => f.NotHelpfulCount)))
            : 0;
        
        ViewBag.MostViewedCount = mostViewedCount;
        ViewBag.HelpfulRate = helpfulRate;

        return View(faqs);
    }

    /// <summary>
    /// تفاصيل السؤال - FAQ details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var faq = await _context.Faqs
            .Include(f => f.Course)
            .Include(f => f.Lesson)
            .Include(f => f.Author)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faq == null)
            return NotFound();

        return View(faq);
    }

    /// <summary>
    /// إنشاء سؤال جديد - Create new FAQ
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new FaqCreateViewModel());
    }

    /// <summary>
    /// حفظ السؤال الجديد - Save new FAQ
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FaqCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var faq = new Faq
            {
                Question = model.Question,
                Answer = model.Answer,
                Category = model.Category,
                CourseId = model.CourseId,
                LessonId = model.LessonId,
                DisplayOrder = model.DisplayOrder,
                IsPublished = model.IsPublished,
                Tags = model.Tags
            };

            _context.Faqs.Add(faq);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء السؤال بنجاح");
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(model.CourseId);
        return View(model);
    }

    /// <summary>
    /// تعديل سؤال - Edit FAQ
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq == null)
            return NotFound();

        var model = new FaqEditViewModel
        {
            Id = faq.Id,
            Question = faq.Question,
            Answer = faq.Answer,
            Category = faq.Category,
            CourseId = faq.CourseId,
            LessonId = faq.LessonId,
            DisplayOrder = faq.DisplayOrder,
            IsPublished = faq.IsPublished,
            Tags = faq.Tags
        };

        await PopulateDropdownsAsync(faq.CourseId);
        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات السؤال - Save FAQ changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FaqEditViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var faq = await _context.Faqs.FindAsync(id);
            if (faq == null)
                return NotFound();

            faq.Question = model.Question;
            faq.Answer = model.Answer;
            faq.Category = model.Category;
            faq.CourseId = model.CourseId;
            faq.LessonId = model.LessonId;
            faq.DisplayOrder = model.DisplayOrder;
            faq.IsPublished = model.IsPublished;
            faq.Tags = model.Tags;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث السؤال بنجاح");
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(model.CourseId);
        return View(model);
    }

    /// <summary>
    /// حذف سؤال - Delete FAQ
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq == null)
            return NotFound();

        _context.Faqs.Remove(faq);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف السؤال بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة النشر - Toggle publish status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq == null)
            return NotFound();

        faq.IsPublished = !faq.IsPublished;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(faq.IsPublished ? "نشر" : "إخفاء")} السؤال");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إعادة ترتيب الأسئلة - Reorder FAQs
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id, int newOrder)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq == null)
            return NotFound();

        faq.DisplayOrder = newOrder;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// تصدير الأسئلة الشائعة - Export FAQs
    /// </summary>
    public async Task<IActionResult> Export(string? category)
    {
        var query = _context.Faqs
            .Include(f => f.Course)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(f => f.Category == category);

        var faqs = await query
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();

        // Generate CSV export
        try
        {
            var csv = "ID,Question,Answer,Category,Display Order,Is Active,Created At\n";
            foreach (var faq in faqs)
            {
                var question = faq.Question?.Replace("\"", "\"\"").Replace("\n", " ") ?? "";
                var answer = faq.Answer?.Replace("\"", "\"\"").Replace("\n", " ") ?? "";
                var faqCategory = faq.Category?.Replace("\"", "\"\"") ?? "";
                
                csv += $"{faq.Id}," +
                       $"\"{question}\"," +
                       $"\"{answer}\"," +
                       $"\"{faqCategory}\"," +
                       $"{faq.DisplayOrder}," +
                       $"{faq.IsActive}," +
                       $"{faq.CreatedAt:yyyy-MM-dd HH:mm}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"faq-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("FAQ export generated. Total: {Count} items", faqs.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting FAQ");
            SetErrorMessage("حدث خطأ أثناء التصدير");
            return RedirectToAction(nameof(Index));
        }
    }

    #region Private Helpers

    private async Task PopulateDropdownsAsync(int? selectedCourseId = null)
    {
        // Courses dropdown
        ViewBag.Courses = await _context.Courses
            .Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();

        // Lessons dropdown (if course is selected)
        if (selectedCourseId.HasValue)
        {
            ViewBag.Lessons = await _context.Lessons
                .Where(l => l.Module.CourseId == selectedCourseId.Value)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Title
                })
                .ToListAsync();
        }
    }

    #endregion
}

