namespace Bizcord.NotificationService.Infrastructure.Http;

/// <summary>
/// Where the other microservices live. Configured under <c>Services</c> in
/// <c>appsettings.json</c> and overridable per environment, for example
/// <c>Services__UserDirectory__BaseUrl=http://user-service:8080</c>.
/// </summary>
public sealed class DownstreamServiceOptions
{
    public const string SectionName = "Services";

    public ServiceEndpoint UserDirectory { get; set; } = new();

    public ServiceEndpoint ChannelDirectory { get; set; } = new();
}

public sealed class ServiceEndpoint
{
    public string BaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>
    /// When <c>false</c> the service is treated as not deployed: lookups answer
    /// "unavailable" immediately instead of waiting for a connection to time out. This
    /// keeps the Notification service usable in a system where the other teams' services
    /// are not running yet.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
