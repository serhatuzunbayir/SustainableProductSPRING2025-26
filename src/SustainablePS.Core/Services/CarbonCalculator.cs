using SustainablePS.Core.Models;

namespace SustainablePS.Core.Services;

/// <summary>Calculates carbon footprints for carts, orders, and customer summaries.</summary>
public sealed class CarbonCalculator
{
    /// <summary>Sums carbon across all cart lines.</summary>
    public double CalculateCartCarbon(IEnumerable<CartLine> lines)
    {
        return Round(lines.Sum(line => line.LineCarbonKg));
    }

    /// <summary>Returns the total carbon for a single order.</summary>
    public double CalculateOrderCarbon(Order order)
    {
        return Round(order.TotalCarbonKg);
    }

    /// <summary>Builds a per-month carbon summary from a customer's paid order history.</summary>
    public CarbonSummary BuildCustomerSummary(IEnumerable<Order> orders)
    {
        var paidOrders = orders
            .Where(order => order.PaymentStatus == PaymentStatus.Paid)
            .OrderBy(order => order.CreatedAt)
            .ToList();

        var total = Round(paidOrders.Sum(order => order.TotalCarbonKg));
        var monthly = paidOrders
            .GroupBy(order => new { order.CreatedAt.Year, order.CreatedAt.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new MonthlyCarbonPoint(
                group.Key.Year,
                group.Key.Month,
                Round(group.Sum(order => order.TotalCarbonKg))))
            .ToList();

        return new CarbonSummary(
            paidOrders.Count,
            total,
            paidOrders.Count == 0 ? 0 : Round(total / paidOrders.Count),
            monthly);
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
