namespace Bizcord.NotificationService.Domain.Preferences;

/// <summary>The port used to read and store a user's notification preferences.</summary>
public interface INotificationPreferenceRepository
{
    /// <summary>Returns the stored preferences, or <c>null</c> when the user never changed them.</summary>
    Task<NotificationPreference?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}
