namespace Bizcord.MessageClient;

/// <summary>
/// A broker-agnostic message client. The interface is intentionally tiny: publish a
/// message, subscribe to a message type and tell whether the transport is connected.
/// Everything else (exchanges, queues, routing keys, serialization, retries) is an
/// implementation detail of the adapter, so RabbitMQ can be swapped for e.g. Kafka
/// without touching a single line of domain or application code.
/// </summary>
public interface IMessageClient : IAsyncDisposable
{
    /// <summary>True when the underlying transport has a live connection to the broker.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Publishes a message to all interested subscribers (fan-out / publish-subscribe).
    /// </summary>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Subscribes to a message type with a delegate. The <paramref name="subscriptionId"/>
    /// identifies the durable queue: the same id means "same logical consumer" (competing
    /// consumers / load balancing), a different id means "another independent consumer".
    /// </summary>
    Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Subscribes to a message type with an <see cref="IMessageHandler{TMessage}"/>.
    /// </summary>
    Task SubscribeAsync<TMessage>(
        string subscriptionId,
        IMessageHandler<TMessage> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
