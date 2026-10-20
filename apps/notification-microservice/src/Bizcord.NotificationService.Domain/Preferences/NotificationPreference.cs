using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Mentions;

namespace Bizcord.NotificationService.Domain.Preferences;

/// <summary>
/// Entity: how one user wants to be notified. This is where the business rule that makes
/// a notification service useful lives - deciding what is worth interrupting someone for.
/// </summary>
public sealed class NotificationPreference
{
    private readonly HashSet<Guid> _mutedChannels;

    private NotificationPreference(
        Guid userId,
        bool allowDirectMentions,
        bool allowChannelWideMentions,
        QuietHours? quietHours,
        IEnumerable<Guid> mutedChannels)
    {
        UserId = userId;
        AllowDirectMentions = allowDirectMentions;
        AllowChannelWideMentions = allowChannelWideMentions;
        QuietHours = quietHours;
        _mutedChannels = [.. mutedChannels];
    }

    public Guid UserId { get; }

    public bool AllowDirectMentions { get; private set; }

    public bool AllowChannelWideMentions { get; private set; }

    public QuietHours? QuietHours { get; private set; }

    public IReadOnlyCollection<Guid> MutedChannels => _mutedChannels;

    /// <summary>
    /// The preferences a user has until they change anything: direct mentions and
    /// channel-wide mentions are both allowed, nothing is muted.
    /// </summary>
    public static NotificationPreference Default(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Preferences must belong to a user.");
        }

        return new NotificationPreference(
            userId,
            allowDirectMentions: true,
            allowChannelWideMentions: true,
            quietHours: null,
            mutedChannels: []);
    }

    public static NotificationPreference Rehydrate(
        Guid userId,
        bool allowDirectMentions,
        bool allowChannelWideMentions,
        QuietHours? quietHours,
        IEnumerable<Guid> mutedChannels) =>
        new(userId, allowDirectMentions, allowChannelWideMentions, quietHours, mutedChannels);

    public void AllowMentions(bool direct, bool channelWide)
    {
        AllowDirectMentions = direct;
        AllowChannelWideMentions = channelWide;
    }

    public void SetQuietHours(QuietHours? quietHours) => QuietHours = quietHours;

    public void MuteChannel(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            throw new DomainException("Cannot mute an unknown channel.");
        }

        _mutedChannels.Add(channelId);
    }

    public void UnmuteChannel(Guid channelId) => _mutedChannels.Remove(channelId);

    public void ReplaceMutedChannels(IEnumerable<Guid> channelIds)
    {
        ArgumentNullException.ThrowIfNull(channelIds);

        _mutedChannels.Clear();

        foreach (var channelId in channelIds)
        {
            MuteChannel(channelId);
        }
    }

    public bool IsChannelMuted(Guid channelId) => _mutedChannels.Contains(channelId);

    /// <summary>
    /// The core delivery decision. A muted channel beats everything, then quiet hours,
    /// then the mention kind. Returning a reason (instead of just <c>false</c>) keeps the
    /// decision explainable in logs and in tests.
    /// </summary>
    public DeliveryDecision Decide(Mention mention, Guid channelId, DateTime momentUtc)
    {
        ArgumentNullException.ThrowIfNull(mention);

        if (IsChannelMuted(channelId))
        {
            return DeliveryDecision.Suppressed("The recipient muted this channel.");
        }

        if (QuietHours?.Includes(momentUtc) == true)
        {
            return DeliveryDecision.Suppressed($"The recipient is in quiet hours ({QuietHours}).");
        }

        if (mention.Kind == MentionKind.User && !AllowDirectMentions)
        {
            return DeliveryDecision.Suppressed("The recipient disabled direct mentions.");
        }

        if (mention.IsChannelWide && !AllowChannelWideMentions)
        {
            return DeliveryDecision.Suppressed("The recipient disabled @here/@everyone mentions.");
        }

        return DeliveryDecision.Deliver();
    }
}

/// <summary>The outcome of evaluating a user's preferences for one mention.</summary>
public sealed record DeliveryDecision(bool ShouldNotify, string? Reason)
{
    public static DeliveryDecision Deliver() => new(true, null);

    public static DeliveryDecision Suppressed(string reason) => new(false, reason);
}
