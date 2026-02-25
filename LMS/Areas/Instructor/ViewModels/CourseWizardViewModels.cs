using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج معالج إنشاء الدورة - Course Creation Wizard ViewModel
/// Enterprise-level multi-step course creation
/// </summary>
public class CourseWizardViewModel
{
    /// <summary>
    /// معرف الدورة (للتحرير) - Course ID for editing
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// الخطوة الحالية - Current wizard step (1-7)
    /// </summary>
    public int CurrentStep { get; set; } = 1;

    /// <summary>
    /// إجمالي الخطوات - Total steps
    /// </summary>
    public int TotalSteps { get; set; } = 7;

    /// <summary>
    /// هل اكتملت الدورة - Is wizard completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// نسبة الاكتمال - Completion percentage
    /// </summary>
    public int CompletionPercentage { get; set; }

    #region Step 1: Basic Information - المعلومات الأساسية

    /// <summary>
    /// عنوان الدورة - Course title
    /// </summary>
    [Required(ErrorMessage = "عنوان الدورة مطلوب")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "عنوان الدورة يجب أن يكون بين 10 و 300 حرف")]
    [Display(Name = "عنوان الدورة")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف مختصر - Short description
    /// </summary>
    [Required(ErrorMessage = "الوصف المختصر مطلوب")]
    [StringLength(500, MinimumLength = 20, ErrorMessage = "الوصف المختصر يجب أن يكون بين 20 و 500 حرف")]
    [Display(Name = "وصف مختصر")]
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// الوصف التفصيلي - Full description (HTML)
    /// </summary>
    [Required(ErrorMessage = "الوصف التفصيلي مطلوب")]
    [MinLength(100, ErrorMessage = "الوصف التفصيلي يجب أن يكون 100 حرف على الأقل")]
    [Display(Name = "الوصف التفصيلي")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// التصنيف الرئيسي - Main category
    /// </summary>
    [Required(ErrorMessage = "التصنيف مطلوب")]
    [Display(Name = "التصنيف")]
    public int CategoryId { get; set; }

    /// <summary>
    /// التصنيف الفرعي - Subcategory
    /// </summary>
    [Display(Name = "التصنيف الفرعي")]
    public int? SubCategoryId { get; set; }

    /// <summary>
    /// مستوى الدورة - Course level
    /// </summary>
    [Required(ErrorMessage = "المستوى مطلوب")]
    [Display(Name = "المستوى")]
    public CourseLevel Level { get; set; } = CourseLevel.AllLevels;

    /// <summary>
    /// لغة الدورة - Course language
    /// </summary>
    [Required(ErrorMessage = "اللغة مطلوبة")]
    [StringLength(10)]
    [Display(Name = "اللغة")]
    public string Language { get; set; } = "ar";

    #endregion

    #region Step 2: Learning Content - المحتوى التعليمي

    /// <summary>
    /// ما ستتعلمه - Learning outcomes
    /// </summary>
    [Display(Name = "ما ستتعلمه")]
    public List<string> LearningOutcomes { get; set; } = new();

    /// <summary>
    /// المتطلبات الأساسية - Prerequisites/Requirements
    /// </summary>
    [Display(Name = "المتطلبات")]
    public List<string> Requirements { get; set; } = new();

    /// <summary>
    /// الجمهور المستهدف - Target audience
    /// </summary>
    [Display(Name = "الجمهور المستهدف")]
    public List<string> TargetAudience { get; set; } = new();

    /// <summary>
    /// المدة المقدرة بالساعات - Estimated duration in hours
    /// </summary>
    [Display(Name = "المدة المقدرة (ساعات)")]
    [Range(0, 1000, ErrorMessage = "المدة يجب أن تكون بين 0 و 1000 ساعة")]
    public decimal? EstimatedDurationHours { get; set; }

    #endregion

    #region Step 3: Course Content - محتوى الدورة

    /// <summary>
    /// الوحدات - Modules with lessons
    /// </summary>
    public List<ModuleWizardViewModel> Modules { get; set; } = new();

    /// <summary>
    /// عدد الوحدات الكلي - Total modules count
    /// </summary>
    public int TotalModulesCount => Modules?.Count ?? 0;

    /// <summary>
    /// عدد الدروس الكلي - Total lessons count
    /// </summary>
    public int TotalLessonsCount => Modules?.Sum(m => m.Lessons?.Count ?? 0) ?? 0;

    #endregion

    #region Step 4: Media - الوسائط

    /// <summary>
    /// صورة الغلاف - Thumbnail URL
    /// </summary>
    [Display(Name = "صورة الغلاف")]
    [StringLength(500)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// ملف صورة الغلاف - Thumbnail file upload
    /// </summary>
    [Display(Name = "ملف صورة الغلاف")]
    public Microsoft.AspNetCore.Http.IFormFile? ThumbnailFile { get; set; }

    /// <summary>
    /// فيديو المعاينة - Preview video URL
    /// </summary>
    [Display(Name = "فيديو المعاينة")]
    [StringLength(500)]
    public string? PreviewVideoUrl { get; set; }

    /// <summary>
    /// مزود الفيديو - Video provider (YouTube, Vimeo, Local)
    /// </summary>
    [Display(Name = "مصدر الفيديو")]
    [StringLength(50)]
    public string? PreviewVideoProvider { get; set; }

    #endregion

    #region Step 5: Pricing - التسعير

    /// <summary>
    /// هل الدورة مجانية - Is course free
    /// </summary>
    [Display(Name = "دورة مجانية")]
    public bool IsFree { get; set; }

    /// <summary>
    /// السعر - Price
    /// </summary>
    [Display(Name = "السعر")]
    [Range(0, 999999.99, ErrorMessage = "السعر يجب أن يكون بين 0 و 999,999")]
    public decimal Price { get; set; }

    /// <summary>
    /// سعر الخصم - Discount price
    /// </summary>
    [Display(Name = "سعر الخصم")]
    [Range(0, 999999.99, ErrorMessage = "سعر الخصم يجب أن يكون بين 0 و 999,999")]
    public decimal? DiscountPrice { get; set; }

    /// <summary>
    /// تاريخ بدء الخصم - Discount start date
    /// </summary>
    [Display(Name = "بداية الخصم")]
    public DateTime? DiscountStartDate { get; set; }

    /// <summary>
    /// تاريخ انتهاء الخصم - Discount end date
    /// </summary>
    [Display(Name = "نهاية الخصم")]
    public DateTime? DiscountEndDate { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    [Display(Name = "العملة")]
    [StringLength(10)]
    public string Currency { get; set; } = "EGP";

    #endregion

    #region Step 6: Settings - الإعدادات

    /// <summary>
    /// تفعيل الشهادة - Enable certificate
    /// </summary>
    [Display(Name = "إصدار شهادة عند الإكمال")]
    public bool HasCertificate { get; set; } = true;

    /// <summary>
    /// السماح بالمناقشات - Allow discussions
    /// </summary>
    [Display(Name = "السماح بالمناقشات")]
    public bool AllowDiscussions { get; set; } = true;

    /// <summary>
    /// السماح بالمراجعات - Allow reviews
    /// </summary>
    [Display(Name = "السماح بالتقييمات")]
    public bool AllowReviews { get; set; } = true;

    /// <summary>
    /// تفعيل التقطير - Enable content drip
    /// </summary>
    [Display(Name = "تفعيل جدولة المحتوى")]
    public bool EnableContentDrip { get; set; }

    /// <summary>
    /// تفعيل العلامة المائية - Enable watermark
    /// </summary>
    [Display(Name = "تفعيل العلامة المائية")]
    public bool EnableWatermark { get; set; }

    /// <summary>
    /// منع التحميل - Prevent download
    /// </summary>
    [Display(Name = "منع تحميل الفيديوهات")]
    public bool PreventDownload { get; set; } = true;

    /// <summary>
    /// المدرسين المشاركين - Co-instructors emails
    /// </summary>
    [Display(Name = "المدرسين المشاركين")]
    public List<string> CoInstructorEmails { get; set; } = new();

    #endregion

    #region Step 7: SEO & Review - SEO والمراجعة

    /// <summary>
    /// عنوان SEO - Meta title
    /// </summary>
    [Display(Name = "عنوان SEO")]
    [StringLength(200)]
    public string? MetaTitle { get; set; }

    /// <summary>
    /// وصف SEO - Meta description
    /// </summary>
    [Display(Name = "وصف SEO")]
    [StringLength(500)]
    public string? MetaDescription { get; set; }

    /// <summary>
    /// الكلمات المفتاحية - Meta keywords
    /// </summary>
    [Display(Name = "الكلمات المفتاحية")]
    [StringLength(500)]
    public string? MetaKeywords { get; set; }

    /// <summary>
    /// حالة الدورة المطلوبة - Desired course status
    /// </summary>
    [Display(Name = "الحالة")]
    public CourseStatus DesiredStatus { get; set; } = CourseStatus.Draft;

    #endregion

    #region Validation & Readiness

    /// <summary>
    /// قائمة العناصر المفقودة - Missing items for publishing
    /// </summary>
    public List<string> MissingItems { get; set; } = new();

    /// <summary>
    /// قائمة العناصر المكتملة - Completed checklist items
    /// </summary>
    public List<CourseReadinessItem> ReadinessChecklist { get; set; } = new();

    /// <summary>
    /// التحقق من صلاحية الخطوة - Validate specific step
    /// </summary>
    public StepValidationResult ValidateStep(int step)
    {
        return step switch
        {
            1 => ValidateStep1(),
            2 => ValidateStep2(),
            3 => ValidateStep3(),
            4 => ValidateStep4(),
            5 => ValidateStep5(),
            6 => ValidateStep6(),
            7 => ValidateStep7(),
            _ => new StepValidationResult { IsValid = false, Errors = new List<string> { "خطوة غير صحيحة" } }
        };
    }

    private StepValidationResult ValidateStep1()
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(Title) || Title.Length < 10)
            errors.Add("عنوان الدورة يجب أن يكون 10 أحرف على الأقل");
        
        if (string.IsNullOrWhiteSpace(ShortDescription) || ShortDescription.Length < 20)
            errors.Add("الوصف المختصر يجب أن يكون 20 حرف على الأقل");
        
        if (string.IsNullOrWhiteSpace(Description) || Description.Length < 100)
            errors.Add("الوصف التفصيلي يجب أن يكون 100 حرف على الأقل");
        
        if (CategoryId <= 0)
            errors.Add("يجب اختيار التصنيف");

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private StepValidationResult ValidateStep2()
    {
        var errors = new List<string>();
        
        var validOutcomes = LearningOutcomes?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? new List<string>();
        if (validOutcomes.Count < 3)
            errors.Add("يجب إضافة 3 نقاط تعلم على الأقل");
        
        var validRequirements = Requirements?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new List<string>();
        if (validRequirements.Count < 1)
            errors.Add("يجب إضافة متطلب واحد على الأقل");

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private StepValidationResult ValidateStep3()
    {
        var errors = new List<string>();
        
        // Filter out empty/invalid modules (modules without titles are not valid)
        var validModules = Modules?.Where(m => !string.IsNullOrWhiteSpace(m.Title)).ToList() ?? new List<ModuleWizardViewModel>();
        
        if (validModules.Count < 1)
            errors.Add("يجب إضافة وحدة واحدة على الأقل");
        
        // Count only valid lessons (lessons with titles are valid)
        var validLessonsCount = validModules
            .Sum(m => m.Lessons?.Count(l => !string.IsNullOrWhiteSpace(l.Title)) ?? 0);
        
        if (validLessonsCount < 3)
            errors.Add("يجب إضافة 3 دروس على الأقل");

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private StepValidationResult ValidateStep4()
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(ThumbnailUrl))
            errors.Add("يجب إضافة صورة للدورة");

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private StepValidationResult ValidateStep5()
    {
        var errors = new List<string>();
        
        if (!IsFree && Price <= 0)
            errors.Add("يجب تحديد سعر للدورة أو جعلها مجانية");
        
        if (DiscountPrice.HasValue && DiscountPrice.Value >= Price)
            errors.Add("سعر الخصم يجب أن يكون أقل من السعر الأصلي");

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    private StepValidationResult ValidateStep6()
    {
        // Settings step is optional, always valid
        return new StepValidationResult { IsValid = true, Errors = new List<string>() };
    }

    private StepValidationResult ValidateStep7()
    {
        var errors = new List<string>();
        
        // Aggregate all validations
        for (int i = 1; i <= 6; i++)
        {
            var stepResult = ValidateStep(i);
            if (!stepResult.IsValid)
                errors.AddRange(stepResult.Errors);
        }

        return new StepValidationResult { IsValid = !errors.Any(), Errors = errors };
    }

    /// <summary>
    /// حساب نسبة الاكتمال - Calculate completion percentage
    /// </summary>
    public int CalculateCompletionPercentage()
    {
        var items = new List<bool>
        {
            !string.IsNullOrWhiteSpace(Title) && Title.Length >= 10,
            !string.IsNullOrWhiteSpace(ShortDescription) && ShortDescription.Length >= 20,
            !string.IsNullOrWhiteSpace(Description) && Description.Length >= 100,
            CategoryId > 0,
            (LearningOutcomes?.Count(o => !string.IsNullOrWhiteSpace(o)) ?? 0) >= 3,
            (Requirements?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0) >= 1,
            TotalModulesCount >= 1,
            TotalLessonsCount >= 3,
            !string.IsNullOrWhiteSpace(ThumbnailUrl),
            IsFree || Price > 0
        };

        var completed = items.Count(x => x);
        return (int)Math.Round((completed / (double)items.Count) * 100);
    }

    #endregion
}

/// <summary>
/// نموذج الوحدة للمعالج - Module wizard view model
/// </summary>
public class ModuleWizardViewModel
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    
    [Required(ErrorMessage = "عنوان الوحدة مطلوب")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "عنوان الوحدة يجب أن يكون بين 3 و 300 حرف")]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public int OrderIndex { get; set; }
    public bool IsPublished { get; set; }
    
    public List<LessonWizardViewModel> Lessons { get; set; } = new();
    
    /// <summary>
    /// عدد الدروس - Lessons count
    /// </summary>
    public int LessonsCount => Lessons?.Count ?? 0;
    
    /// <summary>
    /// المدة الكلية بالدقائق - Total duration in minutes
    /// </summary>
    public int TotalDurationMinutes => Lessons?.Sum(l => l.DurationSeconds / 60) ?? 0;
}

/// <summary>
/// نموذج الدرس للمعالج - Lesson wizard view model
/// </summary>
public class LessonWizardViewModel
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    
    [Required(ErrorMessage = "عنوان الدرس مطلوب")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "عنوان الدرس يجب أن يكون بين 3 و 300 حرف")]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public LessonType Type { get; set; } = LessonType.Video;
    
    [StringLength(500)]
    public string? VideoUrl { get; set; }
    
    [StringLength(50)]
    public string? VideoProvider { get; set; }
    
    public string? HtmlContent { get; set; }
    
    [StringLength(500)]
    public string? FileUrl { get; set; }
    
    public int DurationSeconds { get; set; }
    public int OrderIndex { get; set; }
    
    public bool IsPreviewable { get; set; }
    public bool IsDownloadable { get; set; }
    public bool MustComplete { get; set; } = true;
    
    /// <summary>
    /// أيام الإتاحة بعد التسجيل - Days available after enrollment (for drip)
    /// </summary>
    public int? AvailableAfterDays { get; set; }
    
    /// <summary>
    /// تاريخ الإتاحة - Available from date (for drip)
    /// </summary>
    public DateTime? AvailableFrom { get; set; }
    
    /// <summary>
    /// أيقونة نوع الدرس - Lesson type icon
    /// </summary>
    public string TypeIcon => Type switch
    {
        LessonType.Video => "video",
        LessonType.Text => "file-text",
        LessonType.Article => "file-text",
        LessonType.Quiz => "help-circle",
        LessonType.Assignment => "edit",
        LessonType.Download => "download",
        LessonType.LiveClass => "video",
        LessonType.Audio => "headphones",
        LessonType.PDF => "file",
        LessonType.Interactive => "box",
        LessonType.ExternalLink => "external-link",
        _ => "file"
    };
    
    /// <summary>
    /// اسم نوع الدرس - Lesson type name
    /// </summary>
    public string TypeName => Type switch
    {
        LessonType.Video => "فيديو",
        LessonType.Text => "نص",
        LessonType.Article => "مقال",
        LessonType.Quiz => "اختبار",
        LessonType.Assignment => "تكليف",
        LessonType.Download => "ملف للتحميل",
        LessonType.LiveClass => "بث مباشر",
        LessonType.Audio => "صوتي",
        LessonType.PDF => "ملف PDF",
        LessonType.Interactive => "محتوى تفاعلي",
        LessonType.ExternalLink => "رابط خارجي",
        _ => "محتوى"
    };
}

