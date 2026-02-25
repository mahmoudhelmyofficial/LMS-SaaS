using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Settings;
using LMS.Domain.Enums;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة إعدادات بوابات الدفع - Payment Gateway Settings Controller
/// Enhanced with health checks, testing, and complete configuration
/// </summary>
public class PaymentGatewaySettingsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaymentGatewaySettingsController> _logger;

    public PaymentGatewaySettingsController(
        ApplicationDbContext context,
        IPaymentGatewayFactory gatewayFactory,
        IMemoryCache cache,
        ILogger<PaymentGatewaySettingsController> logger)
    {
        _context = context;
        _gatewayFactory = gatewayFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// قائمة إعدادات بوابات الدفع - Payment Gateway Settings List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var settings = await _context.PaymentGatewaySettings
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        return View(settings);
    }

    /// <summary>
    /// إنشاء إعدادات بوابة دفع جديدة - Create new payment gateway setting
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new PaymentGatewaySettingCreateViewModel());
    }

    /// <summary>
    /// حفظ إعدادات البوابة الجديدة - Save new gateway settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentGatewaySettingCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var setting = new PaymentGatewaySetting
            {
                GatewayName = model.GatewayName,
                GatewayType = model.GatewayType,
                DisplayName = model.DisplayName,
                Description = model.Description,
                ApiKey = model.ApiKey,
                SecretKey = model.SecretKey,
                WebhookSecret = model.WebhookSecret,
                IsTestMode = model.IsTestMode,
                TestApiKey = model.TestApiKey,
                TestSecretKey = model.TestSecretKey,
                IsEnabled = model.IsEnabled,
                DisplayOrder = model.DisplayOrder,
                SupportedCurrencies = model.SupportedCurrencies,
                TransactionFeePercentage = model.TransactionFeePercentage,
                TransactionFeeFixed = model.TransactionFeeFixed,
                MinimumAmount = model.MinimumAmount ?? 0,
                MaximumAmount = model.MaximumAmount ?? 100000,
                ConfigurationJson = model.ConfigurationJson
            };

            _context.PaymentGatewaySettings.Add(setting);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إضافة إعدادات بوابة الدفع بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل إعدادات بوابة الدفع - Edit payment gateway settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        var viewModel = new PaymentGatewaySettingEditViewModel
        {
            Id = setting.Id,
            GatewayName = setting.GatewayName,
            GatewayType = setting.GatewayType,
            DisplayName = setting.DisplayName,
            Description = setting.Description,
            ApiKey = setting.ApiKey,
            SecretKey = setting.SecretKey,
            WebhookSecret = setting.WebhookSecret,
            IsTestMode = setting.IsTestMode,
            TestApiKey = setting.TestApiKey,
            TestSecretKey = setting.TestSecretKey,
            IsEnabled = setting.IsEnabled,
            DisplayOrder = setting.DisplayOrder,
            SupportedCurrencies = setting.SupportedCurrencies,
            TransactionFeePercentage = setting.TransactionFeePercentage,
            TransactionFeeFixed = setting.TransactionFeeFixed,
            MinimumAmount = setting.MinimumAmount,
            MaximumAmount = setting.MaximumAmount,
            ConfigurationJson = setting.ConfigurationJson
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات إعدادات البوابة - Save gateway settings edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PaymentGatewaySettingEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            setting.GatewayName = model.GatewayName;
            setting.GatewayType = model.GatewayType;
            setting.DisplayName = model.DisplayName;
            setting.Description = model.Description;
            setting.ApiKey = model.ApiKey;
            setting.SecretKey = model.SecretKey;
            setting.WebhookSecret = model.WebhookSecret;
            setting.IsTestMode = model.IsTestMode;
            setting.TestApiKey = model.TestApiKey;
            setting.TestSecretKey = model.TestSecretKey;
            setting.IsEnabled = model.IsEnabled;
            setting.DisplayOrder = model.DisplayOrder;
            setting.SupportedCurrencies = model.SupportedCurrencies;
            setting.TransactionFeePercentage = model.TransactionFeePercentage;
            setting.TransactionFeeFixed = model.TransactionFeeFixed;
            setting.MinimumAmount = model.MinimumAmount ?? 0;
            setting.MaximumAmount = model.MaximumAmount ?? 100000;
            setting.ConfigurationJson = model.ConfigurationJson;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث إعدادات بوابة الدفع بنجاح");
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// تبديل حالة البوابة - Toggle gateway status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        setting.IsEnabled = !setting.IsEnabled;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(setting.IsEnabled ? "تفعيل" : "تعطيل")} بوابة الدفع بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف إعدادات البوابة - Delete gateway settings
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        _context.PaymentGatewaySettings.Remove(setting);
        await _context.SaveChangesAsync();

        InvalidateCache();
        SetSuccessMessage("تم حذف إعدادات بوابة الدفع بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// اختبار الاتصال - Test gateway connection
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(int id)
    {
        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting == null)
            return NotFound();

        try
        {
            var gateway = _gatewayFactory.GetGateway(setting.GatewayType);
            var result = await gateway.HealthCheckAsync();

            setting.LastHealthCheck = DateTime.UtcNow;
            setting.IsHealthy = result.IsHealthy;
            setting.LastErrorMessage = result.ErrorMessage;

            if (result.IsHealthy)
            {
                setting.FailedAttemptsCount = 0;
            }
            else
            {
                setting.FailedAttemptsCount++;
            }

            await _context.SaveChangesAsync();
            InvalidateCache();

            return Json(new
            {
                success = result.IsHealthy,
                message = result.IsHealthy ? "تم الاتصال بنجاح" : (result.ErrorMessage ?? "فشل الاتصال"),
                responseTimeMs = result.ResponseTimeMs,
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection for gateway {Id}", id);
            
            setting.LastHealthCheck = DateTime.UtcNow;
            setting.IsHealthy = false;
            setting.LastErrorMessage = ex.Message;
            setting.FailedAttemptsCount++;
            await _context.SaveChangesAsync();
            InvalidateCache();

            return Json(new { success = false, message = $"فشل الاتصال: {ex.Message}" });
        }
    }

    /// <summary>
    /// تعيين البوابة الافتراضية - Set as default gateway
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        // Unset all defaults
        await _context.PaymentGatewaySettings
            .Where(s => s.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));

        // Set new default
        var setting = await _context.PaymentGatewaySettings.FindAsync(id);
        if (setting != null)
        {
            setting.IsDefault = true;
            await _context.SaveChangesAsync();
        }

        InvalidateCache();
        SetSuccessMessage("تم تعيين البوابة الافتراضية");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// اختبار جميع البوابات - Test all enabled gateways
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestAll()
    {
        var settings = await _context.PaymentGatewaySettings
            .Where(s => s.IsEnabled)
            .ToListAsync();

        var results = new List<object>();

        foreach (var setting in settings)
        {
            try
            {
                var gateway = _gatewayFactory.GetGateway(setting.GatewayType);
                var result = await gateway.HealthCheckAsync();

                setting.LastHealthCheck = DateTime.UtcNow;
                setting.IsHealthy = result.IsHealthy;
                setting.LastErrorMessage = result.ErrorMessage;

                results.Add(new
                {
                    id = setting.Id,
                    name = setting.DisplayName ?? setting.Name,
                    success = result.IsHealthy,
                    responseTimeMs = result.ResponseTimeMs,
                    error = result.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                setting.LastHealthCheck = DateTime.UtcNow;
                setting.IsHealthy = false;
                setting.LastErrorMessage = ex.Message;

                results.Add(new
                {
                    id = setting.Id,
                    name = setting.DisplayName ?? setting.Name,
                    success = false,
                    responseTimeMs = 0,
                    error = ex.Message
                });
            }
        }

        await _context.SaveChangesAsync();
        InvalidateCache();

        return Json(new { results });
    }

    /// <summary>
    /// الحصول على رابط الويب هوك - Get webhook URL for gateway
    /// </summary>
    [HttpGet]
    public IActionResult GetWebhookUrl(PaymentGatewayType gatewayType)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var webhookPath = gatewayType switch
        {
            PaymentGatewayType.Stripe => "/api/webhooks/stripe",
            PaymentGatewayType.Paymob => "/api/webhooks/paymob",
            PaymentGatewayType.Fawry => "/api/webhooks/fawry",
            PaymentGatewayType.Tap => "/api/webhooks/tap",
            PaymentGatewayType.MyFatoorah => "/api/webhooks/myfatoorah",
            PaymentGatewayType.Hyperpay => "/api/webhooks/hyperpay",
            PaymentGatewayType.PayTabs => "/api/webhooks/paytabs",
            PaymentGatewayType.PayPal => "/api/webhooks/paypal",
            _ => $"/api/webhooks/{gatewayType.ToString().ToLower()}"
        };

        return Json(new { webhookUrl = $"{baseUrl}{webhookPath}" });
    }

    /// <summary>
    /// إعداد سريع للبوابات الموصى بها - Quick setup for recommended gateways
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickSetup(string region)
    {
        var gatewaysToAdd = region switch
        {
            "egypt" => new[]
            {
                (PaymentGatewayType.Paymob, "باي موب", "Paymob", 1),
                (PaymentGatewayType.Fawry, "فوري", "Fawry", 2),
                (PaymentGatewayType.BankTransfer, "تحويل بنكي", "Bank Transfer", 3)
            },
            "gulf" => new[]
            {
                (PaymentGatewayType.Tap, "تاب", "Tap Payments", 1),
                (PaymentGatewayType.MyFatoorah, "ماي فاتورة", "MyFatoorah", 2),
                (PaymentGatewayType.Hyperpay, "هايبر باي", "Hyperpay", 3)
            },
            "international" => new[]
            {
                (PaymentGatewayType.Stripe, "ستريب", "Stripe", 1),
                (PaymentGatewayType.PayPal, "باي بال", "PayPal", 2)
            },
            _ => Array.Empty<(PaymentGatewayType, string, string, int)>()
        };

        foreach (var (type, nameAr, nameEn, order) in gatewaysToAdd)
        {
            var exists = await _context.PaymentGatewaySettings
                .AnyAsync(s => s.GatewayType == type);

            if (!exists)
            {
                var setting = new PaymentGatewaySetting
                {
                    Name = nameEn,
                    DisplayName = nameEn,
                    DisplayNameAr = nameAr,
                    GatewayType = type,
                    IsEnabled = false,
                    IsSandbox = true,
                    DisplayOrder = order,
                    DefaultCurrency = region == "gulf" ? "SAR" : "EGP",
                    SupportedCountries = region switch
                    {
                        "egypt" => "[\"EG\"]",
                        "gulf" => "[\"SA\",\"AE\",\"KW\",\"BH\",\"QA\",\"OM\"]",
                        _ => "[\"US\",\"GB\",\"EU\"]"
                    }
                };

                _context.PaymentGatewaySettings.Add(setting);
            }
        }

        await _context.SaveChangesAsync();
        InvalidateCache();

        SetSuccessMessage($"تم إضافة بوابات الدفع لمنطقة {region}. يرجى تكوين بيانات الاعتماد لكل بوابة.");
        return RedirectToAction(nameof(Index));
    }

    private void InvalidateCache()
    {
        _cache.InvalidatePaymentGatewayCache();
    }
}

