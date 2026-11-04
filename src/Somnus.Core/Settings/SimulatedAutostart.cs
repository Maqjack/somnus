namespace Somnus.Core.Settings;

/// <summary>
/// Autostart for a dry run: it keeps the choice in memory and never touches Windows, so checking
/// Somnus on a real PC never adds it to the startup apps (SWPA: no autostart without consent).
/// </summary>
public sealed class SimulatedAutostart : IAutostart
{
    /// <inheritdoc />
    public bool IsEnabled { get; private set; }

    /// <inheritdoc />
    public bool TrySet(bool enabled)
    {
        IsEnabled = enabled;
        return true;
    }
}
