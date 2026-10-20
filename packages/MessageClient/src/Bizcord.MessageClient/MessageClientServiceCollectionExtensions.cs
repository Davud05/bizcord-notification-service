using Bizcord.MessageClient.Subscriptions;
using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bizcord.MessageClient;

/// <summary>
/// Registration helpers so a microservice can get a working message client with one line
/// of code in <c>Program.cs</c>.
/// </summary>
public static class MessageClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EasyNetQ backed <see cref="IMessageClient"/> and the hosted service
    /// that starts the subscriptions registered with <see cref="AddMessageHandler{TMessage,THandler}"/>.
    /// </summary>
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string? subscriptionId = null)
    {
        var options = ReadOptions(configuration, subscriptionId);

        services.AddSingleton<IOptions<MessageClientOptions>>(Options.Create(options));
        services.AddEasyNetQ(options.ToConnectionString()).UseSystemTextJson();
        services.TryAddSingleton<IMessageClient, EasyNetQMessageClient>();
        services.AddHostedService<MessageSubscriberService>();

        return services;
    }

    /// <summary>
    /// Registers a handler and the subscription that feeds it.
    /// </summary>
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<THandler>();
        services.AddSingleton<ISubscriptionStarter, SubscriptionStarter<TMessage, THandler>>();

        return services;
    }

    private static MessageClientOptions ReadOptions(IConfiguration configuration, string? subscriptionId)
    {
        var options = new MessageClientOptions();
        configuration.GetSection(MessageClientOptions.SectionName).Bind(options);

        // RABBITMQ_HOST is the variable name used by the course's docker-compose file, so
        // support it as a shorthand for RabbitMq__Host.
        var hostOverride = configuration["RABBITMQ_HOST"];
        if (!string.IsNullOrWhiteSpace(hostOverride))
        {
            options.Host = hostOverride;
        }

        // The argument is a fallback: explicit configuration always wins, which lets a
        // test or a second deployment of the same service use its own queue.
        var configuredSubscriptionId = configuration[$"{MessageClientOptions.SectionName}:SubscriptionId"];

        if (!string.IsNullOrWhiteSpace(subscriptionId) && string.IsNullOrWhiteSpace(configuredSubscriptionId))
        {
            options.SubscriptionId = subscriptionId;
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("RabbitMq:Host must be configured.");
        }

        return options;
    }
}
