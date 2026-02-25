using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Content;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// إدارة المستندات - Documents Management Controller
/// </summary>
public class DocumentsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DocumentsController> _logger;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private readonly string[] AllowedFileTypes = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp" };

    public DocumentsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<DocumentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة المستندات - Documents list
    /// </summary>
    public async Task<IActionResult> Index(string? type, DateTime? from, DateTime? to, int page = 1)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Documents
            .Where(d => d.OwnerId == userId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(d => d.RelatedEntityType == type);

        if (from.HasValue)
            query = query.Where(d => d.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(d => d.CreatedAt <= to.Value);

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(d => new DocumentListViewModel
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                OriginalFileName = d.OriginalFileName,
                FileSize = d.FileSize,
                MimeType = d.MimeType,
                RelatedEntityType = d.RelatedEntityType,
                IsPublic = d.IsPublic,
                IsDownloadable = d.IsDownloadable,
                DownloadCount = d.DownloadCount,
                Version = d.Version,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Page = page;
        ViewBag.TotalCount = await query.CountAsync();

        return View(documents);
    }

    /// <summary>
    /// تفاصيل المستند - Document details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

        if (document == null)
            return NotFound();

        var model = new DocumentViewModel
        {
            Id = document.Id,
            Title = document.Title,
            Description = document.Description,
            OriginalFileName = document.OriginalFileName,
            FileUrl = document.FileUrl,
            FileSize = document.FileSize,
            MimeType = document.MimeType,
            PageCount = document.PageCount,
            WordCount = document.WordCount,
            RelatedEntityType = document.RelatedEntityType,
            RelatedEntityId = document.RelatedEntityId,
            IsPublic = document.IsPublic,
            IsDownloadable = document.IsDownloadable,
            DownloadCount = document.DownloadCount,
            Version = document.Version,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };

        // Get related entity name if applicable
        if (!string.IsNullOrEmpty(document.RelatedEntityType) && document.RelatedEntityId.HasValue)
        {
            model.RelatedEntityName = await GetRelatedEntityName(document.RelatedEntityType, document.RelatedEntityId.Value, userId);
        }

        return View(model);
    }

    /// <summary>
    /// رفع مستند جديد - Upload new document
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Upload()
    {
        await PopulateEntityDropdownsAsync();

        var model = new DocumentUploadViewModel
        {
            IsPublic = false,
            IsDownloadable = true
        };

        return View(model);
    }

    /// <summary>
    /// حفظ المستند المرفوع - Save uploaded document
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model)
    {
        var userId = _currentUserService.UserId;

        // Validate user
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Upload: UserId is null or empty");
            SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // Validate file first (before ModelState check)
        if (model.File == null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "يرجى اختيار ملف");
        }
        else
        {
            // Validate file size
            if (model.File.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError(nameof(model.File), $"حجم الملف يجب ألا يتجاوز {MaxFileSizeBytes / (1024 * 1024)} ميجابايت");
                _logger.LogWarning("Instructor {InstructorId} attempted to upload file exceeding size limit: {FileSize} bytes", userId, model.File.Length);
            }

            // Validate file extension
            var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
            if (!AllowedFileTypes.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.File), $"نوع الملف غير مدعوم. الأنواع المدعومة: {string.Join(", ", AllowedFileTypes)}");
                _logger.LogWarning("Instructor {InstructorId} attempted to upload unsupported file type: {Extension}", userId, ext);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateEntityDropdownsAsync();
            return View(model);
        }

        // Verify ownership of related entity if specified
        if (!string.IsNullOrEmpty(model.RelatedEntityType) && model.RelatedEntityId.HasValue)
        {
            var hasAccess = await VerifyEntityOwnership(model.RelatedEntityType, model.RelatedEntityId.Value, userId);
            if (!hasAccess)
            {
                _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to attach document to {EntityType} {EntityId}", userId, model.RelatedEntityType, model.RelatedEntityId);
                SetErrorMessage("غير مصرح لك بربط المستند بهذا الكيان");
                await PopulateEntityDropdownsAsync();
                return View(model);
            }
        }

        Document? document = null;
        string? savedFilePath = null;
        
        try
        {
            // Generate file name and path BEFORE transaction
            var extension = Path.GetExtension(model.File!.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var relativePath = $"/uploads/documents/{fileName}";
            
            // Get absolute path for file storage
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            
            var absoluteFilePath = Path.Combine(uploadsFolder, fileName);
            
            // Save file to disk BEFORE database transaction
            using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }
            savedFilePath = absoluteFilePath;
            
            // Now save to database using transaction
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    document = new Document
                    {
                        OwnerId = userId,
                        Title = model.Title,
                        Description = model.Description,
                        OriginalFileName = model.File.FileName,
                        FileUrl = relativePath,
                        FileSize = model.File.Length,
                        MimeType = model.File.ContentType,
                        RelatedEntityType = model.RelatedEntityType,
                        RelatedEntityId = model.RelatedEntityId,
                        IsPublic = model.IsPublic,
                        IsDownloadable = model.IsDownloadable,
                        Version = 1
                    };

                    _context.Documents.Add(document);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Document {DocumentTitle} (ID: {DocumentId}) uploaded by Instructor {UserId}", model.Title, document!.Id, userId);
            SetSuccessMessage("تم رفع المستند بنجاح");
            return RedirectToAction(nameof(Details), new { id = document.Id });
        }
        catch (Exception ex)
        {
            // If database save failed, delete the uploaded file
            if (!string.IsNullOrEmpty(savedFilePath) && System.IO.File.Exists(savedFilePath))
            {
                try
                {
                    System.IO.File.Delete(savedFilePath);
                    _logger.LogInformation("Cleaned up orphaned file: {FilePath}", savedFilePath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Failed to cleanup orphaned file: {FilePath}", savedFilePath);
                }
            }
            
            _logger.LogError(ex, "Error uploading document by instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء رفع المستند. يرجى المحاولة مرة أخرى.");
        }

        await PopulateEntityDropdownsAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل المستند - Edit document
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

        if (document == null)
            return NotFound();

        await PopulateEntityDropdownsAsync();

        var model = new DocumentViewModel
        {
            Id = document.Id,
            Title = document.Title,
            Description = document.Description,
            OriginalFileName = document.OriginalFileName,
            FileUrl = document.FileUrl,
            FileSize = document.FileSize,
            MimeType = document.MimeType,
            RelatedEntityType = document.RelatedEntityType,
            RelatedEntityId = document.RelatedEntityId,
            IsPublic = document.IsPublic,
            IsDownloadable = document.IsDownloadable,
            Version = document.Version
        };

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات المستند - Save document changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DocumentViewModel model)
    {
        if (id != model.Id)
        {
            _logger.LogWarning("BadRequest: Document ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, model.Id);
            SetErrorMessage("خطأ في معرّف المستند.");
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

            if (document == null)
            {
                _logger.LogWarning("NotFound: Document {DocumentId} not found or instructor {InstructorId} unauthorized.", id, userId);
                SetErrorMessage("المستند غير موجود أو ليس لديك صلاحية عليه.");
                return NotFound();
            }

            // Verify ownership of related entity if changed
            if (!string.IsNullOrEmpty(model.RelatedEntityType) && model.RelatedEntityId.HasValue)
            {
                var hasAccess = await VerifyEntityOwnership(model.RelatedEntityType, model.RelatedEntityId.Value, userId);
                if (!hasAccess)
                {
                    _logger.LogWarning("Unauthorized access: Instructor {InstructorId} attempted to attach document {DocumentId} to {EntityType} {EntityId}", userId, id, model.RelatedEntityType, model.RelatedEntityId);
                    SetErrorMessage("غير مصرح لك بربط المستند بهذا الكيان");
                    await PopulateEntityDropdownsAsync();
                    return View(model);
                }
            }

            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    document.Title = model.Title;
                    document.Description = model.Description;
                    document.RelatedEntityType = model.RelatedEntityType;
                    document.RelatedEntityId = model.RelatedEntityId;
                    document.IsPublic = model.IsPublic;
                    document.IsDownloadable = model.IsDownloadable;

                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("Document {DocumentId} updated by Instructor {UserId}", id, userId);
                SetSuccessMessage("تم تحديث المستند بنجاح");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId} by instructor {InstructorId}.", id, userId);
                SetErrorMessage("حدث خطأ أثناء تحديث المستند.");
            }
        }

        await PopulateEntityDropdownsAsync();
        return View(model);
    }

    /// <summary>
    /// حذف المستند - Delete document
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

        if (document == null)
        {
            _logger.LogWarning("NotFound: Document {DocumentId} not found or instructor {InstructorId} unauthorized for deletion.", id, userId);
            SetErrorMessage("المستند غير موجود أو ليس لديك صلاحية عليه.");
            return NotFound();
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                // In a real application, you would also delete the physical file
                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Document {DocumentId} deleted by Instructor {UserId}", id, userId);
            SetSuccessMessage("تم حذف المستند بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId} by instructor {InstructorId}.", id, userId);
            SetErrorMessage("حدث خطأ أثناء حذف المستند.");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// تحديث إصدار المستند - Update document version
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UpdateVersion(int id)
    {
        var userId = _currentUserService.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

        if (document == null)
            return NotFound();

        var model = new DocumentVersionViewModel
        {
            DocumentId = id
        };

        ViewBag.Document = document;
        return View(model);
    }

    /// <summary>
    /// حفظ إصدار جديد - Save new version
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVersion(DocumentVersionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _currentUserService.UserId;

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == model.DocumentId && d.OwnerId == userId);

            if (document == null)
                return NotFound();

            if (model.NewFile == null || model.NewFile.Length == 0)
            {
                ModelState.AddModelError(nameof(model.NewFile), "يرجى اختيار ملف");
                ViewBag.Document = document;
                return View(model);
            }

            // Validate file size
            if (model.NewFile.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError(nameof(model.NewFile), $"حجم الملف يجب ألا يتجاوز {MaxFileSizeBytes / (1024 * 1024)} ميجابايت");
                ViewBag.Document = document;
                return View(model);
            }

            // Create new document version
            var extension = Path.GetExtension(model.NewFile.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var fileUrl = $"/uploads/documents/{fileName}"; // Placeholder

            var newDocument = new Document
            {
                OwnerId = userId,
                Title = document.Title,
                Description = document.Description,
                OriginalFileName = model.NewFile.FileName,
                FileUrl = fileUrl,
                FileSize = model.NewFile.Length,
                MimeType = model.NewFile.ContentType,
                RelatedEntityType = document.RelatedEntityType,
                RelatedEntityId = document.RelatedEntityId,
                IsPublic = document.IsPublic,
                IsDownloadable = document.IsDownloadable,
                Version = document.Version + 1,
                PreviousVersionId = document.Id
            };

            _context.Documents.Add(newDocument);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New version created for Document {DocumentId} by Instructor {UserId}", model.DocumentId, userId);

            SetSuccessMessage("تم إنشاء إصدار جديد من المستند بنجاح");
            return RedirectToAction(nameof(Details), new { id = newDocument.Id });
        }

        var doc = await _context.Documents.FindAsync(model.DocumentId);
        ViewBag.Document = doc;
        return View(model);
    }

    /// <summary>
    /// تحميل المستند - Download document
    /// </summary>
    public async Task<IActionResult> Download(int id)
    {
        var userId = _currentUserService.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == userId);

        if (document == null)
            return NotFound();

        if (!document.IsDownloadable)
        {
            SetErrorMessage("هذا المستند غير قابل للتحميل");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Increment download count
        document.DownloadCount++;
        await _context.SaveChangesAsync();

        // In a real application, you would return the actual file
        // For now, we'll redirect to the file URL
        if (!string.IsNullOrEmpty(document.FileUrl))
        {
            return Redirect(document.FileUrl);
        }

        SetErrorMessage("الملف غير متوفر");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// الإحصائيات - Statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var userId = _currentUserService.UserId;

        var documents = await _context.Documents
            .Where(d => d.OwnerId == userId)
            .ToListAsync();

        var stats = new DocumentStatsViewModel
        {
            TotalDocuments = documents.Count,
            PublicDocuments = documents.Count(d => d.IsPublic),
            PrivateDocuments = documents.Count(d => !d.IsPublic),
            TotalFileSize = documents.Sum(d => d.FileSize),
            TotalDownloads = documents.Sum(d => d.DownloadCount),
            DocumentsByType = documents
                .GroupBy(d => d.MimeType ?? "Unknown")
                .Select(g => new DocumentTypeStatsViewModel
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(d => d.FileSize)
                })
                .OrderByDescending(d => d.Count)
                .ToList(),
            DocumentsByEntity = documents
                .Where(d => !string.IsNullOrEmpty(d.RelatedEntityType))
                .GroupBy(d => d.RelatedEntityType!)
                .Select(g => new DocumentEntityStatsViewModel
                {
                    EntityType = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(d => d.Count)
                .ToList(),
            MostDownloaded = documents
                .OrderByDescending(d => d.DownloadCount)
                .Take(10)
                .Select(d => new TopDocumentViewModel
                {
                    Id = d.Id,
                    Title = d.Title,
                    DownloadCount = d.DownloadCount
                })
                .ToList()
        };

        return View(stats);
    }

    #region Private Helpers

    private async Task PopulateEntityDropdownsAsync()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            })
            .ToListAsync();

        ViewBag.Assignments = await _context.Assignments
            .Include(a => a.Lesson)
                .ThenInclude(l => l.Module)
                    .ThenInclude(m => m.Course)
            .Where(a => a.Lesson.Module.Course.InstructorId == userId)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.Lesson.Module.Course.Title} - {a.Title}"
            })
            .ToListAsync();
    }

    private async Task<bool> VerifyEntityOwnership(string entityType, int entityId, string userId)
    {
        return entityType switch
        {
            "Course" => await _context.Courses.AnyAsync(c => c.Id == entityId && c.InstructorId == userId),
            "Assignment" => await _context.Assignments
                .Include(a => a.Lesson.Module.Course)
                .AnyAsync(a => a.Id == entityId && a.Lesson.Module.Course.InstructorId == userId),
            "Lesson" => await _context.Lessons
                .Include(l => l.Module.Course)
                .AnyAsync(l => l.Id == entityId && l.Module.Course.InstructorId == userId),
            _ => false
        };
    }

    private async Task<string?> GetRelatedEntityName(string entityType, int entityId, string userId)
    {
        return entityType switch
        {
            "Course" => await _context.Courses
                .Where(c => c.Id == entityId && c.InstructorId == userId)
                .Select(c => c.Title)
                .FirstOrDefaultAsync(),
            "Assignment" => await _context.Assignments
                .Include(a => a.Lesson.Module.Course)
                .Where(a => a.Id == entityId && a.Lesson.Module.Course.InstructorId == userId)
                .Select(a => a.Title)
                .FirstOrDefaultAsync(),
            "Lesson" => await _context.Lessons
                .Include(l => l.Module.Course)
                .Where(l => l.Id == entityId && l.Module.Course.InstructorId == userId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync(),
            _ => null
        };
    }

    #endregion
}

