using Bizcord.MessageClient;
using Bizcord.Shared.Contracts.Events;
using EasyNetQ;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Bizcord.NotificationService.UnitTests.Messaging;

/// <summary>
/// Unit tests for the EasyNetQ adapter. The broker is mocked at the EasyNetQ interface,
/// which keeps the test fast and proves the adapter forwards to the library correctly.
///
/// Note that only interface members can be mocked: Moq fails with "unsupported
/// expression" on extension methods such as <c>IPubSub.PublishAsync(message, token)</c>,
/// so the expectation is set on the interface method that the extension calls.
/// </summary>
public class EasyNetQMessageClientTests
{
    private readonly Mock<IPubSub> _pubSub = new();
    private readonly Mock<IAdvancedBus> _advancedBus = new();
    private readonly Mock<IBus> _bus = new();

    public EasyNetQMessageClientTests()
    {
        _bus.SetupGet(bus => bus.PubSub).Returns(_pubSub.Object);
        _bus.SetupGet(bus => bus.Advanced).Returns(_advancedBus.Object);
    }

    [Fact]
    public async Task PublishAsync_ForwardsTheMessageToTheBroker()
    {
        _pubSub
            .Setup(pubSub => pubSub.PublishAsync(
                It.IsAny<MentionDetectedEvent>(),
                It.IsAny<Action<IPublishConfiguration>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var client = CreateClient();
        var @event = new MentionDetectedEvent
        {
            MessageId = Guid.NewGuid(),
            MentionKind = "User",
            Preview = "hello"
        };

        await client.PublishAsync(@event);

        _pubSub.Verify(
            pubSub => pubSub.PublishAsync(
                @event,
                It.IsAny<Action<IPublishConfiguration>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Throws_OnANullMessage()
    {
        await using var client = CreateClient();

        var act = () => client.PublishAsync<MentionDetectedEvent>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SubscribeAsync_Throws_OnAnEmptySubscriptionId()
    {
        await using var client = CreateClient();

        var act = () => client.SubscribeAsync<MessagePostedEvent>("  ", (_, _) => Task.CompletedTask);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IsConnected_IsFalse_BeforeTheBrokerReportsAConnection()
    {
        await using var client = CreateClient();

        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_Throws_AfterDisposal()
    {
        var client = CreateClient();
        await client.DisposeAsync();

        var act = () => client.PublishAsync(new NotificationReadEvent());

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void ToConnectionString_ContainsTheConfiguredBroker()
    {
        var options = new MessageClientOptions
        {
            Host = "rabbitmq",
            Port = 5672,
            Username = "bizcord",
            Password = "secret"
        };

        options.ToConnectionString().Should()
            .Contain("host=rabbitmq:5672")
            .And.Contain("username=bizcord")
            .And.Contain("publisherConfirms=true");
    }

    private EasyNetQMessageClient CreateClient() =>
        new(
            _bus.Object,
            Options.Create(new MessageClientOptions { SubscriptionId = "unit-tests" }),
            NullLogger<EasyNetQMessageClient>.Instance);
}
