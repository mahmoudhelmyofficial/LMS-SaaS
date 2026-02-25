using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Add payment method view model
/// </summary>
public class AddPaymentMethodViewModel
{
    [Required(ErrorMessage = "نوع طريقة الدفع مطلوب")]
    [Display(Name = "نوع طريقة الدفع")]
    public string PaymentMethodType { get; set; } = "Card"; // Card, BankAccount, Wallet

    [Required(ErrorMessage = "رقم البطاقة مطلوب")]
    [Display(Name = "رقم البطاقة")]
    [CreditCard(ErrorMessage = "رقم البطاقة غير صحيح")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم حامل البطاقة مطلوب")]
    [Display(Name = "اسم حامل البطاقة")]
    [MaxLength(200)]
    public string CardHolderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شهر الانتهاء مطلوب")]
    [Display(Name = "شهر الانتهاء")]
    [Range(1, 12, ErrorMessage = "شهر غير صحيح")]
    public int ExpiryMonth { get; set; }

    [Required(ErrorMessage = "سنة الانتهاء مطلوبة")]
    [Display(Name = "سنة الانتهاء")]
    [Range(2024, 2050, ErrorMessage = "سنة غير صحيحة")]
    public int ExpiryYear { get; set; }

    [Required(ErrorMessage = "CVV مطلوب")]
    [Display(Name = "CVV")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV يجب أن يكون 3 أو 4 أرقام")]
    public string CVV { get; set; } = string.Empty;

    [Display(Name = "تعيين كطريقة افتراضية")]
    public bool IsDefault { get; set; }

    [Display(Name = "اسم مستعار")]
    [MaxLength(100)]
    public string? NickName { get; set; }
}

/// <summary>
/// Edit payment method view model
/// </summary>
public class EditPaymentMethodViewModel
{
    public int Id { get; set; }

    [Display(Name = "اسم مستعار")]
    [MaxLength(100)]
    public string? NickName { get; set; }

    [Display(Name = "تعيين كطريقة افتراضية")]
    public bool IsDefault { get; set; }
}

/// <summary>
/// Payment method list item view model
/// </summary>
public class PaymentMethodListItemViewModel
{
    public int Id { get; set; }
    public string PaymentMethodType { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public bool IsDefault { get; set; }
    public bool IsVerified { get; set; }
    public bool IsExpired { get; set; }
    public string? NickName { get; set; }
    public DateTime CreatedAt { get; set; }
}

