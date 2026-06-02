namespace SustainablePS.Core.Models;

/// <summary>Determines whether an account belongs to a shopper or a seller.</summary>
public enum UserRole
{
    /// <summary>A customer who can browse and purchase products.</summary>
    Customer,
    /// <summary>A merchant who can add and manage products.</summary>
    Merchant
}

/// <summary>Tracks the fulfillment progress of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order has been confirmed and payment received.</summary>
    Confirmed,
    /// <summary>Merchant is preparing the shipment.</summary>
    Preparing,
    /// <summary>Order has been dispatched to the carrier.</summary>
    Shipped,
    /// <summary>Order has been delivered to the customer.</summary>
    Delivered,
    /// <summary>Order was cancelled before delivery.</summary>
    Cancelled
}

/// <summary>Tracks whether payment has been collected for an order.</summary>
public enum PaymentStatus
{
    /// <summary>Payment has not yet been processed.</summary>
    Pending,
    /// <summary>Payment was successfully charged.</summary>
    Paid,
    /// <summary>Payment attempt failed.</summary>
    Failed,
    /// <summary>Payment was refunded to the customer.</summary>
    Refunded
}

/// <summary>Classifies the kind of in-app notification sent to a user.</summary>
public enum NotificationType
{
    /// <summary>Sent to a customer when their order is confirmed.</summary>
    OrderConfirmation,
    /// <summary>Sent to a customer when their order status changes.</summary>
    ShippingUpdate,
    /// <summary>Sent to a merchant when a product stock is low or sold.</summary>
    StockAlert
}
