using LMS.Areas.Instructor.ViewModels;
using LMS.Common;
using LMS.Data;
using LMS.Domain.Entities.Financial;
using LMS.Domain.Entities.Marketing;
using LMS.Domain.Enums;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// أرباح المدرس - Instructor Earnings Controller
/// </summary>
public class EarningsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemConfigurationService _configService;
    private readonly ICurrencyService _currencyService;
    private readonly IExportService _exportService;
    private readonly ILogger<EarningsController> _logger;

    public EarningsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISystemConfigurationService configService,
        ICurrencyService currencyService,
        IExportService exportService,
        ILogger<EarningsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configService = configService;
        _currencyService = currencyService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// لوحة الأرباح - Earnings dashboard
    /// </summary>
    public async Task<IActionResult> Index()
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, null, _logger);
        
        var userId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        var profile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound();

        var earnings = await _context.InstructorEarnings
            .Include(e => e.Enrollment)
                .ThenInclude(e => e.Course)
            .Include(e => e.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(e => e.InstructorId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(await _configService.GetTopItemsLimitAsync("earnings_list", 50))
            .Select(e => new EarningDisplayViewModel
            {
                Id = e.Id,
                CourseName = e.Enrollment.Course.Title,
                StudentName = $"{e.Enrollment.Student.FirstName} {e.Enrollment.Student.LastName}",
                Amount = e.Amount,
                CommissionRate = e.CommissionRate,
                NetAmount = e.NetAmount,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        var summary = new EarningsSummaryViewModel
        {
            TotalEarnings = profile.TotalEarnings,
            AvailableBalance = profile.AvailableBalance,
            PendingBalance = profile.PendingBalance,
            TotalWithdrawn = profile.TotalWithdrawn,
            MinimumWithdrawal = profile.MinimumWithdrawal,
            CommissionRate = profile.CommissionRate
        };

        // Calculate this month's and last month's earnings
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
        
        var monthlyEarnings = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId && e.CreatedAt >= firstDayOfMonth)
            .SumAsync(e => e.NetAmount);
            
        var lastMonthEarnings = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId && 
                       e.CreatedAt >= firstDayOfLastMonth && 
                       e.CreatedAt < firstDayOfMonth)
            .SumAsync(e => e.NetAmount);
        
        var monthlyGrowth = lastMonthEarnings > 0 
            ? ((monthlyEarnings - lastMonthEarnings) / lastMonthEarnings) * 100 
            : (monthlyEarnings > 0 ? 100 : 0);

        ViewBag.Summary = summary;
        ViewBag.Profile = profile;
        ViewBag.AvailableBalance = profile.AvailableBalance;
        ViewBag.PendingBalance = profile.PendingBalance;
        ViewBag.TotalEarnings = profile.TotalEarnings;
        ViewBag.MonthlyEarnings = monthlyEarnings;
        ViewBag.MonthlyGrowth = monthlyGrowth;
        ViewBag.CommissionRate = profile.CommissionRate;
        
        // Calculate breakdown data
        var salesRevenue = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId)
            .SumAsync(e => e.Amount);
        
        var platformFees = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId)
            .SumAsync(e => e.PlatformCommission);

        // Calculate referral earnings from affiliate commissions
        var referralEarnings = await _context.AffiliateCommissions
            .Include(ac => ac.AffiliateLink)
            .Where(ac => ac.AffiliateLink.AffiliateUserId == userId && ac.Status == "Paid")
            .SumAsync(ac => ac.CommissionAmount);
        
        ViewBag.SalesRevenue = salesRevenue;
        ViewBag.PlatformFees = platformFees;
        ViewBag.ReferralEarnings = referralEarnings;
        
        // Chart data - last 6 months earnings
        var chartLabels = new List<string>();
        var chartData = new List<decimal>();
        var arabicMonths = await _configService.GetMonthNamesAsync("ar");
        
        for (int i = Constants.DisplayLimits.MonthlyChartDataPoints - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            
            var monthName = arabicMonths.TryGetValue(monthStart.Month, out var name) ? name : monthStart.ToString("MMMM");
            chartLabels.Add(monthName);
            
            var monthEarnings = await _context.InstructorEarnings
                .Where(e => e.InstructorId == userId && 
                           e.CreatedAt >= monthStart && 
                           e.CreatedAt < monthEnd)
                .SumAsync(e => e.NetAmount);
                
            chartData.Add(monthEarnings);
        }
        
        ViewBag.ChartLabels = chartLabels;
        ViewBag.ChartData = chartData;
        
        // Earnings by course
        var courseEarnings = await _context.InstructorEarnings
            .Where(e => e.InstructorId == userId)
            .GroupBy(e => e.Enrollment.Course.Title)
            .Select(g => new { CourseName = g.Key, Total = g.Sum(e => e.NetAmount) })
            .OrderByDescending(x => x.Total)
            .Take(await _configService.GetTopItemsLimitAsync("analytics_top_courses", Constants.DisplayLimits.TopCoursesOnAnalytics))
            .ToListAsync();
            
        ViewBag.CourseLabels = courseEarnings.Select(x => x.CourseName).ToList();
        ViewBag.CourseEarnings = courseEarnings.Select(x => x.Total).ToList();
        
        // Recent transactions for the table
        var recentTransactionsLimit = await _configService.GetTopItemsLimitAsync("earnings_recent_transactions", Constants.DisplayLimits.RecentTransactionsOnEarnings);
        var transactionDescription = await _configService.GetLocalizationAsync("transaction_course_sale", "ar", "مبيعات دورة");
        ViewBag.RecentTransactions = earnings.Take(recentTransactionsLimit).Select(e => new {
            Id = e.Id,
            Date = e.CreatedAt,
            Description = transactionDescription,
            CourseId = 0,
            CourseName = e.CourseName,
            StudentName = e.StudentName,
            Amount = e.NetAmount,
            Status = "Completed"
        }).ToList();

        return View(earnings);
    }

    /// <summary>
    /// سجل المعاملات - Transactions history
    /// </summary>
    public async Task<IActionResult> Transactions(string? type, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var userId = _currentUserService.UserId;

        // Default date range: last 3 months (configurable)
        var defaultDateRangeMonths = await _configService.GetIntConfigurationAsync("TimePeriods", "last_90_days", Constants.Earnings.DefaultTransactionDateRangeMonths);
        fromDate ??= DateTime.UtcNow.AddMonths(-defaultDateRangeMonths);
        toDate ??= DateTime.UtcNow;

        var profile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound();

        // Get all earnings (incoming)
        var earningsQuery = _context.InstructorEarnings
            .Include(e => e.Enrollment)
                .ThenInclude(e => e.Course)
            .Include(e => e.Enrollment)
                .ThenInclude(e => e.Student)
            .Where(e => e.InstructorId == userId && 
                       e.CreatedAt >= fromDate.Value && 
                       e.CreatedAt <= toDate.Value)
            .AsQueryable();

        // Get all withdrawals (outgoing)
        var withdrawalsQuery = _context.WithdrawalRequests
            .Include(w => w.WithdrawalMethod)
            .Where(w => w.InstructorId == userId && 
                       w.CreatedAt >= fromDate.Value && 
                       w.CreatedAt <= toDate.Value)
            .AsQueryable();

        // Get localization strings (null-coalesce to prevent string.Format ArgumentNullException)
        var transactionCourseSale = await _configService.GetLocalizationAsync("transaction_course_sale", "ar", "مبيعات دورة") ?? "مبيعات دورة";
        var transactionWithdrawalRequest = await _configService.GetLocalizationAsync("transaction_withdrawal_request", "ar", "طلب سحب") ?? "طلب سحب";
        var notSpecified = await _configService.GetLocalizationAsync("not_specified", "ar", "غير محدد") ?? "غير محدد";
        var orderReferenceFormat = await _configService.GetLocalizationAsync("transaction_order_reference", "ar", "ORD-{0}") ?? "ORD-{0}";
        var withdrawalReferenceFormat = await _configService.GetLocalizationAsync("transaction_withdrawal_reference", "ar", "WD-{0}") ?? "WD-{0}";

        // Build combined transactions list
        var transactions = new List<TransactionViewModel>();

        if (string.IsNullOrEmpty(type) || type == "earning")
        {
            var earnings = await earningsQuery.ToListAsync();
            transactions.AddRange(earnings.Select(e => new TransactionViewModel
            {
                Id = e.Id,
                Date = e.CreatedAt,
                Type = "earning",
                Description = $"{transactionCourseSale}: {e.Enrollment?.Course?.Title ?? notSpecified}",
                CourseName = e.Enrollment?.Course?.Title ?? notSpecified,
                StudentName = e.Enrollment?.Student != null 
                    ? $"{e.Enrollment.Student.FirstName} {e.Enrollment.Student.LastName}" 
                    : notSpecified,
                Amount = e.NetAmount,
                GrossAmount = e.Amount,
                Commission = e.PlatformCommission,
                Status = "Completed",
                Reference = string.Format(orderReferenceFormat, e.EnrollmentId ?? 0)
            }));
        }

        if (string.IsNullOrEmpty(type) || type == "withdrawal")
        {
            var withdrawals = await withdrawalsQuery.ToListAsync();
            transactions.AddRange(withdrawals.Select(w => new TransactionViewModel
            {
                Id = w.Id,
                Date = w.CreatedAt,
                Type = "withdrawal",
                Description = $"{transactionWithdrawalRequest} - {w.WithdrawalMethod?.Name ?? notSpecified}",
                Amount = -w.Amount, // Negative for withdrawals
                Status = w.Status.ToString(),
                Reference = string.Format(withdrawalReferenceFormat, w.Id),
                ProcessedDate = w.ProcessedAt,
                Notes = w.AdminNotes
            }));
        }

        // Sort by date descending
        transactions = transactions.OrderByDescending(t => t.Date).ToList();

        // Pagination
        var totalCount = transactions.Count;
        var pageSize = await _configService.GetPaginationSizeAsync("earnings_transactions", 20);
        var paginatedTransactions = transactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Calculate summary for the period
        var totalEarnings = transactions.Where(t => t.Type == "earning").Sum(t => t.Amount);
        var totalWithdrawals = Math.Abs(transactions.Where(t => t.Type == "withdrawal").Sum(t => t.Amount));
        var totalCommission = transactions.Where(t => t.Type == "earning").Sum(t => t.Commission ?? 0);

        ViewBag.Profile = profile;
        ViewBag.TotalEarnings = totalEarnings;
        ViewBag.TotalWithdrawals = totalWithdrawals;
        ViewBag.TotalCommission = totalCommission;
        ViewBag.NetIncome = totalEarnings - totalWithdrawals;
        ViewBag.Type = type;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalCount = totalCount;

        _logger.LogInformation("Transactions history loaded for instructor {InstructorId}. Total: {Count}", userId, totalCount);

        return View(paginatedTransactions);
    }

    /// <summary>
    /// تصدير المعاملات - Export transactions to CSV
    /// </summary>
    public async Task<IActionResult> ExportTransactionsCsv(string? type, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var transactions = await GetTransactionsForExportAsync(type, fromDate, toDate);
            
            var columns = GetTransactionExportColumns();
            var csvData = await _exportService.ExportToCsvAsync(transactions, columns);
            
            var fileName = $"transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            _logger.LogInformation("Exported {Count} transactions to CSV for instructor {InstructorId}", 
                transactions.Count, _currentUserService.UserId);
            
            return _exportService.CreateFileResult(csvData, ExportFormat.Csv, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions to CSV");
            SetErrorMessage("حدث خطأ أثناء تصدير البيانات");
            return RedirectToAction(nameof(Transactions));
        }
    }

    /// <summary>
    /// تصدير المعاملات إلى Excel - Export transactions to Excel
    /// </summary>
    public async Task<IActionResult> ExportTransactionsExcel(string? type, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var transactions = await GetTransactionsForExportAsync(type, fromDate, toDate);
            
            var columns = GetTransactionExportColumns();
            var excelData = await _exportService.ExportToExcelAsync(transactions, columns, "المعاملات");
            
            var fileName = $"transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            _logger.LogInformation("Exported {Count} transactions to Excel for instructor {InstructorId}", 
                transactions.Count, _currentUserService.UserId);
            
            return _exportService.CreateFileResult(excelData, ExportFormat.Excel, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions to Excel");
            SetErrorMessage("حدث خطأ أثناء تصدير البيانات");
            return RedirectToAction(nameof(Transactions));
        }
    }

    /// <summary>
    /// تصدير المعاملات إلى PDF - Export transactions to PDF
    /// </summary>
    public async Task<IActionResult> ExportTransactionsPdf(string? type, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var transactions = await GetTransactionsForExportAsync(type, fromDate, toDate);
            
            var columns = GetTransactionExportColumns();
            var pdfOptions = new PdfExportOptions
            {
                Title = "تقرير المعاملات المالية",
                Subtitle = $"الفترة: {fromDate?.ToString("dd/MM/yyyy") ?? "غير محدد"} - {toDate?.ToString("dd/MM/yyyy") ?? "اليوم"}",
                IsRtl = true,
                IsLandscape = true
            };
            
            var pdfData = await _exportService.ExportToPdfAsync(transactions, columns, pdfOptions);
            
            var fileName = $"transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
            _logger.LogInformation("Exported {Count} transactions to PDF for instructor {InstructorId}", 
                transactions.Count, _currentUserService.UserId);
            
            return _exportService.CreateFileResult(pdfData, ExportFormat.Pdf, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions to PDF");
            SetErrorMessage("حدث خطأ أثناء تصدير البيانات");
            return RedirectToAction(nameof(Transactions));
        }
    }

    /// <summary>
    /// Helper method to get transactions for export
    /// </summary>
    private async Task<List<TransactionViewModel>> GetTransactionsForExportAsync(
        string? type, DateTime? fromDate, DateTime? toDate)
    {
        var userId = _currentUserService.UserId;
        var defaultDateRangeMonths = await _configService.GetIntConfigurationAsync(
            "TimePeriods", "last_90_days", Constants.Earnings.DefaultTransactionDateRangeMonths);
        
        fromDate ??= DateTime.UtcNow.AddMonths(-defaultDateRangeMonths);
        toDate ??= DateTime.UtcNow;

        var transactions = new List<TransactionViewModel>();

        // Get earnings
        if (string.IsNullOrEmpty(type) || type == "earning")
        {
            var earnings = await _context.InstructorEarnings
                .Include(e => e.Enrollment)
                    .ThenInclude(e => e!.Course)
                .Include(e => e.Enrollment)
                    .ThenInclude(e => e!.Student)
                .Where(e => e.InstructorId == userId && 
                           e.CreatedAt >= fromDate.Value && 
                           e.CreatedAt <= toDate.Value)
                .ToListAsync();

            transactions.AddRange(earnings.Select(e => new TransactionViewModel
            {
                Id = e.Id,
                Date = e.CreatedAt,
                Type = "earning",
                Description = $"مبيعات دورة: {e.Enrollment?.Course?.Title ?? "غير محدد"}",
                CourseName = e.Enrollment?.Course?.Title ?? "غير محدد",
                StudentName = e.Enrollment?.Student != null 
                    ? $"{e.Enrollment.Student.FirstName} {e.Enrollment.Student.LastName}" 
                    : "غير محدد",
                Amount = e.NetAmount,
                GrossAmount = e.Amount,
                Commission = e.PlatformCommission,
                Status = "Completed",
                Reference = $"ORD-{e.EnrollmentId ?? 0}"
            }));
        }

        // Get withdrawals
        if (string.IsNullOrEmpty(type) || type == "withdrawal")
        {
            var withdrawals = await _context.WithdrawalRequests
                .Include(w => w.WithdrawalMethod)
                .Where(w => w.InstructorId == userId && 
                           w.CreatedAt >= fromDate.Value && 
                           w.CreatedAt <= toDate.Value)
                .ToListAsync();

            transactions.AddRange(withdrawals.Select(w => new TransactionViewModel
            {
                Id = w.Id,
                Date = w.CreatedAt,
                Type = "withdrawal",
                Description = $"طلب سحب - {w.WithdrawalMethod?.Name ?? "غير محدد"}",
                Amount = -w.Amount,
                Status = w.Status.ToString(),
                Reference = $"WD-{w.Id}",
                ProcessedDate = w.ProcessedAt,
                Notes = w.AdminNotes
            }));
        }

        return transactions.OrderByDescending(t => t.Date).ToList();
    }

    /// <summary>
    /// Get column definitions for transaction export
    /// </summary>
    private static IEnumerable<ExportColumnDefinition> GetTransactionExportColumns()
    {
        return new List<ExportColumnDefinition>
        {
            new() { PropertyName = "Reference", DisplayName = "المرجع", Order = 1 },
            new() { PropertyName = "Date", DisplayName = "التاريخ", Format = "dd/MM/yyyy HH:mm", Order = 2 },
            new() { PropertyName = "Type", DisplayName = "النوع", Order = 3, 
                ValueFormatter = v => v?.ToString() == "earning" ? "أرباح" : "سحب" },
            new() { PropertyName = "Description", DisplayName = "الوصف", Order = 4 },
            new() { PropertyName = "CourseName", DisplayName = "الدورة", Order = 5 },
            new() { PropertyName = "StudentName", DisplayName = "الطالب", Order = 6 },
            new() { PropertyName = "GrossAmount", DisplayName = "المبلغ الإجمالي", Format = "N2", Order = 7 },
            new() { PropertyName = "Commission", DisplayName = "العمولة", Format = "N2", Order = 8 },
            new() { PropertyName = "Amount", DisplayName = "المبلغ الصافي", Format = "N2", Order = 9 },
            new() { PropertyName = "Status", DisplayName = "الحالة", Order = 10,
                ValueFormatter = v => v?.ToString() switch
                {
                    "Completed" => "مكتملة",
                    "Pending" => "قيد المعالجة",
                    "Approved" => "موافق عليها",
                    "Rejected" => "مرفوضة",
                    "Cancelled" => "ملغاة",
                    _ => v?.ToString() ?? ""
                }
            }
        };
    }

}
