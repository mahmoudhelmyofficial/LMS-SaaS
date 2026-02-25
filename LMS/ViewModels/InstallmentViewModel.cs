namespace LMS.ViewModels;

/// <summary>
/// نموذج عرض خيارات التقسيط - Installment options view model
/// </summary>
public class InstallmentOptionsViewModel
{
    public bool IsAvailable { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public List<InstallmentPlan> Plans { get; set; } = new();
    public string Currency { get; set; } = "EGP";
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderLogo { get; set; } = string.Empty;
    public string? TermsUrl { get; set; }
}

/// <summary>
/// خطة التقسيط - Individual installment plan
/// </summary>
public class InstallmentPlan
{
    public int Months { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public decimal DownPayment { get; set; }
    public decimal AdminFee { get; set; }
    public bool IsRecommended { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}

/// <summary>
/// طلب التقسيط - Installment request
/// </summary>
public class InstallmentRequest
{
    public int CartId { get; set; }
    public string Provider { get; set; } = string.Empty; // valU, Sympl, Souhoola
    public int Months { get; set; }
    public decimal DownPayment { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
}

/// <summary>
/// نتيجة التقسيط - Installment result
/// </summary>
public class InstallmentResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TransactionId { get; set; }
    public InstallmentPlan? ApprovedPlan { get; set; }
    public string? ContractUrl { get; set; }
}

