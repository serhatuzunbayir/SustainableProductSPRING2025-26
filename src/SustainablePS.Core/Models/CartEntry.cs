namespace SustainablePS.Core.Models;

/// <summary>
/// Persisted shopping cart entry stored in the SQLite database.
/// One row per customer+product combination; quantity is updated in place.
/// </summary>
public sealed class CartEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The customer who owns this cart entry.</summary>
    public required Guid CustomerId { get; set; }

    /// <summary>The product added to the cart.</summary>
    public required Guid ProductId { get; set; }

    /// <summary>Number of units in the cart; must be greater than zero.</summary>
    public required int Quantity { get; set; }
}
