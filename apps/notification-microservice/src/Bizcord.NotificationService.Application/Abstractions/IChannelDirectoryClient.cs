namespace Bizcord.NotificationService.Application.Abstractions;

/// <summary>
/// Synchronous (REST) view of the Channel context, used to expand <c>@here</c> and
/// <c>@everyone</c> into actual members.
/// </summary>
public interface IChannelDirectoryClient
{
    /// <summary>
    /// Members of a channel. Returns an empty list when the channel is unknown or the
    /// Channel service cannot be reached: without members there is nobody to notify, so
    /// a channel-wide mention is simply skipped and logged.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetMemberIdsAsync(Guid channelId, CancellationToken cancellationToken = default);
}
