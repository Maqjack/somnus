using Microsoft.Extensions.Logging;
using Somnus.Core.Power;
using Somnus.Windows.Interop;

namespace Somnus.Windows.Power;

/// <summary>
/// Performs real power actions (ADR 0006). Only the composition root may create it (rule R-01);
/// tests never use it.
/// </summary>
public sealed partial class WindowsPowerController : IPowerController
{
    private readonly ILogger<WindowsPowerController> _logger;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">Receives every action and failure.</param>
    public WindowsPowerController(ILogger<WindowsPowerController> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void Sleep() => Suspend(hibernate: false);

    /// <inheritdoc />
    public void Hibernate() => Suspend(hibernate: true);

    /// <inheritdoc />
    /// <remarks>
    /// A normal shutdown uses <c>EWX_FORCEIFHUNG</c>: hung apps are closed, but an app that asks
    /// about unsaved work can still stop the shutdown. A forced one uses <c>EWX_FORCE</c> and closes
    /// every app without asking (assumption A3).
    /// </remarks>
    public void Shutdown(bool forceCloseApps)
    {
        ShutdownPreparation.EnablePrivilege();

        Log.ShuttingDown(_logger, forceCloseApps);
        if (!NativeMethods.ExitWindowsEx(ShutdownPreparation.Flags(forceCloseApps), NativeMethods.ShutdownReasonFlagPlanned))
        {
            throw ShutdownPreparation.Failure(nameof(NativeMethods.ExitWindowsEx));
        }
    }

    private void Suspend(bool hibernate)
    {
        Log.Suspending(_logger, hibernate);

        // Returns after the PC wakes up again. bForce is ignored by current Windows versions.
        if (!NativeMethods.SetSuspendState(hibernate, force: false, wakeupEventsDisabled: false))
        {
            throw ShutdownPreparation.Failure(nameof(NativeMethods.SetSuspendState));
        }

        Log.Resumed(_logger);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 310, Level = LogLevel.Information, Message = "Calling SetSuspendState (hibernate: {Hibernate})")]
        public static partial void Suspending(ILogger logger, bool hibernate);

        [LoggerMessage(EventId = 311, Level = LogLevel.Information, Message = "The PC has resumed")]
        public static partial void Resumed(ILogger logger);

        [LoggerMessage(EventId = 312, Level = LogLevel.Information, Message = "Calling ExitWindowsEx (force close apps: {ForceCloseApps})")]
        public static partial void ShuttingDown(ILogger logger, bool forceCloseApps);
    }
}
