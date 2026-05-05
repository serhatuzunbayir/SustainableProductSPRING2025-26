using SustainablePS.Core.Models;

namespace SustainablePS.Core.Services;

public sealed class MockPaymentProcessor
{
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
