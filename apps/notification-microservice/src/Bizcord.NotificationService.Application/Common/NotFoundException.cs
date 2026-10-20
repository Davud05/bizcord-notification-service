namespace Bizcord.NotificationService.Application.Common;

/// <summary>
/// Thrown when a use case is asked to work on something that does not exist. Translated
/// into HTTP 404 by the API layer.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, Guid id)
        : base($"{resource} '{id}' was not found.")
    {
        Resource = resource;
        Id = id;
    }

    public string Resource { get; }

    public Guid Id { get; }
}
