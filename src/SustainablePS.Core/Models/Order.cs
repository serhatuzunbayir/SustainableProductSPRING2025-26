namespace SustainablePS.Core.Models;

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid CustomerId { get; init; }
    public required string TransactionId { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Paid;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public List<OrderItem> Items { get; init; } = [];

    public decimal TotalPrice => Items.Sum(item => item.LineTotal);
    public double TotalCarbonKg => Items.Sum(item => item.LineCarbonKg);
}

public sealed class OrderItem
{
    public required Guid ProductId { get; init; }
    public required Guid MerchantId { get; init; }
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required double CarbonKgPerUnit { get; init; }

    public decimal LineTotal => UnitPrice * Quantity;
    public double LineCarbonKg => CarbonKgPerUnit * Quantity;
}
