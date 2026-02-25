using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة التقارير المتقدمة - Advanced Reports Management Controller
/// </summary>
public class AdvancedReportsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdvancedReportsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public AdvancedReportsController(
        ApplicationDbContext context, 
        ILogger<AdvancedReportsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// نظرة عامة على التقارير المتقدمة - Advanced Reports Overview
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Get statistics for the overview page
        // Use RequestedAt instead of GeneratedAt as GeneratedAt is a [NotMapped] alias
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var totalTemplates = await _context.ReportTemplates.CountAsync();
        var activeTemplates = await _context.ReportTemplates.CountAsync(t => t.IsActive);
        var totalScheduled = await _context.ScheduledReports.CountAsync();
        var activeScheduled = await _context.ScheduledReports.CountAsync(s => s.IsActive);
        var totalExports = await _context.ReportExports.CountAsync();
        var recentExports = await _context.ReportExports.CountAsync(e => e.RequestedAt >= sevenDaysAgo);

        // Get recent scheduled reports
        var recentScheduledReports = await _context.ScheduledReports
            .Include(r => r.ReportTemplate)
            .OrderByDescending(r => r.LastExecutedAt ?? r.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Get recent exports - using RequestedBy instead of NotMapped alias GeneratedByUser
        var recentExportsList = await _context.ReportExports
            .Include(e => e.RequestedBy)
            .Include(e => e.ScheduledReport)
            .Include(e => e.ReportTemplate)
            .OrderByDescending(e => e.RequestedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.TotalTemplates = totalTemplates;
        ViewBag.ActiveTemplates = activeTemplates;
        ViewBag.TotalScheduled = totalScheduled;
        ViewBag.ActiveScheduled = activeScheduled;
        ViewBag.TotalExports = totalExports;
        ViewBag.RecentExports = recentExports;
        ViewBag.RecentScheduledReports = recentScheduledReports;
        ViewBag.RecentExportsList = recentExportsList;

        return View();
    }

    #region Report Templates

    /// <summary>
    /// قوالب التقارير - Report templates
    /// </summary>
    public async Task<IActionResult> Templates(int page = 1, ReportType? type = null)
    {
        var query = _context.ReportTemplates.AsQueryable();
        
        // Apply type filter if specified
        if (type.HasValue)
        {
            query = query.Where(t => t.ReportType == type.Value);
        }
        
        var pageSize = await _configService.GetPaginationSizeAsync("report_templates", 20);
        var totalCount = await query.CountAsync();
        var templates = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.FilterType = type;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        return View(templates);
    }

    /// <summary>
    /// إنشاء قالب جديد - Create new template
    /// </summary>
    [HttpGet]
    public IActionResult CreateTemplate()
    {
        return View(new ReportTemplateViewModel());
    }

    /// <summary>
    /// حفظ القالب الجديد - Save new template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(ReportTemplateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var template = new ReportTemplate
            {
                Name = model.Name,
                Description = model.Description,
                ReportType = model.ReportType,
                Configuration = model.Configuration,
                IsActive = true
            };

            _context.ReportTemplates.Add(template);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء القالب بنجاح");
            return RedirectToAction(nameof(Templates));
        }

        return View(model);
    }

    /// <summary>
    /// تعديل قالب - Edit template
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        var model = new ReportTemplateViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            ReportType = template.ReportType,
            Configuration = template.Configuration
        };

        // Get usage statistics for this template
        var usageCount = await _context.ReportExports.CountAsync(r => r.ReportTemplateId == id);
        var scheduledCount = await _context.ScheduledReports.CountAsync(r => r.TemplateId == id && r.IsActive);
        var completedExports = await _context.ReportExports.CountAsync(r => r.ReportTemplateId == id && r.Status == "completed");

        ViewBag.UsageCount = usageCount;
        ViewBag.ScheduledCount = scheduledCount;
        ViewBag.CompletedExports = completedExports;
        ViewBag.CreatedAt = template.CreatedAt;
        ViewBag.UpdatedAt = template.UpdatedAt;

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات القالب - Save template changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(int id, ReportTemplateViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var template = await _context.ReportTemplates.FindAsync(id);
            if (template == null)
                return NotFound();

            template.Name = model.Name;
            template.Description = model.Description;
            template.ReportType = model.ReportType;
            template.Configuration = model.Configuration;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث القالب بنجاح");
            return RedirectToAction(nameof(Templates));
        }

        return View(model);
    }

    /// <summary>
    /// حذف قالب - Delete template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
            return NotFound();

        _context.ReportTemplates.Remove(template);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف القالب بنجاح");
        return RedirectToAction(nameof(Templates));
    }

    /// <summary>
    /// نسخ قالب - Duplicate template
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateTemplate(int id)
    {
        var template = await _context.ReportTemplates.FindAsync(id);
        if (template == null)
        {
            SetErrorMessage("القالب غير موجود");
            return RedirectToAction(nameof(Templates));
        }

        // Create a duplicate with a new name
        var duplicate = new ReportTemplate
        {
            Name = $"{template.Name} (نسخة)",
            Description = template.Description,
            ReportType = template.ReportType,
            Configuration = template.Configuration,
            IsActive = false // Start as inactive to allow review
        };

        _context.ReportTemplates.Add(duplicate);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Template {TemplateId} duplicated to new template {NewTemplateId}", id, duplicate.Id);
        SetSuccessMessage($"تم نسخ القالب بنجاح. القالب الجديد: {duplicate.Name}");
        return RedirectToAction(nameof(EditTemplate), new { id = duplicate.Id });
    }

    #endregion

    #region Scheduled Reports

    /// <summary>
    /// التقارير المجدولة - Scheduled reports
    /// </summary>
    public async Task<IActionResult> ScheduledReports(int page = 1, bool? active = null, string? frequency = null)
    {
        var query = _context.ScheduledReports
            .Include(r => r.ReportTemplate)
            .AsQueryable();

        // Apply active filter
        if (active.HasValue)
        {
            query = query.Where(r => r.IsActive == active.Value);
        }

        // Apply frequency filter
        if (!string.IsNullOrEmpty(frequency))
        {
            query = query.Where(r => r.Frequency == frequency);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("scheduled_reports", 20);
        var totalCount = await query.CountAsync();
        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.ActiveFilter = active;
        ViewBag.FrequencyFilter = frequency;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        return View(reports);
    }

    /// <summary>
    /// إنشاء تقرير مجدول - Create scheduled report
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CreateScheduledReport()
    {
        // Get templates
        ViewBag.Templates = await _context.ReportTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return View(new ScheduledReportViewModel());
    }

    /// <summary>
    /// حفظ التقرير المجدول - Save scheduled report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateScheduledReport(ScheduledReportViewModel model)
    {
        if (ModelState.IsValid)
        {
            var report = new ScheduledReport
            {
                TemplateId = model.TemplateId,
                Name = model.Name,
                Description = model.Description,
                Frequency = model.Frequency,
                DayOfWeek = model.DayOfWeek,
                DayOfMonth = model.DayOfMonth,
                TimeOfDay = model.TimeOfDay,
                Recipients = model.Recipients,
                Format = model.Format,
                IsActive = true
            };

            _context.ScheduledReports.Add(report);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء التقرير المجدول بنجاح");
            return RedirectToAction(nameof(ScheduledReports));
        }

        // Reload templates
        ViewBag.Templates = await _context.ReportTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// تعديل تقرير مجدول - Edit scheduled report
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditScheduledReport(int id)
    {
        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        var model = new ScheduledReportViewModel
        {
            Id = report.Id,
            TemplateId = report.TemplateId,
            Name = report.Name,
            Description = report.Description,
            Frequency = report.Frequency,
            DayOfWeek = report.DayOfWeek,
            DayOfMonth = report.DayOfMonth,
            TimeOfDay = report.TimeOfDay ?? new TimeSpan(8, 0, 0),
            Recipients = report.Recipients,
            Format = report.Format
        };

        // Get templates
        ViewBag.Templates = await _context.ReportTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات التقرير المجدول - Save scheduled report changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditScheduledReport(int id, ScheduledReportViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var report = await _context.ScheduledReports.FindAsync(id);
            if (report == null)
                return NotFound();

            report.TemplateId = model.TemplateId;
            report.Name = model.Name;
            report.Description = model.Description;
            report.Frequency = model.Frequency;
            report.DayOfWeek = model.DayOfWeek;
            report.DayOfMonth = model.DayOfMonth;
            report.TimeOfDay = model.TimeOfDay;
            report.Recipients = model.Recipients;
            report.Format = model.Format;

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث التقرير المجدول بنجاح");
            return RedirectToAction(nameof(ScheduledReports));
        }

        // Reload templates
        ViewBag.Templates = await _context.ReportTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// حذف تقرير مجدول - Delete scheduled report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScheduledReport(int id)
    {
        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        _context.ScheduledReports.Remove(report);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التقرير المجدول بنجاح");
        return RedirectToAction(nameof(ScheduledReports));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleScheduledReport(int id)
    {
        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        report.IsActive = !report.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم {(report.IsActive ? "تفعيل" : "تعطيل")} التقرير المجدول");
        return RedirectToAction(nameof(ScheduledReports));
    }

    /// <summary>
    /// تنفيذ التقرير الآن - Run report now
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        try
        {
            var report = await _context.ScheduledReports
                .Include(r => r.ReportTemplate)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                SetErrorMessage("التقرير المجدول غير موجود");
                return RedirectToAction(nameof(ScheduledReports));
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Generate report export record
                var reportExport = new Domain.Entities.Analytics.ReportExport
                {
                    ScheduledReportId = report.Id,
                    ReportType = report.ReportTemplate?.ReportType.ToString() ?? "Custom",
                    ReportName = report.Name,
                    Format = report.Format ?? "PDF",
                    GeneratedByUserId = User.Identity?.Name,
                    ExportDate = DateTime.UtcNow,
                    Status = "Completed",
                    FileSize = 0,
                    RecordCount = 0
                };

                // Generate simple CSV content based on report type
                string csvContent = $"Report: {report.Name}\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}\n\n";
                csvContent += "This is a placeholder report. Full implementation requires template engine integration.\n";
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                var fileName = $"report-{report.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                
                // In production: save to file storage and set FileUrl
                // For now, we'll mark as completed
                reportExport.FileSize = bytes.Length;
                reportExport.RecordCount = 1;
                
                _context.ReportExports.Add(reportExport);

                // Update scheduled report
                report.LastRunAt = DateTime.UtcNow;
                report.NextRunAt = CalculateNextRun(report);

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Scheduled report {ReportId} executed successfully", id);

            SetSuccessMessage("تم تنفيذ التقرير بنجاح");
            return RedirectToAction(nameof(ScheduledReports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running scheduled report {ReportId}", id);
            SetErrorMessage("حدث خطأ أثناء تنفيذ التقرير");
            return RedirectToAction(nameof(ScheduledReports));
        }
    }

    #endregion

    #region Report Exports

    /// <summary>
    /// التقارير المُصدَّرة - Report exports
    /// </summary>
    public async Task<IActionResult> Exports(DateTime? from, DateTime? to, int page = 1)
    {
        var query = _context.ReportExports
            .Include(e => e.ScheduledReport)
            .Include(e => e.RequestedBy)
            .AsQueryable();

        // Use RequestedAt (actual column) instead of GeneratedAt (NotMapped alias)
        if (from.HasValue)
            query = query.Where(e => e.RequestedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.RequestedAt <= to.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("report_exports", 20);
        var totalCount = await query.CountAsync();
        var exports = await query
            .OrderByDescending(e => e.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(exports);
    }

    /// <summary>
    /// تحميل تقرير - Download report
    /// </summary>
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var export = await _context.ReportExports.FindAsync(id);
            if (export == null)
            {
                SetErrorMessage("التقرير غير موجود");
                return RedirectToAction(nameof(Exports));
            }

            if (string.IsNullOrEmpty(export.FileUrl))
            {
                // Generate on-the-fly if no file exists
                var csvContent = $"Report: {export.ReportName}\nGenerated: {export.ExportDate:yyyy-MM-dd HH:mm}\n\n";
                csvContent += "Report data would be here in full implementation.\n";
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                var fileName = $"report-{export.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                
                _logger.LogInformation("Generated report {ExportId} on-the-fly", id);
                
                return File(bytes, "text/csv", fileName);
            }

            // Check if file exists
            if (export.FileUrl.StartsWith("http"))
            {
                return Redirect(export.FileUrl);
            }

            var filePath = Path.Combine("wwwroot", export.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(bytes, "application/octet-stream", Path.GetFileName(filePath));
            }

            SetErrorMessage("ملف التقرير غير متوفر");
            return RedirectToAction(nameof(Exports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading report {ExportId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل التقرير");
            return RedirectToAction(nameof(Exports));
        }
    }

    /// <summary>
    /// حذف تقرير مُصدَّر - Delete exported report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExport(int id)
    {
        try
        {
            var export = await _context.ReportExports.FindAsync(id);
            if (export == null)
            {
                SetErrorMessage("التقرير غير موجود");
                return RedirectToAction(nameof(Exports));
            }

            // Delete physical file if it exists
            if (!string.IsNullOrEmpty(export.FileUrl) && !export.FileUrl.StartsWith("http"))
            {
                var filePath = Path.Combine("wwwroot", export.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                        _logger.LogInformation("Deleted report file: {FilePath}", filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete file: {FilePath}", filePath);
                    }
                }
            }

            _context.ReportExports.Remove(export);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Report export {ExportId} deleted", id);
            SetSuccessMessage("تم حذف التقرير بنجاح");
            return RedirectToAction(nameof(Exports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report export {ExportId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف التقرير");
            return RedirectToAction(nameof(Exports));
        }
    }

    #endregion

    #region Private Helpers

    private DateTime? CalculateNextRun(ScheduledReport report)
    {
        var now = DateTime.UtcNow;

        var timeOfDay = report.TimeOfDay ?? TimeSpan.Zero;
        return report.Frequency switch
        {
            "Daily" => now.Date.AddDays(1).Add(timeOfDay),
            "Weekly" => now.Date.AddDays((7 - (int)now.DayOfWeek + report.DayOfWeek.GetValueOrDefault()) % 7).Add(timeOfDay),
            "Monthly" => new DateTime(now.Year, now.Month, report.DayOfMonth.GetValueOrDefault(1)).AddMonths(1).Add(timeOfDay),
            _ => null
        };
    }

    #endregion
}

