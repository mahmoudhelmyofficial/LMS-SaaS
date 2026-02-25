namespace LMS.Views.Shared;

/// <summary>
/// نموذج قائمة التصدير المنسدلة - Export Dropdown Model
/// </summary>
public class ExportDropdownModel
{
    /// <summary>
    /// رابط التصدير الأساسي (مثل: /api/export/users)
    /// </summary>
    public string ExportUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// نوع الكيان للتصدير (مثل: users, courses, enrollments)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>
    /// نص الزر (افتراضي: تصدير)
    /// </summary>
    public string ButtonText { get; set; } = "تصدير";
    
    /// <summary>
    /// كلاس CSS للزر (افتراضي: btn-light)
    /// </summary>
    public string ButtonClass { get; set; } = "btn-light";
    
    /// <summary>
    /// إظهار أيقونة التحميل
    /// </summary>
    public bool ShowIcon { get; set; } = true;
    
    /// <summary>
    /// محاذاة القائمة لليمين
    /// </summary>
    public bool AlignRight { get; set; } = true;
    
    /// <summary>
    /// إظهار حالة التحميل
    /// </summary>
    public bool ShowLoadingState { get; set; } = true;
    
    /// <summary>
    /// معاملات إضافية للرابط
    /// </summary>
    public Dictionary<string, string>? AdditionalParams { get; set; }
}

