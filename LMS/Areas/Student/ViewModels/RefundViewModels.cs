using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج طلب استرداد - Request Refund ViewModel
/// </summary>
public class RefundRequestViewModel
{
    [Required]
    public int PaymentId { get; set; }

    [Required(ErrorMessage = "سبب الاسترداد مطلوب")]
    [MaxLength(500)]
    [Display(Name = "سبب الاسترداد")]
    public string Reason { get; set; } = string.Empty;

    [Display(Name = "تفاصيل إضافية")]
    public string? Details { get; set; }

    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// نموذج عرض الاسترداد - Refund Display ViewModel
/// </summary>
public class RefundDisplayViewModel
{
    public int Id { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public RefundStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionNotes { get; set; }
}

