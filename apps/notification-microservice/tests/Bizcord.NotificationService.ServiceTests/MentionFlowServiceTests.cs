using System.Net.Http.Json;
using Bizcord.NotificationService.Application.Mentions;
using Bizcord.NotificationService.ServiceTests.Infrastructure;
using Bizcord.NotificationService.TestSupport;
using Bizcord.Shared.Contracts.Events;
using Bizcord.Shared.Contracts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcord.NotificationService.ServiceTests;

/// <summary>
/// Service tests for the main feature. The message handler runs inside the real
/// application (real dependency injection, real HTTP clients, real store), the User and
/// Channel services are WireMock stubs and the result is asserted through the public REST
/// API - the same way another team would see it.
/// </summary>
public class MentionFlowServiceTests : IClassFixture<ServiceTestFactory>
{
    private readonly ServiceTestFactory _factory;
    private readonly HttpClient _client;

    public MentionFlowServiceTests(ServiceTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.ResetStubs();
    }

    [Fact]
    public async Task AMentionedUser_GetsANotification_AndTheSystemGetsAnEvent()
    {
        var author = Guid.NewGuid();
        var mentioned = Guid.NewGuid();
        var channel = Guid.NewGuid();

        _factory.StubUser(author, "Patrick");
        _factory.StubUser(mentioned, "Davud");

        var message = Events.MessagePosted(
            channelId: channel,
            authorId: author,
            content: Events.MentionOf(mentioned, "can you review the PR?"));

        await HandleAsync(message);

        var notifications = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={mentioned}");

        var notification = notifications.Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationTypes.Mention);
        notification.Title.Should().Be("Patrick mentioned you");
        notification.Body.Should().Be("@Davud can you review the PR?");
        notification.MessageId.Should().Be(message.MessageId);
        notification.ChannelId.Should().Be(channel);
        notification.Status.Should().Be(NotificationStatuses.Unread);

        var published = _factory.MessageClient.SinglePublished<MentionDetectedEvent>();
        published.Should().NotBeNull();
        published!.MessageId.Should().Be(message.MessageId);
        published.RecipientId.Should().Be(mentioned);
        published.NotificationId.Should().Be(notification.Id);
    }

    [Fact]
    public async Task Everyone_NotifiesAllChannelMembersExceptTheAuthor()
    {
        var author = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var channel = Guid.NewGuid();

        _factory.StubUser(author, "Patrick");
        _factory.StubUser(first, "Davud");
        _factory.StubUser(second, "Mikkel");
        _factory.StubChannelMembers(channel, author, first, second);

        await HandleAsync(Events.MessagePosted(
            channelId: channel,
            authorId: author,
            content: "@everyone standup in 5 minutes"));

        var notifications = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?channelId={channel}");

        notifications.Should().HaveCount(2);
        notifications!.Select(notification => notification.RecipientId)
            .Should().BeEquivalentTo(new[] { first, second });
        _factory.MessageClient.PublishedOf<MentionDetectedEvent>()
            .Select(published => published.MentionKind)
            .Should().AllBe("Everyone");
    }

    [Fact]
    public async Task AMutedChannel_ProducesNoNotification()
    {
        var author = Guid.NewGuid();
        var mentioned = Guid.NewGuid();
        var channel = Guid.NewGuid();

        _factory.StubUser(author, "Patrick");
        _factory.StubUser(mentioned, "Davud");

        // The recipient mutes the channel through the public API first.
        var muted = await _client.PutAsJsonAsync(
            $"/api/users/{mentioned}/notification-preferences",
            new { allowDirectMentions = true, allowChannelWideMentions = true, mutedChannels = new[] { channel } });
        muted.EnsureSuccessStatusCode();

        await HandleAsync(Events.MessagePosted(
            channelId: channel,
            authorId: author,
            content: Events.MentionOf(mentioned)));

        var notifications = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={mentioned}");

        notifications.Should().BeEmpty();
        _factory.MessageClient.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnknownRecipient_ProducesNoNotification()
    {
        var author = Guid.NewGuid();
        var ghost = Guid.NewGuid();

        _factory.StubUser(author, "Patrick");
        _factory.StubUnknownUser(ghost);

        await HandleAsync(Events.MessagePosted(
            channelId: Guid.NewGuid(),
            authorId: author,
            content: Events.MentionOf(ghost)));

        var notifications = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={ghost}");

        notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ABrokenUserService_IsRetried_AndTheNotificationIsStillDelivered()
    {
        var author = Guid.NewGuid();
        var mentioned = Guid.NewGuid();

        _factory.StubBrokenUserService(author);
        _factory.StubBrokenUserService(mentioned);

        await HandleAsync(Events.MessagePosted(
            channelId: Guid.NewGuid(),
            authorId: author,
            content: Events.MentionOf(mentioned)));

        var notifications = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={mentioned}");

        // Fail open: the mention survives an outage, only the author's name is generic.
        notifications.Should().ContainSingle()
            .Which.Title.Should().Be("Someone mentioned you");

        var attemptsForAuthor = _factory.UserService.LogEntries
            .Count(entry => entry.RequestMessage?.Path == $"/api/users/{author}");
        attemptsForAuthor.Should().BeGreaterThan(1, "the resilience pipeline retries a failing service");
    }

    [Fact]
    public async Task AMessageWithoutMentions_IsIgnored()
    {
        _factory.MessageClient.Clear();

        await HandleAsync(Events.MessagePosted(content: "good morning everybody"));

        _factory.MessageClient.Published.Should().BeEmpty();
        _factory.UserService.LogEntries.Should().BeEmpty("no lookups are needed when nobody is mentioned");
    }

    /// <summary>
    /// Runs the registered handler the same way the broker subscription would: resolved
    /// from a fresh dependency injection scope.
    /// </summary>
    private async Task HandleAsync(MessagePostedEvent message)
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<MessagePostedHandler>();

        await handler.Handle(message, CancellationToken.None);
    }
}
