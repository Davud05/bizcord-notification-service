using EasyNetQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bizcord.MessageClient;

/// <summary>
/// RabbitMQ adapter for <see cref="IMessageClient"/>, implemented on top of EasyNetQ.
/// This is the only type in the solution that knows EasyNetQ exists.
/// </summary>
public sealed class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;
    private readonly MessageClientOptions _options;
    private readonly ILogger<EasyNetQMessageClient> _logger;
    private readonly List<SubscriptionResult> _subscriptions = [];
    private readonly Lock _subscriptionLock = new();
    private volatile bool _connected;
    private bool _disposed;

    public EasyNetQMessageClient(
        IBus bus,
        IOptions<MessageClientOptions> options,
        ILogger<EasyNetQMessageClient> logger)
    {
        _bus = bus;
        _options = options.Value;
        _logger = logger;

        _bus.Advanced.Connected += OnConnected;
        _bus.Advanced.Disconnected += OnDisconnected;
    }

    /// <summary>
    /// Best-effort view of the broker connection, kept up to date by the connection
    /// events of the underlying transport. Used by the readiness probe.
    /// </summary>
    public bool IsConnected => !_disposed && _connected;

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _bus.PubSub.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Published {MessageType} to the broker.", typeof(TMessage).Name);
    }

    public Task SubscribeAsync<TMessage>(
        string subscriptionId,
        IMessageHandler<TMessage> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeAsync<TMessage>(subscriptionId, handler.Handle, cancellationToken);
    }

    public async Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscription = await _bus.PubSub
            .SubscribeAsync(subscriptionId, handler, _ => { }, cancellationToken)
            .ConfigureAwait(false);

        lock (_subscriptionLock)
        {
            _subscriptions.Add(subscription);
        }

        _logger.LogInformation(
            "Subscribed to {MessageType} on queue {Queue} with subscription id {SubscriptionId}.",
            typeof(TMessage).Name,
            subscription.Queue.Name,
            subscriptionId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _bus.Advanced.Connected -= OnConnected;
        _bus.Advanced.Disconnected -= OnDisconnected;

        SubscriptionResult[] subscriptions;
        lock (_subscriptionLock)
        {
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }

        // The bus itself is owned by the dependency injection container, which disposes
        // it when the host shuts down.
        _logger.LogInformation("Message client for {SubscriptionId} disposed.", _options.SubscriptionId);
    }

    private void OnConnected(object? sender, ConnectedEventArgs args)
    {
        _connected = true;
        _logger.LogInformation("Connected to the broker at {Host}:{Port}.", args.Hostname, args.Port);
    }

    private void OnDisconnected(object? sender, DisconnectedEventArgs args)
    {
        _connected = false;
        _logger.LogWarning(
            "Disconnected from the broker at {Host}:{Port}. Reason: {Reason}",
            args.Hostname,
            args.Port,
            args.Reason);
    }
}
