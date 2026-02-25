using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LMS.Helpers;

/// <summary>
/// Enterprise Navigation Helper
/// Provides safe navigation utilities with validation and fallback handling
/// </summary>
public static class NavigationHelper
{
    /// <summary>
    /// Safe redirect with validation - returns fallback if route is invalid
    /// </summary>
    public static IActionResult SafeRedirect(
        Controller controller,
        string action,
        string? controllerName = null,
        object? routeValues = null,
        string fallbackUrl = "/")
    {
        try
        {
            var url = controller.Url.Action(action, controllerName, routeValues);
            
            if (string.IsNullOrEmpty(url))
            {
                return new RedirectResult(fallbackUrl);
            }

            return new RedirectToActionResult(action, controllerName, routeValues);
        }
        catch
        {
            return new RedirectResult(fallbackUrl);
        }
    }

    /// <summary>
    /// Get breadcrumb trail for current location
    /// </summary>
    public static List<BreadcrumbItem> GetBreadcrumbs(string area, string controller, string action, string? entityName = null)
    {
        var breadcrumbs = new List<BreadcrumbItem>
        {
            new() { Title = "الرئيسية", Url = "/", Icon = "fas fa-home" }
        };

        // Add area-specific breadcrumb
        switch (area.ToLowerInvariant())
        {
            case "admin":
                breadcrumbs.Add(new BreadcrumbItem { Title = "لوحة الإدارة", Url = "/Admin/Dashboard", Icon = "fas fa-cogs" });
                break;
            case "instructor":
                breadcrumbs.Add(new BreadcrumbItem { Title = "منطقة المدرس", Url = "/Instructor/Dashboard", Icon = "fas fa-chalkboard-teacher" });
                break;
            case "student":
                breadcrumbs.Add(new BreadcrumbItem { Title = "منطقة الطالب", Url = "/Student/Dashboard", Icon = "fas fa-user-graduate" });
                break;
        }

        // Add controller breadcrumb
        var controllerTitle = GetControllerTitle(controller);
        if (!string.IsNullOrEmpty(controllerTitle))
        {
            breadcrumbs.Add(new BreadcrumbItem
            {
                Title = controllerTitle,
                Url = $"/{area}/{controller}",
                Icon = GetControllerIcon(controller)
            });
        }

        // Add action breadcrumb
        var actionTitle = GetActionTitle(action);
        if (!string.IsNullOrEmpty(actionTitle) && action.ToLowerInvariant() != "index")
        {
            breadcrumbs.Add(new BreadcrumbItem
            {
                Title = entityName ?? actionTitle,
                Url = null, // Current page
                IsActive = true
            });
        }

        return breadcrumbs;
    }

    /// <summary>
    /// Get menu items for area
    /// </summary>
    public static List<MenuItem> GetMenuItems(string area, string? currentController = null)
    {
        return area.ToLowerInvariant() switch
        {
            "student" => GetStudentMenuItems(currentController),
            "instructor" => GetInstructorMenuItems(currentController),
            "admin" => GetAdminMenuItems(currentController),
            _ => new List<MenuItem>()
        };
    }