/// <summary>
/// نتيجة التحقق من الخطوة - Step validation result
/// </summary>
public class StepValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// عنصر جاهزية الدورة - Course readiness checklist item
/// </summary>
public class CourseReadinessItem
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsRequired { get; set; }
    public string? HelpText { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// نموذج الحفظ التلقائي - Auto-save model
/// </summary>
public class CourseAutoSaveModel
{
    public int? CourseId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int? Step { get; set; }
}

/// <summary>
/// نموذج إضافة وحدة سريعة - Quick add module model
/// </summary>
public class QuickAddModuleModel
{
    public int CourseId { get; set; }
    
    [Required(ErrorMessage = "عنوان الوحدة مطلوب")]
    [StringLength(300, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// نموذج تعديل وحدة سريع - Quick edit module model
/// </summary>
public class QuickEditModuleModel
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "عنوان الوحدة مطلوب")]
    [StringLength(300, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// نموذج إضافة درس سريعة - Quick add lesson model
/// </summary>
public class QuickAddLessonModel
{
    public int ModuleId { get; set; }
    
    [Required(ErrorMessage = "عنوان الدرس مطلوب")]
    [StringLength(300, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    
    public LessonType Type { get; set; } = LessonType.Video;
    
    [StringLength(500)]
    public string? VideoUrl { get; set; }
    
    [StringLength(50)]
    public string? VideoProvider { get; set; }
    
    /// <summary>
    /// المحتوى النصي - HTML content (for Text, Article)
    /// </summary>
    public string? HtmlContent { get; set; }
    
    /// <summary>
    /// رابط الملف - File URL (for Download, PDF, Audio)
    /// </summary>
    [StringLength(1000)]
    public string? FileUrl { get; set; }
    
    public int DurationSeconds { get; set; }
    public bool IsPreviewable { get; set; }
    
    /// <summary>
    /// السماح بالتحميل - Is downloadable (for Download, PDF)
    /// </summary>
    public bool IsDownloadable { get; set; }
}

/// <summary>
/// نموذج ترتيب العناصر - Reorder items model
/// </summary>
public class ReorderItemsModel
{
    public List<OrderItem> Items { get; set; } = new();
}

/// <summary>
/// عنصر ترتيب - Order item
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// معلومات التصنيف - Category info for dropdowns
/// </summary>
public class CategorySelectItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public List<CategorySelectItem> SubCategories { get; set; } = new();
}

/// <summary>
/// نموذج إضافة درس موسع - Extended Quick Add Lesson with Quiz/Assignment/Drip settings
/// </summary>
public class QuickAddLessonExtendedModel : QuickAddLessonModel
{
    // Quiz settings (when Type == Quiz)
    public QuizSettingsDto? QuizSettings { get; set; }

    // Assignment settings (when Type == Assignment)
    public AssignmentSettingsDto? AssignmentSettings { get; set; }

    // Content drip settings
    public ContentDripSettingsDto? ContentDripSettings { get; set; }

    // Proctoring settings (when Type == Quiz)
    public ProctoringSettingsDto? ProctoringSettings { get; set; }
}

/// <summary>
/// نموذج تعديل درس سريع - Quick Edit Lesson Model
/// </summary>
public class QuickEditLessonModel
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [StringLength(1000)]
    public string? VideoUrl { get; set; }
    
    [StringLength(50)]
    public string? VideoProvider { get; set; }
    
    public string? HtmlContent { get; set; }
    
    [StringLength(1000)]
    public string? FileUrl { get; set; }
    
    public int DurationSeconds { get; set; }
    public bool IsPreviewable { get; set; }
    public bool IsDownloadable { get; set; }
}

/// <summary>
/// إعدادات الاختبار - Quiz settings for inline builder
/// </summary>
public class QuizSettingsDto
{
    public int PassingScore { get; set; } = 70;
    public int? TimeLimitMinutes { get; set; }
    public int? MaxAttempts { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public bool ShowCorrectAnswers { get; set; } = true;
    public bool ShowScoreImmediately { get; set; } = true;
    public bool AllowBackNavigation { get; set; } = true;
    public bool OneQuestionPerPage { get; set; }
    public string? Instructions { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// إعدادات التكليف - Assignment settings for inline builder
/// </summary>
public class AssignmentSettingsDto
{
    public int MaxPoints { get; set; } = 100;
    public int? PassingPoints { get; set; }
    public int? DueDateDays { get; set; } = 7;
    public DateTime? DueDate { get; set; }
    public bool AllowLateSubmission { get; set; }
    public int? LatePenaltyPercentage { get; set; }
    public bool AllowTextSubmission { get; set; } = true;
    public bool AllowFileUpload { get; set; } = true;
    public string? AcceptedFileTypes { get; set; } = ".pdf,.doc,.docx,.zip,.rar";
    public int? MaxFileSizeMB { get; set; } = 50;
    public int? MaxFiles { get; set; } = 5;
    public bool AllowResubmission { get; set; }
    public int? MaxSubmissions { get; set; } = 3;
    public string? Instructions { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// إعدادات جدولة المحتوى - Content drip settings
/// </summary>
public class ContentDripSettingsDto
{
    public string DripType { get; set; } = "Immediate";
    public int? AvailableAfterDays { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public int? PrerequisiteLessonId { get; set; }
}

/// <summary>
/// إعدادات المراقبة - Proctoring settings for inline quiz builder
/// </summary>
public class ProctoringSettingsDto
{
    public bool IsEnabled { get; set; }
    public bool RequireWebcam { get; set; }
    public bool RecordVideo { get; set; }
    public bool CaptureScreenshots { get; set; }
    public int ScreenshotInterval { get; set; } = 60;
    public bool PreventTabSwitch { get; set; } = true;
    public int MaxTabSwitchWarnings { get; set; } = 3;
    public bool PreventCopyPaste { get; set; } = true;
    public bool DisableRightClick { get; set; } = true;
    public bool RequireFullscreen { get; set; }
    public bool EnableFaceDetection { get; set; }
    public bool DetectMultiplePeople { get; set; }
    public bool LockBrowser { get; set; }
    public bool RequireIdVerification { get; set; }
    public bool AutoTerminate { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>
/// استيراد أسئلة من بنك الأسئلة - Import questions from question bank
/// </summary>
public class ImportFromBankRequest
{
    public int QuizId { get; set; }
    public int QuestionBankId { get; set; }
    public List<int> QuestionBankItemIds { get; set; } = new();
}

/// <summary>
/// استيراد عشوائي من بنك الأسئلة - Random import from question bank
/// </summary>
public class RandomImportFromBankRequest
{
    public int QuizId { get; set; }
    public int QuestionBankId { get; set; }
    public int Count { get; set; } = 10;
    public string? QuestionType { get; set; }
    public string? DifficultyLevel { get; set; }
}

// ========== Step 3: Advanced Setup Models ==========

/// <summary>
/// نموذج إضافة سؤال اختبار سريع - Quick Add Quiz Question
/// </summary>
public class QuickAddQuizQuestionModel
{
    public int QuizId { get; set; }

    [Required(ErrorMessage = "نص السؤال مطلوب")]
    public string QuestionText { get; set; } = string.Empty;

    public int Type { get; set; } = 1;
    public int Points { get; set; } = 1;
    public string? DifficultyLevel { get; set; }
    public string? Explanation { get; set; }
    public string? Hint { get; set; }
    public string? SampleAnswer { get; set; }
    public string? AnswerKeywords { get; set; }
    public int? MinWordCount { get; set; }
    public int? MaxWordCount { get; set; }
    public List<QuestionOptionDto>? Options { get; set; }
}

/// <summary>
/// نموذج تعديل سؤال اختبار - Quick Edit Quiz Question
/// </summary>
public class QuickEditQuizQuestionModel
{
    public int QuestionId { get; set; }
    public string? QuestionText { get; set; }
    public int Type { get; set; } = 1;
    public int Points { get; set; } = 1;
    public string? DifficultyLevel { get; set; }
    public string? Explanation { get; set; }
    public string? Hint { get; set; }
    public List<QuestionOptionDto>? Options { get; set; }
}

/// <summary>
/// نموذج حذف سؤال اختبار - Quick Delete Quiz Question
/// </summary>
public class QuickDeleteQuizQuestionModel
{
    public int QuestionId { get; set; }
}

/// <summary>
/// خيار سؤال - Question Option DTO
/// </summary>
public class QuestionOptionDto
{
    public string? OptionText { get; set; }
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>
/// نموذج إضافة مرفق درس - Quick Add Lesson Resource
/// </summary>
public class QuickAddLessonResourceModel
{
    public int LessonId { get; set; }

    [Required(ErrorMessage = "عنوان المرفق مطلوب")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "رابط المرفق مطلوب")]
    [MaxLength(1000)]
    public string ResourceUrl { get; set; } = string.Empty;
}

/// <summary>
/// نموذج حذف مرفق درس - Quick Delete Lesson Resource
/// </summary>
public class QuickDeleteLessonResourceModel
{
    public int ResourceId { get; set; }
}

/// <summary>
/// نموذج إضافة قاعدة جدولة محتوى - Quick Add Content Drip Rule
/// </summary>
public class QuickAddContentDripRuleModel
{
    public int CourseId { get; set; }
    public int? ModuleId { get; set; }
    public int? LessonId { get; set; }
    public int DripType { get; set; }
    public int? DaysAfterEnrollment { get; set; }
    public string? SpecificDate { get; set; }
    public bool SendNotification { get; set; } = true;
}

/// <summary>
/// نموذج تحديث محتوى الدرس - Quick Update Lesson Content
/// </summary>
public class QuickUpdateLessonContentModel
{
    public int LessonId { get; set; }
    public string? HtmlContent { get; set; }
    public string? FileUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoProvider { get; set; }
    public int? DurationSeconds { get; set; }
    public bool? IsDownloadable { get; set; }
    public string? ScheduledAt { get; set; }
}

// Note: AddCategoryModel and AddSubcategoryModel are defined in CategoryAjaxModels.cs

