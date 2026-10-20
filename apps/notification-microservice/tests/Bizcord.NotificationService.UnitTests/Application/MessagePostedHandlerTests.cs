using Bizcord.MessageClient;
using Bizcord.NotificationService.Application.Abstractions;
using Bizcord.NotificationService.Application.Mentions;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.NotificationService.Domain.Preferences;
using Bizcord.NotificationService.TestSupport;
using Bizcord.Shared.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcord.NotificationService.UnitTests.Application;

/// <summary>
/// Unit tests for the first feature: reacting to <see cref="MessagePostedEvent"/>.
/// Collaborators are mocked with Moq, and the broker is replaced by
/// <see cref="FakeMessageClient"/> so the tests run in milliseconds without RabbitMQ.
/// </summary>
public class MessagePostedHandlerTests
{
    private static readonly Guid Davud = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Patrick = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Author = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Channel = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<INotificationRepository> _notifications = new(MockBehavior.Strict);
    private readonly Mock<INotificationPreferenceRepository> _preferences = new(MockBehavior.Strict);
    private readonly Mock<IUserDirectoryClient> _userDirectory = new(MockBehavior.Strict);
    private readonly Mock<IChannelDirectoryClient> _channelDirectory = new(MockBehavior.Strict);
    private readonly FakeMessageClient _messageClient = new();
    private readonly FixedTimeProvider _clock = FixedTimeProvider.Workday();
    private readonly List<Notification> _saved = [];

