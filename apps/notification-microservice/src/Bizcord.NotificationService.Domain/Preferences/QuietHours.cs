using Bizcord.NotificationService.Domain.Common;

namespace Bizcord.NotificationService.Domain.Preferences;

/// <summary>
/// Value object: a daily period (in UTC) where the user does not want to be disturbed.
/// Supports periods that cross midnight, for example 22:00-07:00.
/// </summary>
public sealed record QuietHours
{
    private QuietHours(TimeOnly start, TimeOnly end)
    {
        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    /// <summary>True when the period crosses midnight, for example 22:00-07:00.</summary>
    public bool CrossesMidnight => End < Start;

    public static QuietHours Create(TimeOnly start, TimeOnly end)
    {
        if (start == end)
        {
            throw new DomainException("Quiet hours must start and end at different times.");
        }

        return new QuietHours(start, end);
    }

    /// <summary>
    /// Parses quiet hours from the <c>HH:mm</c> strings used in the public API.
    /// Returns <c>null</c> when both values are absent, which means "no quiet hours".
    /// </summary>
    public static QuietHours? Parse(string? start, string? end)
    {
        var hasStart = !string.IsNullOrWhiteSpace(start);
        var hasEnd = !string.IsNullOrWhiteSpace(end);

        if (!hasStart && !hasEnd)
        {
            return null;
        }

        if (hasStart != hasEnd)
        {
            throw new DomainException("Quiet hours need both a start and an end time.");
        }

        if (!TimeOnly.TryParse(start, out var parsedStart) || !TimeOnly.TryParse(end, out var parsedEnd))
        {
            throw new DomainException("Quiet hours must be formatted as HH:mm.");
        }

        return Create(parsedStart, parsedEnd);
    }

    /// <summary>True when <paramref name="momentUtc"/> falls inside the quiet period.</summary>
    public bool Includes(DateTime momentUtc)
    {
        var time = TimeOnly.FromDateTime(momentUtc);

        return CrossesMidnight
            ? time >= Start || time < End
            : time >= Start && time < End;
    }

    public override string ToString() => $"{Start:HH\\:mm}-{End:HH\\:mm}";
}
