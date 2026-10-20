using Bizcord.NotificationService.Domain.Preferences;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.Application.Preferences;

/// <summary>Reading and updating a user's notification preferences.</summary>
public interface IPreferenceAppService
{
    Task<NotificationPreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<NotificationPreferenceDto> UpdateAsync(
        Guid userId,
        UpdatePreferencesCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Input for replacing a user's preferences.</summary>
public sealed record UpdatePreferencesCommand
{
    public bool AllowDirectMentions { get; init; } = true;

    public bool AllowChannelWideMentions { get; init; } = true;

    public IReadOnlyCollection<Guid> MutedChannels { get; init; } = [];

    /// <summary><c>HH:mm</c> in UTC, or <c>null</c> for no quiet hours.</summary>
    public string? QuietHoursStart { get; init; }

    /// <summary><c>HH:mm</c> in UTC, or <c>null</c> for no quiet hours.</summary>
    public string? QuietHoursEnd { get; init; }
}

/// <inheritdoc />
public sealed class PreferenceAppService : IPreferenceAppService
{
    private readonly INotificationPreferenceRepository _repository;

    public PreferenceAppService(INotificationPreferenceRepository repository) => _repository = repository;

    public async Task<NotificationPreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // A user that never touched their settings gets the documented defaults rather
        // than a 404, so clients always have something to render.
        var preference = await _repository.GetAsync(userId, cancellationToken)
            ?? NotificationPreference.Default(userId);

        return ToDto(preference);
    }

    public async Task<NotificationPreferenceDto> UpdateAsync(
        Guid userId,
        UpdatePreferencesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var preference = await _repository.GetAsync(userId, cancellationToken)
            ?? NotificationPreference.Default(userId);

        preference.AllowMentions(command.AllowDirectMentions, command.AllowChannelWideMentions);
        preference.ReplaceMutedChannels(command.MutedChannels);
        preference.SetQuietHours(QuietHours.Parse(command.QuietHoursStart, command.QuietHoursEnd));

        await _repository.SaveAsync(preference, cancellationToken);

        return ToDto(preference);
    }

    public static NotificationPreferenceDto ToDto(NotificationPreference preference) =>
        new()
        {
            UserId = preference.UserId,
            AllowDirectMentions = preference.AllowDirectMentions,
            AllowChannelWideMentions = preference.AllowChannelWideMentions,
            MutedChannels = [.. preference.MutedChannels],
            QuietHoursStart = preference.QuietHours?.Start.ToString("HH\\:mm"),
            QuietHoursEnd = preference.QuietHours?.End.ToString("HH\\:mm")
        };
}
