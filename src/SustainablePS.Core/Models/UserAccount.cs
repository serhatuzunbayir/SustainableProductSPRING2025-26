namespace SustainablePS.Core.Models;

/// <summary>A registered user — either a customer or a merchant.</summary>
public sealed class UserAccount
{
    /// <summary>Unique user identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>User's display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Unique email address used for login (stored lower-cased).</summary>
    public required string Email { get; init; }

    /// <summary>PBKDF2 password hash; never the plaintext password.</summary>
    public required string PasswordHash { get; init; }

    /// <summary>Whether this account belongs to a customer or merchant.</summary>
    public required UserRole Role { get; init; }

    /// <summary>UTC timestamp when the account was registered.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
