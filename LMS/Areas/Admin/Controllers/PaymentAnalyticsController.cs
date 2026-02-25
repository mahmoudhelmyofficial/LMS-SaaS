using LMS.Areas.Admin.Controllers;
using LMS.Data;
using LMS.Domain.Enums;
using LMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// لوحة تحليلات المدفوعات - Payment Analytics Dashboard
/// </summary>
public class PaymentAnalyticsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentAnalyticsController> _logger;

    public PaymentAnalyticsController(
        ApplicationDbContext context,
        ILogger<PaymentAnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// لوحة التحكم الرئيسية - Main analytics dashboard
    /// </summary>
    public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var viewModel = new PaymentAnalyticsDashboard
        {
            FromDate = from.Value,
            ToDate = to.Value
        };

        // Revenue metrics
        var payments = await _context.Payments
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .ToListAsync();

        var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();

        viewModel.TotalRevenue = completedPayments.Sum(p => p.TotalAmount);
        viewModel.TotalTransactions = completedPayments.Count;
        viewModel.AverageOrderValue = completedPayments.Any() 
            ? completedPayments.Average(p => p.TotalAmount) 
            : 0;
        viewModel.FailedTransactions = payments.Count(p => p.Status == PaymentStatus.Failed);
        viewModel.PendingTransactions = payments.Count(p => p.Status == PaymentStatus.Pending);
        viewModel.RefundedTransactions = payments.Count(p => p.Status == PaymentStatus.Refunded);
        viewModel.RefundedAmount = payments.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.TotalAmount);

        // Success rate
        var totalAttempts = payments.Count(p => p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Failed);
        viewModel.SuccessRate = totalAttempts > 0 
            ? (decimal)completedPayments.Count / totalAttempts * 100 
            : 100;

        // Revenue by day
        viewModel.RevenueByDay = completedPayments
            .GroupBy(p => p.CompletedAt?.Date ?? p.CreatedAt.Date)
            .Select(g => new DailyRevenue
            {
                Date = g.Key,
                Amount = g.Sum(p => p.TotalAmount),
                TransactionCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Revenue by payment method
        viewModel.RevenueByPaymentMethod = completedPayments
            .GroupBy(p => p.PaymentMethod ?? "Unknown")
            .Select(g => new PaymentMethodStats
            {
                Method = g.Key,
                Amount = g.Sum(p => p.TotalAmount),
                TransactionCount = g.Count(),
                Percentage = viewModel.TotalRevenue > 0 
                    ? g.Sum(p => p.TotalAmount) / viewModel.TotalRevenue * 100 
                    : 0
            })
            .OrderByDescending(m => m.Amount)
            .ToList();

        // Revenue by product type
        viewModel.RevenueByProductType = completedPayments
            .GroupBy(p => p.ProductType)
            .Select(g => new ProductTypeStats
            {
                ProductType = g.Key,
                ProductTypeName = g.Key.ToString(),
                Amount = g.Sum(p => p.TotalAmount),
                TransactionCount = g.Count(),
                Percentage = viewModel.TotalRevenue > 0 
                    ? g.Sum(p => p.TotalAmount) / viewModel.TotalRevenue * 100 
                    : 0
            })
            .OrderByDescending(p => p.Amount)
            .ToList();

        // Revenue by gateway
        viewModel.RevenueByGateway = completedPayments
            .GroupBy(p => p.Provider)
            .Select(g => new GatewayStats
            {
                Gateway = g.Key.ToString(),
                Amount = g.Sum(p => p.TotalAmount),
                TransactionCount = g.Count(),
                SuccessRate = 100, // All completed
                FailedCount = payments.Count(p => p.Provider == g.Key && p.Status == PaymentStatus.Failed)
            })
            .OrderByDescending(g => g.Amount)
            .ToList();

        // Top courses by revenue
        viewModel.TopCoursesByRevenue = await _context.Payments
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && 
                       p.Status == PaymentStatus.Completed && p.CourseId.HasValue)
            .GroupBy(p => new { p.CourseId, p.Course!.Title })
            .Select(g => new TopCourseRevenue
            {
                CourseId = g.Key.CourseId!.Value,
                CourseTitle = g.Key.Title ?? "غير معروف",
                Revenue = g.Sum(p => p.TotalAmount),
                SalesCount = g.Count()
            })
            .OrderByDescending(c => c.Revenue)
            .Take(10)
            .ToListAsync();

        // Coupon usage stats
        var couponPayments = completedPayments.Where(p => !string.IsNullOrEmpty(p.CouponCode)).ToList();
        viewModel.CouponStats = new CouponUsageStats
        {
            TotalCouponUsage = couponPayments.Count,
            TotalDiscountGiven = couponPayments.Sum(p => p.DiscountAmount),
            UniqueCodesUsed = couponPayments.Select(p => p.CouponCode).Distinct().Count(),
            MostUsedCoupon = couponPayments
                .GroupBy(p => p.CouponCode)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault()
        };

        // Retry stats
        var retriedPayments = payments.Where(p => p.RetryCount > 0).ToList();
        viewModel.RetryStats = new RetryStatistics
        {
            TotalRetries = retriedPayments.Sum(p => p.RetryCount ?? 0),
            SuccessfulRetries = retriedPayments.Count(p => p.Status == PaymentStatus.Completed),
            RecoveredRevenue = retriedPayments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.TotalAmount)
        };

        // Recent transactions
        viewModel.RecentTransactions = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .Select(p => new RecentTransaction
            {
                Id = p.Id,
                TransactionId = p.TransactionId,
                CustomerName = p.Student != null ? p.Student.FullName : "غير معروف",
                CustomerEmail = p.Student != null ? p.Student.Email : "",
                Amount = p.TotalAmount,
                Currency = p.Currency,
                Status = p.Status,
                PaymentMethod = p.PaymentMethod,
                ProductName = p.Course != null ? p.Course.Title : (p.Book != null ? p.Book.Title : "غير معروف"),
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return View(viewModel);
    }

    /// <summary>
    /// تصدير التقرير - Export analytics report
    /// </summary>
    public async Task<IActionResult> Export(DateTime? from = null, DateTime? to = null, string format = "csv")
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var payments = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Course)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        if (format.ToLower() == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("رقم العملية,التاريخ,العميل,البريد,المنتج,المبلغ,العملة,طريقة الدفع");

            foreach (var p in payments)
            {
                csv.AppendLine($"{p.TransactionId},{p.CreatedAt:yyyy-MM-dd HH:mm},{p.Student?.FullName},{p.Student?.Email},{p.Course?.Title ?? p.Book?.Title},{p.TotalAmount},{p.Currency},{p.PaymentMethod}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"payment-report-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
        }

        return BadRequest("Unsupported format");
    }

    /// <summary>
    /// API: Get revenue chart data
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRevenueChart(DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;

        var data = await _context.Payments
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.CompletedAt!.Value.Date)
            .Select(g => new
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Revenue = g.Sum(p => p.TotalAmount),
                Transactions = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return Json(data);
    }
}

