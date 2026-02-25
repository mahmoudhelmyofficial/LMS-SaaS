using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء درس جديد - Create Lesson ViewModel
/// </summary>
public class LessonCreateViewModel
{
    /// <summary>
    /// معرف الوحدة - Module ID
    /// </summary>
    [Required]
    public int ModuleId { get; set; }

    /// <summary>
    /// عنوان الدرس - Lesson title
    /// </summary>
    [Required(ErrorMessage = "عنوان الدرس مطلوب")]
    [MaxLength(300, ErrorMessage = "عنوان الدرس يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الدرس")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف الدرس - Lesson description
    /// </summary>
    [MaxLength(1000, ErrorMessage = "الوصف يجب ألا يتجاوز 1000 حرف")]
    [Display(Name = "وصف الدرس")]
    public string? Description { get; set; }

    /// <summary>
    /// نوع الدرس - Lesson type
    /// </summary>
    [Display(Name = "نوع الدرس")]
    public LessonType Type { get; set; } = LessonType.Video;

    /// <summary>
    /// رابط الفيديو - Video URL
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "رابط الفيديو")]
    public string? VideoUrl { get; set; }

    /// <summary>
    /// مزود الفيديو - Video provider
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "مزود الفيديو")]
    public string? VideoProvider { get; set; }

    /// <summary>
    /// معرف الفيديو - Video ID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف الفيديو")]
    public string? VideoId { get; set; }

    /// <summary>
    /// المحتوى النصي - HTML content
    /// </summary>
    [Display(Name = "المحتوى النصي")]
    public string? HtmlContent { get; set; }

    /// <summary>
    /// رابط الملف - File URL
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "رابط الملف")]
    public string? FileUrl { get; set; }

    /// <summary>
    /// مدة الدرس بالثواني - Duration in seconds
    /// </summary>
    [Range(0, int.MaxValue)]
    [Display(Name = "المدة (بالثواني)")]
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// متاح للمعاينة - Is previewable
    /// </summary>
    [Display(Name = "متاح للمعاينة المجانية")]
    public bool IsPreviewable { get; set; } = false;

    /// <summary>
    /// قابل للتحميل - Is downloadable
    /// </summary>
    [Display(Name = "قابل للتحميل")]
    public bool IsDownloadable { get; set; } = false;

    /// <summary>
    /// إجباري الإكمال - Must complete
    /// </summary>
    [Display(Name = "إجباري الإكمال للمتابعة")]
    public bool MustComplete { get; set; } = true;

    /// <summary>
    /// المدة بالدقائق - Duration in minutes
    /// </summary>
    [Display(Name = "المدة (بالدقائق)")]
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// ترتيب العرض - Order index
    /// </summary>
    [Display(Name = "ترتيب العرض")]
    public int OrderIndex { get; set; }

    /// <summary>
    /// ملف الفيديو - Video file
    /// </summary>
    [Display(Name = "ملف الفيديو")]
    public Microsoft.AspNetCore.Http.IFormFile? VideoFile { get; set; }

    /// <summary>
    /// المحتوى النصي - Text content
    /// </summary>
    [Display(Name = "المحتوى النصي")]
    public string? TextContent { get; set; }

    /// <summary>
    /// عدد أسئلة الاختبار - Quiz questions count
    /// </summary>
    [Display(Name = "عدد الأسئلة")]
    public int? QuizQuestionsCount { get; set; }

    /// <summary>
    /// درجة النجاح - Quiz passing score
    /// </summary>
    [Display(Name = "درجة النجاح")]
    public int? QuizPassingScore { get; set; }

    /// <summary>
    /// مجاني - Is free
    /// </summary>
    [Display(Name = "مجاني")]
    public bool IsFree { get; set; }

    /// <summary>
    /// منشور - Is published
    /// </summary>
    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// السماح بالتحميل - Allow download
    /// </summary>
    [Display(Name = "السماح بالتحميل")]
    public bool AllowDownload { get; set; }

    /// <summary>
    /// الحد الأدنى لنسبة المشاهدة - Minimum watch percentage
    /// </summary>
    [Display(Name = "الحد الأدنى لنسبة المشاهدة")]
    public int? MinimumWatchPercentage { get; set; }

    /// <summary>
    /// منع التخطي - Prevent skipping
    /// </summary>
    [Display(Name = "منع التخطي")]
    public bool PreventSkipping { get; set; }

    // Content Drip settings
    /// <summary>
    /// نوع جدولة المحتوى - Content drip type
    /// </summary>
    [Display(Name = "نوع جدولة المحتوى")]
    public string? ContentDripType { get; set; }

    /// <summary>
    /// متاح بعد أيام من التسجيل - Available after enrollment days
    /// </summary>
    [Display(Name = "متاح بعد (أيام)")]
    public int? AvailableAfterDays { get; set; }

    /// <summary>
    /// تاريخ الإتاحة - Available from date
    /// </summary>
    [Display(Name = "متاح من تاريخ")]
    public DateTime? AvailableFrom { get; set; }

    /// <summary>
    /// الدرس المتطلب - Prerequisite lesson ID
    /// </summary>
    [Display(Name = "الدرس المتطلب")]
    public int? PrerequisiteLessonId { get; set; }
}

/// <summary>
/// نموذج تعديل الدرس - Edit Lesson ViewModel
/// </summary>
public class LessonEditViewModel : LessonCreateViewModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// رابط الفيديو الحالي - Existing video URL
    /// </summary>
    public string? ExistingVideoUrl { get; set; }

    /// <summary>
    /// تاريخ الإنشاء - Created at
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// تاريخ التحديث - Updated at
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// نموذج ترتيب الدروس - Lesson Order Item for drag & drop reordering
/// </summary>
public class LessonOrderItem
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// الترتيب الجديد - New order index
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// نموذج التعديل السريع للدرس - Quick Edit Lesson ViewModel
/// </summary>
public class LessonQuickEditModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عنوان الدرس - Lesson title
    /// </summary>
    [Required(ErrorMessage = "عنوان الدرس مطلوب")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// نموذج نسخ الدرس - Duplicate Lesson ViewModel
/// </summary>
public class LessonDuplicateModel
{
    /// <summary>
    /// معرف الدرس الأصلي - Source lesson ID
    /// </summary>
    public int Id { get; set; }
}

/// <summary>
/// نموذج نقل الدرس - Move Lesson ViewModel
/// </summary>
public class LessonMoveModel
{
    /// <summary>
    /// معرف الدرس - Lesson ID
    /// </summary>
    public int LessonId { get; set; }

    /// <summary>
    /// معرف الوحدة المستهدفة - Target module ID
    /// </summary>
    public int TargetModuleId { get; set; }
}

/// <summary>
/// نموذج العمليات المجمعة للدروس - Bulk Lesson Action ViewModel
/// </summary>
public class LessonBulkActionModel
{
    /// <summary>
    /// قائمة المعرفات - List of IDs
    /// </summary>
    [Required(ErrorMessage = "يجب تحديد عنصر واحد على الأقل")]
    public List<int> Ids { get; set; } = new();
}

/// <summary>
/// نموذج تعيين المعاينة المجمعة - Bulk Set Preview ViewModel
/// </summary>
public class LessonBulkPreviewModel
{
    /// <summary>
    /// قائمة المعرفات - List of IDs
    /// </summary>
    [Required(ErrorMessage = "يجب تحديد عنصر واحد على الأقل")]
    public List<int> Ids { get; set; } = new();

    /// <summary>
    /// هل متاح للمعاينة - Is previewable
    /// </summary>
    public bool IsPreviewable { get; set; }
}

