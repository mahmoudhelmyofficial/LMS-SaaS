using LMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// تصدير التقارير - Report Exports Controller
/// </summary>
public class ReportExportsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportExportsController> _logger;

    public ReportExportsController(
        ApplicationDbContext context,
        ILogger<ReportExportsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// قائمة التصديرات - Exports List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var exports = await _context.ReportExports
            .Include(re => re.GeneratedBy)
            .OrderByDescending(re => re.ExportDate)
            .Take(100)
            .ToListAsync();

        return View(exports);
    }

    /// <summary>
    /// تصدير المستخدمين - Export Users
    /// </summary>
    public async Task<IActionResult> ExportUsers(string format = "csv")
    {
        try
        {
            var users = await _context.Users
                .OrderBy(u => u.Email)
                .ToListAsync();

            // Generate CSV
            var csv = "ID,Email,First Name,Last Name,Phone Number,Created At,Email Confirmed,Is Active\n";
            foreach (var user in users)
            {
                csv += $"{user.Id}," +
                       $"\"{user.Email}\"," +
                       $"\"{user.FirstName}\"," +
                       $"\"{user.LastName}\"," +
                       $"\"{user.PhoneNumber ?? "N/A"}\"," +
                       $"{user.CreatedAt:yyyy-MM-dd HH:mm}," +
                       $"{user.EmailConfirmed}," +
                       $"{!user.IsDeleted}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"users-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Users export generated. Total: {Count} users", users.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting users");
            SetErrorMessage("حدث خطأ أثناء تصدير المستخدمين");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير الدورات - Export Courses
    /// </summary>
    public async Task<IActionResult> ExportCourses(string format = "csv")
    {
        try
        {
            var courses = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Category)
                .OrderBy(c => c.Title)
                .ToListAsync();

            // Generate CSV
            var csv = "ID,Title,Instructor,Category,Status,Price,Currency,Students,Rating,Created At\n";
            foreach (var course in courses)
            {
                csv += $"{course.Id}," +
                       $"\"{course.Title}\"," +
                       $"\"{course.Instructor?.FirstName} {course.Instructor?.LastName}\"," +
                       $"\"{course.Category?.Name ?? "N/A"}\"," +
                       $"{course.Status}," +
                       $"{course.Price}," +
                       $"{course.Currency}," +
                       $"{course.TotalStudents}," +
                       $"{course.AverageRating:F2}," +
                       $"{course.CreatedAt:yyyy-MM-dd HH:mm}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"courses-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Courses export generated. Total: {Count} courses", courses.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting courses");
            SetErrorMessage("حدث خطأ أثناء تصدير الدورات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير التسجيلات - Export Enrollments
    /// </summary>
    public async Task<IActionResult> ExportEnrollments(DateTime? fromDate, DateTime? toDate, string format = "csv")
    {
        try
        {
            var query = _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(e => e.EnrolledAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(e => e.EnrolledAt <= toDate.Value);
            }

            var enrollments = await query.OrderByDescending(e => e.EnrolledAt).ToListAsync();

            // Generate CSV
            var csv = "ID,Student Name,Student Email,Course,Status,Enrolled At,Progress,Paid Amount,Is Free\n";
            foreach (var enrollment in enrollments)
            {
                csv += $"{enrollment.Id}," +
                       $"\"{enrollment.Student?.FirstName} {enrollment.Student?.LastName}\"," +
                       $"\"{enrollment.Student?.Email}\"," +
                       $"\"{enrollment.Course?.Title}\"," +
                       $"{enrollment.Status}," +
                       $"{enrollment.EnrolledAt:yyyy-MM-dd HH:mm}," +
                       $"{enrollment.ProgressPercentage}%," +
                       $"{enrollment.PaidAmount}," +
                       $"{enrollment.IsFree}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"enrollments-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Enrollments export generated. Total: {Count} enrollments", enrollments.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting enrollments");
            SetErrorMessage("حدث خطأ أثناء تصدير التسجيلات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير المدفوعات - Export Payments
    /// </summary>
    public async Task<IActionResult> ExportPayments(DateTime? fromDate, DateTime? toDate, string format = "csv")
    {
        try
        {
            var query = _context.Payments
                .Include(p => p.Student)
                .Include(p => p.Course)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate <= toDate.Value);
            }

            var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

            // Generate CSV
            var csv = "ID,Transaction ID,Student Name,Student Email,Course,Status,Original Amount,Discount,Tax,Total Amount,Currency,Payment Date\n";
            foreach (var payment in payments)
            {
                csv += $"{payment.Id}," +
                       $"\"{payment.TransactionId}\"," +
                       $"\"{payment.Student?.FirstName} {payment.Student?.LastName}\"," +
                       $"\"{payment.Student?.Email}\"," +
                       $"\"{payment.Course?.Title ?? "N/A"}\"," +
                       $"{payment.Status}," +
                       $"{payment.OriginalAmount}," +
                       $"{payment.DiscountAmount}," +
                       $"{payment.TaxAmount}," +
                       $"{payment.TotalAmount}," +
                       $"{payment.Currency}," +
                       $"{payment.PaymentDate:yyyy-MM-dd HH:mm}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"payments-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Payments export generated. Total: {Count} payments", payments.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting payments");
            SetErrorMessage("حدث خطأ أثناء تصدير المدفوعات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير التقييمات - Export Reviews
    /// </summary>
    public async Task<IActionResult> ExportReviews(int? courseId, string format = "csv")
    {
        try
        {
            var query = _context.Reviews
                .Include(r => r.Student)
                .Include(r => r.Course)
                .AsQueryable();

            if (courseId.HasValue)
            {
                query = query.Where(r => r.CourseId == courseId.Value);
            }

            var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            // Generate CSV
            var csv = "ID,Student Name,Course,Rating,Comment,Created At,Is Approved\n";
            foreach (var review in reviews)
            {
                var comment = review.Comment?.Replace("\"", "\"\"").Replace("\n", " ") ?? "";
                csv += $"{review.Id}," +
                       $"\"{review.Student?.FirstName} {review.Student?.LastName}\"," +
                       $"\"{review.Course?.Title}\"," +
                       $"{review.Rating}," +
                       $"\"{comment}\"," +
                       $"{review.CreatedAt:yyyy-MM-dd HH:mm}," +
                       $"{review.IsApproved}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"reviews-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            _logger.LogInformation("Reviews export generated. Total: {Count} reviews", reviews.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting reviews");
            SetErrorMessage("حدث خطأ أثناء تصدير التقييمات");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تصدير مخصص - Custom Export
    /// </summary>
    [HttpGet]
    public IActionResult CustomExport()
    {
        return View();
    }

    /// <summary>
    /// تصدير مخصص - Custom Export (Post)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomExport(string reportType, string format, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            return reportType?.ToLower() switch
            {
                "users" => await ExportUsers(format),
                "courses" => await ExportCourses(format),
                "enrollments" => await ExportEnrollments(fromDate, toDate, format),
                "payments" => await ExportPayments(fromDate, toDate, format),
                "reviews" => await ExportReviews(null, format),
                _ => throw new ArgumentException("Invalid report type")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in custom export. Type: {ReportType}", reportType);
            SetErrorMessage("حدث خطأ أثناء التصدير المخصص");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تحميل التصدير - Download Export
    /// </summary>
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var export = await _context.ReportExports.FindAsync(id);
            if (export == null)
            {
                SetErrorMessage("التصدير غير موجود");
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(export.FileUrl))
            {
                SetErrorMessage("ملف التصدير غير متوفر");
                return RedirectToAction(nameof(Index));
            }

            // Check if file exists (for now, redirect to the file URL)
            // In production, you would read the file from storage and return it
            if (export.FileUrl.StartsWith("http"))
            {
                return Redirect(export.FileUrl);
            }

            // If it's a local file path
            var filePath = Path.Combine("wwwroot", export.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(bytes, "application/octet-stream", Path.GetFileName(filePath));
            }

            SetErrorMessage("الملف غير موجود على الخادم");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading export {ExportId}", id);
            SetErrorMessage("حدث خطأ أثناء تحميل الملف");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حذف التصدير - Delete Export
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var export = await _context.ReportExports.FindAsync(id);
            if (export == null)
            {
                SetErrorMessage("التصدير غير موجود");
                return RedirectToAction(nameof(Index));
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
                        _logger.LogInformation("Deleted export file: {FilePath}", filePath);
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
            SetSuccessMessage("تم حذف التصدير بنجاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting export {ExportId}", id);
            SetErrorMessage("حدث خطأ أثناء حذف التصدير");
            return RedirectToAction(nameof(Index));
        }
    }
}

