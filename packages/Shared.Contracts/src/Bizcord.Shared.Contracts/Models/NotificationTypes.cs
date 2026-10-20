namespace Bizcord.Shared.Contracts.Models;

/// <summary>
/// The notification types that are part of the public contract. They are exposed as
/// strings so that adding a new internal type never breaks a consumer's deserialization.
/// </summary>
public static class NotificationTypes
{
    public const string Mention = "Mention";
    public const string ChannelAnnouncement = "ChannelAnnouncement";
    public const string DirectMessage = "DirectMessage";
    public const string System = "System";
}

/// <summary>
/// The lifecycle states of a notification that consumers can observe.
/// </summary>
public static class NotificationStatuses
{
    public const string Unread = "Unread";
    public const string Read = "Read";
    public const string Archived = "Archived";
}
