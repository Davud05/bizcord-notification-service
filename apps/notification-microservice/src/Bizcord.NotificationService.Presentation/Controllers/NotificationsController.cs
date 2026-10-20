using Bizcord.NotificationService.Application.Notifications;
using Bizcord.NotificationService.Presentation.Requests;
using Bizcord.Shared.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.NotificationService.Presentation.Controllers;

/// <summary>
/// CRUD over notifications, for other microservices and for the Bizcord clients.
/// The controller only translates HTTP to use cases: no business logic lives here.
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationAppService _notifications;

    public NotificationsController(INotificationAppService notifications) => _notifications = notifications;

    /// <summary>Creates a notification.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationDto>> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var notification = await _notifications.CreateAsync(request.ToCommand(), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = notification.Id }, notification);
    }

    /// <summary>Reads a single notification.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetAsync(id, cancellationToken);

        return notification is null
            ? Problem(title: "Resource not found", detail: $"Notification '{id}' was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(notification);
    }

    /// <summary>Reads notifications, optionally filtered by recipient, channel and status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(
        [FromQuery] Guid? recipientId,
        [FromQuery] Guid? channelId,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notifications.ListAsync(
            new ListNotificationsQuery
            {
                RecipientId = recipientId,
                ChannelId = channelId,
                Status = status,
                Skip = skip,
                Take = take
            },
            cancellationToken);

        return Ok(notifications);
    }

    /// <summary>Number of unread notifications for a recipient - the badge count.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(
        [FromQuery] Guid recipientId,
        CancellationToken cancellationToken)
    {
        var count = await _notifications.CountUnreadAsync(recipientId, cancellationToken);

        return Ok(new UnreadCountResponse(recipientId, count));
    }

    /// <summary>Replaces the text of an unread notification.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> Update(
        Guid id,
        [FromBody] UpdateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var notification = await _notifications.UpdateAsync(id, request.ToCommand(), cancellationToken);

        return Ok(notification);
    }

    /// <summary>
    /// Marks a notification as read. Idempotent: reading twice gives the same answer and
    /// only publishes <c>NotificationReadEvent</c> once.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _notifications.MarkAsReadAsync(id, cancellationToken);

        return Ok(notification);
    }

    /// <summary>Deletes a notification.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _notifications.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}

/// <summary>Response of <c>GET /api/notifications/unread-count</c>.</summary>
public sealed record UnreadCountResponse(Guid RecipientId, int Unread);
