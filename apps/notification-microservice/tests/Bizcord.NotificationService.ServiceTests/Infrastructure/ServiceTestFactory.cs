using Bizcord.MessageClient;
using Bizcord.MessageClient.Subscriptions;
using Bizcord.NotificationService.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bizcord.NotificationService.ServiceTests.Infrastructure;

/// <summary>
/// Boots the real Notification service in memory with its collaborators replaced by test
/// doubles, which is what makes these *service* tests rather than end-to-end tests:
///
/// <list type="bullet">
///   <item><description>the User and Channel services are stubbed with WireMock,</description></item>
///   <item><description>the broker is replaced by <see cref="FakeMessageClient"/>.</description></item>
/// </list>
///
/// Everything else - routing, model validation, controllers, use cases, the domain model
/// and the in-memory store - is the production code.
/// </summary>
public sealed class ServiceTestFactory : WebApplicationFactory<Program>
{
    public ServiceTestFactory()
    {
        UserService = WireMockServer.Start();
        ChannelService = WireMockServer.Start();
    }

    /// <summary>Stub of the User service owned by another team.</summary>
    public WireMockServer UserService { get; }

    /// <summary>Stub of the Channel service owned by another team.</summary>
    public WireMockServer ChannelService { get; }

    /// <summary>Records everything the service publishes, without needing RabbitMQ.</summary>
    public FakeMessageClient MessageClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:UserDirectory:BaseUrl"] = UserService.Url,
                ["Services:ChannelDirectory:BaseUrl"] = ChannelService.Url,
                ["EnableSwagger"] = "false"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMessageClient>();
            services.AddSingleton<IMessageClient>(MessageClient);

            // The real subscriber would try to reach RabbitMQ; the fake client is driven
            // directly from the tests instead.
            var subscriber = services.FirstOrDefault(service =>
                service.ImplementationType == typeof(MessageSubscriberService));

            if (subscriber is not null)
            {
                services.Remove(subscriber);
            }
        });
    }

    /// <summary>Stubs <c>GET /api/users/{id}</c> on the User service.</summary>
    public void StubUser(Guid userId, string displayName, bool isActive = true) =>
        UserService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = userId, displayName, isActive }));

    /// <summary>Stubs a user that the User service does not know.</summary>
    public void StubUnknownUser(Guid userId) =>
        UserService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

    /// <summary>Stubs a broken User service.</summary>
    public void StubBrokenUserService(Guid userId) =>
        UserService
            .Given(Request.Create().WithPath($"/api/users/{userId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));

    /// <summary>Stubs <c>GET /api/channels/{id}/members</c> on the Channel service.</summary>
    public void StubChannelMembers(Guid channelId, params Guid[] memberIds) =>
        ChannelService
            .Given(Request.Create().WithPath($"/api/channels/{channelId}/members").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { channelId, memberIds }));

    public void ResetStubs()
    {
        UserService.Reset();
        ChannelService.Reset();
        MessageClient.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UserService.Stop();
            UserService.Dispose();
            ChannelService.Stop();
            ChannelService.Dispose();
        }

        base.Dispose(disposing);
    }
}
