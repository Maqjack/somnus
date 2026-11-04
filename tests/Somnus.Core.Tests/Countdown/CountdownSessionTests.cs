using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Core.Tests.TestDoubles;

namespace Somnus.Core.Tests.Countdown;

public sealed class CountdownSessionTests : IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 22, 18, 0, 0, TimeSpan.FromHours(2));

    private readonly FakeTimeProvider _time = new(StartTime);
    private readonly FakePowerController _power;
    private readonly FakeKeepAwake _keepAwake = new();
    private readonly CountdownSession _session;
    private int _changedCount;

    public CountdownSessionTests()
    {
        _power = new FakePowerController(_time);
        _session = new CountdownSession(_time, _power, _keepAwake, NullLogger<CountdownSession>.Instance, RunSynchronously);
        _session.Changed += (_, _) => _changedCount++;
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void NewSession_IsIdle()
    {
        var snapshot = _session.Snapshot;

        Assert.Equal(CountdownState.Idle, snapshot.State);
        Assert.Null(snapshot.Plan);
        Assert.False(snapshot.IsActive);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void Start_WithWarningOff_RunsAndKeepsThePcAwake()
    {
        var plan = PlanIn(TimeSpan.FromMinutes(30));

        _session.Start(plan);

        var snapshot = _session.Snapshot;
        Assert.Equal(CountdownState.Running, snapshot.State);
        Assert.Same(plan, snapshot.Plan);
        Assert.Equal(TimeSpan.FromMinutes(30), snapshot.Remaining);
        Assert.True(snapshot.IsActive);
        Assert.True(_keepAwake.IsAcquired);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void ReachingZero_PerformsTheActionExactlyOnceAndReleasesKeepAwake()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(2), PowerAction.Hibernate));

        AdvanceInTicks(TimeSpan.FromMinutes(2) - TimeSpan.FromSeconds(1));
        Assert.Equal(CountdownState.Running, _session.Snapshot.State);
        Assert.Empty(_power.Calls);

        AdvanceInTicks(TimeSpan.FromSeconds(1));
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
        Assert.Equal(PowerAction.Hibernate, Assert.Single(_power.Calls).Action);
        Assert.False(_keepAwake.IsAcquired);

        AdvanceInTicks(TimeSpan.FromSeconds(10));
        Assert.Single(_power.Calls);
    }

    [Theory]
    [InlineData(PowerAction.Sleep)]
    [InlineData(PowerAction.Hibernate)]
    [InlineData(PowerAction.Shutdown)]
    public void ReachingZero_CallsTheMatchingPowerAction(PowerAction action)
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(10), action));

        AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.Equal(action, Assert.Single(_power.Calls).Action);
    }

    [Fact]
    public void TwentyFourHourCountdown_ActsWithinTwoSecondsOfTheTarget()
    {
        // Success criterion C2: the action runs within ±2 s, also after 24 hours.
        var plan = PlanIn(TimeSpan.FromHours(24), PowerAction.Shutdown);
        _session.Start(plan);

        AdvanceInTicks(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(2));

        var call = Assert.Single(_power.Calls);
        Assert.InRange(call.At - plan.Target, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Shutdown_PassesTheForceCloseAppsFlag()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(10), PowerAction.Shutdown, forceCloseApps: true));

        AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.True(Assert.Single(_power.Calls).ForceCloseApps);
    }

    [Fact]
    public void WarningOn_OpensTheWarningWindowSixtySecondsBeforeTheTarget()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(5), warningEnabled: true));

        AdvanceInTicks(TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(1));
        Assert.Equal(CountdownState.Running, _session.Snapshot.State);

        AdvanceInTicks(TimeSpan.FromSeconds(1));
        Assert.Equal(CountdownState.Warning, _session.Snapshot.State);
        Assert.Equal(TimeSpan.FromSeconds(60), _session.Snapshot.Remaining);
        Assert.Empty(_power.Calls);

        AdvanceInTicks(TimeSpan.FromSeconds(60));
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
        Assert.Single(_power.Calls);
    }

    [Fact]
    public void WarningOn_CountdownOfSixtySecondsOrLess_StartsInTheWarningWindow()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(30), warningEnabled: true));

        Assert.Equal(CountdownState.Warning, _session.Snapshot.State);
    }

    [Fact]
    public void WarningOff_ShortCountdown_NeverOpensTheWarningWindow()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(30)));
        var states = new List<CountdownState>();
        _session.Changed += (_, _) => states.Add(_session.Snapshot.State);

        AdvanceInTicks(TimeSpan.FromSeconds(30));

        Assert.DoesNotContain(CountdownState.Warning, states);
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
    }

    [Fact]
    public void Cancel_WhileRunning_StopsWithoutActing()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(10)));
        AdvanceInTicks(TimeSpan.FromMinutes(3));

        Assert.True(_session.Cancel());
        AdvanceInTicks(TimeSpan.FromMinutes(10));

        Assert.Equal(CountdownState.Cancelled, _session.Snapshot.State);
        Assert.Equal(CancellationReason.User, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void Cancel_InsideTheWarningWindow_StopsWithoutActing()
    {
        // Success criterion C3: cancelling works at any time, including in the warning dialog.
        _session.Start(PlanIn(TimeSpan.FromSeconds(30), warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromSeconds(29));

        Assert.True(_session.Cancel());
        AdvanceInTicks(TimeSpan.FromMinutes(1));

        Assert.Equal(CountdownState.Cancelled, _session.Snapshot.State);
        Assert.Empty(_power.Calls);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void Cancel_WhenNothingIsActive_ReturnsFalse()
    {
        Assert.False(_session.Cancel());

        _session.Start(PlanIn(TimeSpan.FromSeconds(10)));
        AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.False(_session.Cancel());
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
    }

    [Fact]
    public void ExecuteNow_InsideTheWarningWindow_ActsImmediately()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(45), PowerAction.Sleep, warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromSeconds(5));

        Assert.True(_session.ExecuteNow());

        var call = Assert.Single(_power.Calls);
        Assert.Equal(StartTime + TimeSpan.FromSeconds(5), call.At);
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
        Assert.False(_keepAwake.IsAcquired);

        AdvanceInTicks(TimeSpan.FromMinutes(1));
        Assert.Single(_power.Calls);
    }

    [Fact]
    public void ExecuteNow_WhenNothingIsActive_ReturnsFalse()
    {
        Assert.False(_session.ExecuteNow());
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public void SleepingThroughTheDeadline_CancelsInsteadOfActing()
    {
        // No ticks ran until long after the target: the PC was asleep.
        _session.Start(PlanIn(TimeSpan.FromMinutes(10)));

        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(CountdownState.Cancelled, _session.Snapshot.State);
        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void WakingUpSecondsAfterTheDeadline_CancelsInsteadOfActing()
    {
        // Assumption A8: waking the PC must never trigger a surprise shutdown, not even a few seconds late.
        _session.Start(PlanIn(TimeSpan.FromMinutes(10), PowerAction.Shutdown, forceCloseApps: true));
        AdvanceInTicks(TimeSpan.FromMinutes(9));

        _time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(20));

        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void SleepingInsideTheWarningWindow_CancelsOnWakeUp()
    {
        // The lid was closed while the warning was showing; the PC woke up after the deadline.
        _session.Start(PlanIn(TimeSpan.FromMinutes(5), warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(30));
        Assert.Equal(CountdownState.Warning, _session.Snapshot.State);

        _time.Advance(TimeSpan.FromSeconds(50));

        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public void WakingUpInsideTheWarningWindow_CancelsInsteadOfAShortWarning()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(10), warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromMinutes(5));
        var states = new List<CountdownState>();
        _session.Changed += (_, _) => states.Add(_session.Snapshot.State);

        _time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(55));

        Assert.Equal([CountdownState.Cancelled], states);
        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public void WakingUpInTheLastMinuteWithTheWarningOff_Cancels()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(10), PowerAction.Hibernate));
        AdvanceInTicks(TimeSpan.FromMinutes(5));

        _time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(30));

        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public void WakingUpBeforeTheLastMinute_KeepsCountingAndGivesTheFullWarning()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(10), warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromMinutes(2));

        _time.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(CountdownState.Running, _session.Snapshot.State);

        AdvanceInTicks(TimeSpan.FromMinutes(1));
        Assert.Equal(CountdownState.Warning, _session.Snapshot.State);
        Assert.Equal(TimeSpan.FromSeconds(60), _session.Snapshot.Remaining);

        AdvanceInTicks(TimeSpan.FromSeconds(60));
        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
        Assert.Single(_power.Calls);
    }

    [Fact]
    public void TickDelayedWithinTheTolerance_StillActs()
    {
        // A busy PC may delay a tick by a few seconds; that is no reason to cancel.
        var plan = PlanIn(TimeSpan.FromMinutes(10));
        _session.Start(plan);
        AdvanceInTicks(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(2));

        _time.Advance(CountdownSession.TickGapTolerance - TimeSpan.FromSeconds(1));

        Assert.Equal(CountdownState.Done, _session.Snapshot.State);
        Assert.Equal(plan.Target + TimeSpan.FromSeconds(2), Assert.Single(_power.Calls).At);
    }

    [Fact]
    public void SkippedWarningWindow_CancelsInsteadOfActing()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(10), warningEnabled: true));
        AdvanceInTicks(TimeSpan.FromMinutes(8));

        _time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

        Assert.Equal(CancellationReason.DeadlineMissed, _session.Snapshot.CancellationReason);
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public void RefusedPowerAction_EndsInFailedAndReleasesKeepAwake()
    {
        _power.FailWith = new PowerActionFailedException("Access denied.");
        _session.Start(PlanIn(TimeSpan.FromSeconds(10), PowerAction.Hibernate));

        AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.Equal(CountdownState.Failed, _session.Snapshot.State);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void UnexpectedPowerControllerError_EndsInFailed()
    {
        _power.FailWith = new InvalidOperationException("Something unexpected.");
        _session.Start(PlanIn(TimeSpan.FromSeconds(10)));

        AdvanceInTicks(TimeSpan.FromSeconds(10));

        Assert.Equal(CountdownState.Failed, _session.Snapshot.State);
    }

    [Fact]
    public void Start_WhileACountdownIsActive_Throws()
    {
        _session.Start(PlanIn(TimeSpan.FromMinutes(1)));

        Assert.Throws<InvalidOperationException>(() => _session.Start(PlanIn(TimeSpan.FromMinutes(2))));
        Assert.Equal(1, _keepAwake.AcquireCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Start_WithATargetThatIsNotInTheFuture_Throws(int secondsFromNow)
    {
        Assert.Throws<ArgumentException>(() => _session.Start(PlanIn(TimeSpan.FromSeconds(secondsFromNow))));
        Assert.Equal(CountdownState.Idle, _session.Snapshot.State);
        Assert.False(_keepAwake.IsAcquired);
    }

    [Fact]
    public void Start_WithAnUnknownAction_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _session.Start(PlanIn(TimeSpan.FromMinutes(1), (PowerAction)99)));
    }

    [Fact]
    public void Start_WithNullPlan_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _session.Start(null!));
    }

    [Fact]
    public void Start_AfterAFinishedCountdown_StartsANewOne()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(10)));
        AdvanceInTicks(TimeSpan.FromSeconds(10));
        _session.Start(PlanIn(TimeSpan.FromSeconds(20)));
        _session.Cancel();

        var plan = PlanIn(TimeSpan.FromMinutes(1), PowerAction.Shutdown);
        _session.Start(plan);

        Assert.Equal(CountdownState.Running, _session.Snapshot.State);
        Assert.Equal(CancellationReason.None, _session.Snapshot.CancellationReason);
        Assert.Same(plan, _session.Snapshot.Plan);
        Assert.True(_keepAwake.IsAcquired);
    }

    [Fact]
    public void Changed_IsRaisedOncePerWholeSecondNotOnEveryTick()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(20)));
        _changedCount = 0;

        AdvanceInTicks(TimeSpan.FromSeconds(3));

        Assert.Equal(3, _changedCount);
        Assert.Equal(17, _session.Snapshot.RemainingWholeSeconds);
    }

    [Fact]
    public void Dispose_DuringACountdown_ReleasesKeepAwakeAndNeverActs()
    {
        _session.Start(PlanIn(TimeSpan.FromSeconds(20)));

        _session.Dispose();
        _session.Dispose();
        AdvanceInTicks(TimeSpan.FromSeconds(30));

        Assert.Equal(CountdownState.Cancelled, _session.Snapshot.State);
        Assert.Empty(_power.Calls);
        Assert.False(_keepAwake.IsAcquired);
        Assert.Throws<ObjectDisposedException>(() => _session.Start(PlanIn(TimeSpan.FromMinutes(1))));
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var logger = NullLogger<CountdownSession>.Instance;

        Assert.Throws<ArgumentNullException>(() => new CountdownSession(null!, _power, _keepAwake, logger));
        Assert.Throws<ArgumentNullException>(() => new CountdownSession(_time, null!, _keepAwake, logger));
        Assert.Throws<ArgumentNullException>(() => new CountdownSession(_time, _power, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new CountdownSession(_time, _power, _keepAwake, null!));
    }

    [Fact]
    public async Task PublicConstructor_RunsThePowerActionOffTheCallingThread()
    {
        using var session = new CountdownSession(_time, _power, _keepAwake, NullLogger<CountdownSession>.Instance);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, _) =>
        {
            if (session.Snapshot.State == CountdownState.Done)
            {
                finished.TrySetResult();
            }
        };
        session.Start(PlanIn(TimeSpan.FromMinutes(1)));

        Assert.True(session.ExecuteNow());
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Single(_power.Calls);
    }

    private static Task RunSynchronously(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private CountdownPlan PlanIn(
        TimeSpan delay,
        PowerAction action = PowerAction.Sleep,
        bool forceCloseApps = false,
        bool warningEnabled = false) =>
        new(_time.GetUtcNow() + delay, action, forceCloseApps, warningEnabled);

    /// <summary>
    /// Moves the fake clock forward one tick at a time, the way the real timer fires.
    /// </summary>
    private void AdvanceInTicks(TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += CountdownSession.TickInterval)
        {
            _time.Advance(CountdownSession.TickInterval);
        }
    }
}
