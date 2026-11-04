namespace Somnus.Core.Countdown;

/// <summary>
/// The states of a <see cref="CountdownSession"/>.
/// </summary>
public enum CountdownState
{
    /// <summary>No countdown has been started yet.</summary>
    Idle,

    /// <summary>Counting down; the warning window is not open yet.</summary>
    Running,

    /// <summary>Counting down inside the warning window before the action.</summary>
    Warning,

    /// <summary>The power action is being performed.</summary>
    Executing,

    /// <summary>The power action was performed (for sleep and hibernate: the PC has woken up again).</summary>
    Done,

    /// <summary>Windows refused the power action; details are in the log.</summary>
    Failed,

    /// <summary>The countdown was stopped before the action; see <see cref="CancellationReason"/>.</summary>
    Cancelled,
}
