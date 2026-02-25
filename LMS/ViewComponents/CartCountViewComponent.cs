using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.ViewComponents;

/// <summary>
/// مكون عرض عدد عناصر السلة - Cart Count View Component
/// Displays the number of items in the shopping cart
/// </summary>
public class CartCountViewComponent : ViewComponent
{
    private readonly IShoppingCartService _cartService;
    private readonly ICurrentUserService _currentUserService;

    public CartCountViewComponent(
        IShoppingCartService cartService,
        ICurrentUserService currentUserService)
    {
        _cartService = cartService;
        _currentUserService = currentUserService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new CartCountViewModel();

        if (!_currentUserService.IsAuthenticated || string.IsNullOrEmpty(_currentUserService.UserId))
        {
            return View(model);
        }

        try
        {
            var cart = await _cartService.GetCartAsync(_currentUserService.UserId);
            if (cart != null)
            {
                model.ItemCount = cart.Items.Count;
                model.TotalAmount = cart.TotalAmount;
                model.HasItems = cart.Items.Any();
                model.Currency = "EGP";
            }
        }
        catch
        {
            // Silently fail - don't break the page for cart issues
        }

        return View(model);
    }
}

public class CartCountViewModel
{
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool HasItems { get; set; }
    public string Currency { get; set; } = "EGP";
}

