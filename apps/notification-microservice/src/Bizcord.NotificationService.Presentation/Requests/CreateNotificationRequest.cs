using System.ComponentModel.DataAnnotations;
using Bizcord.NotificationService.Application.Notifications;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.Presentation.Requests;

/// <summary>
/// Body of <c>POST /api/notifications</c>. Input validation lives here (shape and
/// required fields); business rules live in the domain model.
/// </summary>
public sealed class CreateNotificationRequest
{
    [Required]
    public Guid RecipientId { get; init; }

    /// <summary>One of <c>Mention</c>, <c>ChannelAnnouncement</c>, <c>DirectMessage</c>, <c>System</c>.</summary>
    [Required]
    [StringLength(40)]
    public string Type { get; init; } = NotificationTypes.System;

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    /// <summary>Set together with <see cref="MessageId"/> and <see cref="AuthorId"/>.</summary>
    public Guid? ChannelId { get; init; }

    public Guid? MessageId { get; init; }

    public Guid? AuthorId { get; init; }

    public DateTime? PostedAt { get; init; }

    public CreateNotificationCommand ToCommand() =>
        new()
        {
            RecipientId = RecipientId,
            Type = Type,
            Title = Title,
            Body = Body,
            ChannelId = ChannelId,
            MessageId = MessageId,
            AuthorId = AuthorId,
            PostedAt = PostedAt
        };
}
