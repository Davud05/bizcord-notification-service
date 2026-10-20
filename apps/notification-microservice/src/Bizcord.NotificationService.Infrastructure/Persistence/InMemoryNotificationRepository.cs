using System.Collections.Concurrent;
using Bizcord.NotificationService.Domain.Notifications;

namespace Bizcord.NotificationService.Infrastructure.Persistence;

/// <summary>
/// Thread-safe in-memory store for notifications.
///
/// The service is deliberately shipped with one deployable unit and no database: the
/// repository is a port, so moving to PostgreSQL or MongoDB later means adding one class
/// here and one line in <see cref="InfrastructureServiceCollectionExtensions"/> - no
/// domain, application or API code changes. See <c>docs/08-architecture-decisions.md</c>.
/// </summary>
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<Guid, Notification> _notifications = new();

    public Task<Notification?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _notifications.TryGetValue(id, out var notification);

        return Task.FromResult(notification);
    }

    public Task<IReadOnlyList<Notification>> ListAsync(
        NotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<Notification> notifications = _notifications.Values;

        if (query.RecipientId.HasValue)
        {
            notifications = notifications.Where(n => n.Recipient.UserId == query.RecipientId.Value);
        }

        if (query.ChannelId.HasValue)
        {
            notifications = notifications.Where(n => n.ChannelId == query.ChannelId.Value);
        }

        if (query.Status.HasValue)
        {
            notifications = notifications.Where(n => n.Status == query.Status.Value);
        }

        IReadOnlyList<Notification> result =
        [
            .. notifications
                .OrderByDescending(n => n.CreatedAt)
                .ThenBy(n => n.Id)
                .Skip(query.Skip)
                .Take(query.Take)
        ];

        return Task.FromResult(result);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_notifications.Values.Count(n => n.Recipient.UserId == recipientId && n.IsUnread));

    public Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _notifications[notification.Id] = notification;

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_notifications.TryRemove(id, out _));
}
