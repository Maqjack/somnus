using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.Services;

/// <summary>Issue #83: a repeating countdown goes on by itself until the user cancels it.</summary>
public sealed class ScheduleCoordinatorTests : IDisposable
{
    private static readonly TimeOnly At = new(18, 30);

    private readonly TestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Begin_KeepsTheScheduleInTheSettings()
    {
        _harness.Schedule.Begin(EveryDay());

        var settings = _harness.Settings.Current;
        Assert.Equal(WeekDays.EveryDay, settings.ScheduleDays);
        Assert.Equal(At, settings.ScheduleTime);
        Assert.Equal(PowerAction.Sleep, settings.ScheduleAction);
        Assert.Equal(TestHarness.StartTime.AddMinutes(30), _harness.Session.Snapshot.Plan!.Target);
        Assert.False(settings.ScheduleForceCloseApps);
    }

    [Fact]
    public void Begin_KeepsTheChoiceToForceAppsToClose()
    {
        _harness.Schedule.Begin(ForcedShutdownEveryDay());

        Assert.True(_harness.Settings.Current.ScheduleForceCloseApps);
        Assert.True(_harness.Session.Snapshot.Plan!.ForceCloseApps);
    }

    [Fact]
    public void AForcedSchedule_WarnsAndForcesAppsToCloseOnTwoRunsInARow()
    {
        using var shell = _harness.CreateShell();
        _harness.Schedule.Begin(ForcedShutdownEveryDay());

        RunWithItsWarning(_harness, shell);
        RunWithItsWarning(_harness, shell);

        Assert.Equal(2, _harness.Power.Calls.Count);
        Assert.All(_harness.Power.Calls, call => Assert.Equal((PowerAction.Shutdown, true), (call.Action, call.ForceCloseApps)));
    }

    [Fact]
    public void Resume_WarnsAndForcesAppsToCloseOnTheNextRun()
    {
        using var harness = new TestHarness(AppSettings.Default with
        {
            ScheduleDays = WeekDays.EveryDay,
            ScheduleTime = At,
            ScheduleAction = PowerAction.Shutdown,
            ScheduleForceCloseApps = true,
        });
        using var shell = harness.CreateShell();

        harness.Schedule.Resume();
        Assert.True(harness.Schedule.Request!.ForceCloseApps);
        RunWithItsWarning(harness, shell);

        var call = Assert.Single(harness.Power.Calls);
        Assert.Equal(PowerAction.Shutdown, call.Action);
        Assert.True(call.ForceCloseApps);
    }

