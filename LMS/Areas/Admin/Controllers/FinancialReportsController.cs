using LMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// التقارير المالية - Financial Reports Controller
/// Enterprise-level financial analytics dashboard
/// </summary>
public class FinancialReportsController : AdminBaseController
{
    private readonly IFinancialReportsService _reportsService;
    private readonly ILogger<FinancialReportsController> _logger;

    public FinancialReportsController(
        IFinancialReportsService reportsService,
        ILogger<FinancialReportsController> logger)
    {
        _reportsService = reportsService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة التحكم المالية الرئيسية - Main financial dashboard
    /// </summary>
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-12);
        var to = toDate ?? DateTime.UtcNow;

        var dashboard = await _reportsService.GetFinancialDashboardAsync(from, to);
        return View(dashboard);
    }

    /// <summary>
    /// تقرير الإيرادات اليومية - Daily revenue report
    /// </summary>
    public async Task<IActionResult> DailyRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetDailyRevenueReportAsync(from, to);
        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        return View(report);
    }

    /// <summary>
    /// تقرير بوابات الدفع - Gateway performance report
    /// </summary>
    public async Task<IActionResult> GatewayPerformance(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-3);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetGatewayPerformanceReportAsync(from, to);
        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        return View(report);
    }

    /// <summary>
    /// تقرير أرباح المدربين - Instructor earnings report
    /// </summary>
    public async Task<IActionResult> InstructorEarnings(string? instructorId, DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-12);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetInstructorEarningsReportAsync(instructorId, from, to);
        ViewBag.InstructorId = instructorId;
        return View(report);
    }

    /// <summary>
    /// تقرير استخدام الكوبونات - Coupon usage report
    /// </summary>
    public async Task<IActionResult> CouponUsage(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-3);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetCouponUsageReportAsync(from, to);
        return View(report);
    }

    /// <summary>
    /// تقرير الاشتراكات - Subscription report
    /// </summary>
    public async Task<IActionResult> Subscriptions(DateTime? fromDate, DateTime? toDate)
    {
        var report = await _reportsService.GetSubscriptionReportAsync(fromDate, toDate);
        return View(report);
    }

    /// <summary>
    /// تقرير المبالغ المستردة - Refund report
    /// </summary>
    public async Task<IActionResult> Refunds(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-3);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetRefundReportAsync(from, to);
        return View(report);
    }

    /// <summary>
    /// تصدير تقرير الإيرادات - Export revenue report
    /// </summary>
    public async Task<IActionResult> ExportRevenue(DateTime? fromDate, DateTime? toDate, string format = "csv")
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-12);
        var to = toDate ?? DateTime.UtcNow;

        var report = await _reportsService.GetDailyRevenueReportAsync(from, to);

        if (format.ToLower() == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("التاريخ,الإيرادات,عدد المعاملات,العملاء الجدد,متوسط قيمة الطلب,إيرادات الدورات,إيرادات الكتب,إيرادات الاشتراكات");

            foreach (var day in report)
            {
                csv.AppendLine($"{day.Date:yyyy-MM-dd},{day.TotalRevenue:F2},{day.TransactionCount},{day.NewCustomers},{day.AverageOrderValue:F2},{day.CourseRevenue:F2},{day.BookRevenue:F2},{day.SubscriptionRevenue:F2}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"revenue_report_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }

        return RedirectToAction(nameof(DailyRevenue), new { fromDate, toDate });
    }

    /// <summary>
    /// بيانات الرسم البياني - Chart data API
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ChartData(string type, DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddMonths(-12);
        var to = toDate ?? DateTime.UtcNow;

        object? data = type.ToLower() switch
        {
            "revenue" => await _reportsService.GetDailyRevenueReportAsync(from, to),
            "gateway" => await _reportsService.GetGatewayPerformanceReportAsync(from, to),
            _ => null
        };

        if (data == null)
            return NotFound();

        return Json(data);
    }
}

