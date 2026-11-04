namespace Somnus.Core.Countdown;

/// <summary>
/// Why a countdown ended in <see cref="CountdownState.Cancelled"/>.
/// </summary>
public enum CancellationReason
{
    /// <summary>The countdown was not cancelled.</summary>
    None,

    /// <summary>The user cancelled it, or closed Somnus.</summary>
    User,

    /// <summary>
    /// The countdown could not run for a while (the PC was asleep or the clock jumped) and came back
    /// less than a minute before the deadline or after it, so Somnus did not act on whoever just
    /// woke the PC.
    /// </summary>
    DeadlineMissed,
}
