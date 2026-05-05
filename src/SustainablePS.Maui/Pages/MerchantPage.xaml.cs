using System.Globalization;
using SustainablePS.Core.Models;
using SustainablePS.Core.Services;

namespace SustainablePS.Maui.Pages;

public partial class MerchantPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private UserAccount? _merchant;
    private Guid? _editingProductId;

    public MerchantPage()
    {
        InitializeComponent();
        _marketplace = AppServices.Marketplace;
        LoginEmailEntry.Text = MarketplaceService.DemoMerchantEmail;
        LoginPasswordEntry.Text = MarketplaceService.DemoPassword;
        Refresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var isLoggedIn = _merchant is not null;
        LoginPanel.IsVisible = !isLoggedIn;
        ConsolePanel.IsVisible = isLoggedIn;
        LogoutButton.IsVisible = isLoggedIn;
        RefreshButton.IsVisible = isLoggedIn;
        SubtitleLabel.Text = isLoggedIn
            ? $"Signed in as {_merchant!.Email}"
            : "Manage products, stock, orders, and merchant notifications.";

        if (!isLoggedIn)
        {
            return;
        }

        var products = _marketplace.Products
            .Where(product => product.MerchantId == _merchant!.Id)
            .OrderBy(product => product.Name)
            .ToList();
        var orders = _marketplace.GetMerchantOrders(_merchant!.Id);
        var notifications = _marketplace.GetNotifications(_merchant!.Id);
        var unreadCount = notifications.Count(notification => !notification.IsRead);

        MerchantProductsView.ItemsSource = products;
        MerchantOrdersView.ItemsSource = orders;
        MerchantNotificationsView.ItemsSource = notifications;
        ProductCountLabel.Text = $"{products.Count} active product(s)";
        OrderCountLabel.Text = $"{orders.Count} order(s) from customer checkout";
        NotificationCountLabel.Text = $"{unreadCount} unread notification(s)";
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        try
        {
            var user = _marketplace.Login(LoginEmailEntry.Text ?? string.Empty, LoginPasswordEntry.Text ?? string.Empty);
            if (user.Role != UserRole.Merchant)
            {
                await DisplayAlertAsync("Login", "Merchant access is required for this desktop console.", "OK");
                return;
            }

            _merchant = user;
            ClearProductForm();
            Refresh();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Login", ex.Message, "OK");
        }
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        _merchant = null;
        ClearProductForm();
        Refresh();
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        Refresh();
    }

    private async void OnSaveProductClicked(object sender, EventArgs e)
    {
        if (_merchant is null)
        {
            return;
        }

        try
        {
            var draft = ReadProductDraft();
            if (_editingProductId is null)
            {
                _marketplace.AddProduct(_merchant.Id, draft);
            }
            else
            {
                _marketplace.UpdateProduct(_merchant.Id, _editingProductId.Value, draft);
            }

            ClearProductForm();
            Refresh();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Product", ex.Message, "OK");
        }
    }

    private void OnEditProductClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not Product product)
        {
            return;
        }

        _editingProductId = product.Id;
        EditorTitleLabel.Text = "Edit Product";
        EditorHintLabel.Text = "Update catalog details, stock, and carbon value.";
        SaveProductButton.Text = "Save Changes";
        DeleteSelectedProductButton.IsVisible = true;
        NameEntry.Text = product.Name;
        CategoryEntry.Text = product.Category;
        DescriptionEntry.Text = product.Description;
        PriceEntry.Text = product.Price.ToString("0.00", CultureInfo.InvariantCulture);
        StockEntry.Text = product.StockQuantity.ToString(CultureInfo.InvariantCulture);
        CarbonEntry.Text = product.CarbonKgPerUnit.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private async void OnDeleteSelectedProductClicked(object sender, EventArgs e)
    {
        if (_merchant is null || _editingProductId is null)
        {
            return;
        }

        var confirm = await DisplayAlertAsync("Delete Product", "Remove this product from the active catalog?", "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        _marketplace.DeleteProduct(_merchant.Id, _editingProductId.Value);
        ClearProductForm();
        Refresh();
    }

    private void OnClearProductFormClicked(object sender, EventArgs e)
    {
        ClearProductForm();
    }

    private void OnDecreaseStockClicked(object sender, EventArgs e)
    {
        if (_merchant is not null && (sender as Button)?.BindingContext is Product product)
        {
            _marketplace.UpdateStock(_merchant.Id, product.Id, Math.Max(0, product.StockQuantity - 1));
            Refresh();
        }
    }

    private void OnIncreaseStockClicked(object sender, EventArgs e)
    {
        if (_merchant is not null && (sender as Button)?.BindingContext is Product product)
        {
            _marketplace.UpdateStock(_merchant.Id, product.Id, product.StockQuantity + 1);
            Refresh();
        }
    }

    private void OnAdvanceOrderStatusClicked(object sender, EventArgs e)
    {
        if (_merchant is null || (sender as Button)?.BindingContext is not Order order)
        {
            return;
        }

        _marketplace.UpdateOrderStatus(_merchant.Id, order.Id, GetNextStatus(order.Status));
        Refresh();
    }

    private void OnMarkNotificationsReadClicked(object sender, EventArgs e)
    {
        if (_merchant is null)
        {
            return;
        }

        _marketplace.MarkAllNotificationsRead(_merchant.Id);
        Refresh();
    }

    private ProductDraft ReadProductDraft()
    {
        return new ProductDraft(
            NameEntry.Text ?? string.Empty,
            DescriptionEntry.Text ?? string.Empty,
            string.IsNullOrWhiteSpace(CategoryEntry.Text) ? "General" : CategoryEntry.Text,
            ParseDecimal(PriceEntry.Text, "price"),
            ParseInt(StockEntry.Text, "stock"),
            ParseDouble(CarbonEntry.Text, "carbon footprint"));
    }

    private void ClearProductForm()
    {
        _editingProductId = null;
        EditorTitleLabel.Text = "Add Product";
        EditorHintLabel.Text = "Create a product with price, stock, and carbon data.";
        SaveProductButton.Text = "Add Product";
        DeleteSelectedProductButton.IsVisible = false;
        NameEntry.Text = string.Empty;
        CategoryEntry.Text = string.Empty;
        DescriptionEntry.Text = string.Empty;
        PriceEntry.Text = string.Empty;
        StockEntry.Text = string.Empty;
        CarbonEntry.Text = string.Empty;
    }

    private static OrderStatus GetNextStatus(OrderStatus current)
    {
        return current switch
        {
            OrderStatus.Confirmed => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.Shipped,
            OrderStatus.Shipped => OrderStatus.Delivered,
            OrderStatus.Delivered => OrderStatus.Delivered,
            OrderStatus.Cancelled => OrderStatus.Cancelled,
            _ => OrderStatus.Preparing
        };
    }

    private static decimal ParseDecimal(string? value, string fieldName)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentValue) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out currentValue))
        {
            return currentValue;
        }

        throw new ArgumentException($"Enter a valid {fieldName}.");
    }

    private static double ParseDouble(string? value, string fieldName)
    {
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentValue) ||
            double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out currentValue))
        {
            return currentValue;
        }

        throw new ArgumentException($"Enter a valid {fieldName}.");
    }

    private static int ParseInt(string? value, string fieldName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Enter a valid {fieldName}.");
    }
}