#region View Models

/// <summary>
/// لوحة تحليلات المدفوعات - Payment analytics dashboard view model
/// </summary>
public class PaymentAnalyticsDashboard
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Summary metrics
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int FailedTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public int RefundedTransactions { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal SuccessRate { get; set; }

    // Breakdown data
    public List<DailyRevenue> RevenueByDay { get; set; } = new();
    public List<PaymentMethodStats> RevenueByPaymentMethod { get; set; } = new();
    public List<ProductTypeStats> RevenueByProductType { get; set; } = new();
    public List<GatewayStats> RevenueByGateway { get; set; } = new();
    public List<TopCourseRevenue> TopCoursesByRevenue { get; set; } = new();
    public List<RecentTransaction> RecentTransactions { get; set; } = new();

    // Additional stats
    public CouponUsageStats CouponStats { get; set; } = new();
    public RetryStatistics RetryStats { get; set; } = new();
}

public class DailyRevenue
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

public class PaymentMethodStats
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; }
}

public class ProductTypeStats
{
    public ProductType ProductType { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; }
}

public class GatewayStats
{
    public string Gateway { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal SuccessRate { get; set; }
    public int FailedCount { get; set; }
}

public class TopCourseRevenue
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int SalesCount { get; set; }
}

public class CouponUsageStats
{
    public int TotalCouponUsage { get; set; }
    public decimal TotalDiscountGiven { get; set; }
    public int UniqueCodesUsed { get; set; }
    public string? MostUsedCoupon { get; set; }
}

public class RetryStatistics
{
    public int TotalRetries { get; set; }
    public int SuccessfulRetries { get; set; }
    public decimal RecoveredRevenue { get; set; }
}

public class RecentTransaction
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ProductName { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion

