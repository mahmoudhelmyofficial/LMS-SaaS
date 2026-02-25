using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// لوحة التحكم المالية - Financial Dashboard
/// Enterprise-level real-time financial analytics and metrics
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class FinancialDashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrencyService _currencyService;
    private readonly IDunningService _dunningService;
    private readonly ILogger<FinancialDashboardController> _logger;

    public FinancialDashboardController(
        ApplicationDbContext context,
        ICurrencyService currencyService,
        IDunningService dunningService,
        ILogger<FinancialDashboardController> logger)
    {
        _context = context;
        _currencyService = currencyService;
        _dunningService = dunningService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var thisYearStart = new DateTime(now.Year, 1, 1);

        // Key Metrics
        var metrics = new FinancialMetrics
        {
            // Today's Revenue
            TodayRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= today)
                .SumAsync(p => p.TotalAmount),
            TodayTransactions = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= today),

            // This Month
            MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart)
                .SumAsync(p => p.TotalAmount),
            MonthlyTransactions = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart),

            // Last Month (for comparison)
            LastMonthRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && 
                           p.CompletedAt >= lastMonthStart && 
                           p.CompletedAt < thisMonthStart)
                .SumAsync(p => p.TotalAmount),

            // Year to Date
            YtdRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisYearStart)
                .SumAsync(p => p.TotalAmount),

            // Active Subscriptions & MRR
            ActiveSubscriptions = await _context.Subscriptions
                .CountAsync(s => s.Status == "Active" || s.Status == "Trialing"),
            MRR = await CalculateMRRAsync(),

            // Pending & At Risk
            PendingPayments = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Pending),
            PendingAmount = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Pending)
                .SumAsync(p => p.TotalAmount),
            FailedPayments = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Failed),

            // Refunds
            RefundedAmount = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Refunded && p.RefundedAt >= thisMonthStart)
                .SumAsync(p => p.TotalAmount),
            RefundCount = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Refunded && p.RefundedAt >= thisMonthStart),

            // Disputes
            OpenDisputes = await _context.PaymentDisputes
                .CountAsync(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.NeedsResponse),
            DisputeAmountAtRisk = await _context.PaymentDisputes
                .Where(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.NeedsResponse)
                .SumAsync(d => d.DisputedAmount),

            // Instructor Payouts
            PendingInstructorPayouts = await _context.WithdrawalRequests
                .CountAsync(w => w.Status == WithdrawalStatus.Pending),
            PendingPayoutAmount = await _context.WithdrawalRequests
                .Where(w => w.Status == WithdrawalStatus.Pending)
                .SumAsync(w => w.NetAmount),

            // Coupons & Discounts
            DiscountsThisMonth = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart)
                .SumAsync(p => p.DiscountAmount),

            // Average Order Value
            AverageOrderValue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart)
                .AverageAsync(p => (decimal?)p.TotalAmount) ?? 0
        };

        // Calculate growth rates
        metrics.MonthlyGrowth = metrics.LastMonthRevenue > 0
            ? ((metrics.MonthlyRevenue - metrics.LastMonthRevenue) / metrics.LastMonthRevenue) * 100
            : 0;

        // Revenue by product type
        var revenueByProduct = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart)
            .GroupBy(p => p.ProductType)
            .Select(g => new { ProductType = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .ToListAsync();

        // Revenue by payment method
        var revenueByGateway = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= thisMonthStart)
            .GroupBy(p => p.Provider)
            .Select(g => new { Gateway = g.Key, Total = g.Sum(p => p.TotalAmount), Count = g.Count() })
            .ToListAsync();

        // Daily revenue for chart (last 30 days)
        var dailyRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= today.AddDays(-30))
            .GroupBy(p => p.CompletedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Top selling courses
        var topCourses = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && 
                       p.CompletedAt >= thisMonthStart && 
                       p.CourseId != null)
            .GroupBy(p => new { p.CourseId, p.Course!.Title })
            .Select(g => new { g.Key.Title, Revenue = g.Sum(p => p.TotalAmount), Sales = g.Count() })
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToListAsync();

        // Dunning stats
        var dunningStats = await _dunningService.GetDunningStatsAsync();

        // Assign to ViewBag
        ViewBag.Metrics = metrics;
        ViewBag.RevenueByProduct = revenueByProduct;
        ViewBag.RevenueByGateway = revenueByGateway;
        ViewBag.DailyRevenue = dailyRevenue;
        ViewBag.TopCourses = topCourses;
        ViewBag.DunningStats = dunningStats;
        ViewBag.ChartDates = dailyRevenue.Select(x => x.Date.ToString("MM/dd")).ToList();
        ViewBag.ChartData = dailyRevenue.Select(x => x.Total).ToList();

        return View();
    }

    /// <summary>
    /// API endpoint for real-time stats refresh
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRealTimeStats()
    {
        var today = DateTime.UtcNow.Date;
        var stats = new
        {
            todayRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= today)
                .SumAsync(p => p.TotalAmount),
            todayTransactions = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= today),
            pendingPayments = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Pending),
            failedPayments = await _context.Payments
                .CountAsync(p => p.Status == PaymentStatus.Failed),
            activeSubscriptions = await _context.Subscriptions
                .CountAsync(s => s.Status == "Active"),
            openDisputes = await _context.PaymentDisputes
                .CountAsync(d => d.Status == DisputeStatus.NeedsResponse)
        };

        return Json(stats);
    }

    /// <summary>
    /// تفاصيل الإيرادات حسب الفترة - Revenue details by period
    /// </summary>
    public async Task<IActionResult> RevenueDetails(string period = "month")
    {
        var (startDate, endDate) = GetDateRange(period);

        var payments = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Book)
            .Include(p => p.Student)
            .Where(p => p.Status == PaymentStatus.Completed &&
                       p.CompletedAt >= startDate &&
                       p.CompletedAt <= endDate)
            .OrderByDescending(p => p.CompletedAt)
            .ToListAsync();

        ViewBag.Period = period;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.TotalRevenue = payments.Sum(p => p.TotalAmount);
        ViewBag.TotalTransactions = payments.Count;

        return View(payments);
    }

    /// <summary>
    /// تقرير المعاملات الفاشلة - Failed transactions report
    /// </summary>
    public async Task<IActionResult> FailedPaymentsReport()
    {
        var failedPayments = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .Where(p => p.Status == PaymentStatus.Failed)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .ToListAsync();

        var dunningStats = await _dunningService.GetDunningStatsAsync();
        ViewBag.DunningStats = dunningStats;

        return View(failedPayments);
    }

    /// <summary>
    /// تقرير الاشتراكات - Subscriptions report
    /// </summary>
    public async Task<IActionResult> SubscriptionsReport()
    {
        var activeCount = await _context.Subscriptions.CountAsync(s => s.Status == "Active");
        var trialingCount = await _context.Subscriptions.CountAsync(s => s.Status == "Trialing");
        var cancelledCount = await _context.Subscriptions.CountAsync(s => s.Status == "Cancelled");
        var pastDueCount = await _context.Subscriptions.CountAsync(s => s.Status == "PastDue");

        var mrr = await CalculateMRRAsync();
        var churnRate = await CalculateChurnRateAsync();

        var recentSubscriptions = await _context.Subscriptions
            .Include(s => s.User)
            .Include(s => s.Plan)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.ActiveCount = activeCount;
        ViewBag.TrialingCount = trialingCount;
        ViewBag.CancelledCount = cancelledCount;
        ViewBag.PastDueCount = pastDueCount;
        ViewBag.MRR = mrr;
        ViewBag.ChurnRate = churnRate;

        return View(recentSubscriptions);
    }

    /// <summary>
    /// تصدير التقرير المالي - Export financial report
    /// </summary>
    public async Task<IActionResult> ExportReport(string period = "month", string format = "csv")
    {
        var (startDate, endDate) = GetDateRange(period);

        var payments = await _context.Payments
            .Include(p => p.Course)
            .Include(p => p.Book)
            .Include(p => p.Student)
            .Where(p => p.Status == PaymentStatus.Completed &&
                       p.CompletedAt >= startDate &&
                       p.CompletedAt <= endDate)
            .OrderByDescending(p => p.CompletedAt)
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("رقم المعاملة,التاريخ,العميل,المنتج,النوع,المبلغ الأصلي,الخصم,الضريبة,الإجمالي,العملة,بوابة الدفع");

        foreach (var p in payments)
        {
            var productName = p.Course?.Title ?? p.Book?.Title ?? "-";
            csv.AppendLine($"{p.TransactionId},{p.CompletedAt:yyyy-MM-dd HH:mm},{p.Student?.FullName},{productName},{p.ProductType},{p.OriginalAmount},{p.DiscountAmount},{p.TaxAmount},{p.TotalAmount},{p.Currency},{p.Provider}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"financial_report_{period}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    #region Private Methods

    private async Task<decimal> CalculateMRRAsync()
    {
        var activeSubscriptions = await _context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.Status == "Active")
            .ToListAsync();

        decimal mrr = 0;
        foreach (var sub in activeSubscriptions)
        {
            if (sub.Plan == null) continue;

            var monthlyAmount = sub.Plan.BillingInterval?.ToLower() switch
            {
                "yearly" or "annual" => sub.Plan.Price / 12,
                "quarterly" => sub.Plan.Price / 3,
                _ => sub.Plan.Price
            };
            mrr += monthlyAmount;
        }

        return mrr;
    }

    private async Task<decimal> CalculateChurnRateAsync()
    {
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        var startOfMonthActive = await _context.Subscriptions
            .CountAsync(s => s.CreatedAt < thisMonth && s.Status == "Active");

        var cancelledThisMonth = await _context.Subscriptions
            .CountAsync(s => s.CancelledAt >= thisMonth && s.CancelledAt < thisMonth.AddMonths(1));

        if (startOfMonthActive == 0) return 0;

        return (decimal)cancelledThisMonth / startOfMonthActive * 100;
    }

    private (DateTime Start, DateTime End) GetDateRange(string period)
    {
        var now = DateTime.UtcNow;
        return period.ToLower() switch
        {
            "today" => (now.Date, now),
            "week" => (now.Date.AddDays(-7), now),
            "month" => (new DateTime(now.Year, now.Month, 1), now),
            "quarter" => (new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), now),
            "year" => (new DateTime(now.Year, 1, 1), now),
            _ => (new DateTime(now.Year, now.Month, 1), now)
        };
    }

    #endregion
}

/// <summary>
/// مقاييس مالية - Financial metrics model
/// </summary>
public class FinancialMetrics
{
    // Today
    public decimal TodayRevenue { get; set; }
    public int TodayTransactions { get; set; }

    // Monthly
    public decimal MonthlyRevenue { get; set; }
    public int MonthlyTransactions { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal MonthlyGrowth { get; set; }

    // YTD
    public decimal YtdRevenue { get; set; }

    // Subscriptions
    public int ActiveSubscriptions { get; set; }
    public decimal MRR { get; set; }

    // Pending & Failed
    public int PendingPayments { get; set; }
    public decimal PendingAmount { get; set; }
    public int FailedPayments { get; set; }

    // Refunds
    public decimal RefundedAmount { get; set; }
    public int RefundCount { get; set; }

    // Disputes
    public int OpenDisputes { get; set; }
    public decimal DisputeAmountAtRisk { get; set; }

    // Instructor Payouts
    public int PendingInstructorPayouts { get; set; }
    public decimal PendingPayoutAmount { get; set; }

    // Discounts
    public decimal DiscountsThisMonth { get; set; }

    // AOV
    public decimal AverageOrderValue { get; set; }
}

