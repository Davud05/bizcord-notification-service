using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Mentions;

namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// The aggregate root of this bounded context: one thing a user should know about.
/// All state changes go through methods that protect the invariants, so a notification
/// can never be read before it exists or read after it has been archived.
/// </summary>
public sealed class Notification
{
    private Notification(
        Guid id,
        Recipient recipient,
        NotificationType type,
        NotificationContent content,
        MessageReference? source,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt)
    {
        Id = id;
        Recipient = recipient;
        Type = type;
        Content = content;
        Source = source;
        Status = status;
        CreatedAt = createdAt;
        ReadAt = readAt;
    }

    public Guid Id { get; }

    public Recipient Recipient { get; }

    public NotificationType Type { get; }

    public NotificationContent Content { get; private set; }

    /// <summary>The message that triggered this notification, when there was one.</summary>
    public MessageReference? Source { get; }

    public NotificationStatus Status { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? ReadAt { get; private set; }

    public Guid? ChannelId => Source?.ChannelId;

    public bool IsUnread => Status == NotificationStatus.Unread;

    public static Notification Create(
        Recipient recipient,
        NotificationType type,
        NotificationContent content,
        MessageReference? source = null,
        DateTime? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(content);

        if (type == NotificationType.Mention && source is null)
        {
            throw new DomainException("A mention notification must reference the message it came from.");
        }

        return new Notification(
            Guid.NewGuid(),
            recipient,
            type,
            content,
            source,
            NotificationStatus.Unread,
            createdAt ?? DateTime.UtcNow,
            readAt: null);
    }

    /// <summary>
    /// Creates the notification for a user that was mentioned in a message. The mention
    /// itself decides nothing about delivery - that is the job of
    /// <see cref="Preferences.NotificationPreference"/>.
    /// </summary>
    public static Notification ForMention(
        Recipient recipient,
        Mention mention,
        MessageReference source,
        string authorDisplayName,
        string messageContent,
        DateTime? createdAt = null,
        Func<Guid, string?>? resolveDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(mention);
        ArgumentNullException.ThrowIfNull(source);

        if (mention.Kind == MentionKind.User && mention.UserId != recipient.UserId)
        {
            throw new DomainException("A direct mention can only notify the user that was mentioned.");
        }

        if (recipient.UserId == source.AuthorId)
        {
            throw new DomainException("A user is never notified about their own message.");
        }

        return Create(
            recipient,
            NotificationType.Mention,
            NotificationContent.ForMention(authorDisplayName, messageContent, resolveDisplayName),
            source,
            createdAt);
    }

    /// <summary>
    /// Rehydrates a notification from storage. Only infrastructure should call this:
    /// it bypasses the creation rules on purpose, because the state was already valid
    /// when it was written.
    /// </summary>
    public static Notification Rehydrate(
        Guid id,
        Recipient recipient,
        NotificationType type,
        NotificationContent content,
        MessageReference? source,
        NotificationStatus status,
        DateTime createdAt,
        DateTime? readAt) =>
        new(id, recipient, type, content, source, status, createdAt, readAt);

    /// <summary>
    /// Marks the notification as read. Idempotent, so a client that retries a request
    /// does not get an error and the original read timestamp is kept.
    /// </summary>
    /// <returns><c>true</c> when this call actually changed the state.</returns>
    public bool MarkAsRead(DateTime? readAt = null)
    {
        if (Status == NotificationStatus.Archived)
        {
            throw new DomainException("An archived notification cannot be read.");
        }

        if (Status == NotificationStatus.Read)
        {
            return false;
        }

        Status = NotificationStatus.Read;
        ReadAt = readAt ?? DateTime.UtcNow;

        return true;
    }

    /// <summary>Archives the notification. Idempotent and terminal.</summary>
    public void Archive()
    {
        Status = NotificationStatus.Archived;
    }

    /// <summary>
    /// Replaces the text of an unread notification, for example when the source message
    /// was edited. Reading history is never rewritten.
    /// </summary>
    public void UpdateContent(NotificationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (Status != NotificationStatus.Unread)
        {
            throw new DomainException("Only an unread notification can be updated.");
        }

        Content = content;
    }
}
