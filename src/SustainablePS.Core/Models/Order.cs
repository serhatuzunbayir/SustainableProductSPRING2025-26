using System.ComponentModel.DataAnnotations.Schema;

namespace SustainablePS.Core.Models;

/// <summary>
/// Represents a customer purchase order containing one or more line items.
/// </summary>
public sealed class Order
{
    /// <summary>Unique identifier for this order.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The customer who placed this order.</summary>
    public required Guid CustomerId { get; init; }

    /// <summary>Payment gateway transaction reference.</summary>
    public required string TransactionId { get; init; }

    /// <summary>Current fulfillment status of the order.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;

    /// <summary>Current payment status of the order.</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Paid;

    /// <summary>UTC timestamp when the order was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Line items that make up this order.</summary>
    public List<OrderItem> Items { get; init; } = [];

    /// <summary>Sum of all line totals; computed in memory, not stored in DB.</summary>
    [NotMapped]
    public decimal TotalPrice => Items.Sum(item => item.LineTotal);

    /// <summary>Total carbon footprint in kg CO2e; computed in memory, not stored in DB.</summary>
    [NotMapped]
    public double TotalCarbonKg => Items.Sum(item => item.LineCarbonKg);
}

/// <summary>
/// A single product line inside an <see cref="Order"/>.
/// </summary>
public sealed class OrderItem
{
    /// <summary>Unique identifier for this line item.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Foreign key linking this item to its parent order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The product that was purchased.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The merchant who owns this product.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Snapshot of the product name at time of purchase.</summary>
    public required string ProductName { get; init; }

    /// <summary>Unit price at time of purchase.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Number of units purchased.</summary>
    public required int Quantity { get; init; }

    /// <summary>Carbon footprint per unit in kg CO2e at time of purchase.</summary>
    public required double CarbonKgPerUnit { get; init; }

    /// <summary>Total price for this line; computed in memory, not stored in DB.</summary>
    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>Total carbon for this line; computed in memory, not stored in DB.</summary>
    [NotMapped]
    public double LineCarbonKg => CarbonKgPerUnit * Quantity;
}
