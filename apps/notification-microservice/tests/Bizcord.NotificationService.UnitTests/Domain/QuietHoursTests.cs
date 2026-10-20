using Bizcord.NotificationService.Domain.Common;
using Bizcord.NotificationService.Domain.Preferences;

namespace Bizcord.NotificationService.UnitTests.Domain;

public class QuietHoursTests
{
    [Fact]
    public void Create_Throws_WhenStartEqualsEnd()
    {
        var act = () => QuietHours.Create(new TimeOnly(22, 0), new TimeOnly(22, 0));

        act.Should().Throw<DomainException>()
            .WithMessage("*different times*");
    }

    [Theory]
    [InlineData(23, 30, true)]
    [InlineData(2, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(12, 0, false)]
    [InlineData(21, 59, false)]
    [InlineData(22, 0, true)]
    public void Includes_HandlesPeriodsThatCrossMidnight(int hour, int minute, bool expected)
    {
        var quietHours = QuietHours.Create(new TimeOnly(22, 0), new TimeOnly(7, 0));

        quietHours.CrossesMidnight.Should().BeTrue();
        quietHours.Includes(new DateTime(2026, 10, 20, hour, minute, 0, DateTimeKind.Utc)).Should().Be(expected);
    }

    [Theory]
    [InlineData(11, 59, false)]
    [InlineData(12, 0, true)]
    [InlineData(12, 30, true)]
    [InlineData(13, 0, false)]
    public void Includes_HandlesPeriodsWithinOneDay(int hour, int minute, bool expected)
    {
        var quietHours = QuietHours.Create(new TimeOnly(12, 0), new TimeOnly(13, 0));

        quietHours.CrossesMidnight.Should().BeFalse();
        quietHours.Includes(new DateTime(2026, 10, 20, hour, minute, 0, DateTimeKind.Utc)).Should().Be(expected);
    }

    [Fact]
    public void Parse_ReturnsNull_WhenNothingIsConfigured()
    {
        QuietHours.Parse(null, null).Should().BeNull();
        QuietHours.Parse("", "").Should().BeNull();
    }

    [Fact]
    public void Parse_Throws_WhenOnlyOneSideIsGiven()
    {
        var act = () => QuietHours.Parse("22:00", null);

        act.Should().Throw<DomainException>()
            .WithMessage("*both a start and an end*");
    }

    [Fact]
    public void Parse_Throws_OnAnUnparsableTime()
    {
        var act = () => QuietHours.Parse("tonight", "tomorrow");

        act.Should().Throw<DomainException>()
            .WithMessage("*HH:mm*");
    }

    [Fact]
    public void Parse_RoundTripsThroughToString()
    {
        QuietHours.Parse("22:00", "07:30")!.ToString().Should().Be("22:00-07:30");
    }
}
