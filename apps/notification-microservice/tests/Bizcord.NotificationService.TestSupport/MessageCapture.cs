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
            (message, _) => _messages.Writer.WriteAsync(message).AsTask());
    }

    /// <summary>
    /// Awaits that the subscription is actually established. Call this before publishing,
    /// otherwise the message can be gone before the queue exists.
    /// </summary>
    public Task WaitUntilReadyAsync() => _subscription;

    /// <summary>Waits for the next captured message, failing the test on timeout.</summary>
    public async Task<TMessage> WaitForMessageAsync(TimeSpan timeout)
    {
        await WaitUntilReadyAsync();

        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            return await _messages.Reader.ReadAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No {typeof(TMessage).Name} was received within {timeout.TotalSeconds:0.#} seconds.");
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
