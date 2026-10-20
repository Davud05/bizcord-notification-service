using Bizcord.NotificationService.Application.Abstractions;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.NotificationService.Domain.Preferences;
using Bizcord.NotificationService.Infrastructure.Http;
using Bizcord.NotificationService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bizcord.NotificationService.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the adapters: storage and the HTTP clients for the other microservices.
    /// This is the only place that decides *how* things are stored and called.
    /// </summary>
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DownstreamServiceOptions>()
            .Bind(configuration.GetSection(DownstreamServiceOptions.SectionName));

        // Singletons because the store lives in memory for the lifetime of the process.
        // A database-backed implementation would be registered as scoped instead.
        services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        services.AddSingleton<INotificationPreferenceRepository, InMemoryNotificationPreferenceRepository>();

        // The base addresses are resolved from options when the client is created, not
        // when it is registered, so environment specific configuration is always picked
        // up - including the WireMock URLs the service tests inject.
        services
            .AddHttpClient<IUserDirectoryClient, UserDirectoryClient>((provider, client) =>
            {
                var endpoints = provider.GetRequiredService<IOptions<DownstreamServiceOptions>>().Value;
                client.BaseAddress = NormalizeBaseAddress(endpoints.UserDirectory.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddResilience();

        services
            .AddHttpClient<IChannelDirectoryClient, ChannelDirectoryClient>((provider, client) =>
            {
                var endpoints = provider.GetRequiredService<IOptions<DownstreamServiceOptions>>().Value;
                client.BaseAddress = NormalizeBaseAddress(endpoints.ChannelDirectory.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddResilience();

        return services;
    }

    /// <summary>
    /// Retry, timeout and circuit breaker for calls to other services. A hiccup in the
    /// User service should not turn into a lost notification, and a dead User service
    /// should not keep every message handler waiting.
    /// </summary>
    private static IHttpClientBuilder AddResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(8);
        });

        return builder;
    }

    private static Uri NormalizeBaseAddress(string baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:5080" : baseUrl.Trim();

        // HttpClient drops the last path segment of a relative request unless the base
        // address ends with a slash, which is a classic source of 404s.
        return new Uri(value.EndsWith('/') ? value : value + "/", UriKind.Absolute);
    }
}
