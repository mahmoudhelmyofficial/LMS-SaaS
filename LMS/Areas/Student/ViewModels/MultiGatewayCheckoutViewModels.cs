using LMS.Domain.Enums;
using LMS.Services.Interfaces;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج صفحة الدفع متعددة البوابات - Multi-Gateway Checkout View Model
/// </summary>
public class MultiGatewayCheckoutViewModel
{
    public ShoppingCartInfo Cart { get; set; } = new();
    public List<AvailablePaymentMethod> AvailablePaymentMethods { get; set; } = new();
    public string UserCountry { get; set; } = "EG";
    public PaymentGatewayType? RecommendedGateway { get; set; }
    public string BillingName { get; set; } = string.Empty;
    public string BillingEmail { get; set; } = string.Empty;
    public string? BillingPhone { get; set; }
    public List<Domain.Entities.Payments.UserPaymentMethod> SavedPaymentMethods { get; set; } = new();
}

/// <summary>
/// طلب معالجة الدفع متعدد البوابات - Multi-Gateway Checkout Request
/// </summary>
public class MultiGatewayCheckoutRequest
{
    public PaymentGatewayType SelectedGateway { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? PaymentMethodSubType { get; set; } // card, wallet, fawry, etc.
    public bool SavePaymentMethod { get; set; }
    public string UserCountry { get; set; } = "EG";
    
    // Billing Info
    public string? BillingName { get; set; }
    public string? BillingEmail { get; set; }
    public string? BillingPhone { get; set; }
    
    // Installment (for BNPL)
    public int? InstallmentMonths { get; set; }
}

/// <summary>
/// نتيجة بدء الدفع - Payment Initiation Result
/// </summary>
public class PaymentInitiationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public PaymentGatewayType Gateway { get; set; }
    
    // For redirect-based gateways (Paymob, Tap, etc.)
    public string? RedirectUrl { get; set; }
    public bool RequiresRedirect { get; set; }
    
    // For Stripe (client-side)
    public string? ClientSecret { get; set; }
    public string? PaymentIntentId { get; set; }
    
    // For Fawry (reference number)
    public bool IsFawry { get; set; }
    public string? FawryReferenceNumber { get; set; }
    public DateTime? FawryExpiresAt { get; set; }
    
    // For Bank Transfer
    public bool IsBankTransfer { get; set; }
    public BankTransferDetails? BankDetails { get; set; }
}

/// <summary>
/// نموذج حالة التحويل البنكي - Bank Transfer Status View Model
/// </summary>
public class BankTransferStatusViewModel
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Status { get; set; } = string.Empty;
    public string StatusAr { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public bool CanUploadProof { get; set; }
    
    // Bank Details
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IBAN { get; set; }
    public string? SwiftCode { get; set; }
    public string? Branch { get; set; }
    public string? Instructions { get; set; }
    
    // Proof (if submitted)
    public string? ProofImageUrl { get; set; }
    public DateTime? ProofSubmittedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// نموذج تقديم إثبات الدفع - Submit Payment Proof View Model
/// </summary>
public class SubmitPaymentProofViewModel
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? TransferReference { get; set; }
    public DateTime? TransferDate { get; set; }
    public string? SenderName { get; set; }
    public string? SenderBankName { get; set; }
    public string? Notes { get; set; }
    // File upload handled separately
}

/// <summary>
/// نموذج فوري - Fawry Payment View Model
/// </summary>
public class FawryPaymentViewModel
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public bool IsPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // For display
    public string FormattedAmount { get; set; } = string.Empty;
    public string ExpiryDisplay { get; set; } = string.Empty;
}

