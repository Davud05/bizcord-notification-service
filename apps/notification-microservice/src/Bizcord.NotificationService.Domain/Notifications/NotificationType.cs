namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>Why a notification was created.</summary>
public enum NotificationType
{
    Mention = 0,
    ChannelAnnouncement = 1,
    DirectMessage = 2,
    System = 3
}
