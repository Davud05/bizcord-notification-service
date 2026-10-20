using Bizcord.NotificationService.Domain.Common;

namespace Bizcord.NotificationService.Domain.Mentions;

/// <summary>
/// Value object: one mention found in a message. A mention is either aimed at a single
/// user or at a whole channel.
/// </summary>
public sealed record Mention
{
    private Mention(MentionKind kind, Guid? userId)
    {
        Kind = kind;
        UserId = userId;
    }

    public MentionKind Kind { get; }

    /// <summary>The mentioned user, set only when <see cref="Kind"/> is <see cref="MentionKind.User"/>.</summary>
    public Guid? UserId { get; }

    public bool IsChannelWide => Kind is MentionKind.Here or MentionKind.Everyone;

    public static Mention ForUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A user mention must point at a user.");
        }

        return new Mention(MentionKind.User, userId);
    }

    public static Mention Here() => new(MentionKind.Here, userId: null);

    public static Mention Everyone() => new(MentionKind.Everyone, userId: null);

    public override string ToString() => Kind == MentionKind.User ? $"@{UserId}" : $"@{Kind.ToString().ToLowerInvariant()}";
}
