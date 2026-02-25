using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج عرض الفاتورة - Invoice Display ViewModel
/// </summary>
public class InvoiceViewModel
{
    /// <summary>
    /// المعرف - Invoice ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// رقم الفاتورة - Invoice number
    /// </summary>
    [Display(Name = "رقم الفاتورة")]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// معرف الدفعة - Payment ID
    /// </summary>
    public int PaymentId { get; set; }

    #region Billing Information

    /// <summary>
    /// اسم المشتري - Billing name
    /// </summary>
    [Display(Name = "الاسم")]
    public string BillingName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني - Billing email
    /// </summary>
    [Display(Name = "البريد الإلكتروني")]
    public string BillingEmail { get; set; } = string.Empty;

    /// <summary>
    /// رقم الهاتف - Billing phone
    /// </summary>
    [Display(Name = "رقم الهاتف")]
    public string? BillingPhone { get; set; }

    /// <summary>
    /// العنوان - Billing address
    /// </summary>
    [Display(Name = "العنوان")]
    public string? BillingAddress { get; set; }

    /// <summary>
    /// المدينة - Billing city
    /// </summary>
    [Display(Name = "المدينة")]
    public string? BillingCity { get; set; }

    /// <summary>
    /// الدولة - Billing country
    /// </summary>
    [Display(Name = "الدولة")]
    public string? BillingCountry { get; set; }

    /// <summary>
    /// الرمز البريدي - Postal code
    /// </summary>
    [Display(Name = "الرمز البريدي")]
    public string? PostalCode { get; set; }

    /// <summary>
    /// الرقم الضريبي - Tax number
    /// </summary>
    [Display(Name = "الرقم الضريبي")]
    public string? TaxNumber { get; set; }

    /// <summary>
    /// اسم الشركة - Company name
    /// </summary>
    [Display(Name = "اسم الشركة")]
    public string? CompanyName { get; set; }

    #endregion

    #region Invoice Amounts

    /// <summary>
    /// المبلغ قبل الضريبة - Subtotal
    /// </summary>
    [Display(Name = "المبلغ قبل الضريبة")]
    public decimal SubTotal { get; set; }

    /// <summary>
    /// الخصم - Discount amount
    /// </summary>
    [Display(Name = "الخصم")]
    public decimal Discount { get; set; }

    /// <summary>
    /// الضريبة - Tax amount
    /// </summary>
    [Display(Name = "الضريبة")]
    public decimal Tax { get; set; }

    /// <summary>
    /// الإجمالي - Total amount
    /// </summary>
    [Display(Name = "الإجمالي")]
    public decimal Total { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    [Display(Name = "العملة")]
    public string Currency { get; set; } = "EGP";

    #endregion

    #region Invoice Details

    /// <summary>
    /// وصف العنصر - Item description
    /// </summary>
    [Display(Name = "البند")]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// اسم الدورة - Course name
    /// </summary>
    [Display(Name = "الدورة")]
    public string? CourseName { get; set; }

    /// <summary>
    /// اسم الحزمة - Bundle name
    /// </summary>
    [Display(Name = "الحزمة")]
    public string? BundleName { get; set; }

    /// <summary>
    /// نوع الشراء - Purchase type
    /// </summary>
    [Display(Name = "نوع الشراء")]
    public string PurchaseType { get; set; } = string.Empty;

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    #endregion

    #region Payment Details

    /// <summary>
    /// طريقة الدفع - Payment method
    /// </summary>
    [Display(Name = "طريقة الدفع")]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// حالة الدفع - Payment status
    /// </summary>
    [Display(Name = "حالة الدفع")]
    public string PaymentStatus { get; set; } = string.Empty;

    /// <summary>
    /// رمز المعاملة - Transaction ID
    /// </summary>
    [Display(Name = "رمز المعاملة")]
    public string? TransactionId { get; set; }

    #endregion

    #region Dates

    /// <summary>
    /// تاريخ الإصدار - Issue date
    /// </summary>
    [Display(Name = "تاريخ الإصدار")]
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// تاريخ الإرسال - Sent date
    /// </summary>
    [Display(Name = "تاريخ الإرسال")]
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// تاريخ الدفع - Payment date
    /// </summary>
    [Display(Name = "تاريخ الدفع")]
    public DateTime? PaidAt { get; set; }

    #endregion

    /// <summary>
    /// هل مدفوعة - Is paid
    /// </summary>
    [Display(Name = "مدفوعة")]
    public bool IsPaid { get; set; }

    /// <summary>
    /// رابط ملف PDF - PDF file URL
    /// </summary>
    public string? PdfUrl { get; set; }

    /// <summary>
    /// كود الكوبون المستخدم - Coupon code used
    /// </summary>
    [Display(Name = "كود الخصم")]
    public string? CouponCode { get; set; }
}

/// <summary>
/// نموذج قائمة الفواتير - Invoices List ViewModel
/// </summary>
public class InvoiceListViewModel
{
    /// <summary>
    /// قائمة الفواتير - Invoices list
    /// </summary>
    public List<InvoiceItemViewModel> Invoices { get; set; } = new();

