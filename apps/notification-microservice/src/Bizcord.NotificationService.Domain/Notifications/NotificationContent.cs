using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Mentions;

namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// Value object: what the user actually gets to read. Immutable, self-validating and
/// responsible for turning a long chat message into a short preview.
/// </summary>
public sealed record NotificationContent
{
    public const int MaxTitleLength = 120;
    public const int MaxBodyLength = 500;
    public const int PreviewLength = 140;

    private NotificationContent(string title, string body)
    {
        Title = title;
        Body = body;
    }

    public string Title { get; }

    public string Body { get; }

    public static NotificationContent Create(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Notification title must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("Notification body must not be empty.");
        }

        title = title.Trim();
        body = body.Trim();

        if (title.Length > MaxTitleLength)
        {
            throw new DomainException($"Notification title must be at most {MaxTitleLength} characters.");
        }

        if (body.Length > MaxBodyLength)
        {
            throw new DomainException($"Notification body must be at most {MaxBodyLength} characters.");
        }

        return new NotificationContent(title, body);
    }

    /// <summary>
    /// Builds the content for a mention: "&lt;author&gt; mentioned you" plus a preview of
    /// the message. Mention markup is replaced so the user never sees raw ids.
    /// </summary>
    public static NotificationContent ForMention(
        string authorDisplayName,
        string messageContent,
        Func<Guid, string?>? resolveDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(authorDisplayName))
        {
            throw new DomainException("A mention notification needs the name of the author.");
        }

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new DomainException("A mention notification needs the message content.");
        }

        var title = $"{authorDisplayName.Trim()} mentioned you";
        var body = Preview(MentionMarkup.Humanize(messageContent, resolveDisplayName));

        return Create(title, body);
    }

    /// <summary>Shortens text to <see cref="PreviewLength"/> characters without cutting mid-word.</summary>
    public static string Preview(string text)
    {
        text = text.Trim();

        if (text.Length <= PreviewLength)
        {
            return text;
        }

        var cut = text[..PreviewLength];
        var lastSpace = cut.LastIndexOf(' ');

        if (lastSpace > PreviewLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd() + "…";
    }
}
