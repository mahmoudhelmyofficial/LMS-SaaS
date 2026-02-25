using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Notifications;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة قوالب البريد الإلكتروني - Email Templates Management Controller
/// </summary>
public class EmailTemplatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailTemplatesController> _logger;

    public EmailTemplatesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<EmailTemplatesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة القوالب - Templates list
    /// </summary>
    public async Task<IActionResult> Index(bool? active = null)
    {
        var query = _context.EmailTemplates.AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(t => t.IsActive == active.Value);
        }

        var templates = await query
            .OrderBy(t => t.TemplateCode)
            .ToListAsync();

        ViewBag.Active = active;

        return View(templates);
    }

    /// <summary>
    /// تفاصيل القالب - Template details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        return View(template);
    }

    /// <summary>
    /// إنشاء قالب جديد - Create new template
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmailTemplateCreateViewModel());
    }

    /// <summary>
    /// حفظ القالب الجديد - Save new template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmailTemplateCreateViewModel model)
    {
        // Check if template code already exists
        var existingTemplate = await _context.EmailTemplates
            .AnyAsync(t => t.TemplateCode == model.TemplateCode);

        if (existingTemplate)
        {
            ModelState.AddModelError("TemplateCode", "رمز القالب مستخدم بالفعل");
        }

        if (ModelState.IsValid)
        {
            var template = new EmailTemplate
            {
                Name = model.Name,
                TemplateCode = model.TemplateCode,
                Subject = model.Subject,
                HtmlBody = model.HtmlBody,
                PlainTextBody = model.PlainTextBody,
                FromName = model.FromName,
                FromEmail = model.FromEmail,
                Description = model.Description,
                IsActive = model.IsActive
            };

            _context.EmailTemplates.Add(template);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء القالب بنجاح", "Template created successfully.");
            return RedirectToAction(nameof(Details), new { id = template.Id });
        }

        return View(model);
    }

    /// <summary>
    /// تعديل القالب - Edit template
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var viewModel = new EmailTemplateEditViewModel
        {
            Id = template.Id,
            Name = template.Name,
            TemplateCode = template.TemplateCode,
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            PlainTextBody = template.PlainTextBody,
            FromName = template.FromName,
            FromEmail = template.FromEmail,
            Description = template.Description,
            IsActive = template.IsActive,
            SentCount = template.SentCount,
            LastSentAt = template.LastSentAt
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات القالب - Save template edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmailTemplateEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        // Check if template code changed and already exists
        if (template.TemplateCode != model.TemplateCode)
        {
            var existingTemplate = await _context.EmailTemplates
                .AnyAsync(t => t.TemplateCode == model.TemplateCode && t.Id != id);

            if (existingTemplate)
            {
                ModelState.AddModelError("TemplateCode", "رمز القالب مستخدم بالفعل");
            }
        }

        if (ModelState.IsValid)
        {
            template.Name = model.Name;
            template.TemplateCode = model.TemplateCode;
            template.Subject = model.Subject;
            template.HtmlBody = model.HtmlBody;
            template.PlainTextBody = model.PlainTextBody;
            template.FromName = model.FromName;
            template.FromEmail = model.FromEmail;
            template.Description = model.Description;
            template.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القالب بنجاح", "Template updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// حذف القالب - Delete template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        _context.EmailTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح", "Template deleted successfully.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// معاينة القالب - Preview template
    /// </summary>
    public async Task<IActionResult> Preview(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        // Return the view instead of raw HTML content to properly render Arabic text
        return View(template);
    }

    /// <summary>
    /// إرسال بريد تجريبي - Send test email (alias for SendTest)
    /// </summary>
    [HttpGet]
    public IActionResult SendTest(int id)
    {
        return RedirectToAction(nameof(Test), new { id });
    }

    /// <summary>
    /// إرسال بريد تجريبي - Send test email
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Test(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        ViewBag.Template = template;

        return View(new EmailTemplateTestViewModel { TemplateId = id });
    }

    /// <summary>
    /// إرسال البريد التجريبي - Process test email
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(EmailTemplateTestViewModel model)
    {
        var template = await _context.EmailTemplates.FindAsync(model.TemplateId);
        if (template == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // Render template with sample data
                var renderedHtml = RenderTemplate(template);
                
                // Send actual test email
                await _emailService.SendEmailAsync(
                    model.TestEmail,
                    $"[اختبار] {template.Subject}",
                    renderedHtml,
                    true);
                
                // Update template stats
                template.SentCount++;
                template.LastSentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Test email sent successfully for template {TemplateId} to {Email}", 
                    model.TemplateId, model.TestEmail);
                
                SetSuccessMessage($"تم إرسال بريد تجريبي بنجاح إلى {model.TestEmail}", $"Test email sent successfully to {model.TestEmail}");
                return RedirectToAction(nameof(Details), new { id = model.TemplateId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test email for template {TemplateId}", model.TemplateId);
                SetErrorMessage($"فشل إرسال البريد التجريبي: {ex.Message}", $"Failed to send test email: {ex.Message}");
            }
        }

        ViewBag.Template = template;
        return View(model);
    }

    /// <summary>
    /// نسخ القالب - Duplicate template (GET shows confirmation; no state change)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Duplicate(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        return View("DuplicateConfirm", template);
    }

    /// <summary>
    /// نسخ القالب - Duplicate template (POST request)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateConfirm(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var newTemplate = new EmailTemplate
        {
            Name = $"{template.Name} - نسخة",
            TemplateCode = $"{template.TemplateCode}_copy_{DateTime.UtcNow.Ticks}",
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            PlainTextBody = template.PlainTextBody,
            FromName = template.FromName,
            FromEmail = template.FromEmail,
            Description = template.Description,
            IsActive = false
        };

        _context.EmailTemplates.Add(newTemplate);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم نسخ القالب بنجاح", "Template copied successfully.");
        return RedirectToAction(nameof(Edit), new { id = newTemplate.Id });
    }

    private string RenderTemplate(EmailTemplate template)
    {
        var html = template.HtmlBody
            .Replace("{{UserName}}", "محمد أحمد")
            .Replace("{{Email}}", "user@example.com")
            .Replace("{{FirstName}}", "محمد")
            .Replace("{{LastName}}", "أحمد")
            .Replace("{{CourseName}}", "دورة تطوير تطبيقات الويب")
            .Replace("{{InstructorName}}", "د. أحمد محمد")
            .Replace("{{PlatformName}}", "منصة التعليم")
            .Replace("{{SupportEmail}}", "support@platform.com")
            .Replace("{{LoginUrl}}", "#")
            .Replace("{{ResetPasswordUrl}}", "#")
            .Replace("{{VerificationCode}}", "123456")
            .Replace("{{OrderNumber}}", "ORD-2025-001234")
            .Replace("{{Amount}}", "299.00 جنيه");

        return html;
    }

    /// <summary>
    /// إنشاء من قالب جاهز - Create from preset template
    /// </summary>
    [HttpGet]
    public IActionResult CreateFromPreset(string templateType)
    {
        var presets = new Dictionary<string, (string Name, string Subject, string HtmlBody, string Description)>
        {
            ["welcome"] = (
                "رسالة ترحيبية",
                "مرحباً بك في منصتنا التعليمية!",
                @"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; line-height: 1.8; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #3454d1, #6b7fd9); color: white; padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
        <h1 style='margin: 0;'>🎉 مرحباً بك!</h1>
    </div>
    <div style='background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2 style='color: #3454d1;'>مرحباً {{UserName}}!</h2>
        <p>نحن سعداء بانضمامك إلى <strong>{{PlatformName}}</strong>.</p>
        <p>يمكنك الآن استكشاف الدورات والبدء في رحلتك التعليمية.</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{{LoginUrl}}' style='background: #3454d1; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>ابدأ الآن</a>
        </div>
        <p style='color: #666;'>مع تحيات،<br/>فريق {{PlatformName}}</p>
    </div>
</body>
</html>",
                "قالب ترحيبي للمستخدمين الجدد"
            ),
            ["purchase"] = (
                "تأكيد شراء",
                "تأكيد طلبك رقم {{OrderNumber}}",
                @"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; line-height: 1.8; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: #28a745; color: white; padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
        <h1 style='margin: 0;'>✅ تم تأكيد طلبك!</h1>
    </div>
    <div style='background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2 style='color: #28a745;'>شكراً لك {{UserName}}!</h2>
        <p>تم تأكيد طلبك بنجاح.</p>
        <div style='background: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-right: 4px solid #28a745;'>
            <p><strong>رقم الطلب:</strong> {{OrderNumber}}</p>
            <p><strong>المبلغ:</strong> {{Amount}}</p>
            <p><strong>الدورة:</strong> {{CourseName}}</p>
        </div>
        <p>يمكنك البدء في الدورة الآن من حسابك.</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{{LoginUrl}}' style='background: #28a745; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>الذهاب للدورة</a>
        </div>
        <p style='color: #666;'>مع تحيات،<br/>فريق {{PlatformName}}</p>
    </div>
</body>
</html>",
                "قالب تأكيد الشراء والطلبات"
            ),
            ["completion"] = (
                "إتمام دورة",
                "🎓 مبروك! أكملت دورة {{CourseName}}",
                @"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; line-height: 1.8; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #ffc107, #ffdb4d); color: #333; padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
        <h1 style='margin: 0;'>🎓 مبروك!</h1>
    </div>
    <div style='background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2 style='color: #ffc107;'>تهانينا {{UserName}}!</h2>
        <p>لقد أتممت بنجاح دورة <strong>{{CourseName}}</strong>.</p>
        <p>هذا إنجاز رائع! 🏆</p>
        <div style='background: white; padding: 20px; border-radius: 5px; margin: 20px 0; text-align: center;'>
            <p style='font-size: 18px;'>🎖️ شهادتك جاهزة للتحميل</p>
        </div>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{{LoginUrl}}' style='background: #ffc107; color: #333; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>تحميل الشهادة</a>
        </div>
        <p style='color: #666;'>مع تحيات،<br/>فريق {{PlatformName}}</p>
    </div>
</body>
</html>",
                "قالب إشعار إتمام الدورة"
            ),
            ["reminder"] = (
                "إشعار تذكير",
                "⏰ تذكير: لديك دورات في انتظارك!",
                @"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; line-height: 1.8; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: #17a2b8; color: white; padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
        <h1 style='margin: 0;'>⏰ تذكير ودي</h1>
    </div>
    <div style='background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2 style='color: #17a2b8;'>مرحباً {{UserName}}!</h2>
        <p>لاحظنا أنك لم تكمل دوراتك بعد.</p>
        <p>لا تفوت فرصة التعلم - عد الآن وأكمل رحلتك التعليمية! 📚</p>
        <div style='background: white; padding: 20px; border-radius: 5px; margin: 20px 0; border-right: 4px solid #17a2b8;'>
            <p>💡 <strong>نصيحة:</strong> حاول تخصيص وقت يومي قصير للتعلم - حتى 15 دقيقة يومياً يمكن أن تحدث فرقاً كبيراً!</p>
        </div>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{{LoginUrl}}' style='background: #17a2b8; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>أكمل التعلم</a>
        </div>
        <p style='color: #666;'>مع تحيات،<br/>فريق {{PlatformName}}</p>
    </div>
</body>
</html>",
                "قالب إشعار تذكير للمستخدمين"
            )
        };

        if (string.IsNullOrEmpty(templateType) || !presets.ContainsKey(templateType))
        {
            _logger.LogWarning("Unknown template type requested: {TemplateType}", templateType);
            SetErrorMessage("نوع القالب غير معروف", "Unknown template type.");
            return RedirectToAction(nameof(Index));
        }

        var preset = presets[templateType];
        var viewModel = new EmailTemplateCreateViewModel
        {
            Name = preset.Name,
            Subject = preset.Subject,
            HtmlBody = preset.HtmlBody,
            Description = preset.Description,
            TemplateCode = $"{templateType}_{DateTime.UtcNow:yyyyMMddHHmmss}",
            IsActive = true
        };

        _logger.LogInformation("Creating email template from preset: {TemplateType}", templateType);
        return View("Create", viewModel);
    }
}

