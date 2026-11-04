using Somnus.Core.Scheduling;
using Somnus.Core.Tests.TestDoubles;

namespace Somnus.Core.Tests.Scheduling;

public sealed class TargetTimeTests
{
    private static readonly TimeZoneInfo Zone = TestTimeZones.CentralEurope;

    [Fact]
    public void After_AddsTheDelayToNow()
    {
        var now = Local(2026, 10, 22, 18, 0, offsetHours: 2);

        Assert.Equal(now.AddMinutes(90), TargetTime.After(now, TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void NextOccurrence_TimeStillAheadToday_ReturnsToday()
    {
        var now = Local(2026, 10, 22, 18, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(23, 30), Zone);

        Assert.Equal(Local(2026, 10, 22, 23, 30, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_TimeAlreadyPassedToday_ReturnsTomorrow()
    {
        var now = Local(2026, 10, 22, 18, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(7, 15), Zone);

        Assert.Equal(Local(2026, 10, 23, 7, 15, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_ExactlyNow_ReturnsTomorrow()
    {
        var now = Local(2026, 10, 22, 18, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(18, 0), Zone);

        Assert.Equal(Local(2026, 10, 23, 18, 0, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_OneSecondBefore_ReturnsToday()
    {
        var now = Local(2026, 10, 22, 17, 59, offsetHours: 2, second: 59);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(18, 0), Zone);

        Assert.Equal(Local(2026, 10, 22, 18, 0, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_Midnight_ReturnsTheComingMidnight()
    {
        var now = Local(2026, 10, 22, 23, 59, offsetHours: 2, second: 30);

        var target = TargetTime.NextOccurrence(now, TimeOnly.MinValue, Zone);

        Assert.Equal(Local(2026, 10, 23, 0, 0, offsetHours: 2), target);
        Assert.Equal(TimeSpan.FromSeconds(30), target - now);
    }

    [Fact]
    public void NextOccurrence_IgnoresSeconds()
    {
        var now = Local(2026, 10, 22, 18, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(23, 30, 45), Zone);

        Assert.Equal(Local(2026, 10, 22, 23, 30, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_TimeSkippedBySpringForward_MovesForwardByTheGap()
    {
        // 29 March 2026: clocks jump from 02:00 to 03:00, so 02:30 never shows.
        var now = Local(2026, 3, 29, 0, 0, offsetHours: 1);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(2, 30), Zone);

        Assert.Equal(Local(2026, 3, 29, 3, 30, offsetHours: 2), target);
        Assert.Equal(TimeSpan.FromHours(2.5), target - now);
    }

    [Fact]
    public void NextOccurrence_SkippedTimeTomorrow_MovesForwardByTheGap()
    {
        var now = Local(2026, 3, 28, 20, 0, offsetHours: 1);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(2, 30), Zone);

        Assert.Equal(Local(2026, 3, 29, 3, 30, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_TimeRepeatedByFallBack_UsesTheFirstOccurrence()
    {
        // 25 October 2026: clocks go back from 03:00 to 02:00, so 02:30 shows twice.
        var now = Local(2026, 10, 25, 0, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(2, 30), Zone);

        Assert.Equal(Local(2026, 10, 25, 2, 30, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_BetweenTheTwoOccurrences_UsesTheSecondOne()
    {
        var now = Local(2026, 10, 25, 2, 45, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(2, 30), Zone);

        Assert.Equal(Local(2026, 10, 25, 2, 30, offsetHours: 1), target);
        Assert.Equal(TimeSpan.FromMinutes(45), target - now);
    }

    [Fact]
    public void NextOccurrence_AfterBothOccurrences_ReturnsTomorrow()
    {
        var now = Local(2026, 10, 25, 2, 45, offsetHours: 1);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(2, 30), Zone);

        Assert.Equal(Local(2026, 10, 26, 2, 30, offsetHours: 1), target);
    }

    [Fact]
    public void NextOccurrence_SameClockTimeAcrossFallBack_IsTwentyFiveHoursAway()
    {
        var now = Local(2026, 10, 24, 22, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(22, 0), Zone);

        Assert.Equal(Local(2026, 10, 25, 22, 0, offsetHours: 1), target);
        Assert.Equal(TimeSpan.FromHours(25), target - now);
    }

    [Fact]
    public void NextOccurrence_WorksInUtc()
    {
        var now = new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(6, 0), TimeZoneInfo.Utc);

        Assert.Equal(new DateTimeOffset(2026, 10, 23, 6, 0, 0, TimeSpan.Zero), target);
    }

    [Fact]
    public void NextOccurrence_NowGivenInAnotherOffset_UsesTheZoneClock()
    {
        // 16:00 UTC is 18:00 in Central Europe (summer time), so 17:00 local has already passed.
        var now = new DateTimeOffset(2026, 10, 22, 16, 0, 0, TimeSpan.Zero);

        var target = TargetTime.NextOccurrence(now, new TimeOnly(17, 0), Zone);

        Assert.Equal(Local(2026, 10, 23, 17, 0, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_NullZone_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TargetTime.NextOccurrence(DateTimeOffset.UnixEpoch, TimeOnly.MinValue, null!));
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int offsetHours, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.FromHours(offsetHours));
}
