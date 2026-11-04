namespace Somnus.Core.Power;

/// <summary>
/// Decides whether power actions are only simulated (rule R-01).
/// </summary>
public static class DryRunPolicy
{
    /// <summary>The command-line switch that turns on the dry run in Release builds.</summary>
    public const string CommandLineSwitch = "--dry-run";

    /// <summary>
    /// Returns whether power actions must be simulated: always in Debug builds, and in Release
    /// builds when the app was started with <see cref="CommandLineSwitch"/>.
    /// </summary>
    /// <param name="isDebugBuild">Whether this is a Debug build.</param>
    /// <param name="commandLineArguments">The arguments the app was started with.</param>
    /// <returns><see langword="true"/> to use <see cref="DryRunPowerController"/>.</returns>
    public static bool ShouldSimulate(bool isDebugBuild, IEnumerable<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);
        return isDebugBuild
            || commandLineArguments.Any(argument => string.Equals(argument, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));
    }
}
