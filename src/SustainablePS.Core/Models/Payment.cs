namespace SustainablePS.Core.Models;

/// <summary>Card details submitted by a customer at checkout.</summary>
public sealed record PaymentDetails(
    string CardNumber,
    string CardHolderName,
    string ExpiryMonth,
    string ExpiryYear,
    string Cvv);

/// <summary>Result returned by the payment processor after attempting a charge.</summary>
public sealed record PaymentResult(
    bool Success,
    string Message,
    string? TransactionId);

/// <summary>Result returned from a checkout attempt, including the created order on success.</summary>
public sealed record CheckoutResult(
    bool Success,
    string Message,
    Order? Order,
    string? TransactionId);
