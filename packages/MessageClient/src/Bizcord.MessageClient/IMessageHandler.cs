namespace Bizcord.MessageClient;

/// <summary>
/// Handles a single message type. Handlers are resolved from a dependency injection
/// scope per message, so they can depend on scoped services such as repositories.
/// </summary>
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    Task Handle(TMessage message, CancellationToken cancellationToken);
}
