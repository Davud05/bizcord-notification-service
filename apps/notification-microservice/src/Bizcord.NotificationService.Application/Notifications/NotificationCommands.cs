using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.Application.Notifications;

/// <summary>Input for creating a notification through the REST API.</summary>
public sealed record CreateNotificationCommand
{
    public Guid RecipientId { get; init; }

    /// <summary>One of <see cref="NotificationTypes"/>.</summary>
    public string Type { get; init; } = NotificationTypes.System;

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public Guid? ChannelId { get; init; }

    public Guid? MessageId { get; init; }

    public Guid? AuthorId { get; init; }

    public DateTime? PostedAt { get; init; }
}

/// <summary>Input for replacing the text of an unread notification.</summary>
public sealed record UpdateNotificationCommand
{
    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;
}

/// <summary>Filter used by the list endpoint.</summary>
public sealed record ListNotificationsQuery
{
    public Guid? RecipientId { get; init; }

    public Guid? ChannelId { get; init; }

    /// <summary>One of <see cref="NotificationStatuses"/>, or <c>null</c> for all.</summary>
    public string? Status { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}
