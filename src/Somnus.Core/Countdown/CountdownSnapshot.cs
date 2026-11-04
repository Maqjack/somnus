namespace Somnus.Core.Countdown;

/// <summary>
/// An immutable view of a <see cref="CountdownSession"/> at one moment.
/// </summary>
/// <param name="State">The state of the session.</param>
/// <param name="Plan">The current or last plan; <see langword="null"/> before the first start.</param>
/// <param name="Remaining">The time left, recomputed on every tick; zero once the action runs.</param>
/// <param name="CancellationReason">Why the countdown was cancelled, if it was.</param>
public sealed record CountdownSnapshot(
    CountdownState State,
    CountdownPlan? Plan,
    TimeSpan Remaining,
    CancellationReason CancellationReason)
{
    /// <summary>Gets the snapshot of a session that has never been started.</summary>
    public static CountdownSnapshot Idle { get; } = new(CountdownState.Idle, null, TimeSpan.Zero, CancellationReason.None);

    /// <summary>Gets a value indicating whether a countdown is running (including the warning window).</summary>
    public bool IsActive => State is CountdownState.Running or CountdownState.Warning;

    /// <summary>
    /// Gets the remaining time in whole seconds, rounded up, as a countdown display shows it:
    /// 00:00:01 until the very last moment.
    /// </summary>
    public long RemainingWholeSeconds => Math.Max(0, (long)Math.Ceiling(Remaining.TotalSeconds));
}
