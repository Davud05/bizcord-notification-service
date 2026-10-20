using Bizcord.Shared.Contracts.Events;

namespace Bizcord.NotificationService.TestSupport;

/// <summary>
/// Builders for the integration events used in tests. Keeping them in one place means a
/// change to the shared contract shows up as one compile error, not fifty.
/// </summary>
public static class Events
{
    /// <summary>
    /// The minimum valid <see cref="MessagePostedEvent"/>: every field a consumer is
    /// allowed to depend on, filled with a realistic value.
    /// </summary>
    public static MessagePostedEvent MessagePosted(
        Guid? messageId = null,
        Guid? channelId = null,
        Guid? authorId = null,
        string content = "Hello world",
        DateTime? postedAt = null) =>
        new()
        {
            MessageId = messageId ?? Guid.NewGuid(),
            ChannelId = channelId ?? Guid.NewGuid(),
            AuthorId = authorId ?? Guid.NewGuid(),
            Content = content,
            PostedAt = postedAt ?? DateTime.UtcNow
        };

    /// <summary>Message content that mentions a specific user, in Bizcord's mention syntax.</summary>
    public static string MentionOf(Guid userId, string text = "can you take a look?") =>
        $"<@{userId}> {text}";
}
