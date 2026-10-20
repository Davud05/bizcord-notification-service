using System.Collections.Concurrent;
using Bizcord.MessageClient;

namespace Bizcord.NotificationService.TestSupport;

/// <summary>
/// In-memory <see cref="IMessageClient"/> for tests: it records everything that was
/// published and can deliver a message to the subscribed handlers without a broker.
///
/// Because the production code depends on the <see cref="IMessageClient"/> interface and
/// not on a concrete class, tests never need Moq tricks such as making methods
/// <c>virtual</c> or adding a parameterless constructor just to be mockable.
/// </summary>
public sealed class FakeMessageClient : IMessageClient
{
    private readonly ConcurrentQueue<object> _published = new();
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _subscribers = new();

    public bool IsConnected { get; set; } = true;

    /// <summary>Every message that was published, in order.</summary>
    public IReadOnlyList<object> Published => [.. _published];

    /// <summary>The subscription ids that were registered, per message type.</summary>
    public ConcurrentBag<(Type MessageType, string SubscriptionId)> Subscriptions { get; } = [];

    /// <summary>Set to throw from <see cref="PublishAsync{TMessage}"/> to simulate a broker outage.</summary>
    public Exception? PublishException { get; set; }

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        if (PublishException is not null)
        {
            return Task.FromException(PublishException);
        }

        _published.Enqueue(message);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        Subscriptions.Add((typeof(TMessage), subscriptionId));

        var handlers = _subscribers.GetOrAdd(typeof(TMessage), _ => []);

        lock (handlers)
        {
            handlers.Add((message, token) => handler((TMessage)message, token));
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TMessage>(
        string subscriptionId,
        IMessageHandler<TMessage> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class =>
        SubscribeAsync<TMessage>(subscriptionId, handler.Handle, cancellationToken);

    /// <summary>Delivers a message to every handler subscribed to <typeparamref name="TMessage"/>.</summary>
    public async Task DeliverAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        if (!_subscribers.TryGetValue(typeof(TMessage), out var handlers))
        {
            return;
        }

        Func<object, CancellationToken, Task>[] snapshot;
        lock (handlers)
        {
            snapshot = [.. handlers];
        }

        foreach (var handler in snapshot)
        {
            await handler(message, cancellationToken);
        }
    }

    /// <summary>Every published message of the given type.</summary>
    public IReadOnlyList<TMessage> PublishedOf<TMessage>() where TMessage : class =>
        [.. _published.OfType<TMessage>()];

    /// <summary>
    /// The one message of the given type that was published, or <c>null</c>. Throws when
    /// more than one was published, because then the test is asserting the wrong thing.
    /// </summary>
    public TMessage? SinglePublished<TMessage>() where TMessage : class =>
        _published.OfType<TMessage>().SingleOrDefault();

    public void Clear() => _published.Clear();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
