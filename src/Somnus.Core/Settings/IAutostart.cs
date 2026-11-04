namespace Somnus.Core.Settings;

/// <summary>
/// Starts Somnus with Windows, only when the user turns it on in the settings (#83, ADR 0011).
/// </summary>
public interface IAutostart
{
    /// <summary>Gets a value indicating whether Windows starts this copy of Somnus at sign-in.</summary>
    bool IsEnabled { get; }

    /// <summary>Turns starting with Windows on or off.</summary>
    /// <param name="enabled">Whether Somnus should start with Windows.</param>
    /// <returns><see langword="true"/> when the change was made.</returns>
    bool TrySet(bool enabled);
}
