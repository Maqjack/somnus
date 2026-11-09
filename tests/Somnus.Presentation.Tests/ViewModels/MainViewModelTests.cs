using System.ComponentModel;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.ViewModels;

public sealed class MainViewModelTests : IDisposable
{
    private readonly TestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void NewWindow_StartsFromTheDefaults()
    {
        using var viewModel = _harness.CreateMain();

        Assert.Equal(TimeMode.In, viewModel.Mode);
        Assert.Equal("30", viewModel.AmountText);
        Assert.Equal(TimeUnit.Minutes, viewModel.Unit);
        Assert.Equal("23", viewModel.HourText);
        Assert.Equal("00", viewModel.MinuteText);
        Assert.Equal(PowerAction.Sleep, viewModel.Action);
        Assert.False(viewModel.ForceCloseApps);
        Assert.Null(viewModel.InputError);
        Assert.True(viewModel.StartCommand.CanExecute(null));
        Assert.Equal("Somnus", viewModel.WindowTitle);
    }

    [Fact]
    public void RememberedValues_AreRestored()
    {
        using var harness = new TestHarness(AppSettings.Default with
        {
            LastMode = TimeMode.At,
            LastAmount = 90,
            LastUnit = TimeUnit.Seconds,
            LastAction = PowerAction.Shutdown,
            LastAtTime = new TimeOnly(7, 5),
        });

        using var viewModel = harness.CreateMain();

        Assert.True(viewModel.IsAtMode);
        Assert.Equal("90", viewModel.AmountText);
        Assert.Equal(TimeUnit.Seconds, viewModel.Unit);
        Assert.Equal("07", viewModel.HourText);
        Assert.Equal("05", viewModel.MinuteText);
        Assert.True(viewModel.IsShutdownSelected);
    }

    [Fact]
    public void RememberLastOff_IgnoresTheRememberedValues()
    {
        using var harness = new TestHarness(AppSettings.Default with { RememberLast = false, LastAmount = 5, LastUnit = TimeUnit.Hours });

        using var viewModel = harness.CreateMain();

        Assert.Equal("30", viewModel.AmountText);
        Assert.Equal(TimeUnit.Minutes, viewModel.Unit);
    }

