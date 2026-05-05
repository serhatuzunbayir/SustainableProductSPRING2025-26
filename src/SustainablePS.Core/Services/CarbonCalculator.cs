using SustainablePS.Core.Models;

namespace SustainablePS.Core.Services;

public sealed class CarbonCalculator
{
    public double CalculateCartCarbon(IEnumerable<CartLine> lines)
    {
        return Round(lines.Sum(line => line.LineCarbonKg));
    }

    public double CalculateOrderCarbon(Order order)
    {
        return Round(order.TotalCarbonKg);
    }

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
