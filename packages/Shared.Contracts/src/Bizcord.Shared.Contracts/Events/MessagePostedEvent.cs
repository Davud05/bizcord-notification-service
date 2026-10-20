namespace Bizcord.Shared.Contracts.Events;

/// <summary>
/// Published by the Messaging service every time a user posts a message in a channel.
/// This is the system-wide integration event that the Notification service reacts to.
/// </summary>
public class MessagePostedEvent
{
    public Guid MessageId { get; set; }

    public Guid ChannelId { get; set; }

    public Guid AuthorId { get; set; }

    public required string Content { get; set; }

    public DateTime PostedAt { get; set; }
}
