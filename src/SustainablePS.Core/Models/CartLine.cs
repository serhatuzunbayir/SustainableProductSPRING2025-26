namespace SustainablePS.Core.Models;

/// <summary>A product line inside the customer's in-memory shopping cart.</summary>
public sealed record CartLine(
    Product Product,
    int Quantity)
{
    /// <summary>Price * Quantity for this line.</summary>
    public decimal LineTotal => Product.Price * Quantity;

    /// <summary>Carbon footprint in kg CO2e for this line.</summary>
    public double LineCarbonKg => Product.CarbonKgPerUnit * Quantity;
}
