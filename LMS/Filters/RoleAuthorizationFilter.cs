using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LMS.Filters;

/// <summary>
/// فلتر التحقق من الصلاحيات - Role Authorization Filter
/// Provides user-friendly redirection for unauthorized access to area-specific pages
/// </summary>
public class RoleAuthorizationFilter : IAuthorizationFilter
{
    private readonly ILogger<RoleAuthorizationFilter> _logger;

    public RoleAuthorizationFilter(ILogger<RoleAuthorizationFilter> logger)
    {
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        try
        {
            var user = context.HttpContext.User;
            var path = context.HttpContext.Request.Path;
            
            // Log all authorization checks for debugging
            _logger.LogDebug("RoleAuthorizationFilter checking path: {Path}, Authenticated: {IsAuth}", 
                path, user.Identity?.IsAuthenticated ?? false);
            
            // If user is not authenticated, let the [Authorize] attribute handle it
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogDebug("User not authenticated, letting [Authorize] attribute handle it");
                return;
            }

            // Get the area from route data
            var area = context.RouteData.Values["area"]?.ToString();
            
            if (string.IsNullOrEmpty(area))
            {
                _logger.LogDebug("No area specified in route, allowing access");
                return;
            }

            var userName = user.Identity?.Name ?? "Unknown";
            var userRoles = GetUserRoles(user).ToList();
            var userRolesString = string.Join(", ", userRoles);
            
            _logger.LogInformation(
                "Authorization check: User {UserName} (Roles: {UserRoles}) accessing {Area} area at {Path}",
                userName,
                userRolesString,
                area,
                path);

            // Check if user has the required role for the area
            var hasRequiredRole = area.ToLower() switch
            {
                "admin" => user.IsInRole("Admin"),
                "instructor" => user.IsInRole("Instructor") || user.IsInRole("Admin"),
                "student" => user.IsInRole("Student") || user.IsInRole("Instructor") || user.IsInRole("Admin"),
                _ => true
            };

            if (!hasRequiredRole)
            {
                _logger.LogWarning(
                    "ACCESS DENIED: User {UserName} (Roles: {UserRoles}) attempted to access {Area} area at {Path}",
                    userName,
                    userRolesString,
                    area,
                    path);

                // Try to set error message in session, but don't fail if session is unavailable
                try
                {
                    if (context.HttpContext.Session != null && context.HttpContext.Session.IsAvailable)
                    {
                        context.HttpContext.Session.SetString("AccessDeniedMessage", 
                            $"ليس لديك الصلاحيات المطلوبة للوصول إلى منطقة {GetAreaNameInArabic(area)}. يرجى تسجيل الدخول بحساب مناسب.");
                    }
                }
                catch (Exception sessionEx)
                {
                    _logger.LogWarning(sessionEx, "Failed to set session message for access denied");
                }

                // Redirect to AccessDenied page with area info in query string
                context.Result = new RedirectToActionResult("AccessDenied", "Account", 
                    new { area = "", attemptedArea = area });
            }
            else
            {
                _logger.LogDebug("Authorization successful for user {UserName} to access {Area} area", userName, area);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RoleAuthorizationFilter for path {Path}", 
                context.HttpContext.Request.Path);
            
            // Redirect to access denied to prevent 500 errors
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
        }
    }

    private static IEnumerable<string> GetUserRoles(System.Security.Claims.ClaimsPrincipal user)
    {
        var roles = new List<string>();
        if (user.IsInRole("Admin")) roles.Add("Admin");
        if (user.IsInRole("Instructor")) roles.Add("Instructor");
        if (user.IsInRole("Student")) roles.Add("Student");
        return roles.Any() ? roles : new List<string> { "No Role" };
    }

    private static string GetAreaNameInArabic(string area)
    {
        return area.ToLower() switch
        {
            "admin" => "الإدارة",
            "instructor" => "المدرس",
            "student" => "الطالب",
            _ => area
        };
    }
}

