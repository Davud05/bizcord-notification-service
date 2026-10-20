using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bizcord.NotificationService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bizcord.NotificationService.Infrastructure.Http;

/// <summary>
/// REST adapter for the Channel service, used to expand channel-wide mentions. A failing
/// lookup returns an empty member list: there is nobody to notify, which the handler logs
/// and moves on from.
/// </summary>
public sealed class ChannelDirectoryClient : IChannelDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly DownstreamServiceOptions _options;
    private readonly ILogger<ChannelDirectoryClient> _logger;

    public ChannelDirectoryClient(
        HttpClient httpClient,
        IOptions<DownstreamServiceOptions> options,
        ILogger<ChannelDirectoryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetMemberIdsAsync(
        Guid channelId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.ChannelDirectory.Enabled)
        {
            return [];
        }

        try
        {
            using var response = await _httpClient.GetAsync($"api/channels/{channelId}/members", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Channel service answered {StatusCode} for channel {ChannelId}.",
                    (int)response.StatusCode,
                    channelId);

                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<ChannelMembersResponse>(cancellationToken);

            return payload?.MemberIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not reach the Channel service for channel {ChannelId}.", channelId);

            return [];
        }
    }

    /// <summary>The shape this service expects from <c>GET /api/channels/{id}/members</c>.</summary>
    private sealed record ChannelMembersResponse
    {
        [JsonPropertyName("channelId")]
        public Guid ChannelId { get; init; }

        [JsonPropertyName("memberIds")]
        public Guid[]? MemberIds { get; init; }
    }
}
