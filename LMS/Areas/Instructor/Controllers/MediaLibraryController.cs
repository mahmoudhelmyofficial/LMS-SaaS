using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Content;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using LMS.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// مكتبة الوسائط - Media Library Controller
/// </summary>
public class MediaLibraryController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MediaLibraryController> _logger;
    private readonly StorageSettings _storageSettings;
    private readonly IWebHostEnvironment _environment;

    // Supported file types
    private static readonly Dictionary<string, string> MimeTypeMap = new()
    {
        // Images
        { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".gif", "image/gif" }, { ".webp", "image/webp" }, { ".svg", "image/svg+xml" },
        // Videos
        { ".mp4", "video/mp4" }, { ".webm", "video/webm" }, { ".mov", "video/quicktime" },
        { ".avi", "video/x-msvideo" }, { ".mkv", "video/x-matroska" },
        // Audio
        { ".mp3", "audio/mpeg" }, { ".wav", "audio/wav" }, { ".ogg", "audio/ogg" },
        { ".m4a", "audio/mp4" }, { ".flac", "audio/flac" },
        // Documents
        { ".pdf", "application/pdf" }, { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".zip", "application/zip" }, { ".rar", "application/x-rar-compressed" }
    };

    public MediaLibraryController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<MediaLibraryController> logger,
        IOptions<StorageSettings> storageSettings,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _storageSettings = storageSettings.Value;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string? mediaType)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var query = _context.Media
                .Where(m => m.UploadedById == userId)
                .AsQueryable();

            // Get counts for all types
            var allMedia = await _context.Media
                .Where(m => m.UploadedById == userId)
                .Select(m => new { m.MediaType })
                .ToListAsync();

            var viewModel = new MediaLibraryViewModel
            {
                TotalCount = allMedia.Count,
                ImageCount = allMedia.Count(m => m.MediaType == "Image"),
                VideoCount = allMedia.Count(m => m.MediaType == "Video"),
                DocumentCount = allMedia.Count(m => m.MediaType == "Document"),
                AudioCount = allMedia.Count(m => m.MediaType == "Audio"),
                CurrentMediaType = mediaType
            };

            if (!string.IsNullOrEmpty(mediaType))
            {
                query = query.Where(m => m.MediaType == mediaType);
            }

            viewModel.Items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MediaDisplayViewModel
                {
                    Id = m.Id,
                    Title = m.Title ?? m.OriginalFileName,
                    Description = m.Description,
                    MediaType = m.MediaType,
                    FileUrl = m.FileUrl,
                    ThumbnailUrl = m.ThumbnailUrl,
                    OriginalFileName = m.OriginalFileName,
                    Extension = m.Extension,
                    FileSize = m.FileSize,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation("Instructor {InstructorId} viewing media library. Type filter: {MediaType}", userId, mediaType ?? "All");
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading media library for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء تحميل مكتبة الوسائط.");
            return View(new MediaLibraryViewModel());
        }
    }

    [HttpGet]
    public IActionResult Upload()
    {
        return View(new MediaUploadViewModel());
    }

    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB for PDFs and large files
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(MediaUploadViewModel model)
    {
        var userId = _currentUserService.UserId;

        // Validate user
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Upload: UserId is null or empty");
            SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Check if file or URL is provided
        if (model.File == null && string.IsNullOrEmpty(model.ExternalUrl))
        {
            ModelState.AddModelError("", "يرجى رفع ملف أو إدخال رابط خارجي");
            return View(model);
        }

        // Title is auto-generated from file name if not provided
        if (string.IsNullOrWhiteSpace(model.Title) && model.File != null)
        {
            model.Title = Path.GetFileNameWithoutExtension(model.File.FileName);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Media? media = null;
        string? savedFilePath = null;

        try
        {
            if (model.File != null)
            {
                // Save file to disk FIRST (outside transaction)
                media = await SaveUploadedFileAsync(model.File, model.Title, model.Description, userId);
                savedFilePath = media.StoragePath;
            }
            else
            {
                // External URL - no file to save
                media = new Media
                {
                    Title = model.Title,
                    Description = model.Description,
                    FileUrl = model.ExternalUrl!,
                    MediaType = DetectMediaTypeFromUrl(model.ExternalUrl!),
                    OriginalFileName = model.Title,
                    StoredFileName = model.Title,
                    StorageProvider = "External",
                    UploadedById = userId,
                    IsPublic = true
                };
            }

            // Now save to database using proper execution strategy pattern
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Media.Add(media);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Media '{MediaTitle}' (ID: {MediaId}) uploaded by instructor {InstructorId}", 
                model.Title, media.Id, userId);
            SetSuccessMessage("تم رفع الملف بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            // If database save failed and we have a local file, delete it
            if (!string.IsNullOrEmpty(savedFilePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, savedFilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                        _logger.LogInformation("Cleaned up orphaned media file: {FilePath}", filePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to cleanup orphaned media file: {FilePath}", filePath);
                    }
                }
            }
            
            _logger.LogError(ex, "Error uploading media by instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء رفع الملف: " + ex.Message);
            return View(model);
        }
    }

    /// <summary>
    /// API endpoint for AJAX file upload
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB for PDFs and large files
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadApi([FromForm] MediaUploadViewModel model)
    {
        var userId = _currentUserService.UserId;

        // Validate user
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("UploadApi: UserId is null or empty");
            return Json(new MediaUploadResultViewModel
            {
                Success = false,
                Message = "حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى."
            });
        }

        Media? media = null;
        string? savedFilePath = null;

        try
        {
            if (model.File == null && string.IsNullOrEmpty(model.ExternalUrl))
            {
                return Json(new MediaUploadResultViewModel
                {
                    Success = false,
                    Message = "يرجى رفع ملف أو إدخال رابط خارجي"
                });
            }

            // Auto-generate title from file name if not provided
            if (string.IsNullOrWhiteSpace(model.Title) && model.File != null)
            {
                model.Title = Path.GetFileNameWithoutExtension(model.File.FileName);
            }

            if (model.File != null)
            {
                // Save file to disk FIRST (outside transaction)
                media = await SaveUploadedFileAsync(model.File, model.Title, model.Description, userId);
                savedFilePath = media.StoragePath;
            }
            else
            {
                // External URL - no file to save
                media = new Media
                {
                    Title = model.Title,
                    Description = model.Description,
                    FileUrl = model.ExternalUrl!,
                    MediaType = DetectMediaTypeFromUrl(model.ExternalUrl!),
                    OriginalFileName = model.Title,
                    StoredFileName = model.Title,
                    StorageProvider = "External",
                    UploadedById = userId,
                    IsPublic = true
                };
            }

            // Now save to database using proper execution strategy pattern
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Media.Add(media);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Media '{MediaTitle}' (ID: {MediaId}) uploaded via API by instructor {InstructorId}", 
                model.Title, media.Id, userId);

            return Json(new MediaUploadResultViewModel
            {
                Success = true,
                Message = "تم رفع الملف بنجاح",
                Media = new MediaDisplayViewModel
                {
                    Id = media.Id,
                    Title = media.Title ?? media.OriginalFileName,
                    MediaType = media.MediaType,
                    FileUrl = media.FileUrl,
                    ThumbnailUrl = media.ThumbnailUrl,
                    FileSize = media.FileSize,
                    CreatedAt = media.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            // If database save failed and we have a local file, delete it
            if (!string.IsNullOrEmpty(savedFilePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, savedFilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                        _logger.LogInformation("Cleaned up orphaned media file: {FilePath}", filePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to cleanup orphaned media file: {FilePath}", filePath);
                    }
                }
            }
            
            _logger.LogError(ex, "Error uploading media via API by instructor {InstructorId}", userId);
            return Json(new MediaUploadResultViewModel
            {
                Success = false,
                Message = "حدث خطأ أثناء رفع الملف: " + ex.Message
            });
        }
    }

    /// <summary>
    /// Get media items for picker modal (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMediaItems(string? mediaType, string? search, int page = 1, int pageSize = 20)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var query = _context.Media
                .Where(m => m.UploadedById == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(mediaType) && mediaType != "All")
            {
                query = query.Where(m => m.MediaType == mediaType);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m => 
                    (m.Title != null && m.Title.Contains(search)) ||
                    m.OriginalFileName.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MediaPickerItemViewModel
                {
                    Id = m.Id,
                    Title = m.Title ?? m.OriginalFileName,
                    MediaType = m.MediaType,
                    FileUrl = m.FileUrl,
                    ThumbnailUrl = m.ThumbnailUrl ?? m.FileUrl,
                    FileSize = m.FileSize,
                    FormattedSize = FormatFileSize(m.FileSize)
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                items,
                totalCount,
                page,
                pageSize,
                hasMore = (page * pageSize) < totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media items for picker");
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Get single media item details
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMediaItem(int id)
    {
        var userId = _currentUserService.UserId;

        var media = await _context.Media
            .Where(m => m.Id == id && m.UploadedById == userId)
            .Select(m => new MediaDisplayViewModel
            {
                Id = m.Id,
                Title = m.Title ?? m.OriginalFileName,
                Description = m.Description,
                MediaType = m.MediaType,
                FileUrl = m.FileUrl,
                ThumbnailUrl = m.ThumbnailUrl,
                FileSize = m.FileSize,
                CreatedAt = m.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (media == null)
        {
            return Json(new { success = false, message = "الملف غير موجود" });
        }

        return Json(new { success = true, media });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var media = await _context.Media
            .FirstOrDefaultAsync(m => m.Id == id && m.UploadedById == userId);

        if (media == null)
        {
            _logger.LogWarning("NotFound: Media {MediaId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("الملف غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        var storagePath = media.StoragePath;
        var storageProvider = media.StorageProvider;

        var (success, error) = await _context.TryExecuteInTransactionAsync(async () =>
        {
            // Delete physical file if local storage
            if (storageProvider == "Local" && !string.IsNullOrEmpty(storagePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, storagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Media.Remove(media);
            await _context.SaveChangesAsync();
        }, _logger);

        if (!success)
        {
            SetErrorMessage(error ?? "حدث خطأ أثناء حذف الملف.");
            return RedirectToAction(nameof(Index));
        }

        _logger.LogInformation("Media {MediaId} deleted by instructor {InstructorId}", id, userId);
        SetSuccessMessage("تم حذف الملف بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteApi(int id)
    {
        var userId = _currentUserService.UserId;

        var media = await _context.Media
            .FirstOrDefaultAsync(m => m.Id == id && m.UploadedById == userId);

        if (media == null)
        {
            return Json(new { success = false, message = "الملف غير موجود" });
        }

        try
        {
            // Delete physical file if local storage
            if (media.StorageProvider == "Local" && !string.IsNullOrEmpty(media.StoragePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, media.StoragePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Media.Remove(media);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Media {MediaId} deleted via API by instructor {InstructorId}", id, userId);
            return Json(new { success = true, message = "تم حذف الملف بنجاح" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media {MediaId} via API", id);
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// نسخ ملف وسائط - Duplicate a media item (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyApi(int id)
    {
        var userId = _currentUserService.UserId;

        var originalMedia = await _context.Media
            .FirstOrDefaultAsync(m => m.Id == id && m.UploadedById == userId);

        if (originalMedia == null)
        {
            return Json(new { success = false, message = "الملف غير موجود" });
        }

        try
        {
            var duplicateMedia = new Media
            {
                Title = originalMedia.Title + " (نسخة)",
                Description = originalMedia.Description,
                MediaType = originalMedia.MediaType,
                FileUrl = originalMedia.FileUrl,
                ThumbnailUrl = originalMedia.ThumbnailUrl,
                OriginalFileName = originalMedia.OriginalFileName,
                StoredFileName = originalMedia.StoredFileName,
                Extension = originalMedia.Extension,
                MimeType = originalMedia.MimeType,
                FileSize = originalMedia.FileSize,
                StorageProvider = originalMedia.StorageProvider,
                StoragePath = originalMedia.StoragePath,
                Folder = originalMedia.Folder,
                UploadedById = userId,
                IsPublic = originalMedia.IsPublic
            };

            // If local storage, copy the physical file
            if (originalMedia.StorageProvider == "Local" && !string.IsNullOrEmpty(originalMedia.StoragePath))
            {
                var sourcePath = Path.Combine(_environment.WebRootPath, originalMedia.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(sourcePath))
                {
                    var extension = Path.GetExtension(originalMedia.OriginalFileName);
                    var newFileName = $"{Guid.NewGuid():N}{extension}";
                    var folderPath = Path.GetDirectoryName(originalMedia.StoragePath.TrimStart('/'));
                    var destDir = Path.Combine(_environment.WebRootPath, folderPath ?? "uploads");
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    var destPath = Path.Combine(destDir, newFileName);

                    System.IO.File.Copy(sourcePath, destPath);

                    var relativePath = $"/{folderPath}/{newFileName}".Replace("\\", "/");
                    duplicateMedia.StoragePath = relativePath;
                    duplicateMedia.FileUrl = relativePath;
                    duplicateMedia.StoredFileName = newFileName;
                    if (originalMedia.MediaType == "Image" || originalMedia.MediaType == "image")
                    {
                        duplicateMedia.ThumbnailUrl = relativePath;
                    }
                }
            }

            _context.Media.Add(duplicateMedia);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Media {MediaId} duplicated as {NewMediaId} by instructor {InstructorId}",
                id, duplicateMedia.Id, userId);

            return Json(new
            {
                success = true,
                message = "تم نسخ الملف بنجاح",
                media = new
                {
                    id = duplicateMedia.Id,
                    title = duplicateMedia.Title,
                    fileUrl = duplicateMedia.FileUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying media {MediaId} via API", id);
            return Json(new { success = false, message = "حدث خطأ أثناء نسخ الملف" });
        }
    }

    #region Private Helpers

    private async Task<Media> SaveUploadedFileAsync(IFormFile file, string title, string? description, string userId)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mediaType = DetectMediaType(extension);
        var mimeType = MimeTypeMap.GetValueOrDefault(extension, "application/octet-stream");

        // Validate file size - use larger limit for PDFs and documents
        long maxSizeBytes;
        if (extension == ".pdf" || mediaType == "Document")
        {
            maxSizeBytes = _storageSettings.MaxDocumentSizeMB * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                throw new InvalidOperationException(
                    $"حجم ملف {extension.ToUpperInvariant()} يتجاوز الحد المسموح ({_storageSettings.MaxDocumentSizeMB} MB). " +
                    $"حجم الملف الحالي: {file.Length / (1024.0 * 1024.0):F2} MB");
            }
        }
        else
        {
            maxSizeBytes = _storageSettings.MaxFileSizeMB * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                throw new InvalidOperationException(
                    $"حجم الملف يتجاوز الحد المسموح ({_storageSettings.MaxFileSizeMB} MB). " +
                    $"حجم الملف الحالي: {file.Length / (1024.0 * 1024.0):F2} MB");
            }
        }

        // Validate file extension is allowed
        var allAllowedExtensions = _storageSettings.AllowedImageExtensions
            .Concat(_storageSettings.AllowedVideoExtensions)
            .Concat(_storageSettings.AllowedDocumentExtensions)
            .ToList();

        if (!allAllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"نوع الملف {extension.ToUpperInvariant()} غير مدعوم. " +
                $"الأنواع المدعومة: {string.Join(", ", allAllowedExtensions)}");
        }

        // Create storage folder
        var folderPath = Path.Combine("uploads", "media", userId, DateTime.UtcNow.ToString("yyyy/MM"));
        var absoluteFolderPath = Path.Combine(_environment.WebRootPath, folderPath);
        Directory.CreateDirectory(absoluteFolderPath);

        // Generate unique file name
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absoluteFilePath = Path.Combine(absoluteFolderPath, storedFileName);
        var relativeFilePath = $"/{folderPath}/{storedFileName}".Replace("\\", "/");

        // Save file
        using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Generate thumbnail for images
        string? thumbnailUrl = null;
        if (mediaType == "Image")
        {
            thumbnailUrl = relativeFilePath; // Use same image as thumbnail for now
        }

        return new Media
        {
            Title = title,
            Description = description,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FileUrl = relativeFilePath,
            ThumbnailUrl = thumbnailUrl,
            Extension = extension,
            MimeType = mimeType,
            MediaType = mediaType,
            FileSize = file.Length,
            StorageProvider = "Local",
            StoragePath = relativeFilePath,
            Folder = folderPath,
            UploadedById = userId,
            IsPublic = true
        };
    }

    private static string DetectMediaType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" => "Image",
            ".mp4" or ".webm" or ".mov" or ".avi" or ".mkv" or ".wmv" => "Video",
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" or ".aac" => "Audio",
            ".pdf" or ".doc" or ".docx" or ".ppt" or ".pptx" or ".xls" or ".xlsx" or ".txt" or ".rtf" => "Document",
            _ => "Other"
        };
    }

    private static string DetectMediaTypeFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.LocalPath);
            if (!string.IsNullOrEmpty(extension))
            {
                return DetectMediaType(extension);
            }
            
            // Try to detect from common video platforms
            var host = uri.Host.ToLower();
            if (host.Contains("youtube") || host.Contains("youtu.be") || host.Contains("vimeo"))
            {
                return "Video";
            }
        }
        catch { }
        
        return "Other";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    #endregion
}

