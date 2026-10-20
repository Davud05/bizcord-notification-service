using Bizcord.NotificationService.Application.Common;
using Bizcord.NotificationService.Application.Notifications;
using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.NotificationService.Infrastructure.Persistence;
using Bizcord.NotificationService.TestSupport;
using Bizcord.Shared.Contracts.Events;
using Bizcord.Shared.Contracts.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcord.NotificationService.UnitTests.Application;

/// <summary>
/// Unit tests for the use cases behind the REST API. The in-memory repository is used as
/// a real (but fast) store; the broker is faked.
/// </summary>
public class NotificationAppServiceTests
{
    private static readonly Guid RecipientId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly InMemoryNotificationRepository _repository = new();
    private readonly FakeMessageClient _messageClient = new();
    private readonly FixedTimeProvider _clock = FixedTimeProvider.Workday();

    [Fact]
    public async Task CreateAsync_StoresAnUnreadNotification()
    {
        var service = CreateService();

        var created = await service.CreateAsync(new CreateNotificationCommand
        {
            RecipientId = RecipientId,
            Type = NotificationTypes.System,
            Title = "Welcome to Bizcord",
            Body = "Say hi in #general"
        });

        created.Id.Should().NotBe(Guid.Empty);
        created.Status.Should().Be(NotificationStatuses.Unread);
        created.CreatedAt.Should().Be(_clock.GetUtcNow().UtcDateTime);
        created.MessageId.Should().BeNull();

        (await service.GetAsync(created.Id)).Should().NotBeNull();
        (await service.CountUnreadAsync(RecipientId)).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTheMessageReferenceIsIncomplete()
    {
        var act = () => CreateService().CreateAsync(new CreateNotificationCommand
        {
            RecipientId = RecipientId,
            Type = NotificationTypes.Mention,
            Title = "Patrick mentioned you",
            Body = "ping",
            MessageId = Guid.NewGuid()
        });

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*messageId, channelId and authorId together*");
    }

    [Fact]
    public async Task CreateAsync_Throws_OnAnUnknownType()
    {
        var act = () => CreateService().CreateAsync(new CreateNotificationCommand
        {
            RecipientId = RecipientId,
            Type = "Carrier pigeon",
            Title = "Title",
            Body = "Body"
        });

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not a known notification type*");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenTheNotificationDoesNotExist()
    {
        (await CreateService().GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_FiltersByRecipientAndStatus()
    {
        var service = CreateService();
        var other = Guid.NewGuid();

        var mine = await service.CreateAsync(Command(RecipientId, "One"));
        await service.CreateAsync(Command(RecipientId, "Two"));
        await service.CreateAsync(Command(other, "Theirs"));
        await service.MarkAsReadAsync(mine.Id);

        var unread = await service.ListAsync(new ListNotificationsQuery
        {
            RecipientId = RecipientId,
            Status = NotificationStatuses.Unread
        });

        unread.Should().ContainSingle().Which.Title.Should().Be("Two");

        var all = await service.ListAsync(new ListNotificationsQuery { RecipientId = RecipientId });
        all.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ListAsync_Throws_OnAnUnreasonablePageSize(int take)
    {
        var act = () => CreateService().ListAsync(new ListNotificationsQuery { Take = take });

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*between 1 and 200*");
    }

    [Fact]
    public async Task MarkAsReadAsync_PublishesNotificationReadEvent()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(RecipientId, "Read me"));

        var read = await service.MarkAsReadAsync(created.Id);

        read.Status.Should().Be(NotificationStatuses.Read);
        read.ReadAt.Should().Be(_clock.GetUtcNow().UtcDateTime);

        var published = _messageClient.SinglePublished<NotificationReadEvent>();
        published.Should().NotBeNull();
        published!.NotificationId.Should().Be(created.Id);
        published.RecipientId.Should().Be(RecipientId);
        published.ReadAt.Should().Be(read.ReadAt!.Value);
    }

    [Fact]
    public async Task MarkAsReadAsync_IsIdempotent_AndPublishesOnlyOnce()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(RecipientId, "Read me"));

        await service.MarkAsReadAsync(created.Id);
        _clock.Advance(TimeSpan.FromHours(1));
        var second = await service.MarkAsReadAsync(created.Id);

        _messageClient.PublishedOf<NotificationReadEvent>().Should().HaveCount(1);
        second.ReadAt.Should().NotBe(_clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task MarkAsReadAsync_Throws_WhenTheNotificationDoesNotExist()
    {
        var missing = Guid.NewGuid();

        var act = () => CreateService().MarkAsReadAsync(missing);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Id.Should().Be(missing);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTheText()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(RecipientId, "Before"));

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateNotificationCommand { Title = "After", Body = "New body" });

        updated.Title.Should().Be("After");
        updated.Body.Should().Be("New body");
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenTheNotificationDoesNotExist()
    {
        var act = () => CreateService().DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheNotification()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(RecipientId, "Temporary"));

        await service.DeleteAsync(created.Id);

        (await service.GetAsync(created.Id)).Should().BeNull();
    }

    /// <summary>
    /// The same use case against a mocked repository, to verify the service does not
    /// publish anything when persistence fails.
    /// </summary>
    [Fact]
    public async Task MarkAsReadAsync_PublishesNothing_WhenTheStoreFails()
    {
        var notification = Notification.Create(
            Recipient.For(RecipientId),
            NotificationType.System,
            NotificationContent.Create("Title", "Body"));

        var repository = new Mock<INotificationRepository>();
        repository
            .Setup(store => store.GetAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        repository
            .Setup(store => store.SaveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is on fire"));

        var service = new NotificationAppService(
            repository.Object,
            _messageClient,
            _clock,
            NullLogger<NotificationAppService>.Instance);

        var act = () => service.MarkAsReadAsync(notification.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _messageClient.Published.Should().BeEmpty();
    }

    private static CreateNotificationCommand Command(Guid recipientId, string title) =>
        new()
        {
            RecipientId = recipientId,
            Type = NotificationTypes.System,
            Title = title,
            Body = "Body of " + title
        };

    private NotificationAppService CreateService() =>
        new(_repository, _messageClient, _clock, NullLogger<NotificationAppService>.Instance);
}
