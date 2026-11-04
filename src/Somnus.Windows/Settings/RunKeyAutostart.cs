using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Somnus.Core.Settings;

namespace Somnus.Windows.Settings;

/// <summary>
/// Starts Somnus with Windows through a value in the user's Run key, where Windows and Task Manager
/// list startup apps (#83). It affects only the current user and needs no administrator.
/// </summary>
public sealed partial class RunKeyAutostart : IAutostart
{
    /// <summary>The argument that tells Somnus it started with Windows, so it stays in the tray.</summary>
    public const string Argument = "--autostart";

    /// <summary>The name of the value; the uninstaller removes the same name.</summary>
    public const string ValueName = "Somnus";

    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string StartupApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _command;
    private readonly string _runKey;
    private readonly string _startupApprovedKey;
    private readonly ILogger<RunKeyAutostart> _logger;

    /// <summary>
    /// Initializes a new instance for the Somnus at <paramref name="executablePath"/>.
    /// </summary>
    /// <param name="executablePath">The full path of <c>Somnus.exe</c>.</param>
    /// <param name="logger">Records changes and failures.</param>
    public RunKeyAutostart(string executablePath, ILogger<RunKeyAutostart> logger)
        : this(executablePath, logger, RunKey, StartupApprovedKey)
    {
    }

    /// <summary>Initializes a new instance with other keys under HKEY_CURRENT_USER, for tests.</summary>
    internal RunKeyAutostart(string executablePath, ILogger<RunKeyAutostart> logger, string runKey, string startupApprovedKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentNullException.ThrowIfNull(logger);
        _command = $"\"{executablePath}\" {Argument}";
        _runKey = runKey;
        _startupApprovedKey = startupApprovedKey;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A value that starts another copy of Somnus does not count, and neither does one that Task
    /// Manager turned off: it keeps the value and sets an odd first byte under StartupApproved.
    /// </remarks>
    public bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(_runKey);
            if (run?.GetValue(ValueName) is not string command || !string.Equals(command, _command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(_startupApprovedKey);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
        }
    }

    /// <inheritdoc />
    public bool TrySet(bool enabled)
    {
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(_runKey, writable: true))
            {
                if (enabled)
                {
                    run.SetValue(ValueName, _command, RegistryValueKind.String);
                }
                else
                {
                    run.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }

            // The choice made in Somnus replaces an earlier "Disabled" from Task Manager.
            using (var approved = Registry.CurrentUser.OpenSubKey(_startupApprovedKey, writable: true))
            {
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            Log.Changed(_logger, enabled);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            Log.Failed(_logger, enabled, exception);
            return false;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 340, Level = LogLevel.Information, Message = "Start with Windows is now {Enabled}")]
        public static partial void Changed(ILogger logger, bool enabled);

        [LoggerMessage(EventId = 341, Level = LogLevel.Warning, Message = "Could not set start with Windows to {Enabled}")]
        public static partial void Failed(ILogger logger, bool enabled, Exception exception);
    }
}
