using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إعدادات المراقبة - Proctoring settings view model
/// </summary>
public class ProctoringSettingViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "معرف الاختبار مطلوب")]
    [Display(Name = "الاختبار")]
    public int QuizId { get; set; }

    [Display(Name = "تفعيل المراقبة")]
    public bool IsEnabled { get; set; } = false;

    [Display(Name = "تفعيل الكاميرا")]
    public bool RequireWebcam { get; set; } = false;

    [Display(Name = "تسجيل الفيديو")]
    public bool RecordVideo { get; set; } = false;

    [Display(Name = "التقاط صور دورية")]
    public bool CaptureScreenshots { get; set; } = false;

    [Range(10, 600, ErrorMessage = "فترة التقاط الصور يجب أن تكون بين 10 و 600 ثانية")]
    [Display(Name = "فترة التقاط الصور (بالثواني)")]
    public int ScreenshotInterval { get; set; } = 60;

    [Display(Name = "منع التبديل بين النوافذ")]
    public bool PreventTabSwitch { get; set; } = true;

    [Range(1, 10, ErrorMessage = "عدد التحذيرات المسموحة يجب أن يكون بين 1 و 10")]
    [Display(Name = "عدد التحذيرات المسموحة")]
    public int MaxTabSwitchWarnings { get; set; } = 3;

    [Display(Name = "منع النسخ واللصق")]
    public bool PreventCopyPaste { get; set; } = true;

    [Display(Name = "تعطيل كليك يمين")]
    public bool DisableRightClick { get; set; } = true;

    [Display(Name = "وضع ملء الشاشة")]
    public bool RequireFullscreen { get; set; } = false;

    [Display(Name = "كشف الوجه")]
    public bool EnableFaceDetection { get; set; } = false;

    [Display(Name = "كشف الأشخاص المتعددين")]
    public bool DetectMultiplePeople { get; set; } = false;

    [Display(Name = "قفل المتصفح")]
    public bool LockBrowser { get; set; } = false;

    [Display(Name = "التحقق من الهوية قبل البدء")]
    public bool RequireIdVerification { get; set; } = false;

    [Display(Name = "إنهاء تلقائي عند الغش")]
    public bool AutoTerminate { get; set; } = false;

    [MaxLength(500, ErrorMessage = "رسالة التحذير يجب ألا تتجاوز 500 حرف")]
    [Display(Name = "رسالة التحذير")]
    public string? WarningMessage { get; set; }

    [MaxLength(1000, ErrorMessage = "الملاحظات يجب ألا تتجاوز 1000 حرف")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    // For display purposes
    public string? QuizTitle { get; set; }
    public string? CourseTitle { get; set; }
}

/// <summary>
/// نموذج قائمة إعدادات المراقبة - Proctoring settings list view model
/// </summary>
public class ProctoringSettingListViewModel
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool RequireWebcam { get; set; }
    public bool RecordVideo { get; set; }
    public bool EnableFaceDetection { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

