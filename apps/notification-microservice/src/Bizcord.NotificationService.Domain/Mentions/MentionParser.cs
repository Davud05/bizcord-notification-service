namespace Bizcord.NotificationService.Domain.Mentions;

/// <summary>
/// Domain service that turns raw message content into the mentions it contains. Pure
/// logic, no infrastructure: this is the heart of the Notification context and it is
/// fully unit tested.
/// </summary>
public static class MentionParser
{
    /// <summary>
    /// Finds every distinct mention in <paramref name="content"/>. Duplicates are
    /// collapsed, so mentioning the same user three times produces one notification.
    /// When a message contains both <c>@everyone</c> and <c>@here</c>, the broader
    /// <c>@everyone</c> wins and <c>@here</c> is dropped.
    /// </summary>
    public static IReadOnlyList<Mention> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var mentions = new List<Mention>();
        var seenUsers = new HashSet<Guid>();

        foreach (var match in MentionMarkup.UserMentionPattern.Matches(content).Cast<System.Text.RegularExpressions.Match>())
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var userId) && seenUsers.Add(userId))
            {
                mentions.Add(Mention.ForUser(userId));
            }
        }

        var everyone = false;
        var here = false;

        foreach (var match in MentionMarkup.ChannelMentionPattern.Matches(content).Cast<System.Text.RegularExpressions.Match>())
        {
            if (match.Groups["keyword"].Value.Equals("everyone", StringComparison.OrdinalIgnoreCase))
            {
                everyone = true;
            }
            else
            {
                here = true;
            }
        }

        if (everyone)
        {
            mentions.Add(Mention.Everyone());
        }
        else if (here)
        {
            mentions.Add(Mention.Here());
        }

        return mentions;
    }

    /// <summary>True when the message addresses at least one user or the whole channel.</summary>
    public static bool ContainsMention(string? content) => Parse(content).Count > 0;
}
