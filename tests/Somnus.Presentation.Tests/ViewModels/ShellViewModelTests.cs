using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.Presentation.Tests.ViewModels;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly ShellViewModel _shell;

    public ShellViewModelTests()
    {
        _shell = _harness.CreateShell();
    }

    public void Dispose()
    {
        _shell.Dispose();
        _harness.Dispose();
    }

    [Fact]
    public void Idle_TooltipIsTheAppName()
    {
        Assert.Equal("Somnus", _shell.TrayToolTip);
        Assert.False(_shell.IsCountdownActive);
        Assert.False(_shell.CancelCountdownCommand.CanExecute(null));
        Assert.Equal("Open Somnus", _shell.ShowMenuText);
        Assert.Equal("Stop countdown", _shell.CancelMenuText);
        Assert.Equal("Exit", _shell.ExitMenuText);
    }

    [Theory]
    [InlineData(PowerAction.Sleep, "Somnus: sleep in 00:10:00")]
    [InlineData(PowerAction.Hibernate, "Somnus: hibernate in 00:10:00")]
    [InlineData(PowerAction.Shutdown, "Somnus: shut down in 00:10:00")]
    public void Running_TooltipShowsTheRemainingTime(PowerAction action, string expected)
    {
        Start(TimeSpan.FromMinutes(10), action);

        Assert.Equal(expected, _shell.TrayToolTip);
        Assert.True(_shell.IsCountdownActive);
        Assert.True(_shell.CancelCountdownCommand.CanExecute(null));
    }

    [Fact]
    public void Running_TheMenuOffersToEditTheCountdown()
    {
        var raised = new List<string?>();
        _shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Start(TimeSpan.FromMinutes(10));

        Assert.Equal("Edit countdown", _shell.ShowMenuText);
        Assert.Contains(nameof(ShellViewModel.ShowMenuText), raised);
    }

    [Fact]
    public void TooltipFollowsTheCountdown()
    {
        Start(TimeSpan.FromMinutes(10));

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(3));

        Assert.Equal("Somnus: sleep in 00:09:57", _shell.TrayToolTip);
    }

    [Fact]
    public void WarningWindow_OpensForTheLastMinuteAndClosesAfterwards()
    {
        Start(TimeSpan.FromMinutes(2), warningEnabled: true);
        Assert.False(_shell.IsWarningOpen);

        _harness.AdvanceInTicks(TimeSpan.FromMinutes(1));
        Assert.True(_shell.IsWarningOpen);
        Assert.Equal("01:00", _shell.Warning.RemainingText);

        _shell.Warning.CancelCommand.Execute(null);
        Assert.False(_shell.IsWarningOpen);
        Assert.Equal(CountdownState.Cancelled, _harness.Session.Snapshot.State);
    }

    [Fact]
    public void CancelFromTheTray_StopsTheCountdown()
    {
        Start(TimeSpan.FromMinutes(5));

        _shell.CancelCountdownCommand.Execute(null);

        Assert.Equal(CountdownState.Cancelled, _harness.Session.Snapshot.State);
        Assert.False(_shell.IsCountdownActive);
    }

    [Fact]
    public void Exit_WhenIdle_ExitsWithoutAsking()
    {
        var exits = 0;
        _shell.ExitRequested += (_, _) => exits++;

        _shell.ExitCommand.Execute(null);

        Assert.Equal(1, exits);
        Assert.Empty(_harness.Dialogs.Requests);
    }

    [Fact]
    public void Exit_DuringACountdown_AsksAndCancels()
    {
        Start(TimeSpan.FromMinutes(5));
        var exits = 0;
        _shell.ExitRequested += (_, _) => exits++;

        _shell.ExitCommand.Execute(null);

        Assert.Equal("Exit Somnus?", Assert.Single(_harness.Dialogs.Requests).Title);
        Assert.Equal(CountdownState.Cancelled, _harness.Session.Snapshot.State);
        Assert.Equal(1, exits);
    }

    [Fact]
    public void Exit_DeclinedKeepsCounting()
    {
        Start(TimeSpan.FromMinutes(5));
        _harness.Dialogs.Answer = false;
        var exits = 0;
        _shell.ExitRequested += (_, _) => exits++;

        _shell.ExitCommand.Execute(null);

        Assert.Equal(0, exits);
        Assert.True(_harness.Session.Snapshot.IsActive);
    }

    [Fact]
    public void ShowMainWindow_AsksTheShell()
    {
        var requests = 0;
        _shell.ShowMainWindowRequested += (_, _) => requests++;

        _shell.ShowMainWindowCommand.Execute(null);

        Assert.Equal(1, requests);
    }

    [Fact]
    public void MissedDeadline_NotifiesAndBringsTheWindowBack()
    {
        Start(TimeSpan.FromMinutes(5));
        var shows = 0;
        _shell.ShowMainWindowRequested += (_, _) => shows++;

        _harness.Time.Advance(TimeSpan.FromHours(1));

        var notification = Assert.Single(_harness.Notifications.Notifications);
        Assert.Contains("the PC woke up", notification.Message, StringComparison.Ordinal);
        Assert.Equal(1, shows);
    }

    [Fact]
    public void FailedAction_NotifiesAndBringsTheWindowBack()
    {
        _harness.Power.FailWith = new PowerActionFailedException("Denied.");
        Start(TimeSpan.FromSeconds(10), PowerAction.Hibernate);
        var shows = 0;
        _shell.ShowMainWindowRequested += (_, _) => shows++;

        _harness.AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.Equal("Windows did not let Somnus hibernate the PC. Details are in the log.", Assert.Single(_harness.Notifications.Notifications).Message);
        Assert.Equal(1, shows);
    }

    [Fact]
    public void CompletedOrCancelledCountdowns_NotifyNothing()
    {
        Start(TimeSpan.FromSeconds(10));
        _harness.AdvanceInTicks(TimeSpan.FromSeconds(10));
        Start(TimeSpan.FromMinutes(1));
        _shell.CancelCountdownCommand.Execute(null);

        Assert.Empty(_harness.Notifications.Notifications);
    }

    [Fact]
    public void HiddenInTray_IsAnnouncedOncePerRun()
    {
        _shell.NotifyHiddenInTray();
        Assert.Empty(_harness.Notifications.Notifications);

        Start(TimeSpan.FromMinutes(30));
        _shell.NotifyHiddenInTray();
        _shell.NotifyHiddenInTray();

        var notification = Assert.Single(_harness.Notifications.Notifications);
        Assert.Equal("Somnus keeps counting", notification.Title);
        Assert.StartsWith("Sleep on Thu, 22 Oct at 18:30.", notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulatedAction_IsAnnounced()
    {
        _shell.NotifySimulated(PowerAction.Shutdown);

        var notification = Assert.Single(_harness.Notifications.Notifications);
        Assert.Equal("Dry run", notification.Title);
        Assert.StartsWith("Shut down simulated.", notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LanguageChange_RefreshesTheMenuTexts()
    {
        var raised = new List<string?>();
        _shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _harness.Localizer.Language = Core.Settings.AppLanguage.Polish;

        Assert.Contains(nameof(ShellViewModel.ShowMenuText), raised);
        Assert.Contains(nameof(ShellViewModel.TrayToolTip), raised);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var h = _harness;

        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(null!, h.Localizer, h.Dialogs, h.Notifications, h.Dispatcher, h.Time));
        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(h.Session, null!, h.Dialogs, h.Notifications, h.Dispatcher, h.Time));
        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(h.Session, h.Localizer, null!, h.Notifications, h.Dispatcher, h.Time));
        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(h.Session, h.Localizer, h.Dialogs, null!, h.Dispatcher, h.Time));
        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(h.Session, h.Localizer, h.Dialogs, h.Notifications, null!, h.Time));
        Assert.Throws<ArgumentNullException>(() => new ShellViewModel(h.Session, h.Localizer, h.Dialogs, h.Notifications, h.Dispatcher, null!));
    }

    [Fact]
    public void HideOnClose_WhileCountingDown_HidesAndTellsTheUserOnce()
    {
        // Assumption A5: closing the window during a countdown must not stop it.
        Start(TimeSpan.FromMinutes(30));

        Assert.True(_shell.HideOnClose());
        Assert.True(_shell.HideOnClose());

        Assert.Equal("Somnus keeps counting", Assert.Single(_harness.Notifications.Notifications).Title);
        Assert.True(_harness.Session.Snapshot.IsActive);
    }

    [Fact]
    public void HideOnClose_WithoutACountdown_LetsSomnusExit()
    {
        Assert.False(_shell.HideOnClose());

        Start(TimeSpan.FromMinutes(30));
        _harness.Session.Cancel();
        _harness.AdvanceInTicks(CountdownSession.TickInterval);

        Assert.False(_shell.HideOnClose());
        Assert.Empty(_harness.Notifications.Notifications);
    }

    private void Start(TimeSpan delay, PowerAction action = PowerAction.Sleep, bool warningEnabled = false) =>
        _harness.Session.Start(new CountdownPlan(_harness.Time.GetUtcNow() + delay, action, ForceCloseApps: false, warningEnabled));
}
