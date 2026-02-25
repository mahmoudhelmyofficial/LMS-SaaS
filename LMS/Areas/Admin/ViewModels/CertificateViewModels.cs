using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج إحصائيات الشهادات - Certificate Statistics ViewModel
/// </summary>
public class CertificateStatisticsViewModel
{
    public int TotalCertificates { get; set; }
    public int RevokedCertificates { get; set; }
    public int ActiveCertificates { get; set; }
    public List<CourseCertificateCount> CertificatesByCourse { get; set; } = new();
    public List<DailyCertificateCount> CertificatesByDay { get; set; } = new();
    public int VerificationAttempts { get; set; }
    public int SuccessfulVerifications { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CourseCertificateCount
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyCertificateCount
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// نموذج فلترة الشهادات - Certificate Filter ViewModel
/// </summary>
public class CertificateFilterViewModel
{
    /// <summary>
    /// كلمة البحث - Search term
    /// </summary>
    [Display(Name = "بحث")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// معرف الدورة - Course ID
    /// </summary>
    [Display(Name = "الدورة")]
    public int? CourseId { get; set; }

    /// <summary>
    /// هل ملغاة - Is revoked
    /// </summary>
    [Display(Name = "ملغاة")]
    public bool? IsRevoked { get; set; }

    /// <summary>
    /// من تاريخ - From date
    /// </summary>
    [Display(Name = "من تاريخ")]
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// إلى تاريخ - To date
    /// </summary>
    [Display(Name = "إلى تاريخ")]
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// نموذج إلغاء الشهادة - Certificate Revoke ViewModel
/// </summary>
public class CertificateRevokeViewModel
{
    /// <summary>
    /// المعرف - Certificate ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// رقم الشهادة - Certificate number
    /// </summary>
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>
    /// اسم الطالب - Student name
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// اسم الدورة - Course name
    /// </summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>
    /// سبب الإلغاء - Revocation reason
    /// </summary>
    [Required(ErrorMessage = "سبب الإلغاء مطلوب")]
    [MaxLength(500)]
    [Display(Name = "سبب الإلغاء")]
    public string RevocationReason { get; set; } = string.Empty;
}

