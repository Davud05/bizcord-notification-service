using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Mentions;
using Bizcord.NotificationService.Domain.Notifications;

namespace Bizcord.NotificationService.UnitTests.Domain;

public class NotificationTests
{
    private static readonly Guid RecipientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AuthorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ChannelId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MessageId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Create_SetsAllFields()
    {
        var createdAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

        var notification = Notification.Create(
            Recipient.For(RecipientId),
            NotificationType.System,
            NotificationContent.Create("Welcome", "Your account is ready"),
            createdAt: createdAt);

        notification.Id.Should().NotBe(Guid.Empty);
        notification.Recipient.UserId.Should().Be(RecipientId);
        notification.Type.Should().Be(NotificationType.System);
        notification.Content.Title.Should().Be("Welcome");
        notification.Content.Body.Should().Be("Your account is ready");
        notification.Status.Should().Be(NotificationStatus.Unread);
        notification.CreatedAt.Should().Be(createdAt);
        notification.ReadAt.Should().BeNull();
        notification.Source.Should().BeNull();
    }

    [Fact]
    public void Create_Throws_WhenMentionHasNoSourceMessage()
    {
        var act = () => Notification.Create(
            Recipient.For(RecipientId),
            NotificationType.Mention,
            NotificationContent.Create("Someone mentioned you", "Hello"));

        act.Should().Throw<DomainException>()
            .WithMessage("*must reference the message*");
    }

    [Fact]
    public void Recipient_Throws_WhenUserIdIsEmpty()
    {
        var act = () => Recipient.For(Guid.Empty);

        act.Should().Throw<DomainException>()
            .WithMessage("*must have a recipient*");
    }

    [Fact]
    public void ForMention_BuildsReadableContent()
    {
        var notification = Notification.ForMention(
            Recipient.For(RecipientId),
            Mention.ForUser(RecipientId),
            Source(),
            "Patrick",
            $"<@{RecipientId}> can you review this?");

        notification.Type.Should().Be(NotificationType.Mention);
        notification.Content.Title.Should().Be("Patrick mentioned you");
        notification.Content.Body.Should().Be("@someone can you review this?");
        notification.Source!.MessageId.Should().Be(MessageId);
        notification.ChannelId.Should().Be(ChannelId);
        notification.IsUnread.Should().BeTrue();
    }

    [Fact]
    public void ForMention_UsesKnownDisplayNames_InsteadOfRawIds()
    {
        var notification = Notification.ForMention(
            Recipient.For(RecipientId),
            Mention.ForUser(RecipientId),
            Source(),
            "Patrick",
            $"<@{RecipientId}> ping",
            resolveDisplayName: id => id == RecipientId ? "Davud" : null);

        notification.Content.Body.Should().Be("@Davud ping");
    }

    [Fact]
    public void ForMention_Throws_WhenRecipientIsTheAuthor()
    {
        var act = () => Notification.ForMention(
            Recipient.For(AuthorId),
            Mention.Everyone(),
            Source(),
            "Patrick",
            "@everyone standup in 5");

        act.Should().Throw<DomainException>()
            .WithMessage("*never notified about their own message*");
    }

    [Fact]
    public void ForMention_Throws_WhenDirectMentionTargetsAnotherUser()
    {
        var someoneElse = Guid.NewGuid();

        var act = () => Notification.ForMention(
            Recipient.For(RecipientId),
            Mention.ForUser(someoneElse),
            Source(),
            "Patrick",
            $"<@{someoneElse}> hi");

        act.Should().Throw<DomainException>()
            .WithMessage("*only notify the user that was mentioned*");
    }

    [Fact]
    public void MarkAsRead_SetsStatusAndTimestamp()
    {
        var notification = Unread();
        var readAt = new DateTime(2026, 10, 20, 11, 30, 0, DateTimeKind.Utc);

        var changed = notification.MarkAsRead(readAt);

        changed.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Read);
        notification.ReadAt.Should().Be(readAt);
        notification.IsUnread.Should().BeFalse();
    }

    [Fact]
    public void MarkAsRead_IsIdempotent()
    {
        var notification = Unread();
        var firstRead = new DateTime(2026, 10, 20, 11, 30, 0, DateTimeKind.Utc);

        notification.MarkAsRead(firstRead);
        var changedAgain = notification.MarkAsRead(firstRead.AddHours(1));

        changedAgain.Should().BeFalse();
        notification.ReadAt.Should().Be(firstRead, "the original read timestamp must not be rewritten");
    }

    [Fact]
    public void MarkAsRead_Throws_WhenArchived()
    {
        var notification = Unread();
        notification.Archive();

        var act = () => notification.MarkAsRead();

        act.Should().Throw<DomainException>()
            .WithMessage("*archived notification cannot be read*");
    }

    [Fact]
    public void UpdateContent_Throws_WhenAlreadyRead()
    {
        var notification = Unread();
        notification.MarkAsRead();

        var act = () => notification.UpdateContent(NotificationContent.Create("New", "Content"));

        act.Should().Throw<DomainException>()
            .WithMessage("*Only an unread notification*");
    }

    [Fact]
    public void UpdateContent_ReplacesText_WhenUnread()
    {
        var notification = Unread();

        notification.UpdateContent(NotificationContent.Create("Edited", "Edited body"));

        notification.Content.Title.Should().Be("Edited");
        notification.Content.Body.Should().Be("Edited body");
    }

    private static MessageReference Source() =>
        MessageReference.Create(MessageId, ChannelId, AuthorId, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));

    private static Notification Unread() =>
        Notification.Create(
            Recipient.For(RecipientId),
            NotificationType.System,
            NotificationContent.Create("Title", "Body"));
}
