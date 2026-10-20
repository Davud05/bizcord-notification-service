using Bizcord.NotificationService.Domain.Common;

namespace Bizcord.NotificationService.Domain.Notifications;

/// <summary>
/// Value object: the user a notification belongs to. The Notification context does not
/// own users, so it only keeps the identity it was given by the User context.
/// </summary>
public sealed record Recipient
{
    private Recipient(Guid userId) => UserId = userId;

    public Guid UserId { get; }

    public static Recipient For(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A notification must have a recipient.");
        }

        return new Recipient(userId);
    }

    public override string ToString() => UserId.ToString();
}