    /// <summary>
    /// إجمالي الفواتير - Total invoices count
    /// </summary>
    public int TotalInvoices { get; set; }

    /// <summary>
    /// الصفحة الحالية - Current page
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// إجمالي الصفحات - Total pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// من تاريخ - From date filter
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// إلى تاريخ - To date filter
    /// </summary>
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// نموذج عنصر الفاتورة في القائمة - Invoice List Item ViewModel
/// </summary>
public class InvoiceItemViewModel
{
    /// <summary>
    /// المعرف - Invoice ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// رقم الفاتورة - Invoice number
    /// </summary>
    [Display(Name = "رقم الفاتورة")]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// تاريخ الإصدار - Issue date
    /// </summary>
    [Display(Name = "التاريخ")]
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// وصف العنصر - Item description
    /// </summary>
    [Display(Name = "البند")]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// الإجمالي - Total amount
    /// </summary>
    [Display(Name = "المبلغ")]
    public decimal Total { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// حالة الدفع - Payment status
    /// </summary>
    [Display(Name = "الحالة")]
    public string PaymentStatus { get; set; } = string.Empty;

    /// <summary>
    /// هل مدفوعة - Is paid
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// رابط PDF - PDF URL
    /// </summary>
    public string? PdfUrl { get; set; }
}

/// <summary>
/// نموذج تحديث معلومات الفواتير - Update Invoice Info ViewModel
/// </summary>
public class InvoiceBillingUpdateViewModel
{
    /// <summary>
    /// اسم المشتري - Billing name
    /// </summary>
    [Required(ErrorMessage = "الاسم مطلوب")]
    [MaxLength(200, ErrorMessage = "الاسم يجب ألا يتجاوز 200 حرف")]
    [Display(Name = "الاسم")]
    public string BillingName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني - Billing email
    /// </summary>
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [MaxLength(200)]
    [Display(Name = "البريد الإلكتروني")]
    public string BillingEmail { get; set; } = string.Empty;

    /// <summary>
    /// رقم الهاتف - Billing phone
    /// </summary>
    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    [MaxLength(20)]
    [Display(Name = "رقم الهاتف")]
    public string? BillingPhone { get; set; }

    /// <summary>
    /// العنوان - Billing address
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "العنوان")]
    public string? BillingAddress { get; set; }

    /// <summary>
    /// المدينة - City
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "المدينة")]
    public string? BillingCity { get; set; }

    /// <summary>
    /// الدولة - Country
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "الدولة")]
    public string? BillingCountry { get; set; }

    /// <summary>
    /// الرمز البريدي - Postal code
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "الرمز البريدي")]
    public string? PostalCode { get; set; }

    /// <summary>
    /// الرقم الضريبي - Tax number
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "الرقم الضريبي")]
    public string? TaxNumber { get; set; }

    /// <summary>
    /// اسم الشركة - Company name
    /// </summary>
    [MaxLength(200)]
    [Display(Name = "اسم الشركة")]
    public string? CompanyName { get; set; }
}

