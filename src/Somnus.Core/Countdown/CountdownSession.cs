using Microsoft.Extensions.Logging;
using Somnus.Core.Power;

namespace Somnus.Core.Countdown;

/// <summary>
/// Runs one countdown at a time and performs the planned power action when it reaches zero.
/// </summary>
/// <remarks>
/// <para>
/// States: <c>Idle → Running → Warning → Executing → Done</c> (or <c>Failed</c>), and
/// <c>Cancelled</c> from <c>Running</c> or <c>Warning</c>. The session is reusable: after
/// <c>Done</c>, <c>Failed</c> or <c>Cancelled</c>, the next <see cref="Start"/> begins a new countdown.
/// </para>
/// <para>
/// The target is an absolute instant and the remaining time is recomputed from the clock on every
/// tick, so the countdown never drifts. A gap in the ticks means the PC was asleep or the clock
/// jumped; if the gap ends less than <see cref="WarningLeadTime"/> before the deadline or after it,
/// the countdown is cancelled instead of acting on whoever just woke the PC (assumption A8).
/// </para>
/// <para>
/// Thread safety: every member may be called from any thread. <see cref="Changed"/> is raised on the
/// thread that caused the change, often a timer thread, and notifications may arrive out of order;
/// handlers read <see cref="Snapshot"/> for the current state and must not throw.
/// </para>
/// </remarks>
public sealed partial class CountdownSession : IDisposable
{
    /// <summary>How often the remaining time is recomputed.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>How long before the action the warning window opens (assumption A4).</summary>
    public static readonly TimeSpan WarningLeadTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The longest gap between two ticks that still counts as a busy PC. Ticks normally come every
    /// <see cref="TickInterval"/>; a longer gap means the PC was asleep or the clock jumped.
    /// </summary>
    public static readonly TimeSpan TickGapTolerance = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long before a repeating action the PC is kept awake. A schedule runs day and night, so
    /// holding the PC awake all the time would stop it from ever sleeping on its own (rule R-06).
    /// </summary>
    public static readonly TimeSpan RepeatKeepAwakeLeadTime = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly IPowerController _powerController;
    private readonly IKeepAwake _keepAwake;
    private readonly ILogger<CountdownSession> _logger;
    private readonly Func<Action, Task> _runInBackground;

    private CountdownSnapshot _snapshot = CountdownSnapshot.Idle;
    private ITimer? _timer;
    private DateTimeOffset _lastTickAt;
    private long _lastReportedSecond;
    private bool _keepingAwake;
    private bool _disposed;

    /// <summary>
    /// Initializes a new session.
    /// </summary>
    /// <param name="timeProvider">The clock and timer source.</param>
    /// <param name="powerController">Performs the action at zero.</param>
    /// <param name="keepAwake">Keeps the PC awake while a countdown is active.</param>
    /// <param name="logger">Receives state changes and failures.</param>
    public CountdownSession(
        TimeProvider timeProvider,
        IPowerController powerController,
        IKeepAwake keepAwake,
        ILogger<CountdownSession> logger)
        : this(timeProvider, powerController, keepAwake, logger, Task.Run)
    {
    }

    /// <summary>
    /// Initializes a new session that runs the power action through <paramref name="runInBackground"/>,
    /// which tests replace with a synchronous runner.
    /// </summary>
    internal CountdownSession(
        TimeProvider timeProvider,
        IPowerController powerController,
        IKeepAwake keepAwake,
        ILogger<CountdownSession> logger,
        Func<Action, Task> runInBackground)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(powerController);
        ArgumentNullException.ThrowIfNull(keepAwake);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(runInBackground);

