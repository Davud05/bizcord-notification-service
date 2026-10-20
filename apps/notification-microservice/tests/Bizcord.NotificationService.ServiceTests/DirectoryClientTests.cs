using Bizcord.NotificationService.Application.Abstractions;
using Bizcord.NotificationService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bizcord.NotificationService.ServiceTests;

/// <summary>
/// Service tests for the outgoing REST calls. WireMock plays the part of the services the
/// other teams own, so every failure mode they can produce is covered here instead of
/// being discovered in production: a missing user, a 500, a timeout and a payload that
/// does not match the agreed contract.
/// </summary>
public class DirectoryClientTests : IAsyncDisposable
{
    private readonly WireMockServer _userService = WireMockServer.Start();
    private readonly WireMockServer _channelService = WireMockServer.Start();
    private readonly ServiceProvider _services;

    public DirectoryClientTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:UserDirectory:BaseUrl"] = _userService.Url,
                ["Services:ChannelDirectory:BaseUrl"] = _channelService.Url
            })
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddNotificationInfrastructure(configuration)
            .BuildServiceProvider();
    }

    private IUserDirectoryClient Users => _services.GetRequiredService<IUserDirectoryClient>();

    private IChannelDirectoryClient Channels => _services.GetRequiredService<IChannelDirectoryClient>();

    [Fact]
    public async Task GetUser_MapsTheAgreedPayload()
    {
        var userId = Guid.NewGuid();
        _userService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = userId, displayName = "Davud", isActive = true }));

        var result = await Users.GetUserAsync(userId);

        result.Status.Should().Be(UserLookupStatus.Found);
        result.User!.Id.Should().Be(userId);
        result.User.DisplayName.Should().Be("Davud");
        result.User.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetUser_ReportsNotFound_OnA404()
    {
        var userId = Guid.NewGuid();
        _userService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var result = await Users.GetUserAsync(userId);

        result.Status.Should().Be(UserLookupStatus.NotFound);
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task GetUser_ReportsUnavailable_OnAServerError()
    {
        var userId = Guid.NewGuid();
        _userService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var result = await Users.GetUserAsync(userId);

        result.Status.Should().Be(UserLookupStatus.Unavailable);
        _userService.LogEntries.Should().HaveCountGreaterThan(1, "a 503 is retried");
    }

    [Fact]
    public async Task GetUser_ReportsUnavailable_WhenTheServiceIsTooSlow()
    {
        var userId = Guid.NewGuid();
        _userService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(4))
                .WithBodyAsJson(new { id = userId, displayName = "Slowpoke", isActive = true }));

        var result = await Users.GetUserAsync(userId);

        result.Status.Should().Be(UserLookupStatus.Unavailable);
    }

    [Fact]
    public async Task GetUser_ReportsUnavailable_OnAPayloadThatBreaksTheContract()
    {
        var userId = Guid.NewGuid();
        _userService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = userId, nickname = "no display name here" }));

        var result = await Users.GetUserAsync(userId);

        result.Status.Should().Be(UserLookupStatus.Unavailable);
    }

    [Fact]
    public async Task GetMembers_MapsTheAgreedPayload()
    {
        var channelId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _channelService
            .Given(Request.Create().WithPath($"/api/channels/{channelId}/members").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { channelId, memberIds = new[] { first, second, second } }));

        var members = await Channels.GetMemberIdsAsync(channelId);

        members.Should().Equal(first, second);
    }

    [Fact]
    public async Task GetMembers_ReturnsEmpty_WhenTheChannelIsUnknown()
    {
        var channelId = Guid.NewGuid();
        _channelService
            .Given(Request.Create().WithPath($"/api/channels/{channelId}/members").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        (await Channels.GetMemberIdsAsync(channelId)).Should().BeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _userService.Stop();
        _userService.Dispose();
        _channelService.Stop();
        _channelService.Dispose();
    }
}
