using System.Globalization;
using System.IO;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Somnus.App.Logging;

/// <summary>
/// The rolling log files of Somnus (SWPA 3.7): one file per day in <c>%APPDATA%\Somnus\logs</c>,
/// seven days kept, timestamps with the UTC offset. The profile folder, which holds the Windows user
/// name, is written as <c>%USERPROFILE%</c>, so the logs carry no personal data.
/// </summary>
internal static class LogFiles
{
    private const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Returns the log folder inside the data folder.</summary>
    /// <param name="dataDirectory">The data folder, normally <c>%APPDATA%\Somnus</c>.</param>
    /// <returns>The log folder.</returns>
    public static string DirectoryIn(string dataDirectory) => Path.Combine(dataDirectory, "logs");

    /// <summary>
    /// Creates the file logger.
    /// </summary>
    /// <param name="logDirectory">Where the files go.</param>
    /// <param name="verbose">Also write debug entries (Debug builds).</param>
    /// <returns>The logger; dispose it on exit to flush the file.</returns>
    public static Serilog.Core.Logger CreateLogger(string logDirectory, bool verbose) =>
        new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
            .WriteTo.File(
                CreateFormatter(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
                Path.Combine(logDirectory, "somnus-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

    /// <summary>Creates the formatter of a log line, with <paramref name="profileDirectory"/> redacted.</summary>
    /// <param name="profileDirectory">The user's profile folder.</param>
    /// <returns>The formatter.</returns>
    internal static ITextFormatter CreateFormatter(string profileDirectory) =>
        new ProfileRedactingFormatter(new MessageTemplateTextFormatter(Template, CultureInfo.InvariantCulture), profileDirectory);
}
