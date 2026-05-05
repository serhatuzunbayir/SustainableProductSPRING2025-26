using SustainablePS.Core.Models;
using SustainablePS.Core.Services;

namespace SustainablePS.Maui.Pages;

public partial class CartPage : ContentPage
{
    private readonly MarketplaceService _marketplace;
    private readonly UserAccount _customer;

    public CartPage()
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
        var cart = _marketplace.GetCart(_customer.Id);
        CartView.ItemsSource = cart;
        TotalLabel.Text = $"${cart.Sum(line => line.LineTotal):F2}";
        CarbonLabel.Text = $"{cart.Sum(line => line.LineCarbonKg):F2} kg CO2e";
    }

    private async void OnCheckoutClicked(object sender, EventArgs e)
    {
        var result = _marketplace.Checkout(
            _customer.Id,
            new PaymentDetails("4242424242424242", "Demo Customer", "12", "30", "123"));

        Refresh();
        await DisplayAlertAsync("Checkout", result.Message, "OK");

        if (result.Success)
        {
            await Shell.Current.GoToAsync("//dashboard");
        }
    }
}
