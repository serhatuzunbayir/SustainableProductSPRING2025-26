namespace SustainablePS.Core.Models;

/// <summary>
/// Per-category aggregate computed via LINQ GroupBy in MarketplaceService.
/// </summary>
public sealed record CategoryStat(
    string Category,
    int ProductCount,
    double AverageCarbonKgPerUnit,
    int TotalStock);

/// <summary>
/// Revenue and carbon totals per merchant, derived from paid order items via LINQ.
/// </summary>
public sealed record MerchantRevenueStat(
    Guid MerchantId,
    string MerchantName,
    decimal TotalRevenue,
    double TotalCarbonKg,
    int ItemsSold);

/// <summary>
/// A product whose stock has dropped to or below the low-stock threshold.
/// </summary>
public sealed record LowStockAlert(
    Guid ProductId,
    string ProductName,
    int CurrentStock);

/// <summary>
/// Full analytics snapshot returned by <see cref="SustainablePS.Core.Services.MarketplaceService.GetCatalogAnalytics"/>.
/// </summary>
public sealed record CatalogAnalytics(
    IReadOnlyList<CategoryStat> CategoryStats,
    IReadOnlyList<MerchantRevenueStat> MerchantRevenueStats,
    IReadOnlyList<LowStockAlert> LowStockAlerts);
