using System.ComponentModel.DataAnnotations;
using Bizcord.NotificationService.Application.Preferences;

namespace Bizcord.NotificationService.Presentation.Requests;

/// <summary>Body of <c>PUT /api/users/{userId}/notification-preferences</c>.</summary>
public sealed class UpdatePreferencesRequest
{
    public bool AllowDirectMentions { get; init; } = true;

    public bool AllowChannelWideMentions { get; init; } = true;

    public IReadOnlyCollection<Guid> MutedChannels { get; init; } = [];

    /// <summary>Start of the daily quiet period in UTC, formatted as <c>HH:mm</c>.</summary>
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "QuietHoursStart must be formatted as HH:mm.")]
    public string? QuietHoursStart { get; init; }

    /// <summary>End of the daily quiet period in UTC, formatted as <c>HH:mm</c>.</summary>
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "QuietHoursEnd must be formatted as HH:mm.")]
    public string? QuietHoursEnd { get; init; }

    public UpdatePreferencesCommand ToCommand() =>
        new()
        {
            AllowDirectMentions = AllowDirectMentions,
            AllowChannelWideMentions = AllowChannelWideMentions,
            MutedChannels = MutedChannels,
            QuietHoursStart = QuietHoursStart,
            QuietHoursEnd = QuietHoursEnd
        };
}
