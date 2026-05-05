namespace SustainablePS.Core.Models;

public sealed record MonthlyCarbonPoint(
    int Year,
    int Month,
    double CarbonKg)
{
    public string Label => $"{Year}-{Month:00}";
}

public sealed record CarbonSummary(
    int OrderCount,
    double TotalCarbonKg,
    double AverageCarbonKgPerOrder,
    IReadOnlyList<MonthlyCarbonPoint> MonthlyTotals);

public sealed record ProductImpactStat(
    Guid ProductId,
    string ProductName,
    string Category,
    int UnitsPurchased,
    double CarbonKgPerUnit,
    double TotalCarbonKg);

public sealed record ImpactReport(
    IReadOnlyList<ProductImpactStat> HighestCarbonProducts,
    IReadOnlyList<ProductImpactStat> LowestCarbonProducts,
    IReadOnlyList<ProductImpactStat> MostPurchasedProducts);
