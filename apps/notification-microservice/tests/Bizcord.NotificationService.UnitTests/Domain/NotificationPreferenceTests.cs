using Bizcord.NotificationService.Domain.Mentions;
using Bizcord.NotificationService.Domain.Preferences;

namespace Bizcord.NotificationService.UnitTests.Domain;

public class NotificationPreferenceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChannelId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Workday = new(2026, 10, 20, 10, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime Night = new(2026, 10, 20, 2, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Default_NotifiesAboutEverything()
    {
        var preference = NotificationPreference.Default(UserId);

        preference.AllowDirectMentions.Should().BeTrue();
        preference.AllowChannelWideMentions.Should().BeTrue();
        preference.MutedChannels.Should().BeEmpty();
        preference.QuietHours.Should().BeNull();

        preference.Decide(Mention.ForUser(UserId), ChannelId, Workday).ShouldNotify.Should().BeTrue();
        preference.Decide(Mention.Everyone(), ChannelId, Workday).ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void Decide_SuppressesEverything_InAMutedChannel()
    {
        var preference = NotificationPreference.Default(UserId);
        preference.MuteChannel(ChannelId);

        var decision = preference.Decide(Mention.ForUser(UserId), ChannelId, Workday);

        decision.ShouldNotify.Should().BeFalse();
        decision.Reason.Should().Contain("muted");
        preference.IsChannelMuted(ChannelId).Should().BeTrue();
    }

    [Fact]
    public void Decide_StillNotifies_InOtherChannels()
    {
        var preference = NotificationPreference.Default(UserId);
        preference.MuteChannel(ChannelId);

        preference.Decide(Mention.ForUser(UserId), Guid.NewGuid(), Workday).ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void Decide_SuppressesDuringQuietHours()
    {
        var preference = NotificationPreference.Default(UserId);
        preference.SetQuietHours(QuietHours.Create(new TimeOnly(22, 0), new TimeOnly(7, 0)));

        preference.Decide(Mention.ForUser(UserId), ChannelId, Night).ShouldNotify.Should().BeFalse();
        preference.Decide(Mention.ForUser(UserId), ChannelId, Workday).ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void Decide_RespectsTheMentionKind()
    {
        var preference = NotificationPreference.Default(UserId);
        preference.AllowMentions(direct: true, channelWide: false);

        preference.Decide(Mention.ForUser(UserId), ChannelId, Workday).ShouldNotify.Should().BeTrue();

        var channelWide = preference.Decide(Mention.Here(), ChannelId, Workday);
        channelWide.ShouldNotify.Should().BeFalse();
        channelWide.Reason.Should().Contain("@here/@everyone");
    }

    [Fact]
    public void Decide_CanTurnOffDirectMentionsOnly()
    {
        var preference = NotificationPreference.Default(UserId);
        preference.AllowMentions(direct: false, channelWide: true);

        preference.Decide(Mention.ForUser(UserId), ChannelId, Workday).ShouldNotify.Should().BeFalse();
        preference.Decide(Mention.Everyone(), ChannelId, Workday).ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void Unmute_RestoresDelivery()
    {
        var preference = NotificationPreference.Default(UserId);

        preference.MuteChannel(ChannelId);
        preference.UnmuteChannel(ChannelId);

        preference.MutedChannels.Should().BeEmpty();
        preference.Decide(Mention.Here(), ChannelId, Workday).ShouldNotify.Should().BeTrue();
    }

    [Fact]
    public void ReplaceMutedChannels_OverwritesThePreviousList()
    {
        var preference = NotificationPreference.Default(UserId);
        var other = Guid.NewGuid();

        preference.MuteChannel(ChannelId);
        preference.ReplaceMutedChannels([other, other]);

        preference.MutedChannels.Should().Equal(other);
    }
}
