using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة المستندات - Documents Management Controller
/// </summary>
public class DocumentsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ApplicationDbContext context,
        ILogger<DocumentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المستندات - Documents List
    /// </summary>
    public async Task<IActionResult> Index(string? type, string? searchTerm)
    {
        var query = _context.Documents.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(d => d.DocumentType == type);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(d => d.Title.Contains(searchTerm) || d.Description!.Contains(searchTerm));
        }

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.SearchTerm = searchTerm;

        return View(documents);
    }

    /// <summary>
    /// تفاصيل المستند - Document Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        return View(document);
    }

    /// <summary>
    /// حذف المستند - Delete Document
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        // Delete physical file from storage if it exists
        if (!string.IsNullOrEmpty(document.FilePath))
        {
            var filePath = Path.Combine("wwwroot", document.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("Deleted document file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete document file: {FilePath}", filePath);
                }
            }
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف المستند بنجاح");
        return RedirectToAction(nameof(Index));
    }
}

