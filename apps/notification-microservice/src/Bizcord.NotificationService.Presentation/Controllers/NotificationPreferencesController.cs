using Bizcord.NotificationService.Application.Preferences;
using Bizcord.NotificationService.Presentation.Requests;
using Bizcord.Shared.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.NotificationService.Presentation.Controllers;

/// <summary>
/// A user's notification preferences: the switches that decide whether a mention is worth
/// interrupting someone for.
/// </summary>
[ApiController]
[Route("api/users/{userId:guid}/notification-preferences")]
public sealed class NotificationPreferencesController : ControllerBase
{
    private readonly IPreferenceAppService _preferences;

    public NotificationPreferencesController(IPreferenceAppService preferences) => _preferences = preferences;

    /// <summary>
    /// Reads the preferences of a user. A user who never changed anything gets the
    /// documented defaults instead of a 404.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferenceDto>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var preferences = await _preferences.GetAsync(userId, cancellationToken);

        return Ok(preferences);
    }

    /// <summary>Replaces the preferences of a user.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationPreferenceDto>> Update(
        Guid userId,
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var preferences = await _preferences.UpdateAsync(userId, request.ToCommand(), cancellationToken);

        return Ok(preferences);
    }
}
