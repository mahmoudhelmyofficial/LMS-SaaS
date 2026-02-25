using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج إنشاء طلب سحب - Create Withdrawal Request ViewModel
/// </summary>
public class WithdrawalRequestCreateViewModel
{
    /// <summary>
    /// الرصيد المتاح - Available balance (for display)
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// الحد الأدنى للسحب - Minimum withdrawal (for display)
    /// </summary>
    public decimal MinimumWithdrawal { get; set; }

    /// <summary>
    /// المبلغ المطلوب - Requested amount
    /// </summary>
    [Required(ErrorMessage = "المبلغ مطلوب")]
    [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
    [Display(Name = "المبلغ المطلوب")]
    public decimal Amount { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    [Required]
    [Display(Name = "العملة")]
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// طريقة السحب - Withdrawal method ID
    /// </summary>
    [Required(ErrorMessage = "طريقة السحب مطلوبة")]
    [Display(Name = "طريقة السحب")]
    public int WithdrawalMethodId { get; set; }

    /// <summary>
    /// تفاصيل الحساب - Account details (JSON)
    /// </summary>
    [Display(Name = "تفاصيل الحساب")]
    public string? AccountDetails { get; set; }

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "ملاحظات إضافية")]
    public string? Notes { get; set; }
}

/// <summary>
/// نموذج طلب سحب - Withdrawal Request ViewModel
/// </summary>
public class WithdrawalRequestViewModel
{
    public decimal Amount { get; set; }
    public int WithdrawalMethodId { get; set; }
    public string WithdrawalMethod { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string? PayPalEmail { get; set; }
    public string? StripeAccountId { get; set; }
    public string? Notes { get; set; }
}

