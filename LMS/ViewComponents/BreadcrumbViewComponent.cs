using LMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LMS.ViewComponents;

/// <summary>
/// Breadcrumb View Component
/// Generates navigation breadcrumbs based on current route
/// </summary>
public class BreadcrumbViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? entityName = null)
    {
        var area = ViewContext.RouteData.Values["area"]?.ToString() ?? "";
        var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";
        var action = ViewContext.RouteData.Values["action"]?.ToString() ?? "";

        var breadcrumbs = NavigationHelper.GetBreadcrumbs(area, controller, action, entityName);

        return View(breadcrumbs);
    }
}

