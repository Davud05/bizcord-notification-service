using System.Net;
using System.Net.Http.Json;
using Bizcord.NotificationService.ServiceTests.Infrastructure;
using Bizcord.Shared.Contracts.Events;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.ServiceTests;

/// <summary>
/// Service tests for the REST API: real HTTP requests against the real application,
/// asserting status codes, payloads and published events.
/// </summary>
public class NotificationsApiTests : IClassFixture<ServiceTestFactory>
{
    private readonly ServiceTestFactory _factory;
    private readonly HttpClient _client;

    public NotificationsApiTests(ServiceTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndPersistsTheNotification()
    {
        var recipientId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId,
            type = NotificationTypes.System,
            title = "Welcome to Bizcord",
            body = "Say hi in #general"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<NotificationDto>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBe(Guid.Empty);
        created.RecipientId.Should().Be(recipientId);
        created.Status.Should().Be(NotificationStatuses.Unread);
        created.ReadAt.Should().BeNull();

        var reread = await _client.GetAsync($"/api/notifications/{created.Id}");
        reread.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reread.Content.ReadFromJsonAsync<NotificationDto>())!.Title.Should().Be("Welcome to Bizcord");
    }

    [Fact]
    public async Task Post_Returns400_WhenTheBodyIsInvalid()
    {
        var response = await _client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId = Guid.NewGuid(),
            type = NotificationTypes.System,
            title = "",
            body = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("Title").And.Contain("Body");
    }

    [Fact]
    public async Task Post_Returns400_OnAnUnknownNotificationType()
    {
        var response = await _client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId = Guid.NewGuid(),
            type = "Carrier pigeon",
            title = "Title",
            body = "Body"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not a known notification type");
    }

    [Fact]
    public async Task Post_Returns400_WhenTheMessageReferenceIsIncomplete()
    {
        var response = await _client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId = Guid.NewGuid(),
            type = NotificationTypes.Mention,
            title = "Patrick mentioned you",
            body = "ping",
            messageId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("messageId, channelId and authorId together");
    }

    [Fact]
    public async Task Get_Returns404_ForAnUnknownNotification()
    {
        var response = await _client.GetAsync($"/api/notifications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Get_FiltersByRecipientAndStatus_AndReportsTheUnreadCount()
    {
        var recipientId = Guid.NewGuid();
        var first = await CreateNotification(recipientId, "First");
        await CreateNotification(recipientId, "Second");

        await _client.PostAsync($"/api/notifications/{first.Id}/read", content: null);

        var unread = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={recipientId}&status={NotificationStatuses.Unread}");
        unread.Should().ContainSingle().Which.Title.Should().Be("Second");

        var all = await _client.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notifications?recipientId={recipientId}");
        all.Should().HaveCount(2);

        var count = await _client.GetFromJsonAsync<UnreadCountDto>(
            $"/api/notifications/unread-count?recipientId={recipientId}");
        count!.Unread.Should().Be(1);
    }

    [Fact]
    public async Task Get_Returns400_OnAnUnreasonablePageSize()
    {
        var response = await _client.GetAsync("/api/notifications?take=5000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ReplacesTheText_AndReturns404ForAnUnknownNotification()
    {
        var created = await CreateNotification(Guid.NewGuid(), "Before");

        var updated = await _client.PutAsJsonAsync(
            $"/api/notifications/{created.Id}",
            new { title = "After", body = "Updated body" });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<NotificationDto>())!.Title.Should().Be("After");

        var missing = await _client.PutAsJsonAsync(
            $"/api/notifications/{Guid.NewGuid()}",
            new { title = "After", body = "Updated body" });

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Read_MarksTheNotificationAsRead_AndPublishesNotificationReadEvent()
    {
        _factory.MessageClient.Clear();
        var created = await CreateNotification(Guid.NewGuid(), "Read me");

        var response = await _client.PostAsync($"/api/notifications/{created.Id}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await response.Content.ReadFromJsonAsync<NotificationDto>();
        read!.Status.Should().Be(NotificationStatuses.Read);
        read.ReadAt.Should().NotBeNull();

        var published = _factory.MessageClient
            .PublishedOf<NotificationReadEvent>()
            .Should().ContainSingle(@event => @event.NotificationId == created.Id).Subject;
        published.RecipientId.Should().Be(created.RecipientId);
    }

    [Fact]
    public async Task Delete_Returns204_AndThen404()
    {
        var created = await CreateNotification(Guid.NewGuid(), "Temporary");

        var deleted = await _client.DeleteAsync($"/api/notifications/{created.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/notifications/{created.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/notifications/{created.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_ReportsLiveness_EvenWithoutABroker()
    {
        var live = await _client.GetAsync("/health/live");

        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<NotificationDto> CreateNotification(Guid recipientId, string title)
    {
        var response = await _client.PostAsJsonAsync("/api/notifications", new
        {
            recipientId,
            type = NotificationTypes.System,
            title,
            body = $"Body of {title}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<NotificationDto>())!;
    }

    private sealed record UnreadCountDto(Guid RecipientId, int Unread);
}