    [Fact]
    public void Cancelling_ForgetsTheChoiceToForceAppsToClose()
    {
        _harness.Schedule.Begin(ForcedShutdownEveryDay());

        _harness.Session.Cancel();

        Assert.Equal(WeekDays.None, _harness.Settings.Current.ScheduleDays);
        Assert.False(_harness.Settings.Current.ScheduleForceCloseApps);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resume_LessThanAMinuteBeforeARun_SkipsToTheNextDay(bool warningEnabled)
    {
        using var harness = new TestHarness(new AppSettings
        {
            WarningEnabled = warningEnabled,
            ScheduleDays = WeekDays.EveryDay,
            ScheduleTime = At,
            ScheduleAction = PowerAction.Shutdown,
        });
        harness.Time.Advance(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(30));

        harness.Schedule.Resume();
        harness.AdvanceInTicks(TimeSpan.FromMinutes(2));

        Assert.Empty(harness.Power.Calls);
        Assert.Equal(TestHarness.StartTime.AddDays(1).AddMinutes(30), harness.Session.Snapshot.Plan!.Target);
    }

    [Fact]
    public void Resume_AMinuteOrMoreBeforeARun_KeepsIt()
    {
        using var harness = new TestHarness(new AppSettings
        {
            ScheduleDays = WeekDays.EveryDay,
            ScheduleTime = At,
            ScheduleAction = PowerAction.Shutdown,
        });
        harness.Time.Advance(TimeSpan.FromMinutes(29));

        harness.Schedule.Resume();

        Assert.Equal(TestHarness.StartTime.AddMinutes(30), harness.Session.Snapshot.Plan!.Target);
        Assert.Equal(CountdownState.Warning, harness.Session.Snapshot.State);
    }

    [Fact]
    public void AfterARun_TheNextOneStarts()
    {
        _harness.Schedule.Begin(EveryDay());

        _harness.AdvanceInTicks(TimeSpan.FromMinutes(31));

        Assert.Single(_harness.Power.Calls);
        Assert.True(_harness.Session.Snapshot.IsActive);
        Assert.Equal(TestHarness.StartTime.AddDays(1).AddMinutes(30), _harness.Session.Snapshot.Plan!.Target);
    }

    [Fact]
    public void AfterAFailedRun_TheNextOneStarts()
    {
        _harness.Power.FailWith = new InvalidOperationException("Refused");
        _harness.Schedule.Begin(EveryDay());

        _harness.AdvanceInTicks(TimeSpan.FromMinutes(31));

        Assert.True(_harness.Session.Snapshot.IsActive);
    }

    [Fact]
    public void ARunThePcSleptThrough_IsSkippedWithoutAction()
    {
        _harness.Schedule.Begin(EveryDay());

        _harness.Time.Advance(TimeSpan.FromHours(2));

        Assert.Empty(_harness.Power.Calls);
        Assert.True(_harness.Session.Snapshot.IsActive);
        Assert.Equal(TestHarness.StartTime.AddDays(1).AddMinutes(30), _harness.Session.Snapshot.Plan!.Target);
    }

    [Fact]
    public void Cancelling_EndsTheSchedule()
    {
        _harness.Schedule.Begin(EveryDay());

        _harness.Session.Cancel();

        Assert.Null(_harness.Schedule.Request);
        Assert.Equal(WeekDays.None, _harness.Settings.Current.ScheduleDays);
        Assert.False(_harness.Session.Snapshot.IsActive);
    }

    [Fact]
    public void Cancelling_EndsTheScheduleBeforeTheUiThreadRunsAgain()
    {
        // Exiting cancels and closes Somnus at once, so nothing posted to the UI thread may run.
        using var schedule = new ScheduleCoordinator(_harness.Session, _harness.Settings, _harness.Time, new NeverDispatcher());
        schedule.Begin(EveryDay());

        _harness.Session.Cancel();

        Assert.Null(schedule.Request);
        Assert.Equal(WeekDays.None, _harness.Settings.Current.ScheduleDays);
    }

    [Fact]
    public void Resume_StartsTheKeptSchedule()
    {
        using var harness = new TestHarness(AppSettings.Default with { ScheduleDays = WeekDays.Friday, ScheduleTime = At, ScheduleAction = PowerAction.Hibernate });

        harness.Schedule.Resume();

        Assert.Equal(WeekDays.Friday, harness.Schedule.Request!.Repeat);
        Assert.Equal(PowerAction.Hibernate, harness.Session.Snapshot.Plan!.Action);
        Assert.Equal(TestHarness.StartTime.AddDays(1).AddMinutes(30), harness.Session.Snapshot.Plan.Target);
    }

    [Fact]
    public void Resume_WithoutASchedule_DoesNothing()
    {
        _harness.Schedule.Resume();

        Assert.Null(_harness.Schedule.Request);
        Assert.False(_harness.Session.Snapshot.IsActive);
    }

    [Fact]
    public void KeepAwake_WaitsForTheLastHourOfARepeatingCountdown()
    {
        _harness.Schedule.Begin(CountdownRequest.At(new TimeOnly(20, 0), PowerAction.Sleep, repeat: WeekDays.EveryDay));
        Assert.False(_harness.KeepAwake.IsAcquired);

        _harness.AdvanceInTicks(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(1));

        Assert.True(_harness.KeepAwake.IsAcquired);
    }

    [Fact]
    public void KeepAwake_HoldsAllTheTimeForAOneOffCountdown()
    {
        var request = CountdownRequest.At(new TimeOnly(20, 0), PowerAction.Sleep);

        _harness.Session.Start(request.ToPlan(_harness.Time.GetUtcNow(), _harness.Time.LocalTimeZone, warningEnabled: true));

        Assert.True(_harness.KeepAwake.IsAcquired);
    }

    private static CountdownRequest EveryDay() => CountdownRequest.At(At, PowerAction.Sleep, repeat: WeekDays.EveryDay);

    private static CountdownRequest ForcedShutdownEveryDay() => CountdownRequest.At(At, PowerAction.Shutdown, forceCloseApps: true, WeekDays.EveryDay);

    /// <summary>
    /// Goes through the next run: straight to two minutes before it (still ahead, so no sleep), tick by
    /// tick into the warning, which must name the data loss, then past the target.
    /// </summary>
    private static void RunWithItsWarning(TestHarness harness, ShellViewModel shell)
    {
        var target = harness.Session.Snapshot.Plan!.Target;
        harness.Time.Advance(target - harness.Time.GetUtcNow() - TimeSpan.FromMinutes(2));
        harness.AdvanceInTicks(TimeSpan.FromSeconds(90));
        Assert.True(shell.IsWarningOpen);
        Assert.True(shell.Warning.IsForceWarningVisible);

        harness.AdvanceInTicks(TimeSpan.FromSeconds(31));
    }

    private sealed class NeverDispatcher : IUiDispatcher
    {
        public void Post(Action action)
        {
        }
    }
}
