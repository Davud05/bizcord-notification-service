namespace Bizcord.Shared.Contracts.Events;

/// <summary>
/// Published by the Notification service when a posted message mentions a user that
/// is allowed to be notified. <see cref="MessageId"/> is carried over from
/// <see cref="MessagePostedEvent"/> so a message can be traced through the whole system.
/// </summary>
public class MentionDetectedEvent
{
    /// <summary>Id of the message that caused the mention. Used for traceability.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Id of the notification that was created for the mention.</summary>
    public Guid NotificationId { get; set; }

    public Guid ChannelId { get; set; }

    /// <summary>The user that wrote the message.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>The user that was mentioned and will be notified.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>How the user was mentioned: <c>User</c>, <c>Here</c> or <c>Everyone</c>.</summary>
    public required string MentionKind { get; set; }

    /// <summary>Short, human readable preview of the message content.</summary>
    public required string Preview { get; set; }

    public DateTime ProcessedAt { get; set; }
}
