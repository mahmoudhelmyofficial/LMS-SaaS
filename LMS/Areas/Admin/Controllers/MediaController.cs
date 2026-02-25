using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الوسائط - Media Management Controller
/// </summary>
public class MediaController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        ApplicationDbContext context,
        ILogger<MediaController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// مكتبة الوسائط - Media Library
    /// </summary>
    public async Task<IActionResult> Index(string? type, string? searchTerm)
    {
        var query = _context.Media.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(m => m.MediaType == type);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(m => m.FileName.Contains(searchTerm) || m.Title!.Contains(searchTerm));
        }

        var media = await query
            .OrderByDescending(m => m.UploadedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.SearchTerm = searchTerm;

        return View(media);
    }

    /// <summary>
    /// تفاصيل الوسائط - Media Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var media = await _context.Media.FindAsync(id);
        if (media == null)
            return NotFound();

        return View(media);
    }

    /// <summary>
    /// حذف الوسائط - Delete Media
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var media = await _context.Media.FindAsync(id);
        if (media == null)
            return NotFound();

        // Delete physical file from storage if it exists
        if (!string.IsNullOrEmpty(media.FilePath))
        {
            var filePath = Path.Combine("wwwroot", media.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("Deleted media file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete media file: {FilePath}", filePath);
                }
            }
        }

        _context.Media.Remove(media);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الملف بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إحصائيات الوسائط - Media Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var totalMedia = await _context.Media.CountAsync();
        var totalSize = await _context.Media.SumAsync(m => (long?)m.FileSize) ?? 0;

        var stats = new MediaStatisticsViewModel
        {
            TotalFiles = totalMedia,
            TotalSizeBytes = totalSize,
            TotalSizeMB = totalSize / (1024.0 * 1024.0),
            TotalSizeGB = totalSize / (1024.0 * 1024.0 * 1024.0),
            ImageCount = await _context.Media.CountAsync(m => m.MediaType == "Image"),
            VideoCount = await _context.Media.CountAsync(m => m.MediaType == "Video"),
            DocumentCount = await _context.Media.CountAsync(m => m.MediaType == "Document"),
            AudioCount = await _context.Media.CountAsync(m => m.MediaType == "Audio"),
            AvgFileSizeMB = totalMedia > 0 ? totalSize / (1024.0 * 1024.0 * totalMedia) : 0
        };

        return View(stats);
    }
}

/// <summary>
/// نموذج إحصائيات الوسائط - Media Statistics ViewModel
/// </summary>
public class MediaStatisticsViewModel
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public double TotalSizeMB { get; set; }
    public double TotalSizeGB { get; set; }
    public int ImageCount { get; set; }
    public int VideoCount { get; set; }
    public int DocumentCount { get; set; }
    public int AudioCount { get; set; }
    public double AvgFileSizeMB { get; set; }
}

