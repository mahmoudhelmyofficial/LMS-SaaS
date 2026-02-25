using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// واجهة برمجة الملف الشخصي - Profile API Controller
/// Provides AJAX endpoints for profile management
/// </summary>
[Area("Instructor")]
[Route("api/instructor/profile")]
[ApiController]
[Authorize(Roles = "Instructor,Admin")]
public class ProfileApiController : ControllerBase
{
    private readonly IInstructorProfileService _profileService;
    private readonly IDropdownConfigService _dropdownService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ProfileApiController> _logger;

    public ProfileApiController(
        IInstructorProfileService profileService,
        IDropdownConfigService dropdownService,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorage,
        ILogger<ProfileApiController> logger)
    {
        _profileService = profileService;
        _dropdownService = dropdownService;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    #region Profile Picture

    /// <summary>
    /// رفع صورة الملف الشخصي - Upload profile picture
    /// </summary>
    [HttpPost("upload-picture")]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "غير مصرح" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "لم يتم اختيار ملف" });

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "حجم الملف يجب ألا يتجاوز 5 ميجابايت" });

            using var stream = file.OpenReadStream();
            var result = await _profileService.UploadProfilePictureAsync(userId, stream, file.FileName);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, url = result.Data, message = "تم رفع الصورة بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile picture");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء رفع الصورة" });
        }
    }

    /// <summary>
    /// حذف صورة الملف الشخصي - Delete profile picture
    /// </summary>
    [HttpDelete("delete-picture")]
    public async Task<IActionResult> DeleteProfilePicture()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "غير مصرح" });

            var result = await _profileService.DeleteProfilePictureAsync(userId);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = "تم حذف الصورة بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile picture");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حذف الصورة" });
        }
    }

    #endregion

    #region Intro Video

    /// <summary>
    /// رفع فيديو تعريفي - Upload intro video
    /// </summary>
    [HttpPost("upload-video")]
    public async Task<IActionResult> UploadIntroVideo(IFormFile file)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "غير مصرح" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "لم يتم اختيار ملف" });

            // Validate file size (max 100MB)
            if (file.Length > 100 * 1024 * 1024)
                return BadRequest(new { success = false, message = "حجم الملف يجب ألا يتجاوز 100 ميجابايت" });

            using var stream = file.OpenReadStream();
            var result = await _profileService.UploadIntroVideoAsync(userId, stream, file.FileName);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, url = result.Data, message = "تم رفع الفيديو بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading intro video");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء رفع الفيديو" });
        }
    }

    /// <summary>
    /// حذف الفيديو التعريفي - Delete intro video
    /// </summary>
    [HttpDelete("delete-video")]
    public async Task<IActionResult> DeleteIntroVideo()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "غير مصرح" });

            var result = await _profileService.DeleteIntroVideoAsync(userId);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = "تم حذف الفيديو بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting intro video");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حذف الفيديو" });
        }
    }

    #endregion

    #region Payment Validation

    /// <summary>
    /// التحقق من معلومات الدفع - Validate payment details
    /// </summary>
    [HttpPost("validate-payment")]
    public async Task<IActionResult> ValidatePaymentDetails([FromBody] ValidatePaymentRequest request)
    {
        try
        {
            var details = new PaymentDetailsDto
            {
                PayPalEmail = request.PayPalEmail,
                BankName = request.BankName,
                BankAccountName = request.BankAccountName,
                BankAccountNumber = request.BankAccountNumber,
                IBAN = request.IBAN,
                SwiftCode = request.SwiftCode,
                MobileWalletNumber = request.MobileWalletNumber,
                MobileWalletProvider = request.MobileWalletProvider
            };

            var result = await _profileService.ValidatePaymentMethodAsync(request.PaymentMethod, details);

            return Ok(new { success = result.IsSuccess, message = result.Error ?? "البيانات صحيحة" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating payment details");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء التحقق" });
        }
    }

    #endregion

    #region Dropdowns

    /// <summary>
    /// الحصول على البنوك حسب الدولة - Get banks by country
    /// </summary>
    [HttpGet("banks/{countryCode}")]
    public async Task<IActionResult> GetBanksByCountry(string countryCode)
    {
        try
        {
            var banks = await _dropdownService.GetBanksAsync(countryCode);
            return Ok(banks.Select(b => new { value = b.Value, text = b.Text }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting banks for country {CountryCode}", countryCode);
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على المدن حسب الدولة - Get cities by country
    /// </summary>
    [HttpGet("cities/{countryCode}")]
    public async Task<IActionResult> GetCitiesByCountry(string countryCode)
    {
        try
        {
            var cities = await _dropdownService.GetCitiesAsync(countryCode);
            return Ok(cities.Select(c => new { value = c.Value, text = c.Text }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cities for country {CountryCode}", countryCode);
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على مزودي المحافظ الإلكترونية - Get mobile wallet providers
    /// </summary>
    [HttpGet("wallet-providers/{countryCode}")]
    public async Task<IActionResult> GetWalletProviders(string countryCode)
    {
        try
        {
            var providers = await _dropdownService.GetMobileWalletProvidersAsync(countryCode);
            return Ok(providers.Select(p => new { value = p.Value, text = p.Text }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet providers for country {CountryCode}", countryCode);
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على طرق الدفع - Get payment methods
    /// </summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods([FromQuery] string? countryCode = null)
    {
        try
        {
            var methods = await _profileService.GetAvailablePaymentMethodsAsync(countryCode);
            return Ok(methods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment methods");
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على حدود السحب - Get withdrawal limits
    /// </summary>
    [HttpGet("withdrawal-limits/{paymentMethod}")]
    public async Task<IActionResult> GetWithdrawalLimits(string paymentMethod)
    {
        try
        {
            var limits = await _dropdownService.GetWithdrawalLimitsAsync(paymentMethod);
            return Ok(limits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawal limits for {PaymentMethod}", paymentMethod);
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على رسوم طريقة الدفع - Get payment method fees
    /// </summary>
    [HttpGet("payment-fees/{paymentMethod}")]
    public async Task<IActionResult> GetPaymentFees(string paymentMethod)
    {
        try
        {
            var fees = await _dropdownService.GetPaymentMethodFeesAsync(paymentMethod);
            return Ok(fees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment fees for {PaymentMethod}", paymentMethod);
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    #endregion

    #region Profile Completeness

    /// <summary>
    /// الحصول على نسبة اكتمال الملف - Get profile completeness
    /// </summary>
    [HttpGet("completeness")]
    public async Task<IActionResult> GetProfileCompleteness()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var completeness = await _profileService.GetProfileCompletenessAsync(userId);
            return Ok(completeness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile completeness");
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    #endregion

    #region Username

    /// <summary>
    /// التحقق من توفر اسم المستخدم - Check username availability
    /// </summary>
    [HttpGet("check-username")]
    public async Task<IActionResult> CheckUsernameAvailability([FromQuery] string username)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var isAvailable = await _profileService.IsUsernameAvailableAsync(username, userId);
            return Ok(new { available = isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking username availability");
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    #endregion

    #region Activity

    /// <summary>
    /// الحصول على سجل النشاط - Get activity log
    /// </summary>
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivityLog([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var activity = await _profileService.GetActivityLogAsync(userId, page, size);
            return Ok(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activity log");
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    #endregion

    #region Qualifications

    /// <summary>
    /// الحصول على المؤهلات - Get qualifications
    /// </summary>
    [HttpGet("qualifications")]
    public async Task<IActionResult> GetQualifications()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var qualifications = await _profileService.GetQualificationsAsync(userId);
            return Ok(qualifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting qualifications");
            return StatusCode(500, new { error = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إضافة مؤهل - Add qualification
    /// </summary>
    [HttpPost("qualifications")]
    public async Task<IActionResult> AddQualification([FromBody] CreateQualificationDto dto)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var result = await _profileService.AddQualificationAsync(userId, dto);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, id = result.Data, message = "تم إضافة المؤهل بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding qualification");
            return StatusCode(500, new { success = false, message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تحديث مؤهل - Update qualification
    /// </summary>
    [HttpPut("qualifications/{id}")]
    public async Task<IActionResult> UpdateQualification(int id, [FromBody] UpdateQualificationDto dto)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var result = await _profileService.UpdateQualificationAsync(userId, id, dto);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = "تم تحديث المؤهل بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating qualification");
            return StatusCode(500, new { success = false, message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// حذف مؤهل - Delete qualification
    /// </summary>
    [HttpDelete("qualifications/{id}")]
    public async Task<IActionResult> DeleteQualification(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "غير مصرح" });

            var result = await _profileService.RemoveQualificationAsync(userId, id);

            if (result.IsSuccess)
            {
                return Ok(new { success = true, message = "تم حذف المؤهل بنجاح" });
            }

            return BadRequest(new { success = false, message = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting qualification");
            return StatusCode(500, new { success = false, message = "حدث خطأ" });
        }
    }

    #endregion
}

/// <summary>
/// طلب التحقق من الدفع - Payment validation request
/// </summary>
public class ValidatePaymentRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string? PayPalEmail { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? IBAN { get; set; }
    public string? SwiftCode { get; set; }
    public string? MobileWalletNumber { get; set; }
    public string? MobileWalletProvider { get; set; }
}

