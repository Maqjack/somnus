using Somnus.Core.Countdown;
using Somnus.Core.Scheduling;

namespace Somnus.Presentation.Services;

/// <summary>
/// Keeps a repeating countdown going (#83): after every run, and after a run the PC slept through,
/// it starts the next one, without an action of its own (assumption A8). The schedule is kept in the
/// settings, so it survives a restart, and it ends when the user cancels the countdown.
/// </summary>
public sealed class ScheduleCoordinator : IDisposable
{
    private readonly CountdownSession _session;
    private readonly SettingsService _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance and starts following the countdown.
    /// </summary>
    /// <param name="session">The countdown.</param>
    /// <param name="settings">Where the schedule is kept.</param>
    /// <param name="timeProvider">The clock and the time zone.</param>
    /// <param name="dispatcher">Moves countdown updates to the UI thread.</param>
    public ScheduleCoordinator(CountdownSession session, SettingsService settings, TimeProvider timeProvider, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _session = session;
        _settings = settings;
        _timeProvider = timeProvider;
        _dispatcher = dispatcher;
        _session.Changed += OnSessionChanged;
    }

    /// <summary>Gets the repeating request in force, or <see langword="null"/> without a schedule.</summary>
    public CountdownRequest? Request { get; private set; }

    /// <summary>Starts a schedule with its first run, in place of any earlier one.</summary>
    /// <param name="request">A request with days of the week.</param>
    public void Begin(CountdownRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        _settings.Update(current => current with
        {
            ScheduleDays = request.Repeat,
            ScheduleTime = request.ClockTime,
            ScheduleAction = request.Action,
        });
        _session.Start(request.ToPlan(_timeProvider.GetUtcNow(), _timeProvider.LocalTimeZone, _settings.Current.WarningEnabled));
    }

    /// <summary>
    /// Starts the schedule kept in the settings, if there is one, when Somnus starts. A run less than
    /// a minute away is skipped, so starting Somnus never ends in an action without a full warning (A8).
    /// </summary>
    public void Resume()
    {
        var current = _settings.Current;
        if (current.ScheduleDays == WeekDays.None || _session.Snapshot.IsActive)
        {
            return;
        }

        var request = CountdownRequest.At(current.ScheduleTime, current.ScheduleAction, repeat: current.ScheduleDays);
        var now = _timeProvider.GetUtcNow();
        var plan = request.ToPlan(now, _timeProvider.LocalTimeZone, current.WarningEnabled);
        if (plan.Target - now < CountdownSession.WarningLeadTime)
        {
            plan = request.ToNextPlan(now, plan.Target, _timeProvider.LocalTimeZone, current.WarningEnabled);
        }

        Request = request;
        _session.Start(plan);
    }

    /// <summary>Forgets the schedule; a countdown that is running stays as it is.</summary>
    public void End()
    {
        if (Request is not null)
        {
            Request = null;
            _settings.Update(current => current with { ScheduleDays = WeekDays.None });
        }
    }

    /// <inheritdoc />
    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        // The user cancels on the UI thread, maybe right before Somnus exits, so the schedule ends at once.
        if (_session.Snapshot is { State: CountdownState.Cancelled, CancellationReason: CancellationReason.User, Plan.Repeats: true })
        {
            End();
            return;
        }

        _dispatcher.Post(Sync);
    }

    private void Sync()
    {
        var snapshot = _session.Snapshot;
        if (Request is not { } request || snapshot.Plan is not { Repeats: true } plan)
        {
            return;
        }

        if (snapshot.State is CountdownState.Done or CountdownState.Failed
            || snapshot is { State: CountdownState.Cancelled, CancellationReason: not CancellationReason.User })
        {
            var next = request.ToNextPlan(_timeProvider.GetUtcNow(), plan.Target, _timeProvider.LocalTimeZone, _settings.Current.WarningEnabled);
            _session.Start(next);
        }
    }
}
