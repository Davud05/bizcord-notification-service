using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Notifications;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.Application.Notifications;

/// <summary>
/// Translates between the internal domain model and the shared model. Having one place
/// for this means internal refactoring never leaks into the published contract.
/// </summary>
public static class NotificationMapper
{
    public static NotificationDto ToDto(Notification notification) =>
        new()
        {
            Id = notification.Id,
            RecipientId = notification.Recipient.UserId,
            ChannelId = notification.Source?.ChannelId,
            MessageId = notification.Source?.MessageId,
            Type = ToContract(notification.Type),
            Title = notification.Content.Title,
            Body = notification.Content.Body,
            Status = ToContract(notification.Status),
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };

    public static string ToContract(NotificationType type) => type switch
    {
        NotificationType.Mention => NotificationTypes.Mention,
        NotificationType.ChannelAnnouncement => NotificationTypes.ChannelAnnouncement,
        NotificationType.DirectMessage => NotificationTypes.DirectMessage,
        NotificationType.System => NotificationTypes.System,
        _ => NotificationTypes.System
    };

    public static string ToContract(NotificationStatus status) => status switch
    {
        NotificationStatus.Unread => NotificationStatuses.Unread,
        NotificationStatus.Read => NotificationStatuses.Read,
        NotificationStatus.Archived => NotificationStatuses.Archived,
        _ => NotificationStatuses.Unread
    };

    public static NotificationType ToNotificationType(string? value) => value switch
    {
        null or "" => NotificationType.System,
        _ when value.Equals(NotificationTypes.Mention, StringComparison.OrdinalIgnoreCase) => NotificationType.Mention,
        _ when value.Equals(NotificationTypes.ChannelAnnouncement, StringComparison.OrdinalIgnoreCase) => NotificationType.ChannelAnnouncement,
        _ when value.Equals(NotificationTypes.DirectMessage, StringComparison.OrdinalIgnoreCase) => NotificationType.DirectMessage,
        _ when value.Equals(NotificationTypes.System, StringComparison.OrdinalIgnoreCase) => NotificationType.System,
        _ => throw new DomainException($"'{value}' is not a known notification type.")
    };

    public static NotificationStatus? ToNotificationStatus(string? value) => value switch
    {
        null or "" => null,
        _ when value.Equals(NotificationStatuses.Unread, StringComparison.OrdinalIgnoreCase) => NotificationStatus.Unread,
        _ when value.Equals(NotificationStatuses.Read, StringComparison.OrdinalIgnoreCase) => NotificationStatus.Read,
        _ when value.Equals(NotificationStatuses.Archived, StringComparison.OrdinalIgnoreCase) => NotificationStatus.Archived,
        _ => throw new DomainException($"'{value}' is not a known notification status.")
    };
}
