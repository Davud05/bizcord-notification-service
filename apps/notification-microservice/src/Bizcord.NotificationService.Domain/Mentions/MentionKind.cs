namespace Bizcord.NotificationService.Domain.Mentions;

/// <summary>How a user was addressed in a message.</summary>
public enum MentionKind
{
    /// <summary>A specific user was mentioned with <c>&lt;@user-id&gt;</c>.</summary>
    User = 0,

    /// <summary><c>@here</c>: everyone in the channel who is currently online.</summary>
    Here = 1,

    /// <summary><c>@everyone</c>: every member of the channel.</summary>
    Everyone = 2
}