    [Theory]
    [InlineData("", "Enter a number.")]
    [InlineData("abc", "Use digits only.")]
    [InlineData("0", "The minimum is 10 seconds.")]
    [InlineData("1000000", "The maximum is 99 hours.")]
    public void InvalidAmount_ShowsAnInlineMessageAndDisablesStart(string amount, string expected)
    {
        using var viewModel = _harness.CreateMain();
        viewModel.Unit = TimeUnit.Seconds;

        viewModel.AmountText = amount;

        Assert.Equal(expected, viewModel.InputError);
        Assert.False(viewModel.StartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("24", "00", "Enter an hour from 0 to 23.")]
    [InlineData("12", "60", "Enter a minute from 0 to 59.")]
    public void InvalidClockTime_ShowsAnInlineMessage(string hour, string minute, string expected)
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;

        viewModel.HourText = hour;
        viewModel.MinuteText = minute;

        Assert.Equal(expected, viewModel.InputError);
        Assert.Null(viewModel.AtPreview);
        Assert.False(viewModel.StartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(PowerAction.Sleep, "The PC will sleep at 07:15 (Fri, 23 Oct)")]
    [InlineData(PowerAction.Hibernate, "The PC will hibernate at 07:15 (Fri, 23 Oct)")]
    [InlineData(PowerAction.Shutdown, "The PC will shut down at 07:15 (Fri, 23 Oct)")]
    public void AtMode_PreviewSaysWhatHappensAndWhen(PowerAction action, string expected)
    {
        // Issue #58: the preview names the action, not only the date.
        using var viewModel = _harness.CreateMain();

        viewModel.IsAtMode = true;
        viewModel.HourText = "7";
        viewModel.MinuteText = "15";
        viewModel.Action = action;

        Assert.Equal(expected, viewModel.AtPreview);
    }

    [Fact]
    public void AtMode_PreviewInPolish_SaysThePcWillShutDown()
    {
        using var viewModel = _harness.CreateMain();
        _harness.Localizer.Language = AppLanguage.Polish;

        viewModel.IsAtMode = true;
        viewModel.HourText = "7";
        viewModel.MinuteText = "15";
        viewModel.IsShutdownSelected = true;

        Assert.Equal("Komputer wyłączy się o 07:15 (pt., 23 paź)", viewModel.AtPreview);
    }

    [Fact]
    public void ModeSwitches_AreReflectedInBothFlags()
    {
        using var viewModel = _harness.CreateMain();

        viewModel.IsAtMode = true;
        Assert.Equal(TimeMode.At, viewModel.Mode);
        Assert.False(viewModel.IsInMode);

        viewModel.IsInMode = false;
        Assert.Equal(TimeMode.At, viewModel.Mode);

        viewModel.IsInMode = true;
        Assert.Equal(TimeMode.In, viewModel.Mode);
    }

    [Fact]
    public void Start_StartsTheCountdownWithTheChosenValues()
    {
        using var viewModel = _harness.CreateMain();
        var started = 0;
        viewModel.CountdownStarted += (_, _) => started++;
        viewModel.AmountText = "45";
        viewModel.IsHibernateSelected = true;

        viewModel.StartCommand.Execute(null);

        var plan = Assert.IsType<CountdownPlan>(_harness.Session.Snapshot.Plan);
        Assert.Equal(TestHarness.StartTime.AddMinutes(45), plan.Target);
        Assert.Equal(PowerAction.Hibernate, plan.Action);
        Assert.False(plan.ForceCloseApps);
        Assert.True(plan.WarningEnabled);
        Assert.True(viewModel.IsActive);
        Assert.Equal("00:45:00", viewModel.CountdownText);
        Assert.Equal("Hibernate on Thu, 22 Oct at 18:45", viewModel.TargetText);
        Assert.Equal(1, started);
        Assert.False(viewModel.StartCommand.CanExecute(null));
        Assert.True(viewModel.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Start_UsesTheWarningSetting()
    {
        _harness.Settings.Update(settings => settings with { WarningEnabled = false });
        using var viewModel = _harness.CreateMain();

        viewModel.StartCommand.Execute(null);

        Assert.False(_harness.Session.Snapshot.Plan!.WarningEnabled);
    }

    [Fact]
    public void Start_RemembersTheValues()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.AmountText = "2";
        viewModel.Unit = TimeUnit.Hours;
        viewModel.IsShutdownSelected = true;
        viewModel.ForceCloseApps = true;

        viewModel.StartCommand.Execute(null);

        var stored = _harness.Store.Stored;
        Assert.Equal(TimeMode.In, stored.LastMode);
        Assert.Equal(2, stored.LastAmount);
        Assert.Equal(TimeUnit.Hours, stored.LastUnit);
        Assert.Equal(PowerAction.Shutdown, stored.LastAction);
    }

    [Fact]
    public void Start_InAtModeKeepsTheRememberedAmountWhenItIsInvalid()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.AmountText = "x";
        viewModel.IsAtMode = true;
        viewModel.HourText = "21";
        viewModel.MinuteText = "30";

        viewModel.StartCommand.Execute(null);

        Assert.Equal(TimeMode.At, _harness.Store.Stored.LastMode);
        Assert.Equal(new TimeOnly(21, 30), _harness.Store.Stored.LastAtTime);
        Assert.Equal(AppSettings.Default.LastAmount, _harness.Store.Stored.LastAmount);
    }

    [Fact]
    public void Start_DoesNotRememberWhenRememberLastIsOff()
    {
        _harness.Settings.Update(settings => settings with { RememberLast = false });
        var savesBefore = _harness.Store.SaveCount;
        using var viewModel = _harness.CreateMain();

        viewModel.StartCommand.Execute(null);

        Assert.Equal(savesBefore, _harness.Store.SaveCount);
    }

    [Fact]
    public void ForceCloseApps_ShowsTheWarningAndIsResetWhenTheActionChanges()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsShutdownSelected = true;
        Assert.True(viewModel.IsForceOptionVisible);

        viewModel.ForceCloseApps = true;
        Assert.True(viewModel.IsForceWarningVisible);

        viewModel.IsSleepSelected = true;
        Assert.False(viewModel.ForceCloseApps);
        Assert.False(viewModel.IsForceOptionVisible);
        Assert.False(viewModel.IsForceWarningVisible);
    }

