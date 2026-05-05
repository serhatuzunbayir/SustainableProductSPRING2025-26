using SustainablePS.Core.Services;

namespace SustainablePS.Maui;

public static class AppServices
{
    public static MarketplaceService Marketplace { get; } = new();
}
