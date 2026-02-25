using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء وحدة - Create Module ViewModel
/// </summary>
public class ModuleCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int CourseId { get; set; }

    /// <summary>
    /// عنوان الوحدة - Module title
    /// </summary>
    [Required(ErrorMessage = "عنوان الوحدة مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان الوحدة")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف الوحدة - Module description
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "وصف الوحدة")]
    public string? Description { get; set; }

    /// <summary>
    /// ترتيب الوحدة - Order index
    /// </summary>
    [Display(Name = "ترتيب الوحدة")]
    public int OrderIndex { get; set; } = 0;

    /// <summary>
    /// هل الوحدة مجانية للمعاينة - Is free preview
    /// </summary>
    [Display(Name = "متاحة للمعاينة المجانية")]
    public bool IsFreePreview { get; set; } = false;
}

/// <summary>
/// نموذج تعديل الوحدة - Edit Module ViewModel
/// </summary>
public class ModuleEditViewModel : ModuleCreateViewModel
{
    /// <summary>
    /// معرف الوحدة - Module ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عنوان الدورة - Course title (for display)
    /// </summary>
    public string? CourseTitle { get; set; }

    /// <summary>
    /// منشور - Is published
    /// </summary>
    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;
}

/// <summary>
/// نموذج إعادة ترتيب الوحدات - Reorder Modules ViewModel
/// </summary>
public class ModulesReorderViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int CourseId { get; set; }

    /// <summary>
    /// قائمة معرفات الوحدات بالترتيب الجديد - Module IDs in new order
    /// </summary>
    [Required]
    public List<int> ModuleIds { get; set; } = new();
}

/// <summary>
/// عنصر ترتيب الوحدة - Module Order Item
/// </summary>
public class ModuleOrderItem
{
    /// <summary>
    /// معرف الوحدة - Module ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// الترتيب الجديد - New order index
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// الترتيب - Order (alias for OrderIndex)
    /// </summary>
    public int Order { get => OrderIndex; set => OrderIndex = value; }
}

/// <summary>
/// نموذج التعديل السريع للوحدة - Quick Edit Module ViewModel
/// </summary>
public class ModuleQuickEditModel
{
    /// <summary>
    /// معرف الوحدة - Module ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عنوان الوحدة - Module title
    /// </summary>
    [Required(ErrorMessage = "عنوان الوحدة مطلوب")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف الوحدة - Module description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// نموذج العمليات المجمعة - Bulk Action ViewModel
/// </summary>
public class BulkActionModel
{
    /// <summary>
    /// قائمة المعرفات - List of IDs
    /// </summary>
    [Required(ErrorMessage = "يجب تحديد عنصر واحد على الأقل")]
    public List<int> Ids { get; set; } = new();
}

