using System.Security.Cryptography;
using System.Text;

namespace SustainablePS.Core.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "PBKDF2";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            Prefix,
            Iterations,
            Convert.ToHexString(salt),
            Convert.ToHexString(hash));
    }

    public static bool Verify(string password, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (!expectedHash.StartsWith($"{Prefix}$", StringComparison.Ordinal))
        {
            return VerifyLegacySha256(password, expectedHash);
        }

        var parts = expectedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromHexString(parts[2]);
        var expectedBytes = Convert.FromHexString(parts[3]);
        var actualBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedBytes.Length);

        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static bool VerifyLegacySha256(string password, string expectedHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var actualHash = Convert.ToHexString(bytes);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }
}
