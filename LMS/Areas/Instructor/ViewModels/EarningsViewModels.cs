using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// نموذج طلب سحب - Withdrawal Request ViewModel
/// </summary>
public class WithdrawViewModel
{
    /// <summary>
    /// المبلغ - Amount
    /// </summary>
    [Required(ErrorMessage = "المبلغ مطلوب")]
    [Range(1, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
    [Display(Name = "المبلغ")]
    public decimal Amount { get; set; }

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    /// <summary>
    /// طريقة السحب - Withdrawal method
    /// </summary>
    [Display(Name = "طريقة السحب")]
    public string? WithdrawalMethod { get; set; }

    /// <summary>
    /// اسم البنك - Bank name
    /// </summary>
    [Display(Name = "اسم البنك")]
    public string? BankName { get; set; }

    /// <summary>
    /// رقم الحساب البنكي - Bank account number
    /// </summary>
    [Display(Name = "رقم الحساب البنكي")]
    public string? BankAccountNumber { get; set; }

    /// <summary>
    /// اسم صاحب الحساب - Account holder name
    /// </summary>
    [Display(Name = "اسم صاحب الحساب")]
    public string? AccountHolderName { get; set; }

    /// <summary>
    /// بريد PayPal - PayPal email
    /// </summary>
    [Display(Name = "بريد PayPal")]
    public string? PayPalEmail { get; set; }

    /// <summary>
    /// معرف حساب Stripe - Stripe account ID
    /// </summary>
    [Display(Name = "معرف حساب Stripe")]
    public string? StripeAccountId { get; set; }
}

/// <summary>
/// نموذج ملخص الأرباح - Earnings Summary ViewModel
/// </summary>
public class EarningsSummaryViewModel
{
    public decimal TotalEarnings { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public decimal MinimumWithdrawal { get; set; }
    public decimal CommissionRate { get; set; }
}

/// <summary>
/// نموذج عرض الأرباح - Earning Display ViewModel
/// </summary>
public class EarningDisplayViewModel
{
    public int Id { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal NetAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// نموذج المعاملة - Transaction ViewModel
/// </summary>
public class TransactionViewModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty; // "earning" or "withdrawal"
    public string Description { get; set; } = string.Empty;
    public string? CourseName { get; set; }
    public string? StudentName { get; set; }
    public decimal Amount { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? Commission { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? Notes { get; set; }
}

