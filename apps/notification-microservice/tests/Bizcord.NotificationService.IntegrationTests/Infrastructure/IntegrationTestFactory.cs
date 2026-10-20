using Bizcord.MessageClient;
using Bizcord.NotificationService.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcord.NotificationService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the service against a real RabbitMQ. Nothing is stubbed on the messaging side:
/// these tests prove that what the service publishes can actually be consumed, and that
/// what other teams publish is actually consumed by us.
///
/// The User and Channel services are switched off instead of stubbed, which also exercises
/// the fail-open path of the handler. Their HTTP contract is covered by the service tests.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    /// <summary>Own queue per test run, so a locally running container cannot steal messages.</summary>
    public string SubscriptionId { get; } = $"notification-service-it-{Guid.NewGuid():N}";

    public IMessageClient MessageClient => Services.GetRequiredService<IMessageClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = BrokerAvailability.Host,
                ["RabbitMq:Port"] = BrokerAvailability.Port.ToString(),
                ["RabbitMq:SubscriptionId"] = SubscriptionId,
                ["Services:UserDirectory:Enabled"] = "false",
                ["Services:ChannelDirectory:Enabled"] = "false",
                ["EnableSwagger"] = "false"
            }));
    }

    /// <summary>
    /// Waits until the readiness probe reports a live broker connection, so a test never
    /// publishes into a void where its queue does not exist yet.
    /// </summary>
    public async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var client = CreateClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/health/ready");
            var status = await response.Content.ReadAsStringAsync();

            if (status.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
            {
                // The connection is up; give the consumer a moment to declare and bind
                // its queue before anything is published.
                await Task.Delay(TimeSpan.FromSeconds(1));
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"The service did not become ready within {timeout.TotalSeconds:0} seconds. {BrokerAvailability.SkipReason}");
    }
}
