using Microsoft.Extensions.Logging;

namespace Somnus.Core.Power;

/// <summary>
/// Simulates power actions: it logs them and raises <see cref="Simulated"/> so the app can show a
/// notification, but never changes the power state (rule R-01). Used by Debug builds and by any
/// run with <c>--dry-run</c>.
/// </summary>
public sealed partial class DryRunPowerController : IPowerController
{
    private readonly ILogger<DryRunPowerController> _logger;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">Receives one entry per simulated action.</param>
    public DryRunPowerController(ILogger<DryRunPowerController> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Raised for every simulated action.</summary>
    public event EventHandler<SimulatedPowerActionEventArgs>? Simulated;

    /// <inheritdoc />
    public void Sleep() => Simulate(PowerAction.Sleep, forceCloseApps: false);

    /// <inheritdoc />
    public void Hibernate() => Simulate(PowerAction.Hibernate, forceCloseApps: false);

    /// <inheritdoc />
    public void Shutdown(bool forceCloseApps) => Simulate(PowerAction.Shutdown, forceCloseApps);

    private void Simulate(PowerAction action, bool forceCloseApps)
    {
        Log.Simulated(_logger, action, forceCloseApps);
        Simulated?.Invoke(this, new SimulatedPowerActionEventArgs(action, forceCloseApps));
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 200, Level = LogLevel.Information,
            Message = "Dry run: {Action} simulated (force close apps: {ForceCloseApps}); the power state is unchanged")]
        public static partial void Simulated(ILogger logger, PowerAction action, bool forceCloseApps);
    }
}

/// <summary>
/// Describes a power action that <see cref="DryRunPowerController"/> simulated.
/// </summary>
public sealed class SimulatedPowerActionEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="action">The simulated action.</param>
    /// <param name="forceCloseApps">Whether a forced shutdown was simulated.</param>
    public SimulatedPowerActionEventArgs(PowerAction action, bool forceCloseApps)
    {
        Action = action;
        ForceCloseApps = forceCloseApps;
    }

    /// <summary>Gets the simulated action.</summary>
    public PowerAction Action { get; }

    /// <summary>Gets a value indicating whether a forced shutdown was simulated.</summary>
    public bool ForceCloseApps { get; }
}
