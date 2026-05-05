namespace SustainablePS.Core.Models;

public sealed record PaymentDetails(
    string CardNumber,
    string CardHolderName,
    string ExpiryMonth,
    string ExpiryYear,
    string Cvv);

public sealed record PaymentResult(
    bool Success,
    string Message,
    string? TransactionId);

public sealed record CheckoutResult(
    bool Success,
    string Message,
    Order? Order,
    string? TransactionId);
