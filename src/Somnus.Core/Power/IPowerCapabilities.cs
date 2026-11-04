namespace Somnus.Core.Power;

/// <summary>
/// Tells which power actions this PC can perform right now.
/// </summary>
public interface IPowerCapabilities
{
    /// <summary>
    /// Reads the current capabilities. Call it again when the window is shown, because the user
    /// may have turned hibernation on or off in the meantime.
    /// </summary>
    /// <returns>What this PC supports.</returns>
    PowerSupport Query();
}

/// <summary>
/// Which power actions are available.
/// </summary>
/// <param name="CanSleep">Classic sleep (S3) is available.</param>
/// <param name="CanHibernate">Hibernation is supported and turned on.</param>
/// <param name="IsModernStandby">
/// The PC uses Modern Standby (S0 low power idle), which an app cannot start the way it starts
/// classic sleep.
/// </param>
public sealed record PowerSupport(bool CanSleep, bool CanHibernate, bool IsModernStandby)
{
    /// <summary>Gets support assumed when it cannot be read: everything except shutdown is unavailable.</summary>
    public static PowerSupport ShutdownOnly { get; } = new(CanSleep: false, CanHibernate: false, IsModernStandby: false);

    /// <summary>
    /// Returns whether <paramref name="action"/> can be performed. Shutdown is always available.
    /// </summary>
    /// <param name="action">The power action.</param>
    /// <returns><see langword="true"/> if the action is available.</returns>
    public bool Supports(PowerAction action) => action switch
    {
        PowerAction.Sleep => CanSleep,
        PowerAction.Hibernate => CanHibernate,
        PowerAction.Shutdown => true,
        _ => false,
    };
}
