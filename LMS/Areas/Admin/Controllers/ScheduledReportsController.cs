using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Analytics;
using LMS.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة التقارير المجدولة - Scheduled Reports Controller
/// </summary>
public class ScheduledReportsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ScheduledReportsController> _logger;

    public ScheduledReportsController(
        ApplicationDbContext context,
        ILogger<ScheduledReportsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التقارير المجدولة - Scheduled Reports List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var scheduledReports = await _context.ScheduledReports
            .Include(sr => sr.Template)
            .OrderBy(sr => sr.Name)
            .ToListAsync();

        return View(scheduledReports);
    }

    /// <summary>
    /// إنشاء تقرير مجدول - Create Scheduled Report
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Templates = await _context.ReportTemplates
            .Where(rt => rt.IsActive)
            .ToListAsync();

        return View(new ScheduledReportCreateViewModel());
    }

    /// <summary>
    /// حفظ التقرير المجدول - Save Scheduled Report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScheduledReportCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var scheduledReport = new ScheduledReport
            {
                Name = model.Name,
                Description = model.Description,
                TemplateId = model.TemplateId,
                Schedule = model.Schedule,
                RecipientEmails = model.RecipientEmails,
                IsActive = model.IsActive,
                NextRunDate = CalculateNextRunDate(model.Schedule)
            };

            _context.ScheduledReports.Add(scheduledReport);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء التقرير المجدول بنجاح");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Templates = await _context.ReportTemplates.Where(rt => rt.IsActive).ToListAsync();
        return View(model);
    }

    /// <summary>
    /// تعديل التقرير المجدول - Edit Scheduled Report
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        var viewModel = new ScheduledReportEditViewModel
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            TemplateId = report.TemplateId,
            Schedule = report.Schedule,
            RecipientEmails = report.RecipientEmails,
            IsActive = report.IsActive
        };

        ViewBag.Templates = await _context.ReportTemplates.Where(rt => rt.IsActive).ToListAsync();
        return View(viewModel);
    }

    /// <summary>
    /// حفظ التعديلات - Save Edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScheduledReportEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            report.Name = model.Name;
            report.Description = model.Description;
            report.TemplateId = model.TemplateId;
            report.Schedule = model.Schedule;
            report.RecipientEmails = model.RecipientEmails;
            report.IsActive = model.IsActive;
            report.NextRunDate = CalculateNextRunDate(model.Schedule);

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث التقرير المجدول بنجاح");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Templates = await _context.ReportTemplates.Where(rt => rt.IsActive).ToListAsync();
        return View(model);
    }

    /// <summary>
    /// حذف التقرير المجدول - Delete Scheduled Report
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var report = await _context.ScheduledReports.FindAsync(id);
        if (report == null)
            return NotFound();

        _context.ScheduledReports.Remove(report);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف التقرير المجدول بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تشغيل التقرير الآن - Run Now
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        try
        {
            var report = await _context.ScheduledReports
                .Include(sr => sr.Template)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (report == null)
            {
                SetErrorMessage("التقرير المجدول غير موجود");
                return RedirectToAction(nameof(Index));
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Generate report export
                var reportExport = new Domain.Entities.Analytics.ReportExport
                {
                    ScheduledReportId = report.Id,
                    ReportType = report.Template?.ReportType.ToString() ?? "Scheduled",
                    ReportName = report.Name,
                    Format = "CSV",
                    GeneratedByUserId = User.Identity?.Name,
                    ExportDate = DateTime.UtcNow,
                    Status = "Completed",
                    FileSize = 0,
                    RecordCount = 0
                };

                // Generate simple report content
                string csvContent = $"Scheduled Report: {report.Name}\n";
                csvContent += $"Description: {report.Description}\n";
                csvContent += $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}\n\n";
                csvContent += "Report data would be generated based on template configuration.\n";
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                reportExport.FileSize = bytes.Length;
                reportExport.RecordCount = 1;
                
                _context.ReportExports.Add(reportExport);

                // Update scheduled report
                report.LastRunDate = DateTime.UtcNow;
                report.NextRunDate = CalculateNextRunDate(report.Schedule);

                await _context.SaveChangesAsync();

                // Send email to recipients (if configured)
                if (!string.IsNullOrEmpty(report.RecipientEmails))
                {
                    var recipients = report.RecipientEmails.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    // In production: send actual email with attachment
                    _logger.LogInformation("Would send report to {Count} recipients", recipients.Length);
                }
            });

            _logger.LogInformation("Scheduled report {ReportId} executed successfully", id);

            SetSuccessMessage("تم تشغيل التقرير بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running scheduled report {ReportId}", id);
            SetErrorMessage("حدث خطأ أثناء تشغيل التقرير");
            return RedirectToAction(nameof(Index));
        }
    }

    private DateTime CalculateNextRunDate(string schedule)
    {
        // Simple calculation - can be enhanced
        return schedule.ToLower() switch
        {
            "daily" => DateTime.UtcNow.AddDays(1),
            "weekly" => DateTime.UtcNow.AddDays(7),
            "monthly" => DateTime.UtcNow.AddMonths(1),
            _ => DateTime.UtcNow.AddDays(1)
        };
    }
}

