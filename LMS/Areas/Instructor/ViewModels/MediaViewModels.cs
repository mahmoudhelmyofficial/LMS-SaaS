using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// ViewModel for uploading media - simplified to require only file and title
/// </summary>
public class MediaUploadViewModel
{
    [Required(ErrorMessage = "عنوان الملف مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان الملف")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "الوصف")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "الملف")]
    public IFormFile? File { get; set; }

    [Display(Name = "نوع الوسائط")]
    public string MediaType { get; set; } = "Other"; // Auto-detected from file

    // For external URL uploads (optional)
    [Display(Name = "رابط خارجي")]
    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }
}

/// <summary>
/// ViewModel for displaying media in library
/// </summary>
public class MediaDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? OriginalFileName { get; set; }
    public string? Extension { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// ViewModel for API response when uploading media
/// </summary>
public class MediaUploadResultViewModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public MediaDisplayViewModel? Media { get; set; }
}

/// <summary>
/// ViewModel for media library list with filters
/// </summary>
public class MediaLibraryViewModel
{
    public List<MediaDisplayViewModel> Items { get; set; } = new();
    public string? CurrentMediaType { get; set; }
    public int TotalCount { get; set; }
    public int ImageCount { get; set; }
    public int VideoCount { get; set; }
    public int DocumentCount { get; set; }
    public int AudioCount { get; set; }
}

/// <summary>
/// ViewModel for media picker modal response
/// </summary>
public class MediaPickerItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long FileSize { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
}

