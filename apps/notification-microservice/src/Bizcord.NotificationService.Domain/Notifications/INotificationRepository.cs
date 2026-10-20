namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// The port the domain uses to persist notifications. The domain owns the contract,
/// infrastructure owns the implementation, which is what makes it possible to swap the
/// in-memory store for a real database without touching business logic.
/// </summary>
public interface INotificationRepository
{
    Task<Notification?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> ListAsync(NotificationQuery query, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates a notification.</summary>
    Task SaveAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary><c>true</c> when a notification was actually removed.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Filter for reading notifications. All criteria are optional and combined with AND.</summary>
public sealed record NotificationQuery
{
    public Guid? RecipientId { get; init; }

    public Guid? ChannelId { get; init; }

    public NotificationStatus? Status { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}