    [Fact]
    public void ForceCloseApps_OfAOneTimeCountdown_IsNeverRemembered()
    {
        using (var viewModel = _harness.CreateMain())
        {
            viewModel.IsShutdownSelected = true;
            viewModel.ForceCloseApps = true;
            viewModel.StartCommand.Execute(null);
        }

        using var next = _harness.CreateMain();

        Assert.True(next.IsShutdownSelected);
        Assert.False(next.ForceCloseApps);
        Assert.False(_harness.Settings.Current.ScheduleForceCloseApps);
    }

    [Fact]
    public void ForcedShutdown_ReachesThePlan()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsShutdownSelected = true;
        viewModel.ForceCloseApps = true;

        viewModel.StartCommand.Execute(null);

        Assert.True(_harness.Session.Snapshot.Plan!.ForceCloseApps);
    }

    [Fact]
    public void Cancel_StopsTheCountdown()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);

        viewModel.CancelCommand.Execute(null);

        Assert.False(viewModel.IsActive);
        Assert.Equal(CountdownState.Cancelled, _harness.Session.Snapshot.State);
        Assert.Null(viewModel.StatusMessage);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(true, CountdownState.Cancelled)]
    [InlineData(false, CountdownState.Running)]
    public void Escape_CancelsOnlyAfterConfirmation(bool confirmed, CountdownState expected)
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);
        _harness.Dialogs.Answer = confirmed;

        viewModel.RequestCancelCommand.Execute(null);

        Assert.Equal(expected, _harness.Session.Snapshot.State);
        var question = Assert.Single(_harness.Dialogs.Requests);
        Assert.Equal("Sleep on Thu, 22 Oct at 18:30 will not happen.", question.Message);
    }

    [Fact]
    public void Escape_WithoutACountdown_AsksNothing()
    {
        using var viewModel = _harness.CreateMain();

        viewModel.RequestCancelCommand.Execute(null);

        Assert.Empty(_harness.Dialogs.Requests);
    }

    [Fact]
    public void EditingWhileRunning_OffersARestartWithTheNewValues()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);
        Assert.False(viewModel.HasPendingChanges);

        viewModel.AmountText = "10";
        viewModel.Unit = TimeUnit.Minutes;

        Assert.True(viewModel.HasPendingChanges);
        Assert.True(viewModel.RestartCommand.CanExecute(null));

        viewModel.RestartCommand.Execute(null);

        Assert.Single(_harness.Dialogs.Requests);
        Assert.Equal(TestHarness.StartTime.AddMinutes(10), _harness.Session.Snapshot.Plan!.Target);
        Assert.True(viewModel.IsActive);
        Assert.False(viewModel.HasPendingChanges);
    }

    [Fact]
    public void SavingChangesWhileRunning_KeepsTheWindowOpen()
    {
        // Mac: after Save the new clock must stay in sight; only the first Start hides the window.
        using var viewModel = _harness.CreateMain();
        var started = 0;
        viewModel.CountdownStarted += (_, _) => started++;
        viewModel.StartCommand.Execute(null);
        viewModel.AmountText = "10";

        viewModel.RestartCommand.Execute(null);

        Assert.Equal(TestHarness.StartTime.AddMinutes(10), _harness.Session.Snapshot.Plan!.Target);
        Assert.Equal(1, started);
    }

    [Fact]
    public void Restart_DeclinedKeepsTheRunningCountdown()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);
        viewModel.AmountText = "10";
        _harness.Dialogs.Answer = false;

        viewModel.RestartCommand.Execute(null);

        Assert.Equal(TestHarness.StartTime.AddMinutes(30), _harness.Session.Snapshot.Plan!.Target);
        Assert.True(viewModel.HasPendingChanges);
    }

    [Fact]
    public void Restart_IsNotOfferedForInvalidInput()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);

        viewModel.AmountText = string.Empty;

        Assert.False(viewModel.HasPendingChanges);
        Assert.False(viewModel.RestartCommand.CanExecute(null));
        viewModel.RestartCommand.Execute(null);
        Assert.Empty(_harness.Dialogs.Requests);
    }

    [Fact]
    public void Countdown_UpdatesTheDisplayEverySecond()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.AmountText = "1";
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        viewModel.StartCommand.Execute(null);
        changes.Clear();

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(1));

        Assert.Contains(nameof(MainViewModel.CountdownText), changes);
        Assert.Equal("00:00:59", viewModel.CountdownText);
    }

    [Fact]
    public void MissedDeadline_IsExplained()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.StartCommand.Execute(null);

        _harness.Time.Advance(TimeSpan.FromHours(2));

        Assert.False(viewModel.IsActive);
        Assert.Equal("The countdown was cancelled: the PC woke up, or the clock changed, less than a minute before the scheduled time or after it.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(PowerAction.Sleep, "Windows did not let Somnus put the PC to sleep. Details are in the log.")]
    [InlineData(PowerAction.Hibernate, "Windows did not let Somnus hibernate the PC. Details are in the log.")]
    [InlineData(PowerAction.Shutdown, "Windows did not let Somnus shut down the PC. Details are in the log.")]
    public void FailedAction_IsExplained(PowerAction action, string expected)
    {
        _harness.Power.FailWith = new PowerActionFailedException("Denied.");
        _harness.Settings.Update(settings => settings with { WarningEnabled = false });
        using var viewModel = _harness.CreateMain();
        viewModel.Action = action;
        viewModel.AmountText = "10";
        viewModel.Unit = TimeUnit.Seconds;

        viewModel.StartCommand.Execute(null);
        _harness.AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.Equal(expected, viewModel.StatusMessage);

        viewModel.StartCommand.Execute(null);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public void UnavailableActions_AreExplainedAndNeverSelected()
    {
        _harness.Capabilities.Support = new PowerSupport(CanSleep: false, CanHibernate: false, IsModernStandby: true);

        using var viewModel = _harness.CreateMain();

        Assert.Equal(PowerAction.Shutdown, viewModel.Action);
        Assert.False(viewModel.IsSleepAvailable);
        Assert.False(viewModel.IsHibernateAvailable);
        Assert.Contains("Modern Standby", viewModel.SleepUnavailableReason, StringComparison.Ordinal);
        Assert.Contains("powercfg /hibernate on", viewModel.HibernateUnavailableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepUnsupportedWithoutModernStandby_SaysSo()
    {
        _harness.Capabilities.Support = new PowerSupport(CanSleep: false, CanHibernate: true, IsModernStandby: false);

        using var viewModel = _harness.CreateMain();

        Assert.Equal("This PC does not support sleep.", viewModel.SleepUnavailableReason);
        Assert.Null(viewModel.HibernateUnavailableReason);
    }

    [Fact]
    public void RefreshCapabilities_MovesAwayFromAnActionThatDisappeared()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsHibernateSelected = true;

        _harness.Capabilities.Support = new PowerSupport(CanSleep: true, CanHibernate: false, IsModernStandby: false);
        viewModel.RefreshCapabilities();

        Assert.Equal(PowerAction.Sleep, viewModel.Action);
        Assert.False(viewModel.IsHibernateAvailable);
    }

    [Fact]
    public void RefreshCapabilities_KeepsARunningCountdownUntouched()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsHibernateSelected = true;
        viewModel.StartCommand.Execute(null);

        _harness.Capabilities.Support = new PowerSupport(CanSleep: true, CanHibernate: false, IsModernStandby: false);
        viewModel.RefreshCapabilities();

        Assert.Equal(PowerAction.Hibernate, viewModel.Action);
    }

    [Fact]
    public void DryRun_IsVisibleInTheTitle()
    {
        using var viewModel = _harness.CreateMain(dryRun: true);

        Assert.Equal("Somnus (dry run)", viewModel.WindowTitle);
    }

    [Fact]
    public void Units_HaveReadableNames()
    {
        using var viewModel = _harness.CreateMain();

        Assert.Equal(["seconds", "minutes", "hours"], viewModel.Units.Select(unit => unit.DisplayName));
    }

    [Fact]
    public void OpenSettings_ShowsThePanelAndBlocksStart()
    {
        // Issue #54: the settings open inside the main window and cover the countdown controls.
        using var viewModel = _harness.CreateMain();
        Assert.True(viewModel.StartCommand.CanExecute(null));

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsOpen);
        Assert.NotNull(viewModel.Settings);
        Assert.False(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public void SavingTheSettings_ClosesThePanelAndKeepsTheLanguage()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(viewModel.Settings);

        settings.Language = AppLanguage.Polish;
        settings.SaveCommand.Execute(null);

        Assert.False(viewModel.IsSettingsOpen);
        Assert.True(viewModel.StartCommand.CanExecute(null));
        Assert.Equal(AppLanguage.Polish, _harness.Store.Stored.Language);
        Assert.Equal(["sek.", "min", "godz."], viewModel.Units.Select(unit => unit.DisplayName));
    }

    [Fact]
    public void Escape_WithTheSettingsOpen_ClosesThemAndEndsThePreview()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings!.Language = AppLanguage.Polish;

        viewModel.RequestCancelCommand.Execute(null);

        Assert.False(viewModel.IsSettingsOpen);
        Assert.Equal(AppLanguage.English, _harness.Localizer.Language);
        Assert.Empty(_harness.Dialogs.Requests);
    }

    [Fact]
    public void CloseSettings_WhenTheWindowCloses_EndsThePreview()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings!.Language = AppLanguage.Polish;

        viewModel.CloseSettings();
        viewModel.CloseSettings();

        Assert.False(viewModel.IsSettingsOpen);
        Assert.Equal(AppLanguage.English, _harness.Localizer.Language);
    }

    [Fact]
    public void ClockTimer_RefreshesThePreview()
    {
        using var viewModel = _harness.CreateMain();
        viewModel.IsAtMode = true;
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _harness.Time.Advance(MainViewModel.ClockRefreshInterval);

        Assert.Contains(nameof(MainViewModel.AtPreview), raised);
    }

    [Fact]
    public void Dispose_StopsListeningToTheCountdown()
    {
        var viewModel = _harness.CreateMain();
        viewModel.Dispose();
        var raised = 0;
        viewModel.PropertyChanged += (_, _) => raised++;

        _harness.Session.Start(new CountdownPlan(TestHarness.StartTime.AddMinutes(5), PowerAction.Sleep, false, false));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var h = _harness;
        var options = new AppRunOptions(false);

        Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, h.Capabilities, h.Settings, h.Autostart, h.Schedule, h.Time, h.Localizer, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, null!, h.Settings, h.Autostart, h.Schedule, h.Time, h.Localizer, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, null!, h.Autostart, h.Schedule, h.Time, h.Localizer, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, null!, h.Schedule, h.Time, h.Localizer, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, h.Autostart, h.Schedule, null!, h.Localizer, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, h.Autostart, h.Schedule, h.Time, null!, h.Dialogs, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, h.Autostart, h.Schedule, h.Time, h.Localizer, null!, h.Dispatcher, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, h.Autostart, h.Schedule, h.Time, h.Localizer, h.Dialogs, null!, options));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(h.Session, h.Capabilities, h.Settings, h.Autostart, h.Schedule, h.Time, h.Localizer, h.Dialogs, h.Dispatcher, null!));
    }

    [Fact]
    public void PropertyChangedSender_IsTheViewModel()
    {
        using var viewModel = _harness.CreateMain();
        object? sender = null;
        PropertyChangedEventHandler handler = (s, _) => sender = s;
        viewModel.PropertyChanged += handler;

        viewModel.AmountText = "12";

        Assert.Same(viewModel, sender);
    }
}
