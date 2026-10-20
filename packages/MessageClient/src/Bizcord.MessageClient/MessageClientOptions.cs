namespace Bizcord.MessageClient;

/// <summary>
/// Configuration of the message client. Bound from the <c>RabbitMq</c> configuration
/// section, which means every value can be overridden with an environment variable
/// (for example <c>RabbitMq__Host=rabbitmq</c> or the shorthand <c>RABBITMQ_HOST</c>).
/// </summary>
public sealed class MessageClientOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public ushort Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>
    /// Logical name of this service. Used as the default subscription id, so queues in
    /// RabbitMQ are named after the consumer that owns them.
    /// </summary>
    public string SubscriptionId { get; set; } = "bizcord-service";

    /// <summary>Seconds EasyNetQ waits for a broker operation before giving up.</summary>
    public ushort TimeoutSeconds { get; set; } = 10;

    /// <summary>Messages a consumer may process concurrently.</summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Builds the EasyNetQ connection string. Kept here so the rest of the application
    /// never has to know what EasyNetQ expects.
    /// </summary>
    public string ToConnectionString() =>
        $"host={Host}:{Port};virtualHost={VirtualHost};username={Username};password={Password};" +
        $"timeout={TimeoutSeconds};prefetchcount={PrefetchCount};publisherConfirms=true;persistentMessages=true";
}
