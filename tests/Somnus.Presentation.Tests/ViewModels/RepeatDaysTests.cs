using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.Presentation.Tests.ViewModels;

/// <summary>Issue #83: Every day and the strip of days in the main window.</summary>
public sealed class RepeatDaysTests : IDisposable
{
    private readonly TestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Days_RunFromMondayToSunday()
    {
        using var viewModel = _harness.CreateMain();

        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], viewModel.Days.Select(day => day.ShortName));
        Assert.Equal("Monday", viewModel.Days[0].FullName);
        Assert.False(viewModel.IsEveryDay);
    }

    [Fact]
    public void CancellingARepeatingCountdown_SaysTheScheduleEnds()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.IsEveryDay = true;
        viewModel.StartCommand.Execute(null);

        viewModel.RequestCancelCommand.Execute(null);

        var request = Assert.Single(_harness.Dialogs.Requests);
        Assert.Equal("Stop the schedule?", request.Title);
        Assert.Contains("stop repeating", request.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Days_FollowTheLanguage()
    {
        using var viewModel = _harness.CreateMain();

        _harness.Localizer.Language = AppLanguage.Polish;

        Assert.Equal(["Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd"], viewModel.Days.Select(day => day.ShortName));
        Assert.Equal("Poniedziałek", viewModel.Days[0].FullName);
    }

    [Fact]
    public void EveryDay_TicksAndUnticksAllDays()
    {
        using var viewModel = _harness.CreateMain();

        viewModel.IsEveryDay = true;
        Assert.All(viewModel.Days, day => Assert.True(day.IsSelected));

        viewModel.IsEveryDay = false;
        Assert.All(viewModel.Days, day => Assert.False(day.IsSelected));
    }

    [Fact]
    public void UntickingOneDay_UnticksEveryDay_AndTickingAllTicksIt()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsEveryDay = true;

        viewModel.Days[2].IsSelected = false;
        Assert.False(viewModel.IsEveryDay);

        viewModel.Days[2].IsSelected = true;
        Assert.True(viewModel.IsEveryDay);
    }

    [Fact]
    public void Preview_ShowsTheNextChosenDay()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;

        viewModel.Days[4].IsSelected = false;
        viewModel.Days[0].IsSelected = true;

        Assert.Contains("26 Oct", viewModel.AtPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_BeginsASchedule_AndSaysWhenItRepeats()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.Days[0].IsSelected = true;
        viewModel.Days[4].IsSelected = true;

        viewModel.StartCommand.Execute(null);

        Assert.Equal(WeekDays.Monday | WeekDays.Friday, _harness.Schedule.Request!.Repeat);
        Assert.Equal("Repeats on Mon, Fri", viewModel.RepeatText);
    }

    [Fact]
    public void Start_EveryDay_SaysSo()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.IsEveryDay = true;

        viewModel.StartCommand.Execute(null);

        Assert.Equal("Repeats every day", viewModel.RepeatText);
    }

    [Fact]
    public void Days_DoNotApplyToTheCountdownMode()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsEveryDay = true;
        viewModel.IsInMode = true;

        viewModel.StartCommand.Execute(null);

        Assert.Null(_harness.Schedule.Request);
        Assert.Null(viewModel.RepeatText);
    }

    [Fact]
    public void ChangingDaysWhileRunning_OffersSave()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.IsEveryDay = true;
        viewModel.StartCommand.Execute(null);

        viewModel.Days[0].IsSelected = false;

        Assert.True(viewModel.HasPendingChanges);
    }

    [Fact]
    public void SavingWithoutDays_EndsTheSchedule()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.IsEveryDay = true;
        viewModel.StartCommand.Execute(null);

        viewModel.IsEveryDay = false;
        viewModel.RestartCommand.Execute(null);

        Assert.Null(_harness.Schedule.Request);
        Assert.Equal(WeekDays.None, _harness.Settings.Current.ScheduleDays);
        Assert.True(viewModel.IsActive);
    }

    [Fact]
    public void ForceCloseApps_DoesNotApplyToARepeatingCountdown()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.IsShutdownSelected = true;
        viewModel.ForceCloseApps = true;
        Assert.True(viewModel.IsForceOptionVisible);

        viewModel.Days[0].IsSelected = true;

        Assert.False(viewModel.ForceCloseApps);
        Assert.False(viewModel.IsForceOptionVisible);
    }

    [Fact]
    public void ARunningSchedule_ShowsItsValuesOnTheNextStart()
    {
        var settings = AppSettings.Default with
        {
            RememberLast = false,
            ScheduleDays = WeekDays.Tuesday,
            ScheduleTime = new TimeOnly(21, 5),
            ScheduleAction = PowerAction.Hibernate,
        };
        using var harness = new TestHarness(settings);

        using var viewModel = harness.CreateMain();

        Assert.True(viewModel.IsAtMode);
        Assert.Equal("21", viewModel.HourText);
        Assert.Equal("05", viewModel.MinuteText);
        Assert.True(viewModel.Days[1].IsSelected);
    }

    [Fact]
    public void Inputs_RememberTheDays()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        viewModel.Days[2].IsSelected = true;

        viewModel.StartCommand.Execute(null);

        Assert.Equal(WeekDays.Wednesday, _harness.Settings.Current.LastRepeat);
    }
}
