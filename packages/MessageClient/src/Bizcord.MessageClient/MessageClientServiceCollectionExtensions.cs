using Bizcord.MessageClient.Subscriptions;
using EasyNetQ;
using EasyNetQ.ConnectionString;
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
        // Configuration is read when the options are first resolved, never here. A test host
        // adds its own configuration sources after Program.cs has run, so anything read at
        // registration time would silently ignore them.
        services.AddOptions<MessageClientOptions>()
            .Bind(configuration.GetSection(MessageClientOptions.SectionName))
            .PostConfigure(options => ApplyFallbacks(options, configuration, subscriptionId))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SubscriptionId), "RabbitMq:SubscriptionId must be configured.");

        services.AddEasyNetQ(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MessageClientOptions>>().Value;

            return new ConnectionStringParser().Parse(options.ToConnectionString());
        }).UseSystemTextJson();

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

    private static void ApplyFallbacks(
        MessageClientOptions options,
        IConfiguration configuration,
        string? subscriptionId)
    {
        // RABBITMQ_HOST is the variable name used by the course's docker-compose file, so
        // support it as a shorthand for RabbitMq__Host.
        var hostOverride = configuration["RABBITMQ_HOST"];
        if (!string.IsNullOrWhiteSpace(hostOverride))
        {
            options.Host = hostOverride;
        }

        // The argument is only a fallback: explicit configuration always wins, which is how
        // a test or a second deployment of the same service gets its own queue.
        if (string.IsNullOrWhiteSpace(options.SubscriptionId) && !string.IsNullOrWhiteSpace(subscriptionId))
        {
            options.SubscriptionId = subscriptionId;
        }
    }
}
