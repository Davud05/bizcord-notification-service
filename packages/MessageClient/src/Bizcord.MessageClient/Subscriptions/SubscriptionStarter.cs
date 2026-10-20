using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bizcord.MessageClient.Subscriptions;

/// <summary>
/// Wires one <typeparamref name="THandler"/> to one <typeparamref name="TMessage"/>.
/// Each incoming message is handled in its own dependency injection scope, so handlers
/// can safely depend on scoped services.
/// </summary>
public sealed class SubscriptionStarter<TMessage, THandler> : ISubscriptionStarter
    where TMessage : class
    where THandler : class, IMessageHandler<TMessage>
{
    private readonly IMessageClient _messageClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessageClientOptions _options;
    private readonly ILogger<SubscriptionStarter<TMessage, THandler>> _logger;

    public SubscriptionStarter(
        IMessageClient messageClient,
        IServiceScopeFactory scopeFactory,
        IOptions<MessageClientOptions> options,
        ILogger<SubscriptionStarter<TMessage, THandler>> logger)
    {
        _messageClient = messageClient;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _messageClient.SubscribeAsync<TMessage>(_options.SubscriptionId, HandleAsync, cancellationToken);

    private async Task HandleAsync(TMessage message, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();

        try
        {
            await handler.Handle(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Let the exception bubble up: EasyNetQ moves the message to its error queue
            // instead of silently dropping it, so it can be inspected and re-published.
            _logger.LogError(
                exception,
                "Handler {Handler} failed while processing {MessageType}.",
                typeof(THandler).Name,
                typeof(TMessage).Name);
            throw;
        }
    }
}
