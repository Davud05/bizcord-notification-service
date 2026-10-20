namespace Bizcord.NotificationService.TestSupport;

/// <summary>
/// A clock that does not move, so tests about quiet hours and timestamps are
/// deterministic instead of depending on when the test suite happens to run.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }

    /// <summary>A fixed workday moment: 2026-10-20 at 10:15 UTC.</summary>
    public static FixedTimeProvider Workday() => new(new DateTimeOffset(2026, 10, 20, 10, 15, 0, TimeSpan.Zero));

    /// <summary>A fixed middle-of-the-night moment: 2026-10-20 at 02:30 UTC.</summary>
    public static FixedTimeProvider Night() => new(new DateTimeOffset(2026, 10, 20, 2, 30, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan amount) => Now = Now.Add(amount);
}
