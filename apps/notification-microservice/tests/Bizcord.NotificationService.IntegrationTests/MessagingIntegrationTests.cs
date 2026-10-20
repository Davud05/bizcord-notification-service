using System.Net;
using System.Net.Http.Json;
using Bizcord.NotificationService.IntegrationTests.Infrastructure;
using Bizcord.NotificationService.TestSupport;
using Bizcord.Shared.Contracts.Events;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.IntegrationTests;

/// <summary>
/// End-to-end tests over a real broker: HTTP in, AMQP out and AMQP in, HTTP out.
/// They are skipped - not failed - when RabbitMQ is not running, so
/// <c>dotnet test</c> stays green on a laptop without Docker.
///
/// Start the broker with: <c>docker compose up -d rabbitmq</c>
/// </summary>
[Collection("Broker")]
public class MessagingIntegrationTests : IAsyncLifetime
{
    private IntegrationTestFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(!BrokerAvailability.IsAvailable(), BrokerAvailability.SkipReason);

        _factory = new IntegrationTestFactory();
        await _factory.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>
    /// The contract test that matters most for a consumer: a message published by the
    /// Messaging team is really picked up from the broker, really turns into a
    /// notification, and the resulting event really carries the fields the rest of the
    /// system reads.
    /// </summary>
    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndMentionDetectedEventIsPublished()
    {
        var mentioned = Guid.NewGuid();
        var author = Guid.NewGuid();
        var channel = Guid.NewGuid();

        await using var capture = new MessageCapture<MentionDetectedEvent>(_factory.MessageClient);
        await capture.WaitUntilReadyAsync();

        var message = Events.MessagePosted(
            channelId: channel,
            authorId: author,
            content: Events.MentionOf(mentioned, "the deploy is done"));

        await _factory.MessageClient.PublishAsync(message);

        var detected = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(20));

        detected.MessageId.Should().Be(message.MessageId, "the message id ties the whole flow together");
        detected.ChannelId.Should().Be(channel);
        detected.AuthorId.Should().Be(author);
        detected.RecipientId.Should().Be(mentioned);
        detected.MentionKind.Should().Be("User");
        detected.NotificationId.Should().NotBe(Guid.Empty);
        detected.Preview.Should().Contain("the deploy is done");
        detected.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // ... and the notification is readable through the REST API.
        var client = _factory.CreateClient();
        var notification = await client.GetFromJsonAsync<NotificationDto>(
            $"/api/notifications/{detected.NotificationId}");

        notification.Should().NotBeNull();
        notification!.RecipientId.Should().Be(mentioned);
        notification.Type.Should().Be(NotificationTypes.Mention);
        notification.Status.Should().Be(NotificationStatuses.Unread);
    }

    [Fact]
    public async Task PostNotification_Returns201_AndPersistsTheNotification()
    {
        var client = _factory.CreateClient();
        var recipientId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId,
            type = NotificationTypes.System,
            title = "Deployment finished",
            body = "The notification service is live"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<NotificationDto>();

        var reread = await client.GetAsync($"/api/notifications/{created!.Id}");
        reread.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MarkingANotificationAsRead_PublishesNotificationReadEventOnTheBroker()
    {
        var client = _factory.CreateClient();

        await using var capture = new MessageCapture<NotificationReadEvent>(_factory.MessageClient);
        await capture.WaitUntilReadyAsync();

        var created = await (await client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId = Guid.NewGuid(),
            type = NotificationTypes.System,
            title = "Read me",
            body = "and tell the rest of the system about it"
        })).Content.ReadFromJsonAsync<NotificationDto>();

        var read = await client.PostAsync($"/api/notifications/{created!.Id}/read", content: null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var published = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(20));

        published.NotificationId.Should().Be(created.Id);
        published.RecipientId.Should().Be(created.RecipientId);
        published.ReadAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TheReadinessProbe_ReportsTheBrokerConnection()
    {
        var client = _factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");

        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}

/// <summary>
/// One broker-backed test class at a time: parallel hosts would each open their own
/// connection and make timing assertions flaky.
/// </summary>
[CollectionDefinition("Broker", DisableParallelization = true)]
public class BrokerCollection;
