namespace Bizcord.Shared.Contracts.Models;

/// <summary>
/// The shared model of a notification. This is the only shape of a notification that
/// leaves the service: it contains the fields other services need for integration and
/// deliberately hides internal details such as delivery bookkeeping and preference
/// evaluation.
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }

    public Guid? ChannelId { get; set; }

    /// <summary>Id of the message that triggered the notification, if any.</summary>
    public Guid? MessageId { get; set; }

    /// <summary><see cref="NotificationTypes"/>.</summary>
    public required string Type { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    /// <summary><see cref="NotificationStatuses"/>.</summary>
    public required string Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
