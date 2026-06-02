using SustainablePS.Core.Models;

namespace SustainablePS.Core.Services;

/// <summary>Simulates a payment gateway; declines cards ending in 0000.</summary>
public sealed class MockPaymentProcessor
{
    /// <summary>Charges the given amount. Returns success/failure with a transaction ID.</summary>
    public PaymentResult Charge(PaymentDetails details, decimal amount)
    {
        var cardDigits = new string(details.CardNumber.Where(char.IsDigit).ToArray());

        if (amount <= 0)
        {
            return new PaymentResult(false, "Payment amount must be greater than zero.", null);
        }

        if (cardDigits.Length < 12)
        {
            return new PaymentResult(false, "Card number is not valid.", null);
        }

        if (cardDigits.EndsWith("0000", StringComparison.Ordinal))
        {
            return new PaymentResult(false, "Bank declined the transaction.", null);
        }

        return new PaymentResult(true, "Payment approved.", $"TX-{Guid.NewGuid():N}"[..11].ToUpperInvariant());
    }
}
