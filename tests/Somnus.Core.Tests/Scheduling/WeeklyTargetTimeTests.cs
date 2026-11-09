using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Core.Tests.TestDoubles;

namespace Somnus.Core.Tests.Scheduling;

/// <summary>Issue #83: a countdown at a clock time repeats on chosen days of the week.</summary>
public sealed class WeeklyTargetTimeTests
{
    private static readonly TimeZoneInfo Zone = TestTimeZones.CentralEurope;

    // Thursday 22 October 2026; clocks go back on Sunday the 25th.
    private static readonly DateTimeOffset Thursday = Local(2026, 10, 22, 18, 0, offsetHours: 2);

    [Fact]
    public void NextOccurrence_GoesToTheNextChosenDay()
    {
        var target = TargetTime.NextOccurrence(Thursday, new TimeOnly(23, 0), Zone, WeekDays.Monday);

        Assert.Equal(Local(2026, 10, 26, 23, 0, offsetHours: 1), target);
    }

    [Fact]
    public void NextOccurrence_TakesTodayWhenTheTimeIsStillAhead()
    {
        var target = TargetTime.NextOccurrence(Thursday, new TimeOnly(23, 0), Zone, WeekDays.Thursday);

        Assert.Equal(Local(2026, 10, 22, 23, 0, offsetHours: 2), target);
    }

    [Fact]
    public void NextOccurrence_WaitsAWeekWhenTodaysTimeHasPassed()
    {
        var target = TargetTime.NextOccurrence(Thursday, new TimeOnly(7, 0), Zone, WeekDays.Thursday);

        Assert.Equal(Local(2026, 10, 29, 7, 0, offsetHours: 1), target);
    }

    [Fact]
    public void NextOccurrence_RunsOverTheEndOfTheWeek()
    {
        var saturday = Local(2026, 10, 24, 12, 0, offsetHours: 2);

        var target = TargetTime.NextOccurrence(saturday, new TimeOnly(8, 0), Zone, WeekDays.Monday | WeekDays.Sunday);

        Assert.Equal(Local(2026, 10, 25, 8, 0, offsetHours: 1), target);
    }

    [Fact]
    public void NextOccurrence_WithoutDays_IsTheNextOccurrenceOnAnyDay()
    {
        var target = TargetTime.NextOccurrence(Thursday, new TimeOnly(7, 0), Zone, WeekDays.None);

        Assert.Equal(Local(2026, 10, 23, 7, 0, offsetHours: 2), target);
    }

    [Fact]
    public void NextRepeat_RunsOnlyOnceTheNightClocksGoBack()
    {
        var firstRun = Local(2026, 10, 25, 2, 30, offsetHours: 2);

        var target = TargetTime.NextRepeat(firstRun.AddSeconds(1), firstRun, new TimeOnly(2, 30), Zone, WeekDays.EveryDay);

        Assert.Equal(Local(2026, 10, 26, 2, 30, offsetHours: 1), target);
    }

    [Fact]
    public void NextRepeat_AfterALongSleep_SkipsWhatItMissed()
    {
        var previous = Local(2026, 10, 22, 23, 0, offsetHours: 2);
        var now = Local(2026, 10, 25, 10, 0, offsetHours: 1);

        var target = TargetTime.NextRepeat(now, previous, new TimeOnly(23, 0), Zone, WeekDays.EveryDay);

        Assert.Equal(Local(2026, 10, 25, 23, 0, offsetHours: 1), target);
    }

    [Fact]
    public void NextRepeat_NeedsADay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetTime.NextRepeat(Thursday, Thursday, new TimeOnly(23, 0), Zone, WeekDays.None));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, WeekDays.Monday)]
    [InlineData(DayOfWeek.Sunday, WeekDays.Sunday)]
    public void ToWeekDays_MapsEveryDay(DayOfWeek day, WeekDays expected)
    {
        Assert.Equal(expected, day.ToWeekDays());
        Assert.True(WeekDays.EveryDay.Includes(day));
    }

    [Fact]
    public void Request_ThatRepeats_MakesARepeatingPlan()
    {
        var request = CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Shutdown, repeat: WeekDays.Friday);

        var plan = request.ToPlan(Thursday, Zone, warningEnabled: true);

        Assert.True(plan.Repeats);
        Assert.Equal(Local(2026, 10, 23, 23, 0, offsetHours: 2), plan.Target);
    }

    [Fact]
    public void Request_ThatRepeats_ForcesAppsToCloseOnlyOnAShutdown()
    {
        // ADR 0013: a schedule may force apps to close, like a one-time shutdown.
        Assert.True(CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Shutdown, true, WeekDays.Friday).ForceCloseApps);
        Assert.Throws<ArgumentException>(() => CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Sleep, true, WeekDays.Friday));
    }

    [Fact]
    public void Request_RejectsUnknownDays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Sleep, repeat: (WeekDays)128));
    }

    [Fact]
    public void Request_ThatRunsOnce_HasNoNextPlan()
    {
        var request = CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Sleep);

        Assert.Throws<InvalidOperationException>(() => request.ToNextPlan(Thursday, Thursday, Zone, warningEnabled: true));
    }

    [Fact]
    public void Settings_KeepTheSchedule()
    {
        var settings = AppSettings.Default with
        {
            LastRepeat = WeekDays.Monday,
            ScheduleDays = WeekDays.Monday | WeekDays.Friday,
            ScheduleTime = new TimeOnly(22, 15),
            ScheduleAction = PowerAction.Hibernate,
        };

        var read = SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings));

        Assert.Equal(settings, read.Settings);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void Settings_ReplaceInvalidDays()
    {
        var read = SettingsSerializer.Deserialize("""{ "scheduleDays": 500 }""");

        Assert.Equal(WeekDays.None, read.Settings.ScheduleDays);
        Assert.Single(read.Problems);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int offsetHours) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(offsetHours));
}
