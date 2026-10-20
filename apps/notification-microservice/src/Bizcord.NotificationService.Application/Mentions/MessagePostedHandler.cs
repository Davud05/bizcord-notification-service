using Bizcord.MessageClient;
using Bizcord.NotificationService.Application.Abstractions;
using Bizcord.NotificationService.Domain.Mentions;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.NotificationService.Domain.Preferences;
using Bizcord.Shared.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Bizcord.NotificationService.Application.Mentions;

/// <summary>
/// The first feature of the service: turn "a message was posted" into "these people need
/// to know about it".
///
/// For every message posted anywhere in Bizcord this handler
/// <list type="number">
///   <item><description>parses the mentions out of the content,</description></item>
///   <item><description>expands <c>@here</c>/<c>@everyone</c> to channel members,</description></item>
///   <item><description>asks each recipient's preferences whether they want to be disturbed,</description></item>
///   <item><description>stores a notification and publishes a <see cref="MentionDetectedEvent"/>.</description></item>
/// </list>
/// </summary>
public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IUserDirectoryClient _userDirectory;
    private readonly IChannelDirectoryClient _channelDirectory;
    private readonly IMessageClient _messageClient;
    private readonly TimeProvider _clock;
    private readonly ILogger<MessagePostedHandler> _logger;

    public MessagePostedHandler(
        INotificationRepository notifications,
        INotificationPreferenceRepository preferences,
        IUserDirectoryClient userDirectory,
        IChannelDirectoryClient channelDirectory,
        IMessageClient messageClient,
        TimeProvider clock,
        ILogger<MessagePostedHandler> logger)
    {
        _notifications = notifications;
        _preferences = preferences;
        _userDirectory = userDirectory;
        _channelDirectory = channelDirectory;
        _messageClient = messageClient;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mentions = MentionParser.Parse(message.Content);

        if (mentions.Count == 0)
        {
            _logger.LogDebug("Message {MessageId} contains no mentions, nothing to do.", message.MessageId);
            return;
        }

        var source = MessageReference.Create(
            message.MessageId,
            message.ChannelId,
            message.AuthorId,
            message.PostedAt);

        var displayNames = new Dictionary<Guid, string>();
        var authorName = await ResolveAuthorNameAsync(message.AuthorId, displayNames, cancellationToken);
        var alreadyNotified = new HashSet<Guid> { message.AuthorId };
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var mention in mentions)
        {
            foreach (var recipientId in await ResolveRecipientsAsync(mention, message.ChannelId, cancellationToken))
            {
                if (!alreadyNotified.Add(recipientId))
                {
                    // Mentioned directly and covered by @everyone: still one notification.
                    continue;
                }

                await NotifyAsync(
                    recipientId,
                    mention,
                    message,
                    source,
                    authorName,
                    displayNames,
                    now,
                    cancellationToken);
            }
        }
    }

    private async Task NotifyAsync(
        Guid recipientId,
        Mention mention,
        MessagePostedEvent message,
        MessageReference source,
        string authorName,
        Dictionary<Guid, string> displayNames,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var preference = await _preferences.GetAsync(recipientId, cancellationToken)
            ?? NotificationPreference.Default(recipientId);

        var decision = preference.Decide(mention, message.ChannelId, now);

        if (!decision.ShouldNotify)
        {
            _logger.LogInformation(
                "Suppressed {Mention} for recipient {RecipientId} from message {MessageId}: {Reason}",
                mention,
                recipientId,
                message.MessageId,
                decision.Reason);
            return;
        }

        var lookup = await _userDirectory.GetUserAsync(recipientId, cancellationToken);

        switch (lookup.Status)
        {
            case UserLookupStatus.NotFound:
                _logger.LogWarning(
                    "Recipient {RecipientId} does not exist in the User service, skipping.",
                    recipientId);
                return;

            case UserLookupStatus.Found when lookup.User is { IsActive: false }:
                _logger.LogInformation("Recipient {RecipientId} is deactivated, skipping.", recipientId);
                return;

            case UserLookupStatus.Found when lookup.User is not null:
                displayNames[recipientId] = lookup.User.DisplayName;
                break;

            case UserLookupStatus.Unavailable:
                // Fail open: a notification with a generic name is better than a missed
                // mention, and the Channel/User services are allowed to be down.
                _logger.LogWarning(
                    "User service unavailable while notifying {RecipientId}; continuing without a display name.",
                    recipientId);
                break;
        }

        var notification = Notification.ForMention(
            Recipient.For(recipientId),
            mention,
            source,
            authorName,
            message.Content,
            now,
            id => displayNames.GetValueOrDefault(id));

        await _notifications.SaveAsync(notification, cancellationToken);

        await _messageClient.PublishAsync(
            new MentionDetectedEvent
            {
                MessageId = message.MessageId,
                NotificationId = notification.Id,
                ChannelId = message.ChannelId,
                AuthorId = message.AuthorId,
                RecipientId = recipientId,
                MentionKind = mention.Kind.ToString(),
                Preview = notification.Content.Body,
                ProcessedAt = now
            },
            cancellationToken);

        _logger.LogInformation(
            "Notified {RecipientId} about {Mention} in message {MessageId} (notification {NotificationId}).",
            recipientId,
            mention,
            message.MessageId,
            notification.Id);
    }

    private async Task<IReadOnlyList<Guid>> ResolveRecipientsAsync(
        Mention mention,
        Guid channelId,
        CancellationToken cancellationToken)
    {
        if (mention.Kind == MentionKind.User)
        {
            return [mention.UserId!.Value];
        }

        var members = await _channelDirectory.GetMemberIdsAsync(channelId, cancellationToken);

        if (members.Count == 0)
        {
            _logger.LogWarning(
                "No members resolved for channel {ChannelId}; {Mention} produced no notifications.",
                channelId,
                mention);
        }

        return members;
    }

    private async Task<string> ResolveAuthorNameAsync(
        Guid authorId,
        Dictionary<Guid, string> displayNames,
        CancellationToken cancellationToken)
    {
        var lookup = await _userDirectory.GetUserAsync(authorId, cancellationToken);

        if (lookup is { Status: UserLookupStatus.Found, User: not null })
        {
            displayNames[authorId] = lookup.User.DisplayName;
            return lookup.User.DisplayName;
        }

        _logger.LogWarning(
            "Could not resolve the author {AuthorId} ({Status}); falling back to a generic name.",
            authorId,
            lookup.Status);

        return "Someone";
    }
}
