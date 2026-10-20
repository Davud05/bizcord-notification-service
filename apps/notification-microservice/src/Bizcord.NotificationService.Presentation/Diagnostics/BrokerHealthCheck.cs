using Bizcord.MessageClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bizcord.NotificationService.Presentation.Diagnostics;

/// <summary>
/// Readiness check: the service is only able to do its real job - reacting to posted
/// messages - when it is connected to the broker. Liveness stays green so an orchestrator
/// does not restart a perfectly healthy container just because RabbitMQ is restarting.
/// </summary>
public sealed class BrokerHealthCheck : IHealthCheck
{
    private readonly IMessageClient _messageClient;

    public BrokerHealthCheck(IMessageClient messageClient) => _messageClient = messageClient;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_messageClient.IsConnected
            ? HealthCheckResult.Healthy("Connected to the message broker.")
            : HealthCheckResult.Degraded("Not connected to the message broker yet."));
}
