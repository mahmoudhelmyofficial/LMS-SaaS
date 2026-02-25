using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إنشاء إعدادات بوابة الدفع - Create Payment Gateway Setting View Model
/// </summary>
public class PaymentGatewaySettingCreateViewModel
{
    [Required(ErrorMessage = "اسم البوابة مطلوب")]
    [MaxLength(100)]
    [Display(Name = "اسم البوابة")]
    public string GatewayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع البوابة مطلوب")]
    [Display(Name = "نوع البوابة")]
    public PaymentGatewayType GatewayType { get; set; }

    [MaxLength(100)]
    [Display(Name = "الاسم المعروض")]
    public string? DisplayName { get; set; }

    [MaxLength(100)]
    [Display(Name = "الاسم بالعربية")]
    public string? DisplayNameAr { get; set; }

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط الأيقونة")]
    public string? IconUrl { get; set; }

    [Display(Name = "مفعل")]
    public bool IsEnabled { get; set; }

    [Display(Name = "وضع الاختبار")]
    public bool IsTestMode { get; set; } = true;

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; }

    #region API Credentials

    [MaxLength(500)]
    [Display(Name = "المفتاح العام")]
    public string? ApiKey { get; set; }

    [MaxLength(500)]
    [Display(Name = "المفتاح السري")]
    public string? SecretKey { get; set; }

    [MaxLength(500)]
    [Display(Name = "مفتاح الويب هوك")]
    public string? WebhookSecret { get; set; }

    [MaxLength(200)]
    [Display(Name = "معرف التاجر")]
    public string? MerchantId { get; set; }

    [MaxLength(500)]
    [Display(Name = "مفتاح API للاختبار")]
    public string? TestApiKey { get; set; }

    [MaxLength(500)]
    [Display(Name = "المفتاح السري للاختبار")]
    public string? TestSecretKey { get; set; }

    #endregion

    #region Paymob Specific

    [MaxLength(100)]
    [Display(Name = "معرف التكامل (Paymob)")]
    public string? IntegrationId { get; set; }

    [MaxLength(100)]
    [Display(Name = "معرف الإطار (Paymob)")]
    public string? IFrameId { get; set; }

    [MaxLength(500)]
    [Display(Name = "مفتاح HMAC")]
    public string? HmacSecret { get; set; }

    [MaxLength(100)]
    [Display(Name = "معرف تكامل المحفظة")]
    public string? WalletIntegrationId { get; set; }

    [MaxLength(100)]
    [Display(Name = "معرف تكامل فوري")]
    public string? CashIntegrationId { get; set; }

    #endregion

    #region Fawry Specific

    [MaxLength(200)]
    [Display(Name = "كود الأمان (Fawry)")]
    public string? SecurityCode { get; set; }

    [Display(Name = "ساعات انتهاء صلاحية فوري")]
    public int? FawryExpiryHours { get; set; } = 48;

    #endregion

    #region Gulf Gateways (Tap, Hyperpay)

    [MaxLength(100)]
    [Display(Name = "معرف الكيان (Hyperpay)")]
    public string? EntityId { get; set; }

    [MaxLength(100)]
    [Display(Name = "معرف كيان مدى")]
    public string? MadaEntityId { get; set; }

    [Display(Name = "دعم Apple Pay")]
    public bool SupportsApplePay { get; set; }

    [Display(Name = "دعم مدى")]
    public bool SupportsMada { get; set; }

    #endregion

    #region URLs

    [MaxLength(500)]
    [Display(Name = "رابط API")]
    public string? ApiBaseUrl { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط API الاختبار")]
    public string? SandboxApiUrl { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط النجاح")]
    public string? CallbackUrl { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط الفشل")]
    public string? ErrorCallbackUrl { get; set; }

    #endregion

    #region Currency & Limits

    [Display(Name = "العملة الافتراضية")]
    public string DefaultCurrency { get; set; } = "EGP";

    [Display(Name = "العملات المدعومة")]
    public string SupportedCurrencies { get; set; } = "[\"EGP\",\"USD\"]";

    [Display(Name = "الدول المدعومة")]
    public string SupportedCountries { get; set; } = "[\"EG\"]";

    [Display(Name = "نسبة رسوم المعاملة")]
    public decimal TransactionFeePercentage { get; set; }

    [Display(Name = "رسوم ثابتة")]
    public decimal TransactionFeeFixed { get; set; }

    [Display(Name = "الحد الأدنى للمبلغ")]
    public decimal? MinimumAmount { get; set; }

    [Display(Name = "الحد الأقصى للمبلغ")]
    public decimal? MaximumAmount { get; set; }

    #endregion

    #region Bank Transfer Settings

    [MaxLength(100)]
    [Display(Name = "اسم البنك")]
    public string? BankName { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم صاحب الحساب")]
    public string? AccountHolderName { get; set; }

    [MaxLength(50)]
    [Display(Name = "رقم الحساب")]
    public string? AccountNumber { get; set; }

    [MaxLength(50)]
    [Display(Name = "رقم IBAN")]
    public string? IBAN { get; set; }

    [MaxLength(20)]
    [Display(Name = "كود SWIFT")]
    public string? SwiftCode { get; set; }

    [MaxLength(200)]
    [Display(Name = "فرع البنك")]
    public string? BankBranch { get; set; }

    [MaxLength(1000)]
    [Display(Name = "تعليمات التحويل")]
    public string? TransferInstructions { get; set; }

    [Display(Name = "أيام انتهاء صلاحية التحويل")]
    public int TransferExpiryDays { get; set; } = 3;

    #endregion

    #region Features

    [Display(Name = "دعم الاشتراكات المتكررة")]
    public bool SupportsRecurring { get; set; }

    [Display(Name = "دعم الاسترداد")]
    public bool SupportsRefund { get; set; } = true;

    [Display(Name = "دعم حفظ البطاقة")]
    public bool SupportsSaveCard { get; set; }

    [Display(Name = "دعم التقسيط")]
    public bool SupportsInstallments { get; set; }

    [Display(Name = "أقصى شهور للتقسيط")]
    public int MaxInstallmentMonths { get; set; } = 12;

    #endregion

    [Display(Name = "إعدادات إضافية (JSON)")]
    public string? ConfigurationJson { get; set; }
}

/// <summary>
/// نموذج تعديل إعدادات بوابة الدفع - Edit Payment Gateway Setting View Model
/// </summary>
public class PaymentGatewaySettingEditViewModel : PaymentGatewaySettingCreateViewModel
{
    public int Id { get; set; }

    [Display(Name = "البوابة الافتراضية")]
    public bool IsDefault { get; set; }

    [Display(Name = "حالة الصحة")]
    public bool IsHealthy { get; set; }

    public DateTime? LastHealthCheck { get; set; }

    public string? LastErrorMessage { get; set; }

    public string? WebhookUrl { get; set; }
}

/// <summary>
/// نموذج قائمة بوابات الدفع - Payment Gateways List View Model
/// </summary>
public class PaymentGatewayListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public PaymentGatewayType GatewayType { get; set; }
    public string? IconUrl { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSandbox { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public int DisplayOrder { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
}
