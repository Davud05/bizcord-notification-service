namespace Bizcord.Shared.Contracts.Events;

/// <summary>
/// Published by the Notification service when a recipient reads a notification.
/// Consumed by e.g. Presence and Analytics.
/// </summary>
public class NotificationReadEvent
{
    public Guid NotificationId { get; set; }

    public Guid RecipientId { get; set; }

    /// <summary>Id of the source message, when the notification originated from one.</summary>
    public Guid? MessageId { get; set; }

    public DateTime ReadAt { get; set; }
}
