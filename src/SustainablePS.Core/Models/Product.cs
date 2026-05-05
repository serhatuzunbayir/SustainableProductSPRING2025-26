namespace SustainablePS.Core.Models;

public sealed class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid MerchantId { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public double CarbonKgPerUnit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

public sealed record ProductDraft(
    string Name,
    string Description,
    string Category,
    decimal Price,
    int StockQuantity,
    double CarbonKgPerUnit)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Product name is required.", nameof(Name));
        }

        if (Price <= 0)
        {
            throw new ArgumentException("Product price must be greater than zero.", nameof(Price));
        }

        if (StockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(StockQuantity));
        }

        if (CarbonKgPerUnit < 0)
        {
            throw new ArgumentException("Carbon footprint cannot be negative.", nameof(CarbonKgPerUnit));
        }
    }
}
