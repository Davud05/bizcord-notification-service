namespace Bizcord.NotificationService.Domain.Common;

/// <summary>
/// Thrown when an operation would leave the domain model in an invalid state. The API
/// layer translates this into an HTTP 400 so broken input never reaches the database.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
