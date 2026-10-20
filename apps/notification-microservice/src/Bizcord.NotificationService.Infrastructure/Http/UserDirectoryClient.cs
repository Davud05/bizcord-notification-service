using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bizcord.NotificationService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bizcord.NotificationService.Infrastructure.Http;

/// <summary>
/// REST adapter for the User service. Every failure mode is mapped to an explicit
/// <see cref="UserLookupResult"/> so the application layer never has to catch
/// <see cref="HttpRequestException"/> or interpret status codes.
/// </summary>
public sealed class UserDirectoryClient : IUserDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly DownstreamServiceOptions _options;
    private readonly ILogger<UserDirectoryClient> _logger;

    public UserDirectoryClient(
        HttpClient httpClient,
        IOptions<DownstreamServiceOptions> options,
        ILogger<UserDirectoryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserLookupResult> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_options.UserDirectory.Enabled)
        {
            return UserLookupResult.Unavailable();
        }

        try
        {
            using var response = await _httpClient.GetAsync($"api/users/{userId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UserLookupResult.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "User service answered {StatusCode} for user {UserId}.",
                    (int)response.StatusCode,
                    userId);

                return UserLookupResult.Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);

            if (payload is null || string.IsNullOrWhiteSpace(payload.DisplayName))
            {
                _logger.LogWarning("User service returned an unusable payload for user {UserId}.", userId);

                return UserLookupResult.Unavailable();
            }

            return UserLookupResult.Found(new UserSummary(
                payload.Id == Guid.Empty ? userId : payload.Id,
                payload.DisplayName,
                payload.IsActive));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Our own caller gave up; that is not a downstream failure.
            throw;
        }
        catch (Exception exception)
        {
            // Everything the transport can throw - connection refused, a Polly timeout,
            // an open circuit breaker, malformed JSON - means the same thing to the
            // application layer: right now this service cannot answer.
            _logger.LogWarning(exception, "Could not reach the User service for user {UserId}.", userId);

            return UserLookupResult.Unavailable();
        }
    }

    /// <summary>The shape this service expects from <c>GET /api/users/{id}</c>.</summary>
    private sealed record UserResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; init; } = true;
    }
}
