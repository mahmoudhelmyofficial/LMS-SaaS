using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج معالجة الاسترداد - Process Refund ViewModel
/// </summary>
public class ProcessRefundViewModel
{
    [Required]
    public int RefundId { get; set; }

    [Required]
    [Display(Name = "القرار")]
    public RefundStatus Status { get; set; }

    [MaxLength(500)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public decimal RefundAmount { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>
/// نموذج عرض الاسترداد - Refund Display ViewModel
/// </summary>
public class AdminRefundDisplayViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Reason { get; set; } = string.Empty;
    public RefundStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessorName { get; set; }
}

