namespace SustainablePS.Core.Models;

public sealed record CartLine(
    Product Product,
    int Quantity)
{
    public decimal LineTotal => Product.Price * Quantity;
    public double LineCarbonKg => Product.CarbonKgPerUnit * Quantity;
}
