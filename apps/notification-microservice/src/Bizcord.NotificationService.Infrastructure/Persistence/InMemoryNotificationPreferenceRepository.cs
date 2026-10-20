using System.Collections.Concurrent;
using Bizcord.NotificationService.Domain.Preferences;

namespace Bizcord.NotificationService.Infrastructure.Persistence;

/// <summary>Thread-safe in-memory store for notification preferences.</summary>
public sealed class InMemoryNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly ConcurrentDictionary<Guid, NotificationPreference> _preferences = new();

    public Task<NotificationPreference?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _preferences.TryGetValue(userId, out var preference);

        return Task.FromResult(preference);
    }

    public Task SaveAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference);

        _preferences[preference.UserId] = preference;

        return Task.CompletedTask;
    }
}
