using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Certifications;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الشهادات الصادرة - Issued Certificates Management Controller
/// </summary>
public class CertificatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CertificatesController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUserService;

    public CertificatesController(
        ApplicationDbContext context,
        ILogger<CertificatesController> logger,
        IEmailService emailService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// قائمة الشهادات الصادرة - Issued Certificates List
    /// </summary>
    public async Task<IActionResult> Index(CertificateFilterViewModel filter)
    {
        var query = _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Include(c => c.Template)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            query = query.Where(c =>
                c.CertificateNumber.Contains(filter.SearchTerm) ||
                c.StudentName.Contains(filter.SearchTerm) ||
                c.CourseName.Contains(filter.SearchTerm));
        }

        if (filter.CourseId.HasValue)
        {
            query = query.Where(c => c.CourseId == filter.CourseId.Value);
        }

        if (filter.IsRevoked.HasValue)
        {
            query = query.Where(c => c.IsRevoked == filter.IsRevoked.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(c => c.IssuedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(c => c.IssuedAt <= filter.ToDate.Value);
        }

        var certificates = await query
            .OrderByDescending(c => c.IssuedAt)
            .Take(100)
            .ToListAsync();

        ViewBag.Filter = filter;
        ViewBag.Courses = await _context.Courses
            .Where(c => c.HasCertificate)
            .OrderBy(c => c.Title)
            .ToListAsync();

        return View(certificates);
    }

    /// <summary>
    /// تفاصيل الشهادة - Certificate Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var certificate = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Include(c => c.Enrollment)
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (certificate == null)
            return NotFound();

        return View(certificate);
    }

    /// <summary>
    /// إلغاء الشهادة - Revoke Certificate
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Revoke(int id)
    {
        var certificate = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (certificate == null)
            return NotFound();

        if (certificate.IsRevoked)
        {
            SetErrorMessage(CultureExtensions.T("هذه الشهادة ملغاة بالفعل", "This certificate is already revoked."));
            return RedirectToAction(nameof(Details), new { id });
        }

        var viewModel = new CertificateRevokeViewModel
        {
            Id = certificate.Id,
            CertificateNumber = certificate.CertificateNumber,
            StudentName = certificate.StudentName,
            CourseName = certificate.CourseName
        };

        return View(viewModel);
    }

    /// <summary>
    /// تأكيد إلغاء الشهادة - Confirm Revoke Certificate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CertificateRevokeViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var certificate = await _context.Certificates.FindAsync(id);
        if (certificate == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            certificate.IsRevoked = true;
            certificate.RevokedAt = DateTime.UtcNow;
            certificate.RevocationReason = model.RevocationReason;
            certificate.RevokedBy = User.Identity?.Name;

            await _context.SaveChangesAsync();

            SetSuccessMessage(CultureExtensions.T("تم إلغاء الشهادة بنجاح", "Certificate revoked successfully."));
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// استعادة الشهادة - Restore Certificate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var certificate = await _context.Certificates.FindAsync(id);
        if (certificate == null)
            return NotFound();

        if (!certificate.IsRevoked)
        {
            SetErrorMessage(CultureExtensions.T("هذه الشهادة ليست ملغاة", "This certificate is not revoked."));
            return RedirectToAction(nameof(Details), new { id });
        }

        certificate.IsRevoked = false;
        certificate.RevokedAt = null;
        certificate.RevocationReason = null;
        certificate.RevokedBy = null;

        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم استعادة الشهادة بنجاح", "Certificate restored successfully."));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// التحقق من الشهادة - Verify Certificate
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            ViewBag.Message = "الرجاء إدخال رمز التحقق";
            return View();
        }

        var certificate = await _context.Certificates
            .Include(c => c.Student)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.VerificationCode == code);

        if (certificate == null)
        {
            ViewBag.Message = "الشهادة غير موجودة";
            return View();
        }

        if (certificate.IsRevoked)
        {
            ViewBag.Message = "هذه الشهادة ملغاة";
            ViewBag.Certificate = certificate;
            return View();
        }

        ViewBag.Message = "الشهادة صالحة";
        ViewBag.Certificate = certificate;
        return View();
    }

    /// <summary>
    /// إعادة إصدار الشهادة - Regenerate Certificate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(int id)
    {
        try
        {
            var certificate = await _context.Certificates
                .Include(c => c.Student)
                .Include(c => c.Course)
                .Include(c => c.Template)
                .Include(c => c.Enrollment)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (certificate == null)
            {
                _logger.LogWarning("Certificate not found: {CertificateId}", id);
                return NotFound();
            }

            if (certificate.IsRevoked)
            {
                SetErrorMessage(CultureExtensions.T("لا يمكن إعادة إصدار شهادة ملغاة", "Cannot reissue a revoked certificate."));
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                // Generate new verification code
                certificate.VerificationCode = CertificateHelper.GenerateVerificationCode();
                
                // Update certificate data
                certificate.RegeneratedAt = DateTime.UtcNow;
                certificate.RegeneratedBy = _currentUserService.UserId;

                // Generate QR Code URL for verification
                var verificationUrl = Url.Action("Verify", "Certificates", 
                    new { code = certificate.VerificationCode }, Request.Scheme);
                certificate.QrCodeUrl = CertificateHelper.GenerateQRCodeDataUrl(verificationUrl!);

                // In production, you would:
                // 1. Generate PDF using template engine (e.g., QuestPDF, DinkToPdf)
                // 2. Upload to file storage
                // 3. Update PdfUrl
                // For now, we'll mark it as regenerated

                await _context.SaveChangesAsync();

                // Send email with new certificate
                if (certificate.Student?.Email != null)
                {
                    await _emailService.SendCourseCompletionEmailAsync(
                        certificate.Student.Email,
                        certificate.CourseName,
                        Url.Action("View", "Certificates", new { area = "Student", id }, Request.Scheme)!);
                }
            });

            _logger.LogInformation("Certificate {CertificateId} regenerated by admin {AdminId}", 
                id, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم إعادة إصدار الشهادة بنجاح وإرسالها للطالب", "Certificate reissued successfully and sent to the student."));
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating certificate {CertificateId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إعادة إصدار الشهادة", "An error occurred while reissuing the certificate."));
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إصدار شهادة يدوياً - Issue certificate manually
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueManually(int enrollmentId)
    {
        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null)
            {
                _logger.LogWarning("Enrollment not found: {EnrollmentId}", enrollmentId);
                return NotFound();
            }

            // Check if course has certificate enabled
            if (!enrollment.Course.HasCertificate)
            {
                SetErrorMessage(CultureExtensions.T("هذه الدورة لا تصدر شهادات", "This course does not issue certificates."));
                return RedirectToAction("Details", "Courses", new { id = enrollment.CourseId });
            }

            // Check if certificate already exists
            var existingCertificate = await _context.Certificates
                .AnyAsync(c => c.EnrollmentId == enrollmentId);

            if (existingCertificate)
            {
                SetWarningMessage(CultureExtensions.T("الشهادة موجودة بالفعل لهذا التسجيل", "Certificate already exists for this enrollment."));
                return RedirectToAction("Index");
            }

            Certificate? certificate = null;

            await _context.ExecuteInTransactionAsync(async () =>
            {
                certificate = new Certificate
                {
                    CertificateNumber = CertificateHelper.GenerateCertificateNumber(),
                    StudentId = enrollment.StudentId,
                    StudentName = $"{enrollment.Student.FirstName} {enrollment.Student.LastName}",
                    CourseId = enrollment.CourseId,
                    CourseName = enrollment.Course.Title,
                    InstructorName = $"{enrollment.Course.Instructor.FirstName} {enrollment.Course.Instructor.LastName}",
                    EnrollmentId = enrollmentId,
                    TemplateId = (await _context.CertificateTemplates
                        .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive))?.Id
                        ?? (await _context.CertificateTemplates
                        .FirstOrDefaultAsync(t => t.IsActive))?.Id
                        ?? 1,
                    IssuedAt = DateTime.UtcNow,
                    CompletionDate = DateTime.UtcNow,
                    Grade = enrollment.FinalGrade,
                    VerificationCode = CertificateHelper.GenerateVerificationCode(),
                    IssuedBy = _currentUserService.UserId,
                    IsRevoked = false
                };

                // Generate QR Code
                var verificationUrl = Url.Action("Verify", "Certificates", 
                    new { code = certificate.VerificationCode }, Request.Scheme);
                certificate.QrCodeUrl = CertificateHelper.GenerateQRCodeDataUrl(verificationUrl!);

                _context.Certificates.Add(certificate);
                
                // Update enrollment
                enrollment.Status = Domain.Enums.EnrollmentStatus.Completed;
                enrollment.CompletedAt = DateTime.UtcNow;
                enrollment.CertificateIssued = true;
                enrollment.CertificateIssuedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Now that certificate has its ID, set it on enrollment
                enrollment.CertificateId = certificate.Id;
                await _context.SaveChangesAsync();

                // Send email
                if (enrollment.Student?.Email != null)
                {
                    await _emailService.SendCourseCompletionEmailAsync(
                        enrollment.Student.Email,
                        enrollment.Course.Title,
                        Url.Action("View", "Certificates", new { area = "Student", id = certificate.Id }, Request.Scheme)!);
                }
            });

            _logger.LogInformation("Certificate manually issued for enrollment {EnrollmentId} by admin {AdminId}", 
                enrollmentId, _currentUserService.UserId);

            SetSuccessMessage(CultureExtensions.T("تم إصدار الشهادة بنجاح وإرسالها للطالب", "Certificate issued successfully and sent to the student."));
            return RedirectToAction(nameof(Details), new { id = certificate!.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing certificate for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إصدار الشهادة", "An error occurred while issuing the certificate."));
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// إحصائيات الشهادات - Certificate statistics
    /// </summary>
    public async Task<IActionResult> Statistics(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var certificatesQuery = _context.Certificates
                .Where(c => c.IssuedAt >= fromDate && c.IssuedAt <= toDate);

            var stats = new CertificateStatisticsViewModel
            {
                TotalCertificates = await certificatesQuery.CountAsync(),
                RevokedCertificates = await certificatesQuery.CountAsync(c => c.IsRevoked),
                ActiveCertificates = await certificatesQuery.CountAsync(c => !c.IsRevoked),
                CertificatesByCourse = await certificatesQuery
                    .GroupBy(c => new { c.CourseId, c.CourseName })
                    .Select(g => new CourseCertificateCount
                    { 
                        CourseId = g.Key.CourseId, 
                        CourseName = g.Key.CourseName ?? "", 
                        Count = g.Count() 
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync(),
                CertificatesByDay = await certificatesQuery
                    .GroupBy(c => c.IssuedAt.Date)
                    .Select(g => new DailyCertificateCount { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync(),
                VerificationAttempts = await _context.Certificates
                    .SumAsync(c => (int?)c.VerificationCount) ?? 0,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading certificate statistics");
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء تحميل الإحصائيات", "An error occurred while loading statistics."));
            return RedirectToAction(nameof(Index));
        }
    }

    #region Helper Methods

    // Certificate number, verification code, and QR code generation
    // are now centralized in CertificateHelper (LMS.Services.CertificateHelper)

    #endregion

    /// <summary>
    /// حذف الشهادة - Delete Certificate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var certificate = await _context.Certificates.FindAsync(id);
        if (certificate == null)
            return NotFound();

        _context.Certificates.Remove(certificate);
        await _context.SaveChangesAsync();

        SetSuccessMessage(CultureExtensions.T("تم حذف الشهادة بنجاح", "Certificate deleted successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// إنشاء/تصميم شهادة جديدة - Create/Design New Certificate (redirects to templates)
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        // Certificate designs are managed via templates
        return RedirectToAction("Create", "CertificateTemplates");
    }
}

