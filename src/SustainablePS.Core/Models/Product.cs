namespace SustainablePS.Core.Models;

/// <summary>An eco-friendly product listed in the marketplace catalog.</summary>
public sealed class Product
{
    /// <summary>Unique product identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The merchant who owns this product.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Display name shown in the catalog.</summary>
    public required string Name { get; set; }

    /// <summary>Full description of the product.</summary>
    public required string Description { get; set; }

    /// <summary>Catalog category (e.g. "Personal Care", "Electronics").</summary>
    public required string Category { get; set; }

    /// <summary>Selling price in the store currency.</summary>
    public decimal Price { get; set; }

    /// <summary>Available units in stock; zero means out of stock.</summary>
    public int StockQuantity { get; set; }

    /// <summary>Carbon footprint per unit in kg CO2e.</summary>
    public double CarbonKgPerUnit { get; set; }

    /// <summary>False when the product has been soft-deleted by the merchant.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the product was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>Validated input data used when creating or updating a product.</summary>
public sealed record ProductDraft(
    string Name,
    string Description,
    string Category,
    decimal Price,
    int StockQuantity,
    double CarbonKgPerUnit)
{
    /// <summary>Throws <see cref="ArgumentException"/> if any field is invalid.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Product name is required.", nameof(Name));
        if (Price <= 0)
            throw new ArgumentException("Product price must be greater than zero.", nameof(Price));
        if (StockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(StockQuantity));
        if (CarbonKgPerUnit < 0)
            throw new ArgumentException("Carbon footprint cannot be negative.", nameof(CarbonKgPerUnit));
    }
}
