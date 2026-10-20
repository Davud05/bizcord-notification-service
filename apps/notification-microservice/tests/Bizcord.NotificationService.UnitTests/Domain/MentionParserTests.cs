using Bizcord.NotificationService.Domain.Mentions;

namespace Bizcord.NotificationService.UnitTests.Domain;

public class MentionParserTests
{
    private static readonly Guid Davud = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Patrick = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just a normal message")]
    [InlineData("an email like davud@here.com is not a mention")]
    [InlineData("neither is a stray @ or an @unknownkeyword")]
    public void Parse_FindsNothing_WhenThereIsNoMention(string? content)
    {
        MentionParser.Parse(content).Should().BeEmpty();
        MentionParser.ContainsMention(content).Should().BeFalse();
    }

    [Fact]
    public void Parse_FindsASingleUserMention()
    {
        var mentions = MentionParser.Parse($"hey <@{Davud}>, look at this");

        mentions.Should().HaveCount(1);
        mentions[0].Kind.Should().Be(MentionKind.User);
        mentions[0].UserId.Should().Be(Davud);
        mentions[0].IsChannelWide.Should().BeFalse();
    }

    [Fact]
    public void Parse_FindsEveryDistinctUser()
    {
        var mentions = MentionParser.Parse($"<@{Davud}> and <@{Patrick}> and <@{Davud}> again");

        mentions.Should().HaveCount(2);
        mentions.Select(m => m.UserId).Should().Equal(Davud, Patrick);
    }

    [Theory]
    [InlineData("@here deploy is done", MentionKind.Here)]
    [InlineData("@HERE deploy is done", MentionKind.Here)]
    [InlineData("@everyone standup in 5", MentionKind.Everyone)]
    public void Parse_FindsChannelWideMentions(string content, MentionKind expected)
    {
        var mentions = MentionParser.Parse(content);

        mentions.Should().HaveCount(1);
        mentions[0].Kind.Should().Be(expected);
        mentions[0].IsChannelWide.Should().BeTrue();
        mentions[0].UserId.Should().BeNull();
    }

    [Fact]
    public void Parse_PrefersEveryone_WhenBothChannelWideMentionsArePresent()
    {
        var mentions = MentionParser.Parse("@here and @everyone");

        mentions.Should().HaveCount(1);
        mentions[0].Kind.Should().Be(MentionKind.Everyone);
    }

    [Fact]
    public void Parse_ReturnsUserMentionsBeforeChannelWideOnes()
    {
        // The handler relies on this order to send one notification to a user that is both
        // mentioned directly and covered by @everyone.
        var mentions = MentionParser.Parse($"@everyone - especially <@{Davud}>");

        mentions.Select(m => m.Kind).Should().Equal(MentionKind.User, MentionKind.Everyone);
    }

    [Fact]
    public void Parse_IgnoresMalformedMentions()
    {
        MentionParser.Parse("<@not-a-guid> <@> <@12345>").Should().BeEmpty();
    }

    [Fact]
    public void Humanize_ReplacesUnknownUsersWithAGenericName()
    {
        MentionMarkup.Humanize($"<@{Davud}> hi").Should().Be("@someone hi");
    }
}
