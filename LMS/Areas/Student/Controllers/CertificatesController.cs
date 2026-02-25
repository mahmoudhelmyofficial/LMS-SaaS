using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// شهادات الطالب - Student Certificates Controller
/// </summary>
public class CertificatesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfGenerationService _pdfService;
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPdfGenerationService pdfService,
        ILogger<CertificatesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// شهاداتي - My certificates
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            // First, try a simple query without complex includes to ensure the table exists
            var certificateCount = await _context.Certificates
                .CountAsync(c => c.StudentId == userId);

            // If that works, proceed with the full query
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                    .ThenInclude(c => c!.Instructor)
                .Include(c => c.Student)
                .Where(c => c.StudentId == userId)
                .OrderByDescending(c => c.IssuedAt)
                .AsNoTracking()
                .ToListAsync();

            // Try to load templates separately to avoid query issues
            foreach (var cert in certificates)
            {
                if (cert.TemplateId > 0)
                {
                    cert.Template = await _context.CertificateTemplates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == cert.TemplateId);
                }
            }

            return View(certificates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading certificates for user {UserId}: {Message}", userId, ex.Message);
            
            // Return an empty list without error message for cleaner UX
            // The view will show "no certificates" message which is acceptable
            return View(new List<Domain.Entities.Certifications.Certificate>());
        }
    }

    /// <summary>
    /// عرض الشهادة - View certificate details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var certificate = await _context.Certificates
            .Include(c => c.Course)
                .ThenInclude(c => c.Instructor)
            .Include(c => c.Template)
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.Id == id && c.StudentId == userId);

        if (certificate == null)
        {
            SetErrorMessage("الشهادة غير موجودة");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.IsRevoked = certificate.IsRevoked;

        // Track view count
        certificate.ViewCount++;
        await _context.SaveChangesAsync();

        return View(certificate);
    }

    /// <summary>
    /// عرض الشهادة (بديل) - View certificate (alternative route supporting enrollmentId)
    /// Supports both certificate id and enrollment id for flexibility
    /// </summary>
    public async Task<IActionResult> View(int? id, int? enrollmentId)
    {
        var userId = _currentUserService.UserId;

        Domain.Entities.Certifications.Certificate? certificate = null;

        if (id.HasValue)
        {
            certificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.Id == id.Value && c.StudentId == userId);
        }
        else if (enrollmentId.HasValue)
        {
            certificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId.Value && c.StudentId == userId);
        }

        if (certificate == null)
        {
            SetErrorMessage("الشهادة غير موجودة");
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Details), new { id = certificate.Id });
    }

    /// <summary>
    /// تحميل الشهادة كملف PDF - Download certificate as PDF
    /// Supports both certificate id and enrollment id
    /// </summary>
    public async Task<IActionResult> Download(int? id, int? enrollmentId)
    {
        var userId = _currentUserService.UserId;

        Domain.Entities.Certifications.Certificate? certificate = null;

        if (id.HasValue)
        {
            certificate = await _context.Certificates
                .Include(c => c.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(c => c.Template)
                .Include(c => c.Student)
                .FirstOrDefaultAsync(c => c.Id == id.Value && c.StudentId == userId);
        }
        else if (enrollmentId.HasValue)
        {
            certificate = await _context.Certificates
                .Include(c => c.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(c => c.Template)
                .Include(c => c.Student)
                .FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId.Value && c.StudentId == userId);
        }

        if (certificate == null)
            return NotFound();

        if (certificate.IsRevoked)
        {
            SetErrorMessage("لا يمكن تحميل شهادة ملغاة");
            return RedirectToAction(nameof(Details), new { id = certificate.Id });
        }

        try
        {
            // Generate PDF
            var pdfBytes = _pdfService.GenerateCertificatePdf(certificate);

            // Track download
            certificate.DownloadCount++;
            certificate.DownloadedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            var studentLastName = certificate.Student?.LastName ?? "Student";
            var fileName = $"Certificate_{certificate.CertificateNumber}_{studentLastName}.pdf";
            
            _logger.LogInformation("Certificate PDF generated for certificate {CertificateId} by student {StudentId}", 
                id, userId);
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate PDF for certificate {CertificateId}", id);
            SetErrorMessage("حدث خطأ أثناء إنشاء ملف PDF");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// التحقق من الشهادة (عام) - Verify certificate (public)
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Verify(string code)
    {
        var certificate = await _context.Certificates
            .Include(c => c.Course)
                .ThenInclude(c => c.Instructor)
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.VerificationCode == code || c.CertificateNumber == code);

        if (certificate == null)
        {
            ViewBag.Error = "لم يتم العثور على الشهادة";
            return View("VerifyResult");
        }

        return View("VerifyResult", certificate);
    }

    /// <summary>
    /// مشاركة الشهادة - Share certificate
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(int id, string platform)
    {
        var userId = _currentUserService.UserId;

        var certificate = await _context.Certificates
            .FirstOrDefaultAsync(c => c.Id == id && c.StudentId == userId);

        if (certificate == null)
            return NotFound();

        var share = new Domain.Entities.Certifications.CertificateShare
        {
            CertificateId = id,
            Platform = platform,
            SharedAt = DateTime.UtcNow
        };

        _context.CertificateShares.Add(share);
        certificate.ShareCount++;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// طلب إصدار شهادة - Request certificate generation for completed course
    /// Recovery mechanism for enrollments that completed before auto-generation was fixed
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCertificate(int enrollmentId)
    {
        var userId = _currentUserService.UserId;
        
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorMessage("يرجى تسجيل الدخول أولاً");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == userId);

            if (enrollment == null)
            {
                SetErrorMessage("التسجيل غير موجود");
                return RedirectToAction("CompletedCourses", "Courses");
            }

            if (enrollment.Status != LMS.Domain.Enums.EnrollmentStatus.Completed)
            {
                SetErrorMessage("يجب إكمال الدورة أولاً للحصول على الشهادة");
                return RedirectToAction("Details", "Courses", new { id = enrollment.CourseId });
            }

            if (!enrollment.Course.HasCertificate)
            {
                SetErrorMessage("هذه الدورة لا تصدر شهادات");
                return RedirectToAction("CompletedCourses", "Courses");
            }

            // Check for existing certificate (idempotency)
            var existingCertificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.StudentId == userId && c.CourseId == enrollment.CourseId);

            if (existingCertificate != null)
            {
                return RedirectToAction(nameof(Details), new { id = existingCertificate.Id });
            }

            // Fetch default template
            var template = await _context.CertificateTemplates
                .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive)
                ?? await _context.CertificateTemplates
                    .FirstOrDefaultAsync(t => t.IsActive);

            var studentName = ((enrollment.Student?.FirstName ?? "") + " " + (enrollment.Student?.LastName ?? "")).Trim();
            var instructorName = ((enrollment.Course.Instructor?.FirstName ?? "") + " " + (enrollment.Course.Instructor?.LastName ?? "")).Trim();

            var certificate = new Domain.Entities.Certifications.Certificate
            {
                StudentId = userId,
                CourseId = enrollment.CourseId,
                EnrollmentId = enrollment.Id,
                TemplateId = template?.Id ?? 1,
                CertificateNumber = LMS.Services.CertificateHelper.GenerateCertificateNumber(),
                StudentName = string.IsNullOrWhiteSpace(studentName) ? "طالب" : studentName,
                CourseName = enrollment.Course.Title,
                InstructorName = string.IsNullOrWhiteSpace(instructorName) ? null : instructorName,
                CompletionDate = enrollment.CompletedAt ?? DateTime.UtcNow,
                IssuedAt = DateTime.UtcNow,
                Grade = enrollment.FinalGrade ?? 0,
                VerificationCode = LMS.Services.CertificateHelper.GenerateVerificationCode(),
                IssuedBy = "System",
                IsRevoked = false,
                LearningHours = (int)(enrollment.TotalWatchTimeSeconds / 3600)
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            // Build verification URL
            var verificationUrl = Url.Action("Verify", "Certificates",
                new { area = "Student", code = certificate.VerificationCode }, Request.Scheme);
            certificate.VerificationUrl = verificationUrl;

            // Update enrollment flags
            enrollment.CertificateIssued = true;
            enrollment.CertificateIssuedAt = DateTime.UtcNow;
            enrollment.CertificateId = certificate.Id;
            await _context.SaveChangesAsync();

            // Create notification
            _context.Notifications.Add(new Domain.Entities.Notifications.Notification
            {
                UserId = userId,
                Title = "تهانينا! شهادتك جاهزة 🎉",
                Message = $"تم إصدار شهادة إتمام دورة {enrollment.Course.Title} بنجاح.",
                Type = LMS.Domain.Enums.NotificationType.CourseCompleted,
                ActionUrl = $"/Student/Certificates/Details/{certificate.Id}",
                ActionText = "عرض الشهادة",
                IconClass = "fas fa-certificate",
                IsRead = false
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Certificate {CertificateId} requested and generated for student {StudentId}, course {CourseId}",
                certificate.Id, userId, enrollment.CourseId);

            SetSuccessMessage("تم إصدار شهادتك بنجاح! 🎉");
            return RedirectToAction(nameof(Details), new { id = certificate.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for enrollment {EnrollmentId}", enrollmentId);
            SetErrorMessage("حدث خطأ أثناء إصدار الشهادة. يرجى المحاولة لاحقاً.");
            return RedirectToAction("CompletedCourses", "Courses");
        }
    }
}

