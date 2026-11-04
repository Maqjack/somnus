namespace Somnus.Core.Power;

/// <summary>
/// The power action Somnus performs when a countdown reaches zero.
/// </summary>
public enum PowerAction
{
    /// <summary>Put the PC to sleep (hybrid sleep when Windows has it turned on).</summary>
    Sleep,

    /// <summary>Hibernate the PC.</summary>
    Hibernate,

    /// <summary>Shut down and power off the PC.</summary>
    Shutdown,
}
