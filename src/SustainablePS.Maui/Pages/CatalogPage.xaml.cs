using SustainablePS.Core.Models;
using SustainablePS.Core.Services;

namespace SustainablePS.Maui.Pages;

public partial class CatalogPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly UserAccount _customer;

    public CatalogPage()
    {
        InitializeComponent();
        _marketplace = AppServices.Marketplace;
        _customer = _marketplace.GetUserByEmail(MarketplaceService.DemoCustomerEmail);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        ProductsView.ItemsSource = _marketplace.Products;
        var cart = _marketplace.GetCart(_customer.Id);
        CartCountLabel.Text = $"{cart.Sum(line => line.Quantity)} item(s) in cart";
        CartCarbonLabel.Text = $"{_marketplace.GetCartCarbon(_customer.Id):F2} kg CO2e in current cart";
    }

    private async void OnAddToCartClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not Product product)
        {
            return;
        }

        try
        {
            _marketplace.AddToCart(_customer.Id, product.Id, 1);
            Refresh();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Cart", ex.Message, "OK");
        }
    }

    private async void OnViewCartClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//cart");
    }
}
