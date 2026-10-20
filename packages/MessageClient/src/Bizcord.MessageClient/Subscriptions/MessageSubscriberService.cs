using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bizcord.MessageClient.Subscriptions;

/// <summary>
/// Starts every registered subscription when the application starts. Running this as a
/// background service keeps the broker out of the startup path: if RabbitMQ is briefly
/// unavailable the API still answers requests (and reports "not ready" on its readiness
/// probe) while the client keeps retrying.
/// </summary>
public sealed class MessageSubscriberService : BackgroundService
{
    private readonly IEnumerable<ISubscriptionStarter> _starters;
    private readonly ILogger<MessageSubscriberService> _logger;

    public MessageSubscriberService(
        IEnumerable<ISubscriptionStarter> starters,
        ILogger<MessageSubscriberService> logger)
    {
        _starters = starters;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var starter in _starters)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await starter.StartAsync(stoppingToken).ConfigureAwait(false);
                    break;
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not start subscription {Subscription}. Retrying in 5 seconds.",
                        starter.GetType().Name);

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
            }
        }

        _logger.LogInformation("All message subscriptions are running.");
    }
}
