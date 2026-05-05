namespace SustainablePS.Core.Models;

public sealed class UserAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public required UserRole Role { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
