using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Presentation.Formatting;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;
using Somnus.Presentation.Services;

namespace Somnus.Presentation.ViewModels;

/// <summary>
/// The main window: choose when and what, start, follow and cancel the countdown.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>How often clock-dependent text (the "at" preview) is refreshed.</summary>
    public static readonly TimeSpan ClockRefreshInterval = TimeSpan.FromSeconds(10);

    private readonly CountdownSession _session;
    private readonly IPowerCapabilities _capabilities;
    private readonly SettingsService _settings;
    private readonly IAutostart _autostart;
    private readonly ScheduleCoordinator _schedule;
    private readonly TimeProvider _timeProvider;
    private readonly Localizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly AppRunOptions _runOptions;
    private readonly ITimer _clockTimer;

    private PowerSupport _support;
    private CountdownSnapshot _snapshot;
    private CountdownRequest? _runningRequest;
    private string? _statusKey;

    /// <summary>
    /// Initializes the view model with the remembered (or default) values.
    /// </summary>
    /// <param name="session">The countdown.</param>
    /// <param name="capabilities">Which actions the PC supports.</param>
    /// <param name="settings">The user's settings.</param>
    /// <param name="autostart">Starts Somnus with Windows when the user turns it on.</param>
    /// <param name="schedule">Keeps repeating countdowns going.</param>
    /// <param name="timeProvider">The clock and the time zone.</param>
    /// <param name="localizer">The interface texts.</param>
    /// <param name="dialogs">Confirmation dialogs.</param>
    /// <param name="dispatcher">Moves countdown updates to the UI thread.</param>
    /// <param name="runOptions">Whether this is a dry run.</param>
    public MainViewModel(
        CountdownSession session,
        IPowerCapabilities capabilities,
        SettingsService settings,
        IAutostart autostart,
        ScheduleCoordinator schedule,
        TimeProvider timeProvider,
        Localizer localizer,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        AppRunOptions runOptions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(runOptions);

        _session = session;
        _capabilities = capabilities;
        _settings = settings;
        _autostart = autostart;
        _schedule = schedule;
        _timeProvider = timeProvider;
        _localizer = localizer;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _runOptions = runOptions;
        _support = capabilities.Query();
        _snapshot = session.Snapshot;

        Days = CreateDayOptions();
        var initial = InitialValues(settings.Current);
        Mode = initial.LastMode;
        AmountText = initial.LastAmount.ToString(CultureInfo.InvariantCulture);
        Unit = initial.LastUnit;
        HourText = initial.LastAtTime.Hour.ToString("00", CultureInfo.InvariantCulture);
        MinuteText = initial.LastAtTime.Minute.ToString("00", CultureInfo.InvariantCulture);
        Action = _support.Supports(initial.LastAction) ? initial.LastAction : FirstAvailableAction();
        Units = CreateUnitOptions();
        foreach (var day in Days)
        {
            day.IsSelected = initial.LastRepeat.HasFlag(day.Day);
        }

        // A running schedule shows its own choice to force apps to close (ADR 0013).
        ForceCloseApps = settings.Current is { ScheduleDays: not WeekDays.None, ScheduleForceCloseApps: true }
            && Action == PowerAction.Shutdown;

        _session.Changed += OnSessionChanged;
        _localizer.LanguageChanged += OnLanguageChanged;
        _clockTimer = timeProvider.CreateTimer(_ => _dispatcher.Post(RefreshDerived), null, ClockRefreshInterval, ClockRefreshInterval);
    }

    /// <summary>
    /// Raised after Start begins a countdown, so the window can hide to the tray. Saving new values
    /// during a countdown keeps the window open, so the new clock stays in sight.
    /// </summary>
    public event EventHandler? CountdownStarted;

    /// <summary>
    /// Gets the settings panel while it is open inside the main window, or <see langword="null"/>.
    /// The panel covers the countdown controls, so nothing can start while it is open (#54).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial SettingsViewModel? Settings { get; private set; }

    /// <summary>Gets a value indicating whether the settings panel is open.</summary>
    public bool IsSettingsOpen => Settings is not null;

    /// <summary>Gets or sets how the time is given.</summary>
    [ObservableProperty]
    public partial TimeMode Mode { get; set; }

    /// <summary>Gets or sets the amount typed in the "in" mode.</summary>
    [ObservableProperty]
    public partial string AmountText { get; set; }

    /// <summary>Gets or sets the unit of the amount.</summary>
    [ObservableProperty]
    public partial TimeUnit Unit { get; set; }

    /// <summary>Gets or sets the hour typed in the "at" mode.</summary>
    [ObservableProperty]
    public partial string HourText { get; set; }

    /// <summary>Gets or sets the minute typed in the "at" mode.</summary>
    [ObservableProperty]
    public partial string MinuteText { get; set; }

    /// <summary>Gets or sets the chosen power action.</summary>
    [ObservableProperty]
    public partial PowerAction Action { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a shutdown closes apps without asking (rule R-05). A
    /// one-time countdown never remembers it; a schedule keeps it (ADR 0013).
    /// </summary>
    [ObservableProperty]
    public partial bool ForceCloseApps { get; set; }

    /// <summary>Gets the days of the week on which an "at" countdown can repeat, Monday first (#83).</summary>
    public IReadOnlyList<DayOption> Days { get; }

    /// <summary>
    /// Gets or sets a value indicating whether every day is ticked; setting it ticks or unticks them all.
    /// </summary>
    public bool IsEveryDay
    {
        get => RepeatDays == WeekDays.EveryDay;
        set
        {
            foreach (var day in Days)
            {
                day.IsSelected = value;
            }
        }
    }

    /// <summary>Gets the units for the drop-down, with localized names.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<UnitOption> Units { get; private set; }

    /// <summary>Gets the window title; it says so when power actions are only simulated.</summary>
    public string WindowTitle => _localizer[_runOptions.IsDryRun ? nameof(Strings.App_NameDryRun) : nameof(Strings.App_Name)];

    /// <summary>Gets or sets a value indicating whether the "in" mode is selected.</summary>
    public bool IsInMode
    {
        get => Mode == TimeMode.In;
        set
        {
            if (value)
            {
                Mode = TimeMode.In;
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the "at" mode is selected.</summary>
    public bool IsAtMode
    {
        get => Mode == TimeMode.At;
        set
        {
            if (value)
            {
                Mode = TimeMode.At;
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether sleep is selected.</summary>
    public bool IsSleepSelected
    {
        get => Action == PowerAction.Sleep;
        set => SelectAction(PowerAction.Sleep, value);
    }

    /// <summary>Gets or sets a value indicating whether hibernate is selected.</summary>
    public bool IsHibernateSelected
    {
        get => Action == PowerAction.Hibernate;
        set => SelectAction(PowerAction.Hibernate, value);
    }

    /// <summary>Gets or sets a value indicating whether shut down is selected.</summary>
    public bool IsShutdownSelected
    {
        get => Action == PowerAction.Shutdown;
        set => SelectAction(PowerAction.Shutdown, value);
    }

    /// <summary>Gets a value indicating whether sleep can be chosen.</summary>
    public bool IsSleepAvailable => _support.CanSleep;

    /// <summary>Gets a value indicating whether hibernate can be chosen.</summary>
    public bool IsHibernateAvailable => _support.CanHibernate;

    /// <summary>Gets why sleep is unavailable, or <see langword="null"/> when it is available.</summary>
    public string? SleepUnavailableReason => _support.CanSleep
        ? null
        : _localizer[_support.IsModernStandby ? nameof(Strings.Main_SleepUnavailableModernStandby) : nameof(Strings.Main_SleepUnavailable)];

    /// <summary>Gets why hibernate is unavailable, or <see langword="null"/> when it is available.</summary>
    public string? HibernateUnavailableReason => _support.CanHibernate ? null : _localizer[nameof(Strings.Main_HibernateUnavailable)];

    /// <summary>
    /// Gets a value indicating whether the "force close apps" box is shown: for every shutdown, once or
    /// on a schedule (ADR 0013).
    /// </summary>
    public bool IsForceOptionVisible => Action == PowerAction.Shutdown;

    /// <summary>Gets a value indicating whether the data-loss warning is shown (rule R-05).</summary>
    public bool IsForceWarningVisible => ForceCloseApps && IsForceOptionVisible;

    /// <summary>Gets the inline validation message, or <see langword="null"/> when the input is valid.</summary>
    public string? InputError => TryBuildRequest(out _, out var errorKey) ? null : _localizer[errorKey];

    /// <summary>Gets when an "at" countdown would run if started now, for example "Runs on Thu, 22 Oct at 23:30".</summary>
    public string? AtPreview
    {
        get
        {
            if (Mode != TimeMode.At || !TryBuildRequest(out var request, out _))
            {
                return null;
            }

            // It says what will happen, not only when (#58).
            var target = request.ResolveTarget(_timeProvider.GetUtcNow(), _timeProvider.LocalTimeZone);
            var (date, time) = CountdownFormat.LocalDateAndTime(_localizer, target, _timeProvider.LocalTimeZone);
            var key = Action switch
            {
                PowerAction.Sleep => nameof(Strings.Main_AtPreviewSleep),
                PowerAction.Hibernate => nameof(Strings.Main_AtPreviewHibernate),
                _ => nameof(Strings.Main_AtPreviewShutdown),
            };
            return _localizer.Format(key, date, time);
        }
    }

    /// <summary>Gets a value indicating whether a countdown is running.</summary>
    public bool IsActive => _snapshot.IsActive;

    /// <summary>Gets the remaining time as HH:MM:SS.</summary>
    public string CountdownText => CountdownFormat.Remaining(_snapshot.RemainingWholeSeconds);

    /// <summary>Gets what will happen and when, while a countdown runs.</summary>
    public string? TargetText => _snapshot is { IsActive: true, Plan: { } plan }
        ? CountdownFormat.Target(_localizer, plan.Action, plan.Target, _timeProvider.LocalTimeZone)
        : null;

    /// <summary>Gets on which days the running countdown repeats, or <see langword="null"/> if it does not.</summary>
    public string? RepeatText
    {
        get
        {
            if (!IsActive || _runningRequest is not { Repeat: not WeekDays.None } request)
            {
                return null;
            }

            var days = Days.Where(day => request.Repeat.HasFlag(day.Day)).Select(day => day.ShortName);
            return request.Repeat == WeekDays.EveryDay
                ? _localizer[nameof(Strings.Main_RepeatsEveryDay)]
                : _localizer.Format(nameof(Strings.Main_RepeatsOn), string.Join(", ", days));
        }
    }

    /// <summary>Gets a value indicating whether the inputs differ from the running countdown.</summary>
    public bool HasPendingChanges => IsActive
        && _runningRequest is not null
        && TryBuildRequest(out var request, out _)
        && request != _runningRequest;

    /// <summary>Gets the outcome of the last countdown when the user needs to know it.</summary>
    public string? StatusMessage => _statusKey is null ? null : _localizer[_statusKey];

    /// <summary>
    /// Reads the power capabilities again (call it when the window is shown) and picks another
    /// action if the chosen one is no longer available.
    /// </summary>
    public void RefreshCapabilities()
    {
        _support = _capabilities.Query();
        if (!IsActive && !_support.Supports(Action))
        {
            Action = FirstAvailableAction();
        }

        RefreshDerived();
    }

    /// <summary>
    /// Closes the settings panel without saving, which also ends the language preview; for example
    /// when the main window closes.
    /// </summary>
    public void CloseSettings() => Settings?.CancelCommand.Execute(null);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Settings is { } settings)
        {
            settings.CloseRequested -= OnSettingsCloseRequested;
        }

        _session.Changed -= OnSessionChanged;
        _localizer.LanguageChanged -= OnLanguageChanged;
        _clockTimer.Dispose();
    }

    private bool CanStart() => !IsActive && !IsSettingsOpen && TryBuildRequest(out _, out _) && _support.Supports(Action);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (TryBuildRequest(out var request, out _))
        {
            StartCountdown(request);
            CountdownStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool CanCancel() => IsActive;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _session.Cancel();

    /// <summary>
    /// Bound to Esc: closes the settings panel without saving when it is open; otherwise cancels the
    /// countdown after a confirmation, so a stray key press cannot cancel.
    /// </summary>
    [RelayCommand]
    private void RequestCancel()
    {
        if (IsSettingsOpen)
        {
            CloseSettings();
        }
        else if (IsActive && _dialogs.Confirm(CancelConfirmation()))
        {
            _session.Cancel();
        }
    }

    private bool CanRestart() => HasPendingChanges && _support.Supports(Action);

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        if (!TryBuildRequest(out var request, out _))
        {
            return;
        }

        var confirmation = new ConfirmationRequest(
            _localizer[nameof(Strings.Confirm_RestartTitle)],
            _localizer[nameof(Strings.Confirm_RestartMessage)],
            _localizer[nameof(Strings.Confirm_RestartYes)],
            _localizer[nameof(Strings.Confirm_RestartNo)]);

        if (_dialogs.Confirm(confirmation))
        {
            _session.Cancel();
            StartCountdown(request);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (IsSettingsOpen)
        {
            return;
        }

        var settings = new SettingsViewModel(_settings, _autostart, _dialogs, _localizer);
        settings.CloseRequested += OnSettingsCloseRequested;
        Settings = settings;
    }

    private void OnSettingsCloseRequested(object? sender, bool saved)
    {
        if (sender is SettingsViewModel settings)
        {
            settings.CloseRequested -= OnSettingsCloseRequested;
        }

        Settings = null;
    }

    private ConfirmationRequest CancelConfirmation() => _snapshot.Plan is { Repeats: true }
        ? new(
            _localizer[nameof(Strings.Confirm_CancelScheduleTitle)],
            _localizer.Format(nameof(Strings.Confirm_CancelScheduleMessage), TargetText),
            _localizer[nameof(Strings.Confirm_CancelScheduleYes)],
            _localizer[nameof(Strings.Confirm_CancelScheduleNo)])
        : new(
            _localizer[nameof(Strings.Confirm_CancelTitle)],
            _localizer.Format(nameof(Strings.Confirm_CancelMessage), TargetText),
            _localizer[nameof(Strings.Confirm_CancelYes)],
            _localizer[nameof(Strings.Confirm_CancelNo)]);

    private static AppSettings InitialValues(AppSettings settings)
    {
        var initial = settings.RememberLast ? settings : AppSettings.Default;

        // A running schedule shows its own values, whether or not Somnus remembers the last countdown.
        return settings.ScheduleDays == WeekDays.None
            ? initial
            : initial with
            {
                LastMode = TimeMode.At,
                LastAtTime = settings.ScheduleTime,
                LastAction = settings.ScheduleAction,
                LastRepeat = settings.ScheduleDays,
            };
    }

    private void StartCountdown(CountdownRequest request)
    {
        _statusKey = null;
        if (request.Repeat == WeekDays.None)
        {
            _schedule.End();
            _session.Start(request.ToPlan(_timeProvider.GetUtcNow(), _timeProvider.LocalTimeZone, _settings.Current.WarningEnabled));
        }
        else
        {
            _schedule.Begin(request);
        }

        _runningRequest = request;
        RememberInputs();
        Sync();
    }

    private void RememberInputs()
    {
        if (!_settings.Current.RememberLast)
        {
            return;
        }

        var delay = CountdownInput.ParseDelay(AmountText, Unit);
        var clockTime = CountdownInput.ParseClockTime(HourText, MinuteText);
        _settings.Update(current => current with
        {
            LastMode = Mode,
            LastAction = Action,
            LastAmount = delay.IsValid ? int.Parse(AmountText.Trim(), CultureInfo.InvariantCulture) : current.LastAmount,
            LastUnit = delay.IsValid ? Unit : current.LastUnit,
            LastAtTime = clockTime.IsValid ? clockTime.Time : current.LastAtTime,
            LastRepeat = RepeatDays,
        });
    }

    private bool TryBuildRequest([MaybeNullWhen(false)] out CountdownRequest request, [NotNullWhen(false)] out string? errorKey)
    {
        var force = ForceCloseApps && IsForceOptionVisible;
        if (Mode == TimeMode.In)
        {
            var delay = CountdownInput.ParseDelay(AmountText, Unit);
            errorKey = delay.Error switch
            {
                DelayInputError.None => null,
                DelayInputError.Empty => nameof(Strings.Input_Empty),
                DelayInputError.NotANumber => nameof(Strings.Input_NotANumber),
                DelayInputError.TooShort => nameof(Strings.Input_TooShort),
                _ => nameof(Strings.Input_TooLong),
            };
            request = delay.IsValid ? CountdownRequest.In(delay.Delay, Action, force) : null;
        }
        else
        {
            var clockTime = CountdownInput.ParseClockTime(HourText, MinuteText);
            errorKey = clockTime.Error switch
            {
                ClockTimeInputError.None => null,
                ClockTimeInputError.InvalidHour => nameof(Strings.Input_InvalidHour),
                _ => nameof(Strings.Input_InvalidMinute),
            };
            request = clockTime.IsValid ? CountdownRequest.At(clockTime.Time, Action, force, RepeatDays) : null;
        }

        return errorKey is null;
    }

    private void SelectAction(PowerAction action, bool selected)
    {
        if (selected)
        {
            Action = action;
        }
    }

    private PowerAction FirstAvailableAction() =>
        Enum.GetValues<PowerAction>().First(_support.Supports);

    private WeekDays RepeatDays => Days.Where(day => day.IsSelected).Aggregate(WeekDays.None, (all, day) => all | day.Day);

    private DayOption[] CreateDayOptions()
    {
        DayOption[] days =
        [
            new(WeekDays.Monday, RefreshDerived),
            new(WeekDays.Tuesday, RefreshDerived),
            new(WeekDays.Wednesday, RefreshDerived),
            new(WeekDays.Thursday, RefreshDerived),
            new(WeekDays.Friday, RefreshDerived),
            new(WeekDays.Saturday, RefreshDerived),
            new(WeekDays.Sunday, RefreshDerived),
        ];
        NameDays(days);
        return days;
    }

    private void NameDays(DayOption[] days)
    {
        foreach (var day in days)
        {
            day.ShortName = _localizer[$"Day_Short{day.Day}"];
            day.FullName = _localizer[$"Day_{day.Day}"];
        }
    }

    private List<UnitOption> CreateUnitOptions() =>
    [
        new(TimeUnit.Seconds, _localizer[nameof(Strings.Unit_Seconds)]),
        new(TimeUnit.Minutes, _localizer[nameof(Strings.Unit_Minutes)]),
        new(TimeUnit.Hours, _localizer[nameof(Strings.Unit_Hours)]),
    ];

    partial void OnModeChanged(TimeMode value) => RefreshDerived();

    partial void OnAmountTextChanged(string value) => RefreshDerived();

    partial void OnUnitChanged(TimeUnit value) => RefreshDerived();

    partial void OnHourTextChanged(string value) => RefreshDerived();

    partial void OnMinuteTextChanged(string value) => RefreshDerived();

    partial void OnActionChanged(PowerAction value)
    {
        // Rule R-05: the force option never carries over to another action.
        if (value != PowerAction.Shutdown)
        {
            ForceCloseApps = false;
        }

        RefreshDerived();
    }

    partial void OnForceCloseAppsChanged(bool value) => RefreshDerived();

    private void OnSessionChanged(object? sender, EventArgs e) => _dispatcher.Post(Sync);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Units = CreateUnitOptions();
        NameDays([.. Days]);

        // A new list clears the selection of the unit box; announcing the unit again restores it.
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(WindowTitle));
        RefreshDerived();
    }

    private void Sync()
    {
        var previous = _snapshot;
        _snapshot = _session.Snapshot;

        if (_snapshot.State != previous.State)
        {
            // An outcome message stays until a new countdown starts or one ends normally.
            _statusKey = CountdownFormat.OutcomeMessageKey(_snapshot)
                ?? (_snapshot.IsActive || _snapshot.State is CountdownState.Done or CountdownState.Cancelled ? null : _statusKey);
        }

        _runningRequest = _snapshot.IsActive ? _runningRequest ?? _schedule.Request : null;

        RefreshDerived();
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(IsInMode));
        OnPropertyChanged(nameof(IsAtMode));
        OnPropertyChanged(nameof(IsEveryDay));
        OnPropertyChanged(nameof(IsSleepSelected));
        OnPropertyChanged(nameof(IsHibernateSelected));
        OnPropertyChanged(nameof(IsShutdownSelected));
        OnPropertyChanged(nameof(IsSleepAvailable));
        OnPropertyChanged(nameof(IsHibernateAvailable));
        OnPropertyChanged(nameof(SleepUnavailableReason));
        OnPropertyChanged(nameof(HibernateUnavailableReason));
        OnPropertyChanged(nameof(IsForceOptionVisible));
        OnPropertyChanged(nameof(IsForceWarningVisible));
        OnPropertyChanged(nameof(InputError));
        OnPropertyChanged(nameof(AtPreview));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(TargetText));
        OnPropertyChanged(nameof(RepeatText));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(StatusMessage));
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>
/// A time unit with its localized name, for the unit drop-down.
/// </summary>
/// <param name="Value">The unit.</param>
/// <param name="DisplayName">The name shown to the user.</param>
public sealed record UnitOption(TimeUnit Value, string DisplayName);
