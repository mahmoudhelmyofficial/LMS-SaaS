using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// فحص صحة النظام - Health Check Controller
/// Provides system health status for monitoring
/// </summary>
[Area("Instructor")]
[Authorize(Roles = "Instructor,Admin")]
public class HealthController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<HealthController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// فحص صحة النظام الأساسي - Basic health check
    /// Returns quick status for load balancer/monitoring
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "LMS-Instructor"
        });
    }

    /// <summary>
    /// فحص صحة شامل - Comprehensive health check
    /// Returns detailed status including database connectivity
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var stopwatch = Stopwatch.StartNew();
        var healthStatus = new InstructorHealthStatus
        {
            ServiceName = "LMS-Instructor",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        };

        try
        {
            // Check database connectivity
            var dbStopwatch = Stopwatch.StartNew();
            var canConnect = await _context.Database.CanConnectAsync();
            dbStopwatch.Stop();

            healthStatus.Database = new ComponentHealth
            {
                Status = canConnect ? "healthy" : "unhealthy",
                ResponseTimeMs = dbStopwatch.ElapsedMilliseconds,
                Message = canConnect ? "Connected successfully" : "Connection failed"
            };

            // Check instructor data access
            var userId = _currentUserService.UserId;
            if (!string.IsNullOrEmpty(userId))
            {
                var dataStopwatch = Stopwatch.StartNew();
                var courseCount = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .CountAsync();
                dataStopwatch.Stop();

                healthStatus.InstructorData = new ComponentHealth
                {
                    Status = "healthy",
                    ResponseTimeMs = dataStopwatch.ElapsedMilliseconds,
                    Message = $"Instructor has {courseCount} courses"
                };
            }
            else
            {
                healthStatus.InstructorData = new ComponentHealth
                {
                    Status = "warning",
                    ResponseTimeMs = 0,
                    Message = "User not authenticated or ID not available"
                };
            }

            // Memory usage
            var process = Process.GetCurrentProcess();
            healthStatus.Memory = new MemoryHealth
            {
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                GCTotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024)
            };

            // Overall status
            healthStatus.Status = healthStatus.Database.Status == "healthy" ? "healthy" : "degraded";
            
            stopwatch.Stop();
            healthStatus.TotalResponseTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Health check completed. Status: {Status}, Database: {DbStatus}, ResponseTime: {ResponseTime}ms",
                healthStatus.Status, healthStatus.Database.Status, healthStatus.TotalResponseTimeMs);

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Health check failed");
            
            healthStatus.Status = "unhealthy";
            healthStatus.TotalResponseTimeMs = stopwatch.ElapsedMilliseconds;
            healthStatus.Error = ex.Message;
            healthStatus.Database = new ComponentHealth
            {
                Status = "unhealthy",
                ResponseTimeMs = 0,
                Message = ex.Message
            };

            return StatusCode(503, healthStatus);
        }
    }

    /// <summary>
    /// فحص جاهزية النظام - Readiness check
    /// Returns whether the service is ready to handle requests
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Ready()
    {
        try
        {
            // Quick database check
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (canConnect)
            {
                return Ok(new
                {
                    ready = true,
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(503, new
                {
                    ready = false,
                    reason = "Database not available",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new
            {
                ready = false,
                reason = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// معلومات الإصدار - Version information
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Version()
    {
        var assembly = typeof(HealthController).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
        var buildDate = System.IO.File.GetLastWriteTime(assembly.Location);

        return Ok(new
        {
            version = version,
            buildDate = buildDate.ToString("yyyy-MM-dd HH:mm:ss"),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        });
    }
}

/// <summary>
/// نموذج حالة الصحة للمدرس - Instructor Health Status Model
/// </summary>
public class InstructorHealthStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Environment { get; set; } = string.Empty;
    public long TotalResponseTimeMs { get; set; }
    public string? Error { get; set; }
    public ComponentHealth Database { get; set; } = new();
    public ComponentHealth InstructorData { get; set; } = new();
    public MemoryHealth Memory { get; set; } = new();
}

public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MemoryHealth
{
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long GCTotalMemoryMB { get; set; }
}

