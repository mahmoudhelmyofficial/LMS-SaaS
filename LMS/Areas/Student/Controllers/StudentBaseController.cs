using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// وحدة التحكم الأساسية للطالب - Base controller for Student area
/// All student controllers should inherit from this
/// </summary>
[Area("Student")]
[Authorize(Roles = "Student,Instructor,Admin")]
public abstract class StudentBaseController : Controller
{
    /// <summary>
    /// رسالة نجاح - Success message for TempData
    /// </summary>
    protected void SetSuccessMessage(string message)
    {
        TempData["SuccessMessage"] = message;
    }

    /// <summary>
    /// رسالة خطأ - Error message for TempData
    /// </summary>
    protected void SetErrorMessage(string message)
    {
        TempData["ErrorMessage"] = message;
    }

    /// <summary>
    /// رسالة تحذير - Warning message for TempData
    /// </summary>
    protected void SetWarningMessage(string message)
    {
        TempData["WarningMessage"] = message;
    }

    /// <summary>
    /// رسالة معلومات - Info message for TempData
    /// </summary>
    protected void SetInfoMessage(string message)
    {
        TempData["InfoMessage"] = message;
    }
}

