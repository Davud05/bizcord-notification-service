# Bizcord.MessageClient

The messaging abstraction every Bizcord service uses. RabbitMQ and EasyNetQ live behind it,
so a service can publish and subscribe without knowing which broker it is talking to.

## Usage

```csharp
// Program.cs
builder.Services.AddMessageClient(builder.Configuration, subscriptionId: "notification-service");
builder.Services.AddMessageHandler<MessagePostedEvent, MessagePostedHandler>();
```

```csharp
// publishing
public sealed class SomeService(IMessageClient messageClient)
{
    public Task Announce(CancellationToken ct) =>
        messageClient.PublishAsync(new MentionDetectedEvent { ... }, ct);
}

// consuming
public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    public Task Handle(MessagePostedEvent message, CancellationToken cancellationToken) { ... }
}
```

Each handled message runs in **its own DI scope**, exactly like an HTTP request, so handlers
can depend on scoped services.

## The interface

```csharp
public interface IMessageClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default) where TMessage : class;
    Task SubscribeAsync<TMessage>(string subscriptionId, Func<TMessage, CancellationToken, Task> handler, CancellationToken ct = default) where TMessage : class;
    Task SubscribeAsync<TMessage>(string subscriptionId, IMessageHandler<TMessage> handler, CancellationToken ct = default) where TMessage : class;
}
```

No exchange, no routing key, no `IBus`. Replacing RabbitMQ with Kafka means adding a
`KafkaMessageClient : IMessageClient` and changing one registration — nothing else in any
service changes.

## Contents

| File | Role |
| --- | --- |
| `IMessageClient.cs` | The abstraction services depend on |
| `IMessageHandler.cs` | What a consumer implements |
| `MessageClientOptions.cs` | Broker settings (`RabbitMq` section), plus the connection string it builds |
| `EasyNetQMessageClient.cs` | **The only type in the solution that references EasyNetQ** |
| `Subscriptions/SubscriptionStarter.cs` | Starts one typed subscription and scopes each message |
| `Subscriptions/MessageSubscriberService.cs` | Background service that retries subscribing every 5 seconds |
| `MessageClientServiceCollectionExtensions.cs` | `AddMessageClient`, `AddMessageHandler<,>` |

## Configuration

Bound from the `RabbitMq` configuration section; `RABBITMQ_HOST` on its own is also accepted
because that is what the course's Compose samples set.

| Setting | Default | Notes |
| --- | --- | --- |
| `Host` / `Port` | `localhost` / `5672` | |
| `Username` / `Password` / `VirtualHost` | `guest` / `guest` / `/` | |
| `SubscriptionId` | required per service | **This is the queue name.** Same value on several instances = competing consumers; a different value per service = fan-out. |
| `Timeout`, `PrefetchCount` | | Publisher confirms and persistent messages are always on |

## Behaviour worth knowing

* **The broker is not on the startup path.** If RabbitMQ is unavailable, the host still
  starts and the subscription is retried in the background. `IsConnected` is what a health
  check should report.
* **A throwing handler does not acknowledge.** The exception is logged and rethrown, so
  EasyNetQ moves the message to its error queue instead of dropping it.
* **Testability was a design goal.** Because the seam is an interface, tests use a
  `FakeMessageClient` or `Mock<IMessageClient>` — no `virtual` keywords, no proxy-class
  constructor tricks, no broker.
