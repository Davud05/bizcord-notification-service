namespace Bizcord.NotificationService.Application.Abstractions;

/// <summary>
/// Synchronous (REST) view of the User context. The Notification service does not own
/// users, it only needs a display name and whether the account is still active.
/// </summary>
public interface IUserDirectoryClient
{
    Task<UserLookupResult> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>The subset of a user this service needs. Nothing more is requested or stored.</summary>
public sealed record UserSummary(Guid Id, string DisplayName, bool IsActive);

/// <summary>
/// The three outcomes of a lookup. Keeping "does not exist" and "I could not reach the
/// service" apart is what allows the handler to fail closed in one case and fail open in
/// the other, instead of guessing from a <c>null</c>.
/// </summary>
public enum UserLookupStatus
{
    Found = 0,
    NotFound = 1,
    Unavailable = 2
}

public sealed record UserLookupResult(UserLookupStatus Status, UserSummary? User)
{
    public static UserLookupResult Found(UserSummary user) => new(UserLookupStatus.Found, user);

    public static UserLookupResult NotFound() => new(UserLookupStatus.NotFound, null);

    public static UserLookupResult Unavailable() => new(UserLookupStatus.Unavailable, null);
}
