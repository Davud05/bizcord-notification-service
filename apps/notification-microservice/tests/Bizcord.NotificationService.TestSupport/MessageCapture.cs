using System.Threading.Channels;
using Bizcord.MessageClient;

namespace Bizcord.NotificationService.TestSupport;

/// <summary>
/// Subscribes to a message type on a real broker and lets a test wait for the next
/// message of that type. Used by the integration tests to assert what the service
/// actually puts on the wire.
/// </summary>
public sealed class MessageCapture<TMessage> : IAsyncDisposable
    where TMessage : class
{
    private readonly Channel<TMessage> _messages = Channel.CreateUnbounded<TMessage>();
    private readonly Task _subscription;

    public MessageCapture(IMessageClient messageClient, string? subscriptionId = null)
    {
        ArgumentNullException.ThrowIfNull(messageClient);

        // A unique subscription id gives this capture its own queue, so it never steals
        // messages from the service's own subscription.
        var id = subscriptionId ?? $"test-capture-{typeof(TMessage).Name}-{Guid.NewGuid():N}";

        _subscription = messageClient.SubscribeAsync<TMessage>(
            id,
            (message, _) =>
            {
                // TryWrite instead of WriteAsync: a message that arrives after the test has
                // finished should be dropped, not turned into a broker level error.
                _messages.Writer.TryWrite(message);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Awaits that the subscription is actually established. Call this before publishing,
    /// otherwise the message can be gone before the queue exists.
    /// </summary>
    public Task WaitUntilReadyAsync() => _subscription;

    /// <summary>Waits for the next captured message, failing the test on timeout.</summary>
    public Task<TMessage> WaitForMessageAsync(TimeSpan timeout) =>
        WaitForMessageAsync(_ => true, timeout);

    /// <summary>
    /// Waits for the next captured message that matches <paramref name="predicate"/>,
    /// failing the test on timeout.
    ///
    /// The predicate matters on a shared broker: a locally running instance of the service
    /// is bound to the same exchange and publishes its own copy of the result event, so a
    /// test has to recognise the message that belongs to it - usually by correlating on the
    /// message id it published.
    /// </summary>
    public async Task<TMessage> WaitForMessageAsync(Func<TMessage, bool> predicate, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        await WaitUntilReadyAsync();

        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            while (true)
            {
                var message = await _messages.Reader.ReadAsync(cancellation.Token);

                if (predicate(message))
                {
                    return message;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No matching {typeof(TMessage).Name} was received within {timeout.TotalSeconds:0.#} seconds.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _messages.Writer.TryComplete();

        try
        {
            await _subscription;
        }
        catch
        {
            // A failed subscription is reported by the test that waited for a message.
        }
    }
}
