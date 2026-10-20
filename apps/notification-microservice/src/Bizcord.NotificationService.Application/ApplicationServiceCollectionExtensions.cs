using Bizcord.NotificationService.Application.Notifications;
using Bizcord.NotificationService.Application.Preferences;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bizcord.NotificationService.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers the use cases of the Notification service.</summary>
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<INotificationAppService, NotificationAppService>();
        services.AddScoped<IPreferenceAppService, PreferenceAppService>();

        return services;
    }
}
