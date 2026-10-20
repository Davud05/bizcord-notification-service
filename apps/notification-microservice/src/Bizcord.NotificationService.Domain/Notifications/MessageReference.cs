using Bizcord.NotificationService.Domain.Common;

namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// Value object: a pointer back to the message in the Messaging context that caused the
/// notification. The Notification context stores no message bodies of its own beyond the
/// preview, it only remembers where the notification came from.
/// </summary>
public sealed record MessageReference
{
    private MessageReference(Guid messageId, Guid channelId, Guid authorId, DateTime postedAt)
    {
        MessageId = messageId;
        ChannelId = channelId;
        AuthorId = authorId;
        PostedAt = postedAt;
    }

    public Guid MessageId { get; }

    public Guid ChannelId { get; }

    public Guid AuthorId { get; }

    public DateTime PostedAt { get; }

    public static MessageReference Create(Guid messageId, Guid channelId, Guid authorId, DateTime postedAt)
    {
        if (messageId == Guid.Empty)
        {
            throw new DomainException("Message id must not be empty.");
        }

        if (channelId == Guid.Empty)
        {
            throw new DomainException("Channel id must not be empty.");
        }

        if (authorId == Guid.Empty)
        {
            throw new DomainException("Author id must not be empty.");
        }

        return new MessageReference(messageId, channelId, authorId, postedAt);
    }
}
