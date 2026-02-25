using System.ComponentModel.DataAnnotations;
using LMS.Domain.Enums;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إعدادات الرسائل النصية - SMS Setting ViewModel
/// </summary>
public class SmsSettingViewModel
{
    public int Id { get; set; }

    /// <summary>
    /// مزود الخدمة - Provider
    /// </summary>
    [Required(ErrorMessage = "مزود الخدمة مطلوب")]
    [Display(Name = "مزود الخدمة")]
    public SmsProvider Provider { get; set; }

    /// <summary>
    /// مفتاح API - API Key
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "مفتاح API")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// المفتاح السري - API Secret
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "المفتاح السري")]
    public string? ApiSecret { get; set; }

    /// <summary>
    /// معرف المرسل - Sender ID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف المرسل")]
    public string? SenderId { get; set; }

    /// <summary>
    /// مفعل - Is enabled
    /// </summary>
    [Display(Name = "مفعل")]
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// وضع الاختبار - Test mode
    /// </summary>
    [Display(Name = "وضع الاختبار")]
    public bool TestMode { get; set; } = true;

    /// <summary>
    /// رقم هاتف الاختبار - Test phone number
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "رقم هاتف الاختبار")]
    public string? TestPhoneNumber { get; set; }

    /// <summary>
    /// محاولات إعادة الإرسال - Max retry attempts
    /// </summary>
    [Range(0, 10)]
    [Display(Name = "محاولات إعادة الإرسال")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// تأخير إعادة المحاولة - Retry delay in seconds
    /// </summary>
    [Range(0, 3600)]
    [Display(Name = "تأخير إعادة المحاولة (ثواني)")]
    public int RetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// تقارير التسليم - Enable delivery reports
    /// </summary>
    [Display(Name = "تفعيل تقارير التسليم")]
    public bool EnableDeliveryReports { get; set; } = true;

    /// <summary>
    /// رابط Webhook - Webhook URL
    /// </summary>
    [MaxLength(500)]
    [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
    [Display(Name = "رابط Webhook")]
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// الدول المدعومة - Supported countries (JSON)
    /// </summary>
    [Display(Name = "الدول المدعومة")]
    public string? SupportedCountries { get; set; }

    /// <summary>
    /// إعدادات إضافية - Additional configuration (JSON)
    /// </summary>
    [Display(Name = "إعدادات إضافية")]
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// معرف الحساب - Account SID
    /// </summary>
    [MaxLength(100)]
    [Display(Name = "معرف الحساب")]
    public string? AccountSid { get; set; }

    /// <summary>
    /// اسم المرسل - Sender name
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "اسم المرسل")]
    public string? SenderName { get; set; }

    /// <summary>
    /// رقم الهاتف - Phone number
    /// </summary>
    [MaxLength(20)]
    [Display(Name = "رقم الهاتف")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// نوع الرسالة الافتراضي - Default message type
    /// </summary>
    [MaxLength(50)]
    [Display(Name = "نوع الرسالة الافتراضي")]
    public string? DefaultMessageType { get; set; }

    /// <summary>
    /// الحد الأقصى لطول الرسالة - Max message length
    /// </summary>
    [Display(Name = "الحد الأقصى لطول الرسالة")]
    public int MaxMessageLength { get; set; } = 160;

    /// <summary>
    /// دعم Unicode - Supports Unicode
    /// </summary>
    [Display(Name = "دعم Unicode")]
    public bool SupportsUnicode { get; set; } = true;

    /// <summary>
    /// الحد اليومي للمستخدم - Daily limit per user
    /// </summary>
    [Display(Name = "الحد اليومي للمستخدم")]
    public int DailyLimitPerUser { get; set; } = 10;

    /// <summary>
    /// ملاحظات - Notes
    /// </summary>
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }
}

