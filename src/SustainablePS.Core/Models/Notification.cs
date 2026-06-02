namespace SustainablePS.Core.Models;

/// <summary>An in-app notification delivered to a customer or merchant.</summary>
public sealed class Notification
{
    /// <summary>Unique notification identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The user this notification belongs to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Category of this notification (order, shipping, or stock).</summary>
    public required NotificationType Type { get; init; }

    /// <summary>Short headline displayed in the notification list.</summary>
    public required string Title { get; init; }

    /// <summary>Full notification body text.</summary>
    public required string Message { get; init; }

    /// <summary>True once the user has marked the notification as read.</summary>
    public bool IsRead { get; set; }

    /// <summary>UTC timestamp when the notification was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
