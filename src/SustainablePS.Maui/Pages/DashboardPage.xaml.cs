using SustainablePS.Core.Services;

namespace SustainablePS.Maui.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly MarketplaceService _marketplace;

    public DashboardPage()
    {
        InitializeComponent();
        _marketplace = AppServices.Marketplace;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var customer = _marketplace.GetUserByEmail(MarketplaceService.DemoCustomerEmail);
        var summary = _marketplace.GetCustomerCarbonSummary(customer.Id);
        TotalCarbonLabel.Text = $"{summary.TotalCarbonKg:F2} kg";
        OrderCountLabel.Text = summary.OrderCount.ToString();
        MonthlyView.ItemsSource = summary.MonthlyTotals;
        HistoryView.ItemsSource = _marketplace.GetCustomerOrders(customer.Id);
    }
}
