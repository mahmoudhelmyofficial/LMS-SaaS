using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

public class LessonResourceCreateViewModel
{
    [Required]
    public int LessonId { get; set; }

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(300)]
    [Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "رابط الملف مطلوب")]
    [MaxLength(1000)]
    [Display(Name = "رابط الملف")]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "نوع الملف")]
    public string? FileType { get; set; }

    [Display(Name = "حجم الملف (بايت)")]
    public long FileSize { get; set; }

    [Display(Name = "قابل للتحميل")]
    public bool IsDownloadable { get; set; } = true;
}

public class LessonResourceEditViewModel : LessonResourceCreateViewModel
{
    public int Id { get; set; }
}

