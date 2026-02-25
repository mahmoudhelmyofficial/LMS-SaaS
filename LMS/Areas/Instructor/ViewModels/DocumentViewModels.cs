using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج المستند - Document view model
/// </summary>
public class DocumentViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان المستند مطلوب")]
    [MaxLength(300, ErrorMessage = "عنوان المستند يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان المستند")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "وصف المستند يجب ألا يتجاوز 1000 حرف")]
    [Display(Name = "وصف المستند")]
    public string? Description { get; set; }

    [MaxLength(300, ErrorMessage = "اسم الملف يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "اسم الملف الأصلي")]
    public string? OriginalFileName { get; set; }

    [MaxLength(1000, ErrorMessage = "رابط الملف يجب ألا يتجاوز 1000 حرف")]
    [Display(Name = "رابط الملف")]
    public string? FileUrl { get; set; }

    [Display(Name = "حجم الملف (بايت)")]
    public long FileSize { get; set; }

    [Display(Name = "نوع الملف")]
    public string? MimeType { get; set; }

    [Display(Name = "عدد الصفحات")]
    public int? PageCount { get; set; }

    [Display(Name = "عدد الكلمات")]
    public int? WordCount { get; set; }

    [MaxLength(50, ErrorMessage = "نوع الكيان يجب ألا يتجاوز 50 حرف")]
    [Display(Name = "نوع الكيان المرتبط")]
    public string? RelatedEntityType { get; set; }

    [Display(Name = "معرف الكيان المرتبط")]
    public int? RelatedEntityId { get; set; }

    [Display(Name = "عام")]
    public bool IsPublic { get; set; } = false;

    [Display(Name = "قابل للتحميل")]
    public bool IsDownloadable { get; set; } = true;

    [Display(Name = "عدد التحميلات")]
    public int DownloadCount { get; set; }

    [Display(Name = "رقم الإصدار")]
    public int Version { get; set; } = 1;

    // For display purposes
    public string? RelatedEntityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// نموذج قائمة المستندات - Documents list view model
/// </summary>
public class DocumentListViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OriginalFileName { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityName { get; set; }
    public bool IsPublic { get; set; }
    public bool IsDownloadable { get; set; }
    public int DownloadCount { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج رفع المستند - Document upload view model
/// </summary>
public class DocumentUploadViewModel
{
    [Required(ErrorMessage = "عنوان المستند مطلوب")]
    [MaxLength(300, ErrorMessage = "عنوان المستند يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان المستند")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "وصف المستند يجب ألا يتجاوز 1000 حرف")]
    [Display(Name = "وصف المستند")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "الملف مطلوب")]
    [Display(Name = "الملف")]
    public IFormFile? File { get; set; }

    [MaxLength(50, ErrorMessage = "نوع الكيان يجب ألا يتجاوز 50 حرف")]
    [Display(Name = "نوع الكيان المرتبط")]
    public string? RelatedEntityType { get; set; }

    [Display(Name = "معرف الكيان المرتبط")]
    public int? RelatedEntityId { get; set; }

    [Display(Name = "عام")]
    public bool IsPublic { get; set; } = false;

    [Display(Name = "قابل للتحميل")]
    public bool IsDownloadable { get; set; } = true;
}

/// <summary>
/// نموذج تحديث إصدار المستند - Document version update view model
/// </summary>
public class DocumentVersionViewModel
{
    public int DocumentId { get; set; }

    [Required(ErrorMessage = "الملف الجديد مطلوب")]
    [Display(Name = "الملف الجديد")]
    public IFormFile? NewFile { get; set; }

    [Display(Name = "ملاحظات التحديث")]
    [MaxLength(500, ErrorMessage = "ملاحظات التحديث يجب ألا تتجاوز 500 حرف")]
    public string? UpdateNotes { get; set; }
}

/// <summary>
/// نموذج إحصائيات المستندات - Document statistics view model
/// </summary>
public class DocumentStatsViewModel
{
    public int TotalDocuments { get; set; }
    public int PublicDocuments { get; set; }
    public int PrivateDocuments { get; set; }
    public long TotalFileSize { get; set; }
    public int TotalDownloads { get; set; }
    public List<DocumentTypeStatsViewModel> DocumentsByType { get; set; } = new();
    public List<DocumentEntityStatsViewModel> DocumentsByEntity { get; set; } = new();
    public List<TopDocumentViewModel> MostDownloaded { get; set; } = new();
}

/// <summary>
/// نموذج إحصائيات نوع المستند - Document type statistics view model
/// </summary>
public class DocumentTypeStatsViewModel
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalSize { get; set; }
}

/// <summary>
/// نموذج إحصائيات الكيان - Entity statistics view model
/// </summary>
public class DocumentEntityStatsViewModel
{
    public string EntityType { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// نموذج أكثر المستندات تحميلاً - Top downloaded documents view model
/// </summary>
public class TopDocumentViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
}

/// <summary>
/// نموذج البحث عن المستندات - Document search view model
/// </summary>
public class DocumentSearchViewModel
{
    [Display(Name = "العنوان")]
    public string? Title { get; set; }

    [Display(Name = "نوع الملف")]
    public string? MimeType { get; set; }

    [Display(Name = "نوع الكيان")]
    public string? RelatedEntityType { get; set; }

    [Display(Name = "من تاريخ")]
    public DateTime? FromDate { get; set; }

    [Display(Name = "إلى تاريخ")]
    public DateTime? ToDate { get; set; }

    [Display(Name = "عام فقط")]
    public bool? PublicOnly { get; set; }
}

