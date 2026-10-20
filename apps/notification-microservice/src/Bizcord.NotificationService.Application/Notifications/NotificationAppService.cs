using Bizcord.MessageClient;
using Bizcord.NotificationService.Application.Common;
using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.Shared.Contracts.Events;
using Bizcord.Shared.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace Bizcord.NotificationService.Application.Notifications;

/// <inheritdoc />
public sealed class NotificationAppService : INotificationAppService
{
    private readonly INotificationRepository _repository;
    private readonly IMessageClient _messageClient;
    private readonly TimeProvider _clock;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(
        INotificationRepository repository,
        IMessageClient messageClient,
        TimeProvider clock,
        ILogger<NotificationAppService> logger)
    {
        _repository = repository;
        _messageClient = messageClient;
        _clock = clock;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateAsync(
        CreateNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var type = NotificationMapper.ToNotificationType(command.Type);
        var source = BuildSource(command);

        var notification = Notification.Create(
            Recipient.For(command.RecipientId),
            type,
            NotificationContent.Create(command.Title, command.Body),
            source,
            _clock.GetUtcNow().UtcDateTime);

        await _repository.SaveAsync(notification, cancellationToken);

        _logger.LogInformation(
            "Created {Type} notification {NotificationId} for recipient {RecipientId}.",
            type,
            notification.Id,
            notification.Recipient.UserId);

        return NotificationMapper.ToDto(notification);
    }

    public async Task<NotificationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetAsync(id, cancellationToken);

        return notification is null ? null : NotificationMapper.ToDto(notification);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Take is < 1 or > 200)
        {
            throw new DomainException("Take must be between 1 and 200.");
        }

        if (query.Skip < 0)
        {
            throw new DomainException("Skip must not be negative.");
        }

        var notifications = await _repository.ListAsync(
            new NotificationQuery
            {
                RecipientId = query.RecipientId,
                ChannelId = query.ChannelId,
                Status = NotificationMapper.ToNotificationStatus(query.Status),
                Skip = query.Skip,
                Take = query.Take
            },
            cancellationToken);

        return [.. notifications.Select(NotificationMapper.ToDto)];
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        _repository.CountUnreadAsync(recipientId, cancellationToken);

    public async Task<NotificationDto> UpdateAsync(
        Guid id,
        UpdateNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notification = await _repository.GetAsync(id, cancellationToken)
            ?? throw new NotFoundException("Notification", id);

        notification.UpdateContent(NotificationContent.Create(command.Title, command.Body));
        await _repository.SaveAsync(notification, cancellationToken);

        return NotificationMapper.ToDto(notification);
    }

    public async Task<NotificationDto> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetAsync(id, cancellationToken)
            ?? throw new NotFoundException("Notification", id);

        var changed = notification.MarkAsRead(_clock.GetUtcNow().UtcDateTime);

        if (!changed)
        {
            // Reading an already read notification is a no-op, so no event is published.
            return NotificationMapper.ToDto(notification);
        }

        await _repository.SaveAsync(notification, cancellationToken);

        await _messageClient.PublishAsync(
            new NotificationReadEvent
            {
                NotificationId = notification.Id,
                RecipientId = notification.Recipient.UserId,
                MessageId = notification.Source?.MessageId,
                ReadAt = notification.ReadAt!.Value
            },
            cancellationToken);

        return NotificationMapper.ToDto(notification);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            throw new NotFoundException("Notification", id);
        }

        _logger.LogInformation("Deleted notification {NotificationId}.", id);
    }

    private static MessageReference? BuildSource(CreateNotificationCommand command)
    {
        var hasAnySourceField = command.MessageId.HasValue || command.ChannelId.HasValue || command.AuthorId.HasValue;

        if (!hasAnySourceField)
        {
            return null;
        }

        if (!command.MessageId.HasValue || !command.ChannelId.HasValue || !command.AuthorId.HasValue)
        {
            throw new DomainException(
                "A notification that references a message needs messageId, channelId and authorId together.");
        }

        return MessageReference.Create(
            command.MessageId.Value,
            command.ChannelId.Value,
            command.AuthorId.Value,
            command.PostedAt ?? DateTime.UtcNow);
    }
}