    /// <summary>
    /// Validate that URL is safe for redirect (prevents open redirects)
    /// </summary>
    public static bool IsLocalUrl(string? url, HttpRequest request)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Check if URL is local
        if (url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\"))
            return true;

        // Check if same host
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Host.Equals(request.Host.Host, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Get return URL with validation
    /// </summary>
    public static string GetSafeReturnUrl(HttpRequest request, string? returnUrl, string defaultUrl = "/")
    {
        if (IsLocalUrl(returnUrl, request))
        {
            return returnUrl!;
        }
        return defaultUrl;
    }

    #region Private Helpers

    private static string GetControllerTitle(string controller)
    {
        return controller.ToLowerInvariant() switch
        {
            "courses" => "الدورات",
            "lessons" => "الدروس",
            "lessonresources" => "موارد الدروس",
            "modules" => "الوحدات",
            "quizzes" => "الاختبارات",
            "questionbank" => "بنك الأسئلة",
            "assignments" => "التكليفات",
            "assignmentsubmissions" => "إجابات الواجبات",
            "students" => "الطلاب",
            "submissions" => "الإجابات",
            "quizattempts" => "محاولات الاختبارات",
            "dashboard" => "لوحة التحكم",
            "profile" => "الملف الشخصي",
            "certificates" => "الشهادات",
            "earnings" => "الأرباح",
            "commissions" => "العمولات",
            "withdrawalrequests" => "طلبات السحب",
            "analytics" => "التحليلات",
            "settings" => "الإعدادات",
            "notifications" => "الإشعارات",
            "messages" => "الرسائل",
            "reviews" => "التقييمات",
            "faq" => "الأسئلة الشائعة",
            "discussions" => "المناقشات",
            "comments" => "التعليقات",
            "announcements" => "الإعلانات",
            "learning" => "التعلم",
            "learningpaths" => "مسارات التعلم",
            "checkout" => "الدفع",
            "liveclasses" => "البث المباشر",
            "livesessionschedules" => "جداول الحصص",
            "liveclassattendance" => "سجل الحضور",
            "recordings" => "التسجيلات",
            "books" => "الكتب",
            "bundles" => "الباقات",
            "contentdrip" => "جدولة المحتوى",
            "resources" => "الموارد",
            "documents" => "المستندات",
            "medialibrary" => "مكتبة الوسائط",
            "courseinstructors" => "المدرسين المشاركين",
            "proctoring" => "مراقبة الاختبارات",
            "coupons" => "الكوبونات",
            "flashsales" => "العروض السريعة",
            "affiliates" => "التسويق بالعمولة",
            "help" => "مركز المساعدة",
            "progress" => "تحليل الأداء",
            _ => controller
        };
    }

    private static string GetControllerIcon(string controller)
    {
        return controller.ToLowerInvariant() switch
        {
            "courses" => "fas fa-book",
            "lessons" => "fas fa-play-circle",
            "lessonresources" => "fas fa-paperclip",
            "modules" => "fas fa-layer-group",
            "quizzes" => "fas fa-question-circle",
            "questionbank" => "fas fa-database",
            "assignments" => "fas fa-file-alt",
            "assignmentsubmissions" => "fas fa-inbox",
            "students" => "fas fa-users",
            "submissions" => "fas fa-inbox",
            "quizattempts" => "fas fa-check-square",
            "dashboard" => "fas fa-tachometer-alt",
            "profile" => "fas fa-user",
            "certificates" => "fas fa-certificate",
            "earnings" => "fas fa-dollar-sign",
            "commissions" => "fas fa-percent",
            "withdrawalrequests" => "fas fa-arrow-circle-up",
            "analytics" => "fas fa-chart-line",
            "settings" => "fas fa-cog",
            "notifications" => "fas fa-bell",
            "messages" => "fas fa-envelope",
            "reviews" => "fas fa-star",
            "faq" => "fas fa-question-circle",
            "discussions" => "fas fa-comments",
            "comments" => "fas fa-comment-dots",
            "announcements" => "fas fa-bullhorn",
            "learning" => "fas fa-graduation-cap",
            "learningpaths" => "fas fa-route",
            "checkout" => "fas fa-shopping-cart",
            "liveclasses" => "fas fa-video",
            "livesessionschedules" => "fas fa-calendar",
            "liveclassattendance" => "fas fa-clipboard-list",
            "recordings" => "fas fa-film",
            "books" => "fas fa-book-open",
            "bundles" => "fas fa-box",
            "contentdrip" => "fas fa-clock",
            "resources" => "fas fa-archive",
            "documents" => "fas fa-file-alt",
            "medialibrary" => "fas fa-image",
            "courseinstructors" => "fas fa-user-plus",
            "proctoring" => "fas fa-eye",
            "coupons" => "fas fa-tags",
            "flashsales" => "fas fa-bolt",
            "affiliates" => "fas fa-link",
            "help" => "fas fa-life-ring",
            "progress" => "fas fa-chart-bar",
            _ => "fas fa-circle"
        };
    }

    private static string GetActionTitle(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "index" => "",
            "create" => "إنشاء جديد",
            "edit" => "تعديل",
            "details" => "التفاصيل",
            "delete" => "حذف",
            "preview" => "معاينة",
            "start" => "بدء",
            "results" => "النتائج",
            "settings" => "الإعدادات",
            "analytics" => "التحليلات",
            "students" => "الطلاب",
            "content" => "المحتوى",
            "pricing" => "التسعير",
            "publish" => "نشر",
            _ => action
        };
    }

    private static List<MenuItem> GetStudentMenuItems(string? currentController)
    {
        return new List<MenuItem>
        {
            new() { Title = "لوحة التحكم", Url = "/Student/Dashboard", Icon = "fas fa-tachometer-alt", Controller = "Dashboard" },
            new() { Title = "دوراتي", Url = "/Student/Courses", Icon = "fas fa-book", Controller = "Courses" },
            new() { Title = "الاختبارات", Url = "/Student/Quizzes", Icon = "fas fa-question-circle", Controller = "Quizzes" },
            new() { Title = "التكليفات", Url = "/Student/Assignments", Icon = "fas fa-file-alt", Controller = "Assignments" },
            new() { Title = "الشهادات", Url = "/Student/Certificates", Icon = "fas fa-certificate", Controller = "Certificates" },
            new() { Title = "الإنجازات", Url = "/Student/Achievements", Icon = "fas fa-trophy", Controller = "Achievements" },
            new() { Title = "المفضلة", Url = "/Student/Courses/Wishlist", Icon = "fas fa-heart", Controller = "Wishlist" },
            new() { Title = "الملف الشخصي", Url = "/Student/Profile", Icon = "fas fa-user", Controller = "Profile" },
        };
    }

    private static List<MenuItem> GetInstructorMenuItems(string? currentController)
    {
        return new List<MenuItem>
        {
            // Main
            new() { Title = "لوحة التحكم", Url = "/Instructor/Dashboard", Icon = "fas fa-tachometer-alt", Controller = "Dashboard" },
            // Courses & Content
            new() { Title = "دوراتي", Url = "/Instructor/Courses", Icon = "fas fa-book", Controller = "Courses" },
            new() { Title = "البث المباشر", Url = "/Instructor/LiveClasses", Icon = "fas fa-video", Controller = "LiveClasses" },
            new() { Title = "الوحدات والدروس", Url = "/Instructor/Modules", Icon = "fas fa-layer-group", Controller = "Modules" },
            new() { Title = "الاختبارات وبنك الأسئلة", Url = "/Instructor/QuestionBank", Icon = "fas fa-question-circle", Controller = "QuestionBank" },
            new() { Title = "الواجبات", Url = "/Instructor/Assignments", Icon = "fas fa-file-alt", Controller = "Assignments" },
            new() { Title = "كتبي", Url = "/Instructor/Books", Icon = "fas fa-book-open", Controller = "Books" },
            new() { Title = "جدولة المحتوى", Url = "/Instructor/ContentDrip", Icon = "fas fa-clock", Controller = "ContentDrip" },
            new() { Title = "مسارات التعلم", Url = "/Instructor/LearningPaths", Icon = "fas fa-route", Controller = "LearningPaths" },
            new() { Title = "الباقات", Url = "/Instructor/Bundles", Icon = "fas fa-box", Controller = "Bundles" },
            new() { Title = "الموارد والملفات", Url = "/Instructor/Resources", Icon = "fas fa-archive", Controller = "Resources" },
            // Students & Engagement
            new() { Title = "الطلاب", Url = "/Instructor/Students", Icon = "fas fa-users", Controller = "Students" },
            new() { Title = "الإجابات", Url = "/Instructor/Submissions", Icon = "fas fa-inbox", Controller = "Submissions" },
            new() { Title = "الرسائل", Url = "/Instructor/Messages", Icon = "fas fa-envelope", Controller = "Messages" },
            new() { Title = "المناقشات", Url = "/Instructor/Discussions", Icon = "fas fa-comments", Controller = "Discussions" },
            new() { Title = "التقييمات والأسئلة", Url = "/Instructor/Reviews", Icon = "fas fa-star", Controller = "Reviews" },
            new() { Title = "الإعلانات", Url = "/Instructor/Announcements", Icon = "fas fa-bullhorn", Controller = "Announcements" },
            // Management & Analytics
            new() { Title = "المدرسين المشاركين", Url = "/Instructor/CourseInstructors", Icon = "fas fa-user-plus", Controller = "CourseInstructors" },
            new() { Title = "مراقبة الاختبارات", Url = "/Instructor/Proctoring", Icon = "fas fa-eye", Controller = "Proctoring" },
            new() { Title = "التحليلات", Url = "/Instructor/Analytics", Icon = "fas fa-chart-line", Controller = "Analytics" },
            new() { Title = "الأرباح", Url = "/Instructor/Earnings", Icon = "fas fa-dollar-sign", Controller = "Earnings" },
            new() { Title = "العروض والكوبونات", Url = "/Instructor/Coupons", Icon = "fas fa-tags", Controller = "Coupons" },
            // Settings
            new() { Title = "الملف الشخصي", Url = "/Instructor/Profile", Icon = "fas fa-user", Controller = "Profile" },
            new() { Title = "مركز المساعدة", Url = "/Instructor/Help", Icon = "fas fa-life-ring", Controller = "Help" },
            new() { Title = "الإعدادات", Url = "/Instructor/Settings", Icon = "fas fa-cog", Controller = "Settings" },
        };
    }

    private static List<MenuItem> GetAdminMenuItems(string? currentController)
    {
        return new List<MenuItem>
        {
            new() { Title = "لوحة التحكم", Url = "/Admin/Dashboard", Icon = "fas fa-tachometer-alt", Controller = "Dashboard" },
            new() { Title = "المستخدمين", Url = "/Admin/Users", Icon = "fas fa-users", Controller = "Users" },
            new() { Title = "الدورات", Url = "/Admin/Courses", Icon = "fas fa-book", Controller = "Courses" },
            new() { Title = "التصنيفات", Url = "/Admin/Categories", Icon = "fas fa-folder", Controller = "Categories" },
            new() { Title = "المدفوعات", Url = "/Admin/Payments", Icon = "fas fa-credit-card", Controller = "Payments" },
            new() { Title = "التقارير", Url = "/Admin/Reports", Icon = "fas fa-chart-bar", Controller = "Reports" },
            new() { Title = "الإعدادات", Url = "/Admin/Settings", Icon = "fas fa-cog", Controller = "Settings" },
        };
    }

    #endregion
}

#region DTOs

public class BreadcrumbItem
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
}

public class MenuItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Controller { get; set; }
    public bool IsActive { get; set; }
    public List<MenuItem>? SubItems { get; set; }
    public string? Badge { get; set; }
    public string? BadgeClass { get; set; }
}

#endregion

