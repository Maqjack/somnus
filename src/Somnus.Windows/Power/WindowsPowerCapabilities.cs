using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Somnus.Core.Power;
using Somnus.Windows.Interop;

namespace Somnus.Windows.Power;

/// <summary>
/// Reads the power capabilities of this PC with <c>GetPwrCapabilities</c> (ADR 0006).
/// </summary>
public sealed partial class WindowsPowerCapabilities : IPowerCapabilities
{
    private readonly ILogger<WindowsPowerCapabilities> _logger;
    private readonly Lock _gate = new();
    private SystemPowerCapabilities? _lastLogged;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">Receives the capabilities read and any failure.</param>
    public WindowsPowerCapabilities(ILogger<WindowsPowerCapabilities> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// If Windows cannot report its capabilities, only shutdown is offered. The capabilities are
    /// logged the first time and whenever they change, not on every query: the main window asks
    /// again each time it is activated.
    /// </remarks>
    public PowerSupport Query()
    {
        if (!NativeMethods.GetPwrCapabilities(out var capabilities))
        {
            var error = Marshal.GetLastPInvokeError();
            Log.QueryFailed(_logger, error, new Win32Exception(error).Message);
            return PowerSupport.ShutdownOnly;
        }

        var support = FromCapabilities(capabilities);
        if (ChangedSinceLastLog(capabilities))
        {
            Log.Queried(
                _logger,
                capabilities.SystemS3 != 0,
                capabilities.SystemS4 != 0,
                capabilities.HiberFilePresent != 0,
                capabilities.AoAc != 0,
                support.CanSleep,
                support.CanHibernate);
        }

        return support;
    }

    /// <summary>
    /// Maps the raw capabilities: sleep needs S3 (Modern Standby PCs have no S3 that an app can
    /// enter); hibernation needs S4 and a hibernation file, which <c>powercfg /hibernate off</c> removes.
    /// </summary>
    internal static PowerSupport FromCapabilities(in SystemPowerCapabilities capabilities) => new(
        CanSleep: capabilities.SystemS3 != 0,
        CanHibernate: capabilities.SystemS4 != 0 && capabilities.HiberFilePresent != 0,
        IsModernStandby: capabilities.AoAc != 0);

    private bool ChangedSinceLastLog(SystemPowerCapabilities capabilities)
    {
        lock (_gate)
        {
            var changed = _lastLogged is not { } last
                || last.SystemS3 != capabilities.SystemS3
                || last.SystemS4 != capabilities.SystemS4
                || last.HiberFilePresent != capabilities.HiberFilePresent
                || last.AoAc != capabilities.AoAc;
            _lastLogged = capabilities;
            return changed;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 300, Level = LogLevel.Information,
            Message = "Power capabilities: S3 {SystemS3}, S4 {SystemS4}, hibernation file {HiberFilePresent}, Modern Standby {ModernStandby} -> sleep {CanSleep}, hibernate {CanHibernate}")]
        public static partial void Queried(ILogger logger, bool systemS3, bool systemS4, bool hiberFilePresent, bool modernStandby, bool canSleep, bool canHibernate);

        [LoggerMessage(EventId = 301, Level = LogLevel.Warning,
            Message = "GetPwrCapabilities failed with error {Error} ({Description}); offering shutdown only")]
        public static partial void QueryFailed(ILogger logger, int error, string description);
    }
}
