using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// وحدة التحكم في الفيديو الآمن - Secure Video Controller
/// Enterprise-level video streaming security
/// </summary>
[Area("Student")]
[Authorize]
[Route("api/secure-video")]
[ApiController]
public class SecureVideoController : ControllerBase
{
    private readonly ISecureStreamingService _streamingService;
    private readonly IPlaybackSessionService _sessionService;
    private readonly IDRMService _drmService;
    private readonly IVideoAuditService _auditService;
    private readonly IGeoRestrictionService _geoService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SecureVideoController> _logger;

    public SecureVideoController(
        ISecureStreamingService streamingService,
        IPlaybackSessionService sessionService,
        IDRMService drmService,
        IVideoAuditService auditService,
        IGeoRestrictionService geoService,
        ICurrentUserService currentUserService,
        ILogger<SecureVideoController> logger)
    {
        _streamingService = streamingService;
        _sessionService = sessionService;
        _drmService = drmService;
        _auditService = auditService;
        _geoService = geoService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Session Management

    /// <summary>
    /// بدء جلسة تشغيل - Start playback session
    /// </summary>
    [HttpPost("session/start")]
    [AllowAnonymous]
    public async Task<IActionResult> StartSession([FromBody] StartSessionApiRequest request)
    {
        var userId = _currentUserService.UserId;
        
        // Check if lesson is free preview for unauthenticated users
        if (string.IsNullOrEmpty(userId))
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            var lesson = await context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == request.LessonId);
            
            if (lesson == null)
                return BadRequest(new { error = "Lesson not found" });
            
            if (lesson.IsPreviewable || lesson.IsFreePreview)
            {
                userId = $"preview-{Guid.NewGuid()}";
            }
            else
            {
                return Unauthorized(new { error = "User not authenticated" });
            }
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _sessionService.StartSessionAsync(new StartSessionRequest
        {
            UserId = userId,
            LessonId = request.LessonId,
            DeviceFingerprint = request.DeviceFingerprint,
            DeviceId = request.DeviceId,
            DeviceType = request.DeviceType,
            BrowserName = request.BrowserName,
            OperatingSystem = request.OperatingSystem,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ResumeFromSeconds = request.ResumeFromSeconds
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        if (!data.IsAllowed)
        {
            return Ok(new
            {
                success = false,
                blocked = true,
                reason = data.BlockReason,
                code = data.BlockCode,
                activeSessions = data.ActiveSessions,
                maxSessions = data.MaxAllowedSessions,
                conflictingSessions = data.ConflictingSessions
            });
        }

        return Ok(new
        {
            success = true,
            sessionToken = data.SessionToken,
            expiresAt = data.SessionExpiresAt,
            heartbeatInterval = data.HeartbeatIntervalSeconds,
            activeSessions = data.ActiveSessions,
            maxSessions = data.MaxAllowedSessions
        });
    }

    /// <summary>
    /// إنهاء جلسة - End session
    /// </summary>
    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession([FromBody] EndSessionApiRequest request)
    {
        var result = await _sessionService.EndSessionAsync(request.SessionToken, request.Reason ?? "UserEnded");

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    /// <summary>
    /// نبضة القلب - Session heartbeat
    /// </summary>
    [HttpPost("session/heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatApiRequest request)
    {
        var ipAddress = GetClientIpAddress();

        var result = await _sessionService.UpdateHeartbeatAsync(new HeartbeatRequest
        {
            SessionToken = request.SessionToken,
            CurrentPlaybackPosition = request.Position,
            IsPaused = request.IsPaused,
            IsBuffering = request.IsBuffering,
            IpAddress = ipAddress,
            CurrentQuality = request.Quality,
            BufferHealth = request.BufferHealth
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        return Ok(new
        {
            success = data.IsValid,
            shouldContinue = data.ShouldContinue,
            terminationReason = data.TerminationReason,
            nextHeartbeat = data.NextHeartbeatSeconds,
            forceRefresh = data.ForceRefreshToken,
            warnings = data.Warnings
        });
    }

    /// <summary>
    /// الجلسات النشطة - Get active sessions
    /// </summary>
    [HttpGet("session/active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sessionService.GetActiveSessionsAsync(userId);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true, sessions = result.Data });
    }

    /// <summary>
    /// إنهاء جميع الجلسات - End all sessions
    /// </summary>
    [HttpPost("session/end-all")]
    public async Task<IActionResult> EndAllSessions()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sessionService.EndAllUserSessionsAsync(userId, "UserRequestedEndAll");

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true, endedCount = result.Data });
    }

    #endregion

    #region Video Access

    /// <summary>
    /// الحصول على رابط البث - Get streaming URL
    /// </summary>
    [HttpGet("stream/{lessonId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStreamingUrl(int lessonId, [FromQuery] string? quality = null)
    {
        var userId = _currentUserService.UserId;
        
        // Check if lesson is free preview for unauthenticated users
        if (string.IsNullOrEmpty(userId))
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            var lesson = await context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);
            
            if (lesson == null || (!lesson.IsPreviewable && !lesson.IsFreePreview))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }
            
            // Use temporary user ID for preview access
            userId = $"preview-{Guid.NewGuid()}";
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();
        var deviceFingerprint = Request.Headers["X-Device-Fingerprint"].FirstOrDefault();

        var result = await _streamingService.GetStreamingUrlAsync(userId, lessonId, new StreamingOptions
        {
            PreferredQuality = quality,
            RequireEncryption = true,
            IpAddress = ipAddress,
            DeviceFingerprint = deviceFingerprint,
            UserAgent = userAgent
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        return Ok(new
        {
            success = true,
            url = data.Url,
            streamingType = data.StreamingType,
            encrypted = data.IsEncrypted,
            sessionToken = data.SessionToken,
            expiresAt = data.SessionExpiresAt,
            headers = data.Headers,
            watermark = data.Watermark,
            resumeFrom = data.ResumeFromSeconds
        });
    }

    /// <summary>
    /// التحقق من الوصول - Check video access
    /// </summary>
    [HttpGet("access-check/{lessonId}")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckAccess(int lessonId)
    {
        var userId = _currentUserService.UserId;
        
        // Check if lesson is free preview for unauthenticated users
        if (string.IsNullOrEmpty(userId))
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            var lesson = await context.Lessons
                .Include(l => l.Module)
                .FirstOrDefaultAsync(l => l.Id == lessonId);
            
            if (lesson == null)
                return BadRequest(new { error = "Lesson not found" });
            
            if (lesson.IsPreviewable || lesson.IsFreePreview)
            {
                // Allow preview access
                userId = $"preview-{Guid.NewGuid()}";
            }
            else
            {
                return Unauthorized();
            }
        }

        var ipAddress = GetClientIpAddress();
        var deviceFingerprint = Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        var userAgent = Request.Headers.UserAgent.ToString();

        // Get geo info with fault tolerance
        string? countryCode = null;
        try
        {
            var geoResult = await _geoService.GetGeoLocationAsync(ipAddress);
            countryCode = geoResult.Data?.CountryCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geo lookup failed for IP {IpAddress}, continuing without geo info", ipAddress);
        }

        var result = await _streamingService.CheckVideoAccessAsync(userId, lessonId, new VideoAccessCheckRequest
        {
            IpAddress = ipAddress,
            DeviceFingerprint = deviceFingerprint,
            UserAgent = userAgent,
            CountryCode = countryCode
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        return Ok(new
        {
            allowed = data.IsAllowed,
            reason = data.DenialReason,
            code = data.DenialCode,
            enrolled = data.IsEnrolled,
            geoAllowed = data.IsGeoAllowed,
            deviceAllowed = data.IsDeviceAllowed,
            sessionAllowed = data.IsConcurrentSessionAllowed,
            activeSessions = data.ActiveSessionCount,
            maxSessions = data.MaxAllowedSessions,
            warnings = data.Warnings
        });
    }

    /// <summary>
    /// التحقق من الرابط الموقع - Validate signed URL
    /// </summary>
    [HttpPost("validate-token")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateToken([FromBody] ValidateTokenRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();
        var referrer = Request.Headers.Referer.ToString();

        var result = await _streamingService.ValidateSignedUrlAsync(request.Token, request.Signature, new ValidateUrlRequest
        {
            IpAddress = ipAddress,
            DeviceFingerprint = request.DeviceFingerprint,
            UserAgent = userAgent,
            Referrer = referrer
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        return Ok(new
        {
            valid = data.IsValid,
            reason = data.InvalidReason,
            remainingAccess = data.RemainingAccess,
            expiresAt = data.ExpiresAt
        });
    }

    #endregion

    #region DRM

    /// <summary>
    /// الحصول على ترخيص DRM - Get DRM license
    /// </summary>
    [HttpPost("drm/license")]
    public async Task<IActionResult> GetDRMLicense([FromBody] DRMLicenseApiRequest request)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User not authenticated" });

        var ipAddress = GetClientIpAddress();

        var result = await _drmService.GenerateLicenseAsync(new DRMLicenseRequest
        {
            UserId = userId,
            LessonId = request.LessonId,
            DRMType = request.DrmType,
            LicenseRequest = request.LicenseRequest != null 
                ? Convert.FromBase64String(request.LicenseRequest) 
                : null,
            ContentId = request.ContentId,
            KeyId = request.KeyId,
            SessionToken = request.SessionToken,
            IpAddress = ipAddress,
            DeviceFingerprint = request.DeviceFingerprint,
            DeviceInfo = request.DeviceInfo
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        if (!data.IsSuccess)
        {
            return Ok(new
            {
                success = false,
                error = data.ErrorMessage,
                code = data.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            license = data.License,
            licenseId = data.LicenseId,
            expiresAt = data.ExpiresAt,
            duration = data.DurationSeconds,
            policies = data.Policies
        });
    }

    /// <summary>
    /// الحصول على مفتاح التشفير - Get encryption key (for HLS-AES)
    /// </summary>
    [HttpGet("key/{lessonId}")]
    public async Task<IActionResult> GetEncryptionKey(int lessonId, [FromQuery] string token, [FromQuery] string sig)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Validate session
        var sessionToken = Request.Headers["X-Session-Token"].FirstOrDefault() ?? token;
        
        var keyResult = await _streamingService.GetHlsEncryptionKeyAsync(userId, lessonId, sessionToken);

        if (!keyResult.IsSuccess)
            return Forbid();

        return File(keyResult.Data!, "application/octet-stream");
    }

    #endregion

    #region Device Management

    /// <summary>
    /// تسجيل جهاز - Register device
    /// </summary>
    [HttpPost("device/register")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceApiRequest request)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var ipAddress = GetClientIpAddress();

        var result = await _sessionService.RegisterDeviceAsync(new DeviceRegistrationRequest
        {
            UserId = userId,
            DeviceFingerprint = request.DeviceFingerprint,
            DeviceName = request.DeviceName,
            DeviceType = request.DeviceType,
            OperatingSystem = request.OperatingSystem,
            OsVersion = request.OsVersion,
            Browser = request.Browser,
            BrowserVersion = request.BrowserVersion,
            ScreenResolution = request.ScreenResolution,
            Timezone = request.Timezone,
            Language = request.Language,
            IpAddress = ipAddress
        });

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        var data = result.Data!;
        return Ok(new
        {
            success = data.IsAllowed,
            isNewDevice = data.IsNewDevice,
            blockReason = data.BlockReason,
            totalDevices = data.TotalRegisteredDevices,
            maxDevices = data.MaxAllowedDevices,
            riskLevel = data.RiskLevel
        });
    }

    /// <summary>
    /// قائمة الأجهزة - Get user devices
    /// </summary>
    [HttpGet("device/list")]
    public async Task<IActionResult> GetDevices()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sessionService.GetUserDevicesAsync(userId);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true, devices = result.Data });
    }

    /// <summary>
    /// حذف جهاز - Remove device
    /// </summary>
    [HttpDelete("device/{deviceFingerprint}")]
    public async Task<IActionResult> RemoveDevice(string deviceFingerprint)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _sessionService.RemoveDeviceAsync(userId, deviceFingerprint);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    #endregion

    #region Security Reporting

    /// <summary>
    /// الإبلاغ عن نشاط مشبوه - Report suspicious activity
    /// </summary>
    [HttpPost("report/suspicious")]
    public async Task<IActionResult> ReportSuspiciousActivity([FromBody] SuspiciousActivityApiRequest request)
    {
        var userId = _currentUserService.UserId;
        var ipAddress = GetClientIpAddress();

        await _sessionService.ReportSuspiciousActivityAsync(new SuspiciousActivityReport
        {
            UserId = userId,
            SessionToken = request.SessionToken,
            LessonId = request.LessonId,
            ActivityType = request.ActivityType,
            Description = request.Description,
            IpAddress = ipAddress,
            DeviceFingerprint = request.DeviceFingerprint,
            UserAgent = Request.Headers.UserAgent.ToString(),
            RiskScore = request.RiskScore
        });

        return Ok(new { success = true });
    }

    #endregion

    #region Private Helpers

    private string GetClientIpAddress()
    {
        // Check for forwarded IP (when behind proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    #endregion
}

#region API Request Models

public class StartSessionApiRequest
{
    public int LessonId { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string? BrowserName { get; set; }
    public string? OperatingSystem { get; set; }
    public int ResumeFromSeconds { get; set; }
}

public class EndSessionApiRequest
{
    public string SessionToken { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class HeartbeatApiRequest
{
    public string SessionToken { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsPaused { get; set; }
    public bool IsBuffering { get; set; }
    public string? Quality { get; set; }
    public double? BufferHealth { get; set; }
}

public class ValidateTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
}

public class DRMLicenseApiRequest
{
    public int LessonId { get; set; }
    public string DrmType { get; set; } = string.Empty;
    public string? LicenseRequest { get; set; }
    public string? ContentId { get; set; }
    public string? KeyId { get; set; }
    public string? SessionToken { get; set; }
    public string? DeviceFingerprint { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
}

public class RegisterDeviceApiRequest
{
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? DeviceType { get; set; }
    public string? OperatingSystem { get; set; }
    public string? OsVersion { get; set; }
    public string? Browser { get; set; }
    public string? BrowserVersion { get; set; }
    public string? ScreenResolution { get; set; }
    public string? Timezone { get; set; }
    public string? Language { get; set; }
}

public class SuspiciousActivityApiRequest
{
    public string? SessionToken { get; set; }
    public int? LessonId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DeviceFingerprint { get; set; }
    public int RiskScore { get; set; }
}

#endregion

