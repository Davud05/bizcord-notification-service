using System.Text.RegularExpressions;

namespace Bizcord.NotificationService.Domain.Mentions;

/// <summary>
/// The mention syntax understood by Bizcord. Users are mentioned the same way as in
/// Discord, with the raw id wrapped in markup, so renaming a user never rewrites history:
/// <c>Hey &lt;@3f4c...&gt;, can you look at this? @here</c>
/// </summary>
public static partial class MentionMarkup
{
    public const string HereKeyword = "@here";
    public const string EveryoneKeyword = "@everyone";

    /// <summary>Matches <c>&lt;@user-id&gt;</c> where the id is a GUID.</summary>
    [GeneratedRegex(
        @"<@(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})>",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    public static partial Regex UserMentionPattern { get; }

    /// <summary>Matches <c>@here</c> / <c>@everyone</c> as whole words.</summary>
    [GeneratedRegex(
        @"(?<![\w@])@(?<keyword>here|everyone)\b",
        RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex ChannelMentionPattern { get; }

    /// <summary>
    /// Replaces mention markup with something a human can read, so a notification body
    /// never exposes raw ids. Unknown users become <c>@someone</c>.
    /// </summary>
    public static string Humanize(string content, Func<Guid, string?>? resolveDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return UserMentionPattern.Replace(content, match =>
        {
            var id = Guid.Parse(match.Groups["id"].Value);
            var name = resolveDisplayName?.Invoke(id);

            return string.IsNullOrWhiteSpace(name) ? "@someone" : $"@{name}";
        });
    }
}
