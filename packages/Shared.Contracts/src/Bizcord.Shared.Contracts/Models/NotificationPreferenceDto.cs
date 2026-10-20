namespace Bizcord.Shared.Contracts.Models;

/// <summary>
/// The shared model of a user's notification preferences. Internally the service keeps
/// a richer model (per-channel mute bookkeeping, quiet-hour evaluation); only the
/// integration-relevant flags are exposed here.
/// </summary>
public class NotificationPreferenceDto
{
    public Guid UserId { get; set; }

    /// <summary>Whether the user wants to be notified when mentioned directly.</summary>
    public bool AllowDirectMentions { get; set; }

    /// <summary>Whether the user wants to be notified by <c>@here</c> / <c>@everyone</c>.</summary>
    public bool AllowChannelWideMentions { get; set; }

    /// <summary>Channels the user has muted.</summary>
    public IReadOnlyCollection<Guid> MutedChannels { get; set; } = [];

    /// <summary>Start of the daily quiet period (UTC, <c>HH:mm</c>), or <c>null</c>.</summary>
    public string? QuietHoursStart { get; set; }

    /// <summary>End of the daily quiet period (UTC, <c>HH:mm</c>), or <c>null</c>.</summary>
    public string? QuietHoursEnd { get; set; }
}
