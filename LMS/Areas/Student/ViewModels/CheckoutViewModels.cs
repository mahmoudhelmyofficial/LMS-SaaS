using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج الدفع - Checkout ViewModel
/// </summary>
public class CheckoutViewModel
{
    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int? CourseId { get; set; }

    /// <summary>
    /// معرف الحزمة - Bundle ID
    /// </summary>
    public int? BundleId { get; set; }

    /// <summary>
    /// معرف مسار التعلم - Learning Path ID
    /// </summary>
    public int? LearningPathId { get; set; }

    /// <summary>
    /// السعر الأصلي - Original price
    /// </summary>
    public decimal OriginalPrice { get; set; }

    /// <summary>
    /// مبلغ الخصم - Discount amount
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// الضريبة - Tax amount
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// المبلغ الإجمالي - Total amount
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// كود الكوبون - Coupon code
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "كود الكوبون")]
    public string? CouponCode { get; set; }

    /// <summary>
    /// مزود الدفع - Payment provider
    /// </summary>
    [Required(ErrorMessage = "يرجى اختيار طريقة الدفع")]
    [Display(Name = "طريقة الدفع")]
    public PaymentProvider PaymentProvider { get; set; }

    /// <summary>
    /// حفظ طريقة الدفع - Save payment method
    /// </summary>
    [Display(Name = "حفظ طريقة الدفع")]
    public bool SavePaymentMethod { get; set; }
}

/// <summary>
/// نموذج تطبيق الكوبون - Apply Coupon ViewModel
/// </summary>
public class ApplyCouponViewModel
{
    /// <summary>
    /// كود الكوبون - Coupon code
    /// </summary>
    [Required(ErrorMessage = "كود الكوبون مطلوب")]
    [MaxLength(50)]
    [Display(Name = "كود الكوبون")]
    public string CouponCode { get; set; } = string.Empty;

    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    public int? CourseId { get; set; }

    /// <summary>
    /// المبلغ الأصلي - Original amount
    /// </summary>
    public decimal OriginalAmount { get; set; }
}

/// <summary>
/// نموذج نتيجة الكوبون - Coupon Result ViewModel
/// </summary>
public class CouponResultViewModel
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
}

/// <summary>
/// نموذج إتمام الدفع - Payment Completion ViewModel
/// </summary>
public class PaymentCompletionViewModel
{
    public int PaymentId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime CompletedAt { get; set; }
    public int? EnrollmentId { get; set; }
    public string? InvoiceUrl { get; set; }
}

/// <summary>
/// نموذج عرض الفاتورة - Invoice Display ViewModel
/// </summary>
public class InvoiceDisplayViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? CourseName { get; set; }
    public string? CouponCode { get; set; }
}

