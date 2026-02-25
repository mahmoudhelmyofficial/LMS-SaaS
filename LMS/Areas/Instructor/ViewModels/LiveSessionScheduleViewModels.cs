using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء جدول حصص - Create Session Schedule ViewModel
/// </summary>
public class LiveSessionScheduleCreateViewModel
{
    [Required(ErrorMessage = "عنوان جدول الحصص مطلوب")]
    [MaxLength(300, ErrorMessage = "العنوان يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الجدول")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(300)]
    [Display(Name = "العنوان بالعربية")]
    public string? TitleAr { get; set; }

    [MaxLength(2000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "السعر مطلوب")]
    [Range(0, 100000, ErrorMessage = "السعر يجب أن يكون بين 0 و 100,000")]
    [Display(Name = "سعر الاشتراك (ج.م)")]
    public decimal Price { get; set; }

    [Display(Name = "السعر قبل الخصم")]
    public decimal? OriginalPrice { get; set; }

    [Display(Name = "الدورة المرتبطة")]
    public int? CourseId { get; set; }

    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    [Display(Name = "تاريخ البداية")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
    [Display(Name = "تاريخ النهاية")]
    public DateTime EndDate { get; set; }

    [Display(Name = "الحد الأقصى للطلاب")]
    public int? MaxStudents { get; set; }

    [Display(Name = "صورة مصغرة")]
    public string? ThumbnailUrl { get; set; }

    public List<ScheduleSessionItemViewModel> Sessions { get; set; } = new();
}

/// <summary>
/// نموذج جلسة في الجدول - Schedule Session Item ViewModel
/// </summary>
public class ScheduleSessionItemViewModel
{
    [Required(ErrorMessage = "عنوان الجلسة مطلوب")]
    [Display(Name = "عنوان الجلسة")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "الموضوع")]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "تاريخ ووقت البدء مطلوب")]
    [Display(Name = "تاريخ ووقت البدء")]
    public DateTime ScheduledStartTime { get; set; }

    [Required(ErrorMessage = "تاريخ ووقت الانتهاء مطلوب")]
    [Display(Name = "تاريخ ووقت الانتهاء")]
    public DateTime ScheduledEndTime { get; set; }

    [Display(Name = "المدة (دقيقة)")]
    public int DurationMinutes { get; set; } = 60;

    [Display(Name = "المنصة")]
    public string Platform { get; set; } = "Zoom";

    [Display(Name = "رابط الاجتماع")]
    public string MeetingUrl { get; set; } = string.Empty;

    [Display(Name = "معرف الاجتماع")]
    public string? MeetingId { get; set; }

    [Display(Name = "كلمة المرور")]
    public string? Password { get; set; }

    public int ScheduleOrder { get; set; }
}

/// <summary>
/// نموذج تعديل جدول حصص - Edit Session Schedule ViewModel
/// </summary>
public class LiveSessionScheduleEditViewModel : LiveSessionScheduleCreateViewModel
{
    public int Id { get; set; }
    public LiveScheduleStatus Status { get; set; }
    public int EnrolledCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

/// <summary>
/// نموذج عرض جدول الحصص - Session Schedule Display ViewModel
/// </summary>
public class LiveSessionScheduleDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxStudents { get; set; }
    public LiveScheduleStatus Status { get; set; }
    public int TotalSessions { get; set; }
    public int EnrolledCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string? CourseName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج رفع تسجيل - Recording Upload ViewModel
/// </summary>
public class RecordingUploadViewModel
{
    [Required(ErrorMessage = "اختر الجلسة")]
    [Display(Name = "الجلسة المباشرة")]
    public int LiveClassId { get; set; }

    [Required(ErrorMessage = "عنوان التسجيل مطلوب")]
    [MaxLength(300)]
    [Display(Name = "عنوان التسجيل")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "ملف التسجيل")]
    public IFormFile? RecordingFile { get; set; }

    [Display(Name = "رابط خارجي")]
    public string? ExternalUrl { get; set; }

    [Display(Name = "يتطلب شراء للمشاهدة")]
    public bool AccessRequiresPurchase { get; set; } = true;
}

/// <summary>
/// نموذج عرض التسجيل - Recording Display ViewModel
/// </summary>
public class RecordingDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LiveClassTitle { get; set; } = string.Empty;
    public int LiveClassId { get; set; }
    public int DurationSeconds { get; set; }
    public long FileSize { get; set; }
    public string? Resolution { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public bool IsAvailable { get; set; }
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
    public DateTime? RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public RecordingStorageType StorageType { get; set; }
}

/// <summary>
/// نموذج تقرير الحضور - Schedule Attendance Report ViewModel
/// </summary>
public class ScheduleAttendanceReportViewModel
{
    public int ScheduleId { get; set; }
    public string ScheduleTitle { get; set; } = string.Empty;
    public List<SessionAttendanceColumn> Sessions { get; set; } = new();
    public List<StudentAttendanceRow> Students { get; set; } = new();
}

/// <summary>
/// عمود جلسة الحضور - Session Attendance Column
/// </summary>
public class SessionAttendanceColumn
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime ScheduledDate { get; set; }
}

/// <summary>
/// صف حضور الطالب - Student Attendance Row
/// </summary>
public class StudentAttendanceRow
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public Dictionary<int, AttendanceStatus> SessionAttendance { get; set; } = new();
    public Dictionary<int, decimal> SessionScores { get; set; } = new();
    public decimal AverageScore { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalLate { get; set; }
}
