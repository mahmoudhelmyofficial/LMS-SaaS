using LMS.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// وحدة التحكم الأساسية للإدارة - Base controller for Admin area
/// All admin controllers should inherit from this
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin")]
public abstract class AdminBaseController : Controller
{
    /// <summary>
    /// رسالة نجاح - Success message for TempData (culture-aware)
    /// </summary>
    protected void SetSuccessMessage(string arabic, string english)
    {
        TempData["SuccessMessage"] = CultureExtensions.T(arabic, english);
    }

    /// <summary>
    /// رسالة نجاح - Success message for TempData (already translated string)
    /// </summary>
    protected void SetSuccessMessage(string message)
    {
        TempData["SuccessMessage"] = message;
    }

    /// <summary>
    /// رسالة خطأ - Error message for TempData (culture-aware)
    /// </summary>
    protected void SetErrorMessage(string arabic, string english)
    {
        TempData["ErrorMessage"] = CultureExtensions.T(arabic, english);
    }

    /// <summary>
    /// رسالة خطأ - Error message for TempData (already translated string)
    /// </summary>
    protected void SetErrorMessage(string message)
    {
        TempData["ErrorMessage"] = message;
    }

    /// <summary>
    /// رسالة تحذير - Warning message for TempData (culture-aware)
    /// </summary>
    protected void SetWarningMessage(string arabic, string english)
    {
        TempData["WarningMessage"] = CultureExtensions.T(arabic, english);
    }

    /// <summary>
    /// رسالة تحذير - Warning message for TempData (already translated string)
    /// </summary>
    protected void SetWarningMessage(string message)
    {
        TempData["WarningMessage"] = message;
    }

    /// <summary>
    /// رسالة معلومات - Info message for TempData (culture-aware)
    /// </summary>
    protected void SetInfoMessage(string arabic, string english)
    {
        TempData["InfoMessage"] = CultureExtensions.T(arabic, english);
    }

    /// <summary>
    /// رسالة معلومات - Info message for TempData (already translated string)
    /// </summary>
    protected void SetInfoMessage(string message)
    {
        TempData["InfoMessage"] = message;
    }
}

