using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة التسجيلات - Recordings Controller
/// </summary>
public class RecordingsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<RecordingsController> _logger;

    public RecordingsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILiveSessionService liveSessionService,
        ILogger<RecordingsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التسجيلات - Recordings list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        var recordings = await _context.LiveClassRecordings
            .Include(r => r.LiveClass)
            .Where(r => r.LiveClass.InstructorId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RecordingDisplayViewModel
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                LiveClassTitle = r.LiveClass.Title,
                LiveClassId = r.LiveClassId,
                DurationSeconds = r.DurationSeconds,
                FileSize = r.FileSize,
                Resolution = r.Resolution,
                ProcessingStatus = r.ProcessingStatus,
                IsPublished = r.IsPublished,
                IsAvailable = r.IsAvailable,
                ViewCount = r.ViewCount,
                DownloadCount = r.DownloadCount,
                RecordedAt = r.RecordedAt,
                CreatedAt = r.CreatedAt,
                VideoUrl = r.VideoUrl,
                ThumbnailUrl = r.ThumbnailUrl,
                StorageType = r.StorageType
            })
            .ToListAsync();

        return View(recordings);
    }

    /// <summary>
    /// رفع تسجيل جديد - Upload recording
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Upload()
    {
        var userId = _currentUserService.UserId;
        await LoadUploadViewBag(userId!);
        return View(new RecordingUploadViewModel());
    }

    /// <summary>
    /// حفظ التسجيل - Save recording
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)] // 2GB
    public async Task<IActionResult> Upload(RecordingUploadViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (model.RecordingFile == null && string.IsNullOrEmpty(model.ExternalUrl))
        {
            ModelState.AddModelError("", "يجب رفع ملف أو إدخال رابط خارجي");
        }

        if (!ModelState.IsValid)
        {
            await LoadUploadViewBag(userId!);
            return View(model);
        }

        try
        {
            if (model.RecordingFile != null)
            {
                using var stream = model.RecordingFile.OpenReadStream();
                var recording = await _liveSessionService.SaveRecordingAsync(
                    model.LiveClassId, userId!, stream, model.RecordingFile.FileName, model.RecordingFile.ContentType);

                recording.Title = model.Title;
                recording.Description = model.Description;
                recording.AccessRequiresPurchase = model.AccessRequiresPurchase;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Recording uploaded for live class {LiveClassId} by instructor {InstructorId}",
                    model.LiveClassId, userId);

                SetSuccessMessage("تم رفع التسجيل بنجاح");
                return RedirectToAction(nameof(Index));
            }
            else
            {
                var recording = await _liveSessionService.AddExternalRecordingAsync(
                    model.LiveClassId, userId!, model.ExternalUrl!, model.Title, model.Description);

                recording.AccessRequiresPurchase = model.AccessRequiresPurchase;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "External recording added for live class {LiveClassId} by instructor {InstructorId}",
                    model.LiveClassId, userId);

                SetSuccessMessage("تم إضافة التسجيل الخارجي بنجاح");
                return RedirectToAction(nameof(Index));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading recording for live class {LiveClassId}", model.LiveClassId);
            SetErrorMessage("حدث خطأ أثناء رفع التسجيل");
            await LoadUploadViewBag(userId!);
            return View(model);
        }
    }

    /// <summary>
    /// تفاصيل التسجيل - Recording details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;
        var recording = await _context.LiveClassRecordings
            .Include(r => r.LiveClass)
            .FirstOrDefaultAsync(r => r.Id == id && r.LiveClass.InstructorId == userId && !r.IsDeleted);

        if (recording == null)
        {
            SetErrorMessage("التسجيل غير موجود");
            return RedirectToAction(nameof(Index));
        }

        var vm = new RecordingDisplayViewModel
        {
            Id = recording.Id,
            Title = recording.Title,
            Description = recording.Description,
            LiveClassTitle = recording.LiveClass.Title,
            LiveClassId = recording.LiveClassId,
            DurationSeconds = recording.DurationSeconds,
            FileSize = recording.FileSize,
            Resolution = recording.Resolution,
            ProcessingStatus = recording.ProcessingStatus,
            IsPublished = recording.IsPublished,
            IsAvailable = recording.IsAvailable,
            ViewCount = recording.ViewCount,
            DownloadCount = recording.DownloadCount,
            RecordedAt = recording.RecordedAt,
            CreatedAt = recording.CreatedAt,
            VideoUrl = recording.VideoUrl,
            ThumbnailUrl = recording.ThumbnailUrl,
            StorageType = recording.StorageType
        };

        return View(vm);
    }

    /// <summary>
    /// نشر التسجيل - Publish recording
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _liveSessionService.PublishRecordingAsync(id, userId!);
        if (result) SetSuccessMessage("تم نشر التسجيل");
        else SetErrorMessage("فشل نشر التسجيل");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إلغاء نشر التسجيل - Unpublish recording
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(int id)
    {
        var userId = _currentUserService.UserId;
        var result = await _liveSessionService.UnpublishRecordingAsync(id, userId!);
        if (result) SetSuccessMessage("تم إلغاء نشر التسجيل");
        else SetErrorMessage("فشل إلغاء نشر التسجيل");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف التسجيل - Delete recording
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;
        var recording = await _context.LiveClassRecordings
            .Include(r => r.LiveClass)
            .FirstOrDefaultAsync(r => r.Id == id && r.LiveClass.InstructorId == userId);

        if (recording == null)
        {
            SetErrorMessage("التسجيل غير موجود");
            return RedirectToAction(nameof(Index));
        }

        recording.IsDeleted = true;
        recording.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Recording {RecordingId} deleted by instructor {InstructorId}",
            id, userId);

        SetSuccessMessage("تم حذف التسجيل");
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadUploadViewBag(string userId)
    {
        var completedSessions = await _context.LiveClasses
            .Where(lc => lc.InstructorId == userId && !lc.IsDeleted &&
                         (lc.Status == LiveClassStatus.Completed || lc.Status == LiveClassStatus.Live))
            .OrderByDescending(lc => lc.ScheduledStartTime)
            .Select(lc => new { lc.Id, lc.Title, lc.ScheduledStartTime })
            .ToListAsync();

        ViewBag.LiveClasses = new SelectList(
            completedSessions.Select(s => new { s.Id, Display = $"{s.Title} - {s.ScheduledStartTime:yyyy/MM/dd}" }),
            "Id", "Display");
    }
}
