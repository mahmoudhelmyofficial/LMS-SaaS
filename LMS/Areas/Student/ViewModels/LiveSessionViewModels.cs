using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج جدول الحصص المتاح - Available Schedule ViewModel
/// </summary>
public class AvailableScheduleViewModel
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
    public int TotalSessions { get; set; }
    public int EnrolledCount { get; set; }
    public int? MaxStudents { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorAvatar { get; set; }
    public string? CourseName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public LiveScheduleStatus Status { get; set; }
    public bool IsEnrolled { get; set; }
}

/// <summary>
/// نموذج تفاصيل جدول الحصص - Schedule Details ViewModel
/// </summary>
public class ScheduleDetailsViewModel
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
    public int TotalSessions { get; set; }
    public int EnrolledCount { get; set; }
    public int? MaxStudents { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string? InstructorAvatar { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsEnrolled { get; set; }
    public List<ScheduleSessionPreviewViewModel> Sessions { get; set; } = new();
}

/// <summary>
/// نموذج معاينة الجلسة في الجدول - Schedule Session Preview ViewModel
/// </summary>
public class ScheduleSessionPreviewViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Platform { get; set; } = string.Empty;
    public LiveClassStatus Status { get; set; }
    public int ScheduleOrder { get; set; }
}

/// <summary>
/// نموذج شراء الجلسة - Session Checkout ViewModel
/// </summary>
public class SessionCheckoutViewModel
{
    public int LiveClassId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime ScheduledStartTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? Subject { get; set; }
    public string? CouponCode { get; set; }
}

/// <summary>
/// نموذج شراء جدول الحصص - Schedule Checkout ViewModel
/// </summary>
public class ScheduleCheckoutViewModel
{
    public int ScheduleId { get; set; }
    public string ScheduleTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public int TotalSessions { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ScheduleSessionPreviewViewModel> Sessions { get; set; } = new();
    public string? CouponCode { get; set; }
}

/// <summary>
/// نموذج سجل حضور الطالب - Student Attendance History ViewModel
/// </summary>
public class StudentAttendanceHistoryViewModel
{
    public List<StudentAttendanceItemViewModel> Attendances { get; set; } = new();
    public decimal OverallScore { get; set; }
    public int TotalSessions { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalLate { get; set; }
}

/// <summary>
/// نموذج عنصر حضور الطالب - Student Attendance Item ViewModel
/// </summary>
public class StudentAttendanceItemViewModel
{
    public int LiveClassId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string? ScheduleTitle { get; set; }
    public DateTime SessionDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public decimal Score { get; set; }
    public int DurationMinutes { get; set; }
    public int LateMinutes { get; set; }
    public string? ExcuseReason { get; set; }
    public bool? ExcuseApproved { get; set; }
}

/// <summary>
/// نموذج عرض الجلسة المباشرة - Live Session Display ViewModel
/// </summary>
public class LiveSessionDisplayViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Platform { get; set; } = string.Empty;
    public LiveClassStatus Status { get; set; }
    public decimal Price { get; set; }
    public string PriceCurrency { get; set; } = "EGP";
    public LiveSessionPricingType PricingType { get; set; }
    public bool IsPurchased { get; set; }
    public bool IsFreeForAll { get; set; }
    public int? MaxParticipants { get; set; }
    public int RegisteredCount { get; set; }
    public bool HasRecordings { get; set; }
    public string? ScheduleTitle { get; set; }
    public int? LiveSessionScheduleId { get; set; }
}
