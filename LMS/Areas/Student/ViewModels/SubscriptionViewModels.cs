using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// نموذج الدفع للاشتراك - Subscription Checkout ViewModel
/// </summary>
public class SubscriptionCheckoutViewModel
{
    /// <summary>
    /// معرف الخطة - Plan ID
    /// </summary>
    public int PlanId { get; set; }

    /// <summary>
    /// اسم الخطة - Plan name
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// السعر - Price
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// العملة - Currency
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// دورة الفوترة - Billing cycle
    /// </summary>
    public string BillingCycle { get; set; } = "Monthly";

    /// <summary>
    /// الميزات - Features
    /// </summary>
    public string? Features { get; set; }

    /// <summary>
    /// طرق الدفع المحفوظة - Saved payment methods
    /// </summary>
    public List<SavedPaymentMethodDto> SavedPaymentMethods { get; set; } = new();

    /// <summary>
    /// تفاصيل الخطة - Plan details
    /// </summary>
    public SubscriptionPlanDetails Plan { get; set; } = new();

    /// <summary>
    /// خطط بديلة - Alternative plans
    /// </summary>
    public List<SubscriptionPlanDetails> AlternativePlans { get; set; } = new();

    /// <summary>
    /// اسم المستخدم - User name
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني للمستخدم - User email
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;
}

/// <summary>
/// تفاصيل خطة الاشتراك - Subscription plan details
/// </summary>
public class SubscriptionPlanDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = "monthly";
    public decimal MonthlyEquivalent { get; set; }
    public decimal YearlySavingsPercent { get; set; }
    public List<string> Features { get; set; } = new();
}

/// <summary>
/// طريقة دفع محفوظة - Saved Payment Method DTO
/// </summary>
public class SavedPaymentMethodDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
}

/// <summary>
/// نموذج إلغاء الاشتراك - Subscription Cancel ViewModel
/// </summary>
public class SubscriptionCancelViewModel
{
    /// <summary>
    /// معرف الاشتراك - Subscription ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// اسم الخطة - Plan name
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// نهاية الفترة الحالية - Current period end
    /// </summary>
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>
    /// الإلغاء في نهاية الفترة - Cancel at period end
    /// </summary>
    [Display(Name = "إلغاء في نهاية الفترة الحالية")]
    public bool CancelAtPeriodEnd { get; set; } = true;

    /// <summary>
    /// سبب الإلغاء - Cancellation reason
    /// </summary>
    [Display(Name = "سبب الإلغاء (اختياري)")]
    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// إحصائيات الاستخدام - Usage stats
    /// </summary>
    public SubscriptionUsageStats? Stats { get; set; }
}

/// <summary>
/// إحصائيات استخدام الاشتراك - Subscription usage stats
/// </summary>
public class SubscriptionUsageStats
{
    public int CoursesAccessed { get; set; }
    public int CoursesCompleted { get; set; }
    public int LessonsCompleted { get; set; }
    public int CertificatesEarned { get; set; }
    public decimal LearningHours { get; set; }
}

