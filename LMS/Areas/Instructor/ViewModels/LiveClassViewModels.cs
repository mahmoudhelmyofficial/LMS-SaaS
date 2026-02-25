using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء بث مباشر - Create Live Class ViewModel
/// </summary>
public class LiveClassCreateViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Required(ErrorMessage = "الدورة مطلوبة")]
    [Display(Name = "الدورة")]
    public int CourseId { get; set; }

    /// <summary>
    /// معرف الدرس - Lesson ID (optional)
    /// </summary>
    [Display(Name = "الدرس (اختياري)")]
    public int? LessonId { get; set; }

    /// <summary>
    /// عنوان الجلسة - Session title
    /// </summary>
    [Required(ErrorMessage = "عنوان الجلسة مطلوب")]
    [MaxLength(300, ErrorMessage = "العنوان يجب ألا يتجاوز 300 حرف")]
    [Display(Name = "عنوان الجلسة")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// وصف الجلسة - Session description
    /// </summary>
    [MaxLength(1000)]
    [Display(Name = "وصف الجلسة")]
    public string? Description { get; set; }

    /// <summary>
    /// جدول الأعمال - Agenda
    /// </summary>
    [Display(Name = "جدول الأعمال")]
    public string? Agenda { get; set; }

    /// <summary>
    /// وقت البدء - Scheduled start time
    /// </summary>
    [Required(ErrorMessage = "وقت البدء مطلوب")]
    [Display(Name = "وقت البدء")]
    public DateTime ScheduledStartTime { get; set; } = DateTime.UtcNow.AddDays(1);

    /// <summary>
    /// وقت الانتهاء - Scheduled end time
    /// </summary>
    [Required(ErrorMessage = "وقت الانتهاء مطلوب")]
    [Display(Name = "وقت الانتهاء")]
    public DateTime ScheduledEndTime { get; set; } = DateTime.UtcNow.AddDays(1).AddHours(1);

    /// <summary>
    /// المدة بالدقائق - Duration in minutes
    /// </summary>
    [Range(15, 480, ErrorMessage = "المدة يجب أن تكون بين 15 و 480 دقيقة")]
    [Display(Name = "المدة (دقائق)")]
    public int DurationMinutes { get; set; } = 60;

    /// <summary>
    /// المنصة - Platform
    /// </summary>
    [Required(ErrorMessage = "المنصة مطلوبة")]
    [MaxLength(50)]
    [Display(Name = "المنصة")]
    public string Platform { get; set; } = "Zoom";

    /// <summary>
    /// رابط الاجتماع - Meeting URL
    /// </summary>
    [Required(ErrorMessage = "رابط الاجتماع مطلوب")]
    [MaxLength(1000)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "رابط الاجتماع")]
    public string MeetingUrl { get; set; } = string.Empty;

    /// <summary>
    /// معرف الاجتماع - Meeting ID
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "معرف الاجتماع")]
    public string? MeetingId { get; set; }

    /// <summary>
    /// كلمة المرور - Password
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "كلمة المرور")]
    public string? Password { get; set; }

    /// <summary>
    /// الحد الأقصى للمشاركين - Max participants
    /// </summary>
    [Range(1, 10000)]
    [Display(Name = "الحد الأقصى للمشاركين")]
    public int? MaxParticipants { get; set; }

    /// <summary>
    /// السماح بإعادة المشاهدة - Allow replay
    /// </summary>
    [Display(Name = "السماح بإعادة المشاهدة")]
    public bool AllowReplay { get; set; } = true;

    /// <summary>
    /// إرسال تذكير - Send reminder
    /// </summary>
    [Display(Name = "إرسال تذكير")]
    public bool SendReminder { get; set; } = true;

    /// <summary>
    /// دقائق التذكير - Reminder minutes before
    /// </summary>
    [Range(5, 1440)]
    [Display(Name = "التذكير قبل (دقائق)")]
    public int ReminderMinutesBefore { get; set; } = 30;

    /// <summary>
    /// مجاني للجميع - Free for all
    /// </summary>
    [Display(Name = "مجاني للجميع")]
    public bool IsFreeForAll { get; set; } = false;
}

/// <summary>
/// نموذج تعديل بث مباشر - Edit Live Class ViewModel
/// </summary>
public class LiveClassEditViewModel : LiveClassCreateViewModel
{
    /// <summary>
    /// معرف البث المباشر - Live Class ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// الحالة - Status
    /// </summary>
    [Display(Name = "الحالة")]
    public LiveClassStatus Status { get; set; }
}

/// <summary>
/// نموذج عرض البث المباشر - Live Class Display ViewModel
/// </summary>
public class LiveClassDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    public LiveClassStatus Status { get; set; }
    public int RegisteredCount { get; set; }
    public int AttendedCount { get; set; }
}