        _timeProvider = timeProvider;
        _powerController = powerController;
        _keepAwake = keepAwake;
        _logger = logger;
        _runInBackground = runInBackground;
    }

    /// <summary>
    /// Raised after the state changes and when the remaining time crosses a whole second.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Gets the current state of the session.</summary>
    public CountdownSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>
    /// Starts a countdown. With the warning on and 60 seconds or less to go, it starts directly in
    /// <see cref="CountdownState.Warning"/>.
    /// </summary>
    /// <param name="plan">What to do and when.</param>
    /// <exception cref="ArgumentException">The target is not in the future.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The plan names an unknown action.</exception>
    /// <exception cref="InvalidOperationException">A countdown is already active or executing.</exception>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    public void Start(CountdownPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(plan.Action))
        {
            throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, "Unknown power action.");
        }

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_snapshot.IsActive || _snapshot.State == CountdownState.Executing)
            {
                throw new InvalidOperationException("A countdown is already active.");
            }

            var remaining = plan.Target - now;
            if (remaining <= TimeSpan.Zero)
            {
                throw new ArgumentException("The target must be in the future.", nameof(plan));
            }

            var state = plan.WarningEnabled && remaining <= WarningLeadTime
                ? CountdownState.Warning
                : CountdownState.Running;

            KeepAwakeIfDue(plan, remaining);
            _snapshot = new CountdownSnapshot(state, plan, remaining, CancellationReason.None);
            _lastTickAt = now;
            _lastReportedSecond = _snapshot.RemainingWholeSeconds;
            _timer = _timeProvider.CreateTimer(_ => OnTick(), null, TickInterval, TickInterval);
        }

        Log.Started(_logger, plan.Action, plan.Target, plan.ForceCloseApps, plan.WarningEnabled);
        OnChanged();
    }

    /// <summary>
    /// Cancels the active countdown. Safe to call at any time.
    /// </summary>
    /// <returns><see langword="true"/> if a countdown was active and is now cancelled.</returns>
    public bool Cancel()
    {
        lock (_gate)
        {
            if (!_snapshot.IsActive)
            {
                return false;
            }

            StopLocked();
            _snapshot = _snapshot with { State = CountdownState.Cancelled, CancellationReason = CancellationReason.User };
        }

        Log.Cancelled(_logger);
        OnChanged();
        return true;
    }

    /// <summary>
    /// Performs the action of the active countdown right away ("Do it now").
    /// </summary>
    /// <returns><see langword="true"/> if a countdown was active and its action has started.</returns>
    public bool ExecuteNow()
    {
        CountdownPlan plan;
        lock (_gate)
        {
            if (_snapshot is not { IsActive: true, Plan: { } activePlan })
            {
                return false;
            }

            plan = activePlan;
            StopLocked();
            _snapshot = _snapshot with { State = CountdownState.Executing, Remaining = TimeSpan.Zero };
        }

        OnChanged();
        BeginExecution(plan);
        return true;
    }

    /// <summary>
    /// Stops the timer and releases keep-awake. An active countdown ends as cancelled; an action
    /// that is already executing is not interrupted.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_snapshot.IsActive)
            {
                StopLocked();
                _snapshot = _snapshot with { State = CountdownState.Cancelled, CancellationReason = CancellationReason.User };
            }
        }
    }

    private void OnTick()
    {
        var now = _timeProvider.GetUtcNow();
        CountdownPlan? planToExecute = null;
        (TimeSpan Gap, TimeSpan Remaining)? missed = null;
        bool changed;

        lock (_gate)
        {
            if (_snapshot is not { IsActive: true, Plan: { } plan })
            {
                // A tick that raced with Cancel, ExecuteNow or Dispose.
                return;
            }

            var remaining = plan.Target - now;
            var gap = now - _lastTickAt;
            _lastTickAt = now;

            if (IsDeadlineMissed(gap, remaining))
            {
                StopLocked();
                _snapshot = _snapshot with
                {
                    State = CountdownState.Cancelled,
                    Remaining = TimeSpan.Zero,
                    CancellationReason = CancellationReason.DeadlineMissed,
                };
                missed = (gap, remaining);
                changed = true;
            }
            else if (remaining <= TimeSpan.Zero)
            {
                StopLocked();
                _snapshot = _snapshot with { State = CountdownState.Executing, Remaining = TimeSpan.Zero };
                planToExecute = plan;
                changed = true;
            }
            else
            {
                KeepAwakeIfDue(plan, remaining);
                var enterWarning = plan.WarningEnabled
                    && _snapshot.State == CountdownState.Running
                    && remaining <= WarningLeadTime;

                _snapshot = _snapshot with
                {
                    State = enterWarning ? CountdownState.Warning : _snapshot.State,
                    Remaining = remaining,
                };

                var wholeSeconds = _snapshot.RemainingWholeSeconds;
                changed = enterWarning || wholeSeconds != _lastReportedSecond;
                _lastReportedSecond = wholeSeconds;
            }
        }

        if (missed is { } deadline)
        {
            Log.DeadlineMissed(_logger, deadline.Gap, deadline.Remaining);
        }

        if (changed)
        {
            OnChanged();
        }

        if (planToExecute is not null)
        {
            BeginExecution(planToExecute);
        }
    }

    /// <summary>
    /// The deadline counts as missed when the countdown could not run for a while (the PC was asleep
    /// or the clock jumped) and comes back less than <see cref="WarningLeadTime"/> before the
    /// deadline or after it. Acting then would surprise whoever just woke the PC, without the full
    /// warning, so this applies whether or not the warning is on (assumption A8).
    /// </summary>
    private static bool IsDeadlineMissed(TimeSpan gap, TimeSpan remaining) =>
        gap > TickGapTolerance && remaining <= WarningLeadTime;

    private void KeepAwakeIfDue(CountdownPlan plan, TimeSpan remaining)
    {
        if (!_keepingAwake && (!plan.Repeats || remaining <= RepeatKeepAwakeLeadTime))
        {
            _keepAwake.Acquire();
            _keepingAwake = true;
        }
    }

    private void StopLocked()
    {
        _timer?.Dispose();
        _timer = null;
        _keepAwake.Release();
        _keepingAwake = false;
    }

    private void BeginExecution(CountdownPlan plan)
    {
        Log.Executing(_logger, plan.Action, plan.ForceCloseApps);
        _ = _runInBackground(() => Execute(plan));
    }

    private void Execute(CountdownPlan plan)
    {
        CountdownState outcome;
        try
        {
            switch (plan.Action)
            {
                case PowerAction.Sleep:
                    _powerController.Sleep();
                    break;
                case PowerAction.Hibernate:
                    _powerController.Hibernate();
                    break;
                case PowerAction.Shutdown:
                    _powerController.Shutdown(plan.ForceCloseApps);
                    break;
            }

            outcome = CountdownState.Done;
            Log.Executed(_logger, plan.Action);
        }
#pragma warning disable CA1031 // Any failure must end the session in Failed instead of leaving it stuck in Executing.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            outcome = CountdownState.Failed;
            Log.ExecutionFailed(_logger, plan.Action, exception);
        }

        lock (_gate)
        {
            _snapshot = _snapshot with { State = outcome };
        }

        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static partial class Log
    {
        [LoggerMessage(EventId = 100, Level = LogLevel.Information,
            Message = "Countdown started: {Action} at {Target:O}, force close apps: {ForceCloseApps}, warning: {WarningEnabled}")]
        public static partial void Started(ILogger logger, PowerAction action, DateTimeOffset target, bool forceCloseApps, bool warningEnabled);

        [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Countdown cancelled by the user")]
        public static partial void Cancelled(ILogger logger);

        [LoggerMessage(EventId = 102, Level = LogLevel.Warning,
            Message = "No tick for {Gap} (the PC was asleep or the clock changed), {Remaining} left until the deadline; countdown cancelled without acting")]
        public static partial void DeadlineMissed(ILogger logger, TimeSpan gap, TimeSpan remaining);

        [LoggerMessage(EventId = 103, Level = LogLevel.Information, Message = "Executing {Action} (force close apps: {ForceCloseApps})")]
        public static partial void Executing(ILogger logger, PowerAction action, bool forceCloseApps);

        [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "{Action} completed")]
        public static partial void Executed(ILogger logger, PowerAction action);

        [LoggerMessage(EventId = 105, Level = LogLevel.Error, Message = "{Action} failed")]
        public static partial void ExecutionFailed(ILogger logger, PowerAction action, Exception exception);
    }
}
