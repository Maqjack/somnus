using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Somnus.Core.Power;
using Somnus.Windows.Interop;

namespace Somnus.Windows.Power;

/// <summary>
/// Keeps Windows from idle sleep with a system-required power request (ADR 0007). The request is
/// tied to a handle rather than to a thread, so it can be released from any thread, and
/// <c>powercfg /requests</c> shows it with the reason "Somnus countdown in progress".
/// </summary>
public sealed partial class WindowsKeepAwake : IKeepAwake, IDisposable
{
    /// <summary>The reason shown by <c>powercfg /requests</c>.</summary>
    public const string Reason = "Somnus countdown in progress";

    private readonly Lock _gate = new();
    private readonly ILogger<WindowsKeepAwake> _logger;
    private SafePowerRequestHandle? _request;
    private nint _reasonText;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">Receives acquire, release and failures.</param>
    public WindowsKeepAwake(ILogger<WindowsKeepAwake> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Gets a value indicating whether the power request is active.</summary>
    public bool IsAcquired
    {
        get
        {
            lock (_gate)
            {
                return _request is not null;
            }
        }
    }

    /// <inheritdoc />
    public void Acquire()
    {
        lock (_gate)
        {
            if (_request is not null)
            {
                return;
            }

            var reasonText = Marshal.StringToHGlobalUni(Reason);
            var context = new ReasonContext
            {
                Version = NativeMethods.PowerRequestContextVersion,
                Flags = NativeMethods.PowerRequestContextSimpleString,
                SimpleReasonString = reasonText,
            };

            var request = NativeMethods.PowerCreateRequest(in context);
            if (request.IsInvalid)
            {
                LogFailure(nameof(NativeMethods.PowerCreateRequest));
                request.Dispose();
                Marshal.FreeHGlobal(reasonText);
                return;
            }

            if (!NativeMethods.PowerSetRequest(request, PowerRequestType.SystemRequired))
            {
                LogFailure(nameof(NativeMethods.PowerSetRequest));
                request.Dispose();
                Marshal.FreeHGlobal(reasonText);
                return;
            }

            _request = request;
            _reasonText = reasonText;
        }

        Log.Acquired(_logger);
    }

    /// <inheritdoc />
    public void Release()
    {
        lock (_gate)
        {
            if (_request is null)
            {
                return;
            }

            if (!NativeMethods.PowerClearRequest(_request, PowerRequestType.SystemRequired))
            {
                LogFailure(nameof(NativeMethods.PowerClearRequest));
            }

            // Closing the handle ends the request even if clearing it failed.
            _request.Dispose();
            _request = null;
            Marshal.FreeHGlobal(_reasonText);
            _reasonText = 0;
        }

        Log.Released(_logger);
    }

    /// <summary>Releases the power request if it is still held.</summary>
    public void Dispose() => Release();

    private void LogFailure(string function)
    {
        var error = Marshal.GetLastPInvokeError();
        Log.Failed(_logger, function, error, new Win32Exception(error).Message);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 320, Level = LogLevel.Information, Message = "Keep-awake acquired: the PC will not idle-sleep during the countdown")]
        public static partial void Acquired(ILogger logger);

        [LoggerMessage(EventId = 321, Level = LogLevel.Information, Message = "Keep-awake released")]
        public static partial void Released(ILogger logger);

        [LoggerMessage(EventId = 322, Level = LogLevel.Warning,
            Message = "{Function} failed with error {Error} ({Description}); the countdown continues, but idle sleep may interrupt it")]
        public static partial void Failed(ILogger logger, string function, int error, string description);
    }
}
