namespace SustainablePS.Core.Models;

/// <summary>Carbon total for a single calendar month.</summary>
public sealed record MonthlyCarbonPoint(
    int Year,
    int Month,
    double CarbonKg)
{
    /// <summary>Human-readable label in YYYY-MM format.</summary>
    public string Label => $"{Year}-{Month:00}";
}

/// <summary>Aggregated carbon footprint summary for a customer's order history.</summary>
public sealed record CarbonSummary(
    int OrderCount,
    double TotalCarbonKg,
    double AverageCarbonKgPerOrder,
    IReadOnlyList<MonthlyCarbonPoint> MonthlyTotals);

/// <summary>Carbon and sales stats for a single product in the impact report.</summary>
public sealed record ProductImpactStat(
    Guid ProductId,
    string ProductName,
    string Category,
    int UnitsPurchased,
    double CarbonKgPerUnit,
    double TotalCarbonKg);

/// <summary>Top-5 rankings by carbon and by units sold across the catalog.</summary>
public sealed record ImpactReport(
    IReadOnlyList<ProductImpactStat> HighestCarbonProducts,
    IReadOnlyList<ProductImpactStat> LowestCarbonProducts,
    IReadOnlyList<ProductImpactStat> MostPurchasedProducts);