    public MessagePostedHandlerTests()
    {
        _notifications
            .Setup(repository => repository.SaveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => _saved.Add(notification))
            .Returns(Task.CompletedTask);

        // No stored preferences means "the documented defaults".
        _preferences
            .Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        _userDirectory
            .Setup(client => client.GetUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                UserLookupResult.Found(new UserSummary(id, DisplayNameOf(id), IsActive: true)));

        _channelDirectory
            .Setup(client => client.GetMemberIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_PublishesMentionDetectedEvent_WithCorrectMessageId()
    {
        var message = Events.MessagePosted(
            channelId: Channel,
            authorId: Author,
            content: Events.MentionOf(Davud));

        await CreateHandler().Handle(message, CancellationToken.None);

        var published = _messageClient.SinglePublished<MentionDetectedEvent>();
        published.Should().NotBeNull();
        published!.MessageId.Should().Be(message.MessageId);
        published.ChannelId.Should().Be(Channel);
        published.AuthorId.Should().Be(Author);
        published.RecipientId.Should().Be(Davud);
        published.MentionKind.Should().Be("User");
        published.Preview.Should().Contain("can you take a look?");
        published.NotificationId.Should().Be(_saved.Single().Id);
        published.ProcessedAt.Should().Be(_clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Handle_StoresTheNotification_ForTheMentionedUser()
    {
        var message = Events.MessagePosted(
            channelId: Channel,
            authorId: Author,
            content: Events.MentionOf(Davud));

        await CreateHandler().Handle(message, CancellationToken.None);

        var notification = _saved.Should().ContainSingle().Subject;
        notification.Recipient.UserId.Should().Be(Davud);
        notification.Type.Should().Be(NotificationType.Mention);
        notification.Status.Should().Be(NotificationStatus.Unread);
        notification.Content.Title.Should().Be($"{DisplayNameOf(Author)} mentioned you");
        notification.Source!.MessageId.Should().Be(message.MessageId);

        _notifications.Verify(
            repository => repository.SaveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Consumer contract test: the handler must survive the minimum valid
    /// <see cref="MessagePostedEvent"/> the Messaging team promises to publish. If they
    /// change the event, this test fails in CI instead of in production.
    /// </summary>
    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        var handler = CreateHandler();

        var act = async () => await handler.Handle(
            new MessagePostedEvent
            {
                MessageId = Guid.NewGuid(),
                ChannelId = Guid.NewGuid(),
                AuthorId = Guid.NewGuid(),
                Content = Events.MentionOf(Davud, "hello world"),
                PostedAt = DateTime.UtcNow
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        _messageClient.PublishedOf<MentionDetectedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheMessageHasNoMentions()
    {
        await CreateHandler().Handle(
            Events.MessagePosted(content: "just saying hello"),
            CancellationToken.None);

        _saved.Should().BeEmpty();
        _messageClient.Published.Should().BeEmpty();
        _notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_NeverNotifiesTheAuthorAboutTheirOwnMessage()
    {
        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Author)),
            CancellationToken.None);

        _saved.Should().BeEmpty();
        _messageClient.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExpandsEveryone_ToTheChannelMembers()
    {
        _channelDirectory
            .Setup(client => client.GetMemberIdsAsync(Channel, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Davud, Patrick, Author]);

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: "@everyone standup in 5"),
            CancellationToken.None);

        _saved.Select(notification => notification.Recipient.UserId)
            .Should().BeEquivalentTo(new[] { Davud, Patrick }, "the author is not notified");
        _messageClient.PublishedOf<MentionDetectedEvent>()
            .Select(published => published.MentionKind)
            .Should().AllBe("Everyone");
    }

    [Fact]
    public async Task Handle_SendsOneNotification_WhenAUserIsBothMentionedAndCoveredByEveryone()
    {
        _channelDirectory
            .Setup(client => client.GetMemberIdsAsync(Channel, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Davud, Author]);

        await CreateHandler().Handle(
            Events.MessagePosted(
                channelId: Channel,
                authorId: Author,
                content: $"@everyone - especially <@{Davud}>"),
            CancellationToken.None);

        _saved.Should().ContainSingle()
            .Which.Recipient.UserId.Should().Be(Davud);
        _messageClient.PublishedOf<MentionDetectedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_SkipsRecipients_WhoMutedTheChannel()
    {
        var muted = NotificationPreference.Default(Davud);
        muted.MuteChannel(Channel);

        _preferences
            .Setup(repository => repository.GetAsync(Davud, It.IsAny<CancellationToken>()))
            .ReturnsAsync(muted);

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        _saved.Should().BeEmpty();
        _messageClient.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsRecipients_InQuietHours()
    {
        var sleeping = NotificationPreference.Default(Davud);
        sleeping.SetQuietHours(QuietHours.Create(new TimeOnly(22, 0), new TimeOnly(7, 0)));

        _preferences
            .Setup(repository => repository.GetAsync(Davud, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sleeping);

        _clock.Now = FixedTimeProvider.Night().Now;

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsRecipients_ThatTheUserServiceDoesNotKnow()
    {
        _userDirectory
            .Setup(client => client.GetUserAsync(Davud, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserLookupResult.NotFound());

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        _saved.Should().BeEmpty();
        _messageClient.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsDeactivatedRecipients()
    {
        _userDirectory
            .Setup(client => client.GetUserAsync(Davud, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserLookupResult.Found(new UserSummary(Davud, "Davud", IsActive: false)));

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_StillNotifies_WhenTheUserServiceIsUnavailable()
    {
        // Fail open: a notification with a generic author name beats a lost mention.
        _userDirectory
            .Setup(client => client.GetUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserLookupResult.Unavailable());

        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        _saved.Should().ContainSingle()
            .Which.Content.Title.Should().Be("Someone mentioned you");
        _messageClient.PublishedOf<MentionDetectedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenTheChannelHasNoResolvableMembers()
    {
        await CreateHandler().Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: "@here anyone around?"),
            CancellationToken.None);

        _saved.Should().BeEmpty();
        _messageClient.Published.Should().BeEmpty();
        _channelDirectory.Verify(
            client => client.GetMemberIdsAsync(Channel, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The same behaviour verified with a Moq mock of <see cref="IMessageClient"/> instead
    /// of the hand written fake. Mocking the interface works out of the box - no
    /// <c>virtual</c> members or extra constructors needed, which is exactly why the
    /// message client is an interface.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesExactlyOneEvent_VerifiedWithMoq()
    {
        var messageClient = new Mock<IMessageClient>();
        messageClient
            .Setup(client => client.PublishAsync(It.IsAny<MentionDetectedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(messageClient.Object);

        await handler.Handle(
            Events.MessagePosted(channelId: Channel, authorId: Author, content: Events.MentionOf(Davud)),
            CancellationToken.None);

        messageClient.Verify(
            client => client.PublishAsync(
                It.Is<MentionDetectedEvent>(published => published.RecipientId == Davud),
                It.IsAny<CancellationToken>()),
            Times.Once);
        messageClient.VerifyNoOtherCalls();
    }

    private MessagePostedHandler CreateHandler(IMessageClient? messageClient = null) =>
        new(
            _notifications.Object,
            _preferences.Object,
            _userDirectory.Object,
            _channelDirectory.Object,
            messageClient ?? _messageClient,
            _clock,
            NullLogger<MessagePostedHandler>.Instance);

    private static string DisplayNameOf(Guid id) => id switch
    {
        _ when id == Davud => "Davud",
        _ when id == Patrick => "Patrick",
        _ when id == Author => "Mikkel",
        _ => "Unknown"
    };
}
