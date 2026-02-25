using LMS.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers.Api;

/// <summary>
/// Security Monitoring API Controller
/// واجهة برمجة تطبيقات مراقبة الأمان
/// Provides endpoints for the security dashboard
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin,SecurityAdmin")]
[Route("api/admin/security")]
[ApiController]
public class SecurityMonitoringApiController : ControllerBase
{
    private readonly ISecurityMonitoringService _monitoringService;
    private readonly IGDPRComplianceService _gdprService;
    private readonly IIPReputationService _ipReputationService;
    private readonly ILogger<SecurityMonitoringApiController> _logger;

    public SecurityMonitoringApiController(
        ISecurityMonitoringService monitoringService,
        IGDPRComplianceService gdprService,
        IIPReputationService ipReputationService,
        ILogger<SecurityMonitoringApiController> logger)
    {
        _monitoringService = monitoringService;
        _gdprService = gdprService;
        _ipReputationService = ipReputationService;
        _logger = logger;
    }

    #region Dashboard

    /// <summary>
    /// Get security dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _monitoringService.GetDashboardDataAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get real-time security status
    /// </summary>
    [HttpGet("realtime")]
    public async Task<IActionResult> GetRealTimeStatus()
    {
        var result = await _monitoringService.GetRealTimeStatusAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    #endregion

    #region Alerts

    /// <summary>
    /// Get active security alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var result = await _monitoringService.GetActiveAlertsAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    [HttpPost("alerts/{alertId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(int alertId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var result = await _monitoringService.AcknowledgeAlertAsync(alertId, userId ?? "admin");
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    /// <summary>
    /// Resolve an alert
    /// </summary>
    [HttpPost("alerts/{alertId}/resolve")]
    public async Task<IActionResult> ResolveAlert(int alertId, [FromBody] ResolveAlertRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var result = await _monitoringService.ResolveAlertAsync(alertId, userId ?? "admin", request.Resolution);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get threat report for date range
    /// </summary>
    [HttpGet("reports/threats")]
    public async Task<IActionResult> GetThreatReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var result = await _monitoringService.GetThreatReportAsync(from, to);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get user security profile
    /// </summary>
    [HttpGet("users/{userId}/profile")]
    public async Task<IActionResult> GetUserSecurityProfile(string userId)
    {
        var result = await _monitoringService.GetUserSecurityProfileAsync(userId);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    #endregion

    #region IP Management

    /// <summary>
    /// Check IP reputation
    /// </summary>
    [HttpGet("ip/{ipAddress}/reputation")]
    public async Task<IActionResult> CheckIPReputation(string ipAddress)
    {
        var result = await _ipReputationService.CheckIPReputationAsync(ipAddress);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get blocked IPs
    /// </summary>
    [HttpGet("ip/blocked")]
    public async Task<IActionResult> GetBlockedIPs()
    {
        var result = await _ipReputationService.GetBlockedIPsAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Block an IP address
    /// </summary>
    [HttpPost("ip/{ipAddress}/block")]
    public async Task<IActionResult> BlockIP(string ipAddress, [FromBody] BlockIPRequest request)
    {
        var duration = request.DurationHours.HasValue 
            ? TimeSpan.FromHours(request.DurationHours.Value) 
            : (TimeSpan?)null;

        var result = await _ipReputationService.BlockIPAsync(ipAddress, request.Reason ?? "Manual block", duration);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    /// <summary>
    /// Unblock an IP address
    /// </summary>
    [HttpPost("ip/{ipAddress}/unblock")]
    public async Task<IActionResult> UnblockIP(string ipAddress)
    {
        var result = await _ipReputationService.UnblockIPAsync(ipAddress);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    #endregion

    #region Security Actions

    /// <summary>
    /// Take a security action
    /// </summary>
    [HttpPost("actions")]
    public async Task<IActionResult> TakeSecurityAction([FromBody] SecurityActionApiRequest request)
    {
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "admin";

        var result = await _monitoringService.TakeSecurityActionAsync(new SecurityActionRequest
        {
            Action = request.Action,
            UserId = request.UserId,
            IpAddress = request.IpAddress,
            DeviceFingerprint = request.DeviceFingerprint,
            Reason = request.Reason,
            ActorId = actorId
        });
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    #endregion

    #region GDPR

    /// <summary>
    /// Export user data (GDPR)
    /// </summary>
    [HttpGet("gdpr/export/{userId}")]
    public async Task<IActionResult> ExportUserData(string userId)
    {
        var result = await _gdprService.ExportUserDataAsync(userId);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Download user data as zip archive
    /// </summary>
    [HttpGet("gdpr/export/{userId}/download")]
    public async Task<IActionResult> DownloadUserData(string userId)
    {
        var result = await _gdprService.GenerateExportArchiveAsync(userId);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return File(result.Data!, "application/zip", $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.zip");
    }

    /// <summary>
    /// Preview data deletion
    /// </summary>
    [HttpGet("gdpr/delete/{userId}/preview")]
    public async Task<IActionResult> PreviewDataDeletion(string userId)
    {
        var result = await _gdprService.PreviewDataDeletionAsync(userId);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Delete user data (GDPR Right to Erasure)
    /// </summary>
    [HttpPost("gdpr/delete/{userId}")]
    public async Task<IActionResult> DeleteUserData(string userId, [FromBody] DataDeletionOptions options)
    {
        var result = await _gdprService.DeleteUserDataAsync(userId, options);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get retention report
    /// </summary>
    [HttpGet("gdpr/retention")]
    public async Task<IActionResult> GetRetentionReport()
    {
        var result = await _gdprService.GetRetentionReportAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Run data retention cleanup
    /// </summary>
    [HttpPost("gdpr/cleanup")]
    public async Task<IActionResult> RunCleanup()
    {
        var result = await _gdprService.CleanupExpiredDataAsync();
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { recordsDeleted = result.Data });
    }

    /// <summary>
    /// Get GDPR audit log
    /// </summary>
    [HttpGet("gdpr/audit")]
    public async Task<IActionResult> GetGDPRAuditLog([FromQuery] string? userId, [FromQuery] DateTime? fromDate)
    {
        var result = await _gdprService.GetGDPRAuditLogAsync(userId, fromDate);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    #endregion
}

#region Request DTOs

public class ResolveAlertRequest
{
    public string Resolution { get; set; } = string.Empty;
}

public class BlockIPRequest
{
    public string? Reason { get; set; }
    public int? DurationHours { get; set; }
}

public class SecurityActionApiRequest
{
    public SecurityAction Action { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? Reason { get; set; }
}

#endregion

