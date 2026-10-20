using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Notifications;

namespace Bizcord.NotificationService.UnitTests.Domain;

public class NotificationContentTests
{
    [Theory]
    [InlineData("", "body")]
    [InlineData("   ", "body")]
    [InlineData("title", "")]
    [InlineData("title", "   ")]
    public void Create_Throws_WhenTitleOrBodyIsBlank(string title, string body)
    {
        var act = () => NotificationContent.Create(title, body);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var content = NotificationContent.Create("  Mentioned  ", "  in #general  ");

        content.Title.Should().Be("Mentioned");
        content.Body.Should().Be("in #general");
    }

    [Fact]
    public void Create_Throws_WhenTitleIsTooLong()
    {
        var act = () => NotificationContent.Create(new string('a', NotificationContent.MaxTitleLength + 1), "body");

        act.Should().Throw<DomainException>()
            .WithMessage($"*at most {NotificationContent.MaxTitleLength} characters*");
    }

    [Fact]
    public void Preview_KeepsShortTextUntouched()
    {
        NotificationContent.Preview("standup in 5").Should().Be("standup in 5");
    }

    [Fact]
    public void Preview_TruncatesLongTextOnAWordBoundary()
    {
        var text = string.Join(' ', Enumerable.Repeat("notification", 40));

        var preview = NotificationContent.Preview(text);

        preview.Length.Should().BeLessThanOrEqualTo(NotificationContent.PreviewLength + 1);
        preview.Should().EndWith("…");
        preview.Should().NotContain("notificatio…", "the text is cut between words, not inside one");
    }

    [Fact]
    public void ForMention_ReplacesMentionMarkupWithNames()
    {
        var mentioned = Guid.NewGuid();

        var content = NotificationContent.ForMention(
            "Patrick",
            $"<@{mentioned}> please review",
            id => id == mentioned ? "Davud" : null);

        content.Title.Should().Be("Patrick mentioned you");
        content.Body.Should().Be("@Davud please review");
        content.Body.Should().NotContain(mentioned.ToString(), "a user should never see a raw id");
    }

    [Fact]
    public void ForMention_Throws_WhenAuthorNameIsMissing()
    {
        var act = () => NotificationContent.ForMention(" ", "hello");

        act.Should().Throw<DomainException>()
            .WithMessage("*needs the name of the author*");
    }
}
