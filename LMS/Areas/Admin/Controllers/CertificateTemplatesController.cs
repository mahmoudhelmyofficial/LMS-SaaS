using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Certifications;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة قوالب الشهادات - Certificate Templates Management Controller
/// </summary>
public class CertificateTemplatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CertificateTemplatesController> _logger;

    public CertificateTemplatesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<CertificateTemplatesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة القوالب - Templates list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var templates = await _context.CertificateTemplates
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();

        return View(templates);
    }

    /// <summary>
    /// تفاصيل القالب - Template details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        // Count usage
        var usageCount = await _context.Certificates
            .CountAsync(c => c.TemplateId == id);

        ViewBag.UsageCount = usageCount;

        return View(template);
    }

    /// <summary>
    /// إنشاء قالب جديد - Create new template
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var defaultTemplate = GetDefaultTemplate();
        return View(defaultTemplate);
    }

    /// <summary>
    /// حفظ القالب الجديد - Save new template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CertificateTemplateCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            // If setting as default, unset other defaults
            if (model.IsDefault)
            {
                var existingDefaults = await _context.CertificateTemplates
                    .Where(t => t.IsDefault)
                    .ToListAsync();

                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }

            var template = new CertificateTemplate
            {
                Name = model.Name,
                Description = model.Description,
                HtmlContent = model.HtmlContent,
                CssStyles = model.CssStyles,
                BackgroundImageUrl = model.BackgroundImageUrl,
                Width = model.Width,
                Height = model.Height,
                Orientation = model.Orientation,
                IsDefault = model.IsDefault,
                IsActive = model.IsActive,
                CreatedById = _currentUserService.UserId
            };

            _context.CertificateTemplates.Add(template);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء القالب بنجاح");
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
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var usageCount = await _context.Certificates
            .CountAsync(c => c.TemplateId == id);

        var viewModel = new CertificateTemplateEditViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            HtmlContent = template.HtmlContent,
            CssStyles = template.CssStyles,
            BackgroundImageUrl = template.BackgroundImageUrl,
            Width = template.Width,
            Height = template.Height,
            Orientation = template.Orientation,
            IsDefault = template.IsDefault,
            IsActive = template.IsActive,
            UsageCount = usageCount
        };

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات القالب - Save template edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CertificateTemplateEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            // If setting as default, unset other defaults
            if (model.IsDefault && !template.IsDefault)
            {
                var existingDefaults = await _context.CertificateTemplates
                    .Where(t => t.IsDefault && t.Id != id)
                    .ToListAsync();

                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }

            template.Name = model.Name;
            template.Description = model.Description;
            template.HtmlContent = model.HtmlContent;
            template.CssStyles = model.CssStyles;
            template.BackgroundImageUrl = model.BackgroundImageUrl;
            template.Width = model.Width;
            template.Height = model.Height;
            template.Orientation = model.Orientation;
            template.IsDefault = model.IsDefault;
            template.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القالب بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// معاينة القالب - Preview template
    /// </summary>
    public async Task<IActionResult> Preview(int id)
    {
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var preview = new CertificateTemplatePreviewViewModel
        {
            TemplateId = template.Id
        };

        var html = RenderTemplate(template, preview);

        return Content(html, "text/html");
    }

    /// <summary>
    /// نسخ القالب - Duplicate template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var newTemplate = new CertificateTemplate
        {
            Name = $"{template.Name} - نسخة",
            Description = template.Description,
            HtmlContent = template.HtmlContent,
            CssStyles = template.CssStyles,
            BackgroundImageUrl = template.BackgroundImageUrl,
            Width = template.Width,
            Height = template.Height,
            Orientation = template.Orientation,
            IsDefault = false,
            IsActive = true,
            CreatedById = _currentUserService.UserId
        };

        _context.CertificateTemplates.Add(newTemplate);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم نسخ القالب بنجاح");
        return RedirectToAction(nameof(Edit), new { id = newTemplate.Id });
    }

    /// <summary>
    /// حذف القالب - Delete template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var usageCount = await _context.Certificates
            .CountAsync(c => c.TemplateId == id);

        if (usageCount > 0)
        {
            SetErrorMessage($"لا يمكن حذف القالب لأنه مستخدم في {usageCount} شهادة");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (template.IsDefault)
        {
            SetErrorMessage("لا يمكن حذف القالب الافتراضي");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.CertificateTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تعيين كقالب افتراضي - Set as default template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        var template = await _context.CertificateTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        // Unset all other defaults
        var currentDefaults = await _context.CertificateTemplates
            .Where(t => t.IsDefault)
            .ToListAsync();

        foreach (var t in currentDefaults)
        {
            t.IsDefault = false;
        }

        // Set new default
        template.IsDefault = true;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعيين القالب كافتراضي بنجاح");
        return RedirectToAction(nameof(Index));
    }

    private CertificateTemplateCreateViewModel GetDefaultTemplate()
    {
        return new CertificateTemplateCreateViewModel
        {
            Name = "قالب افتراضي",
            HtmlContent = @"
<div class='certificate'>
    <h1>شهادة إتمام</h1>
    <p class='recipient'>هذا يشهد أن</p>
    <h2>{{StudentName}}</h2>
    <p>قد أتم بنجاح دورة</p>
    <h3>{{CourseName}}</h3>
    <p>بتاريخ {{CompletionDate}}</p>
    <div class='signatures'>
        <div class='signature'>
            <p>{{InstructorName}}</p>
            <p class='title'>المدرب</p>
        </div>
    </div>
    <p class='certificate-number'>رقم الشهادة: {{CertificateNumber}}</p>
</div>",
            CssStyles = @"
.certificate {
    font-family: 'Arial', sans-serif;
    text-align: center;
    padding: 50px;
    background: #fff;
}
h1 { font-size: 48px; color: #1a73e8; }
h2 { font-size: 36px; color: #333; margin: 20px 0; }
h3 { font-size: 28px; color: #666; }
.signatures { margin-top: 50px; }
.certificate-number { font-size: 12px; color: #999; margin-top: 30px; }",
            Width = 800,
            Height = 600,
            Orientation = "Landscape"
        };
    }

    private string RenderTemplate(CertificateTemplate template, CertificateTemplatePreviewViewModel data)
    {
        var html = template.HtmlContent
            .Replace("{{StudentName}}", data.StudentName)
            .Replace("{{CourseName}}", data.CourseName)
            .Replace("{{InstructorName}}", data.InstructorName)
            .Replace("{{CompletionDate}}", data.CompletionDate.ToString("yyyy/MM/dd"))
            .Replace("{{CertificateNumber}}", data.CertificateNumber)
            .Replace("{{Grade}}", data.Grade.ToString("F1"));

        var backgroundStyle = !string.IsNullOrEmpty(template.BackgroundImageUrl) 
            ? $"background-image: url('{template.BackgroundImageUrl}'); background-size: cover; background-position: center;" 
            : "";

        return $@"
<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>معاينة الشهادة - {template.Name}</title>
    <link href='https://fonts.googleapis.com/css2?family=Cairo:wght@300;400;600;700;800&display=swap' rel='stylesheet'>
    <link rel='stylesheet' href='/assets/vendors/css/feather.min.css'>
    <style>
        :root {{
            --primary: #004aad;
            --primary-light: #1a5cb8;
            --accent: #00d4ff;
            --text-dark: #1e293b;
            --text-muted: #64748b;
            --gradient-hero: linear-gradient(135deg, #004aad 0%, #0055c4 50%, #1a5cb9 100%);
        }}
        
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        
        body {{
            font-family: 'Cairo', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 0;
            direction: rtl;
        }}
        
        .preview-controls {{
            position: fixed;
            top: 20px;
            left: 20px;
            right: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            z-index: 1000;
            pointer-events: none;
        }}
        
        .preview-badge {{
            background: rgba(255,255,255,0.15);
            backdrop-filter: blur(20px);
            border: 1px solid rgba(255,255,255,0.2);
            color: white;
            padding: 14px 28px;
            border-radius: 50px;
            font-weight: 700;
            font-size: 0.95rem;
            display: flex;
            align-items: center;
            gap: 12px;
            pointer-events: auto;
            box-shadow: 0 8px 32px rgba(0,0,0,0.2);
        }}
        
        .close-preview {{
            background: white;
            border: none;
            padding: 14px 28px;
            border-radius: 50px;
            font-weight: 700;
            font-size: 0.95rem;
            font-family: 'Cairo', sans-serif;
            box-shadow: 0 8px 32px rgba(0,0,0,0.2);
            cursor: pointer;
            transition: all 0.3s;
            display: flex;
            align-items: center;
            gap: 10px;
            pointer-events: auto;
            color: var(--text-dark);
        }}
        
        .close-preview:hover {{
            background: var(--primary);
            color: white;
            transform: translateY(-2px);
        }}
        
        .preview-container {{
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 100px 24px 48px;
        }}
        
        .certificate-wrapper {{
            position: relative;
        }}
        
        .certificate-shadow {{
            position: absolute;
            top: 20px;
            left: 20px;
            right: -20px;
            bottom: -20px;
            background: rgba(0,0,0,0.3);
            border-radius: 20px;
            filter: blur(30px);
        }}
        
        .certificate-frame {{
            position: relative;
            width: {template.Width}px;
            max-width: 95vw;
            background: white;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 25px 80px rgba(0,0,0,0.3);
        }}
        
        .certificate-content {{
            width: 100%;
            height: {template.Height}px;
            {backgroundStyle}
            {template.CssStyles}
        }}
        
        .info-panel {{
            background: rgba(255,255,255,0.1);
            backdrop-filter: blur(20px);
            border: 1px solid rgba(255,255,255,0.2);
            padding: 20px 28px;
            border-radius: 16px;
            margin-top: 24px;
            display: flex;
            justify-content: center;
            gap: 32px;
            flex-wrap: wrap;
        }}
        
        .info-item {{
            display: flex;
            align-items: center;
            gap: 10px;
            color: rgba(255,255,255,0.9);
            font-size: 0.9rem;
        }}
        
        .info-item i {{
            color: var(--accent);
        }}
        
        .info-item strong {{
            color: white;
            font-weight: 700;
        }}
        
        @media print {{
            body {{
                background: white;
                padding: 0;
            }}
            .preview-controls, .info-panel {{
                display: none !important;
            }}
            .preview-container {{
                padding: 0;
            }}
            .certificate-wrapper, .certificate-shadow {{
                box-shadow: none;
            }}
            .certificate-frame {{
                box-shadow: none;
                border-radius: 0;
            }}
        }}
        
        @media (max-width: 768px) {{
            .preview-controls {{
                top: 12px;
                left: 12px;
                right: 12px;
            }}
            .preview-badge, .close-preview {{
                padding: 10px 16px;
                font-size: 0.8rem;
            }}
            .certificate-frame {{
                width: 100%;
            }}
            .certificate-content {{
                height: auto;
                min-height: 400px;
            }}
            .info-panel {{
                flex-direction: column;
                gap: 12px;
                align-items: center;
            }}
        }}
    </style>
</head>
<body>
    <div class='preview-controls'>
        <div class='preview-badge'>
            <i class='feather-award'></i>
            <span>معاينة الشهادة</span>
        </div>
        
        <button class='close-preview' onclick='window.close()'>
            <i class='feather-x'></i>
            <span>إغلاق</span>
        </button>
    </div>
    
    <div class='preview-container'>
        <div>
            <div class='certificate-wrapper'>
                <div class='certificate-shadow'></div>
                <div class='certificate-frame'>
                    <div class='certificate-content'>
                        {html}
                    </div>
                </div>
            </div>
            
            <div class='info-panel'>
                <div class='info-item'>
                    <i class='feather-layout'></i>
                    <span>القالب: <strong>{template.Name}</strong></span>
                </div>
                <div class='info-item'>
                    <i class='feather-maximize-2'></i>
                    <span>الأبعاد: <strong>{template.Width} × {template.Height}</strong></span>
                </div>
                <div class='info-item'>
                    <i class='feather-monitor'></i>
                    <span>الاتجاه: <strong>{(template.Orientation == "Landscape" ? "أفقي" : "عمودي")}</strong></span>
                </div>
            </div>
        </div>
    </div>
    
    <script src='https://cdn.jsdelivr.net/npm/feather-icons/dist/feather.min.js'></script>
    <script>
        if (typeof feather !== 'undefined') {{
            feather.replace();
        }}
    </script>
</body>
</html>";
    }
}

