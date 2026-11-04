using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Presentation.Formatting;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;
using Somnus.Presentation.Services;

namespace Somnus.Presentation.ViewModels;

/// <summary>
/// Everything around the main window: the tray icon, the warning window, notifications and exit.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly CountdownSession _session;
    private readonly Localizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private CountdownSnapshot _snapshot;
    private bool _hiddenNoticeShown;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="session">The countdown.</param>
    /// <param name="localizer">The interface texts.</param>
    /// <param name="dialogs">Confirmation dialogs.</param>
    /// <param name="notifications">Tray notifications.</param>
    /// <param name="dispatcher">Moves countdown updates to the UI thread.</param>
    /// <param name="timeProvider">The time zone for the texts.</param>
    public ShellViewModel(
        CountdownSession session,
        Localizer localizer,
        IDialogService dialogs,
        INotificationService notifications,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _session = session;
        _localizer = localizer;
        _dialogs = dialogs;
        _notifications = notifications;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _snapshot = session.Snapshot;
        Warning = new WarningViewModel(session, localizer);

        _session.Changed += OnSessionChanged;
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Raised when the main window should come back from the tray.</summary>
    public event EventHandler? ShowMainWindowRequested;

    /// <summary>Raised when Somnus should exit.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Gets the view model of the warning window.</summary>
    public WarningViewModel Warning { get; }

    /// <summary>Gets the tray tooltip: the remaining time while counting down.</summary>
    public string TrayToolTip
    {
        get
        {
            if (_snapshot is not { IsActive: true, Plan: { } plan })
            {
                return _localizer[nameof(Strings.App_Name)];
            }

            var key = plan.Action switch
            {
                PowerAction.Sleep => nameof(Strings.Tray_ToolTipSleep),
                PowerAction.Hibernate => nameof(Strings.Tray_ToolTipHibernate),
                _ => nameof(Strings.Tray_ToolTipShutdown),
            };
            return _localizer.Format(key, CountdownFormat.Remaining(_snapshot.RemainingWholeSeconds));
        }
    }

    /// <summary>Gets a value indicating whether a countdown can be cancelled from the tray.</summary>
    public bool IsCountdownActive => _snapshot.IsActive;

    /// <summary>Gets a value indicating whether the warning window should be open.</summary>
    public bool IsWarningOpen => _snapshot.State == CountdownState.Warning;

    /// <summary>
    /// Gets the label of the tray menu item that shows the window: it says what the window is for
    /// while a countdown runs, and otherwise names the app.
    /// </summary>
    public string ShowMenuText => _localizer[_snapshot.IsActive ? nameof(Strings.Tray_EditCountdown) : nameof(Strings.Tray_Open)];

    /// <summary>Gets the label of the tray menu item that cancels the countdown.</summary>
    public string CancelMenuText => _localizer[nameof(Strings.Tray_Cancel)];

    /// <summary>Gets the label of the tray menu item that exits Somnus.</summary>
    public string ExitMenuText => _localizer[nameof(Strings.Tray_Exit)];

    /// <summary>
    /// Tells the user, once per run, that the countdown goes on in the tray after the window hid.
    /// </summary>
    public void NotifyHiddenInTray()
    {
        if (_hiddenNoticeShown || _snapshot is not { IsActive: true, Plan: { } plan })
        {
            return;
        }

        _hiddenNoticeShown = true;
        var target = CountdownFormat.Target(_localizer, plan.Action, plan.Target, _timeProvider.LocalTimeZone);
        _notifications.Notify(_localizer[nameof(Strings.Tray_HiddenTitle)], _localizer.Format(nameof(Strings.Tray_HiddenMessage), target));
    }

    /// <summary>
    /// Decides what closing the main window does (assumption A5): while a countdown runs, the window
    /// only hides to the tray and the user is told once per run; otherwise Somnus exits.
    /// </summary>
    /// <returns><see langword="true"/> to hide the window instead of closing it.</returns>
    public bool HideOnClose()
    {
        if (!_session.Snapshot.IsActive)
        {
            return false;
        }

        NotifyHiddenInTray();
        return true;
    }

    /// <summary>
    /// Tells the user that a power action was only simulated (dry run, rule R-01).
    /// </summary>
    /// <param name="action">The simulated action.</param>
    public void NotifySimulated(PowerAction action) => _dispatcher.Post(() => _notifications.Notify(
        _localizer[nameof(Strings.DryRun_Title)],
        _localizer.Format(nameof(Strings.DryRun_Message), CountdownFormat.ActionName(_localizer, action))));

    /// <inheritdoc />
    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _localizer.LanguageChanged -= OnLanguageChanged;
    }

    [RelayCommand]
    private void ShowMainWindow() => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(IsCountdownActive))]
    private void CancelCountdown() => _session.Cancel();

    /// <summary>Exits Somnus; a running countdown is cancelled after a confirmation (assumption A5).</summary>
    [RelayCommand]
    private void Exit()
    {
        if (_snapshot.IsActive)
        {
            var confirmation = new ConfirmationRequest(
                _localizer[nameof(Strings.Confirm_ExitTitle)],
                _localizer[_snapshot.Plan is { Repeats: true } ? nameof(Strings.Confirm_ExitScheduleMessage) : nameof(Strings.Confirm_ExitMessage)],
                _localizer[nameof(Strings.Confirm_ExitYes)],
                _localizer[nameof(Strings.Confirm_ExitNo)]);

            if (!_dialogs.Confirm(confirmation))
            {
                return;
            }

            _session.Cancel();
        }

        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionChanged(object? sender, EventArgs e) => _dispatcher.Post(Sync);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Warning.RefreshTexts();
        OnPropertyChanged(nameof(ShowMenuText));
        OnPropertyChanged(nameof(CancelMenuText));
        OnPropertyChanged(nameof(ExitMenuText));
        OnPropertyChanged(nameof(TrayToolTip));
    }

    private void Sync()
    {
        var previous = _snapshot;
        _snapshot = _session.Snapshot;
        Warning.Update(_snapshot);

        if (_snapshot.State != previous.State)
        {
            ReportOutcome();
            OnPropertyChanged(nameof(IsCountdownActive));
            OnPropertyChanged(nameof(IsWarningOpen));
            OnPropertyChanged(nameof(ShowMenuText));
            CancelCountdownCommand.NotifyCanExecuteChanged();
        }

        OnPropertyChanged(nameof(TrayToolTip));
    }

    /// <summary>Brings the window back and notifies when a countdown ended without its action.</summary>
    private void ReportOutcome()
    {
        var messageKey = CountdownFormat.OutcomeMessageKey(_snapshot);
        if (messageKey is not null)
        {
            _notifications.Notify(_localizer[nameof(Strings.App_Name)], _localizer[messageKey]);
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
