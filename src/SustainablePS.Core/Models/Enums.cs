namespace SustainablePS.Core.Models;

public enum UserRole
{
    Customer,
    Merchant
}

public enum OrderStatus
{
    Confirmed,
    Preparing,
    Shipped,
    Delivered,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded
}

public enum NotificationType
{
    OrderConfirmation,
    ShippingUpdate,
    StockAlert
}
