using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.Application.Notifications;

/// <summary>
/// The use cases behind the REST API. Controllers only translate HTTP to these calls,
/// which keeps presentation logic out of the business logic (and makes the use cases
/// testable without a web server).
/// </summary>
public interface INotificationAppService
{
    Task<NotificationDto> CreateAsync(CreateNotificationCommand command, CancellationToken cancellationToken = default);

    Task<NotificationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> ListAsync(ListNotificationsQuery query, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<NotificationDto> UpdateAsync(Guid id, UpdateNotificationCommand command, CancellationToken cancellationToken = default);

    Task<NotificationDto> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
