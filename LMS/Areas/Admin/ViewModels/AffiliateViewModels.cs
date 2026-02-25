using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

public class AffiliateDisplayViewModel
{
    public int Id { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public string AffiliateEmail { get; set; } = string.Empty;
    public string UniqueCode { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public int TotalClicks { get; set; }
    public int TotalConversions { get; set; }
    public decimal TotalEarnings { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AffiliateCommissionDisplayViewModel
{
    public int Id { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public decimal SaleAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

