namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// The lifecycle of a notification: it is created unread, can be read once and can be
/// archived from any state. Archived is terminal.
/// </summary>
public enum NotificationStatus
{
    Unread = 0,
    Read = 1,
    Archived = 2
}
