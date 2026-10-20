using System.ComponentModel.DataAnnotations;
using Bizcord.NotificationService.Application.Notifications;

namespace Bizcord.NotificationService.Presentation.Requests;

/// <summary>Body of <c>PUT /api/notifications/{id}</c>.</summary>
public sealed class UpdateNotificationRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    public UpdateNotificationCommand ToCommand() => new() { Title = Title, Body = Body };
}
