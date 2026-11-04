using System.Text;
using Microsoft.Extensions.Logging;
using Somnus.Core.Settings;

namespace Somnus.Windows.Settings;

/// <summary>
/// Stores settings as JSON in <c>settings.json</c> inside a folder, normally <c>%APPDATA%\Somnus</c>.
/// </summary>
/// <remarks>
/// Writes go to a temporary file that is flushed to disk and then replaces the real one, so a crash
/// or power loss leaves either the old or the new settings. A file that cannot be read at all is
/// renamed to <c>settings.json.bak</c> before defaults are used, so nothing the user had is
/// silently destroyed.
/// </remarks>
public sealed partial class JsonFileSettingsStore : ISettingsStore
{
    /// <summary>The name of the settings file.</summary>
    public const string FileName = "settings.json";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly ILogger<JsonFileSettingsStore> _logger;
    private readonly AppSettings _firstStart;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="directory">The folder that holds the settings file; created on first save.</param>
    /// <param name="logger">Receives problems found while loading or saving.</param>
    /// <param name="firstStart">
    /// The settings while there is no settings file yet, for example with the installer's language;
    /// <see cref="AppSettings.Default"/> if not given.
    /// </param>
    public JsonFileSettingsStore(string directory, ILogger<JsonFileSettingsStore> logger, AppSettings? firstStart = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);
        _directory = directory;
        _logger = logger;
        _firstStart = firstStart ?? AppSettings.Default;
    }

    /// <summary>Gets the full path of the settings file.</summary>
    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>Gets the full path of the backup kept when the settings file is unreadable.</summary>
    public string BackupPath => FilePath + ".bak";

    /// <summary>
    /// Returns the folder Somnus uses for settings and logs: <c>%APPDATA%\Somnus</c>.
    /// </summary>
    /// <returns>The full path of the folder.</returns>
    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Somnus");

    /// <inheritdoc />
    public AppSettings Load()
    {
        string json;
        try
        {
            json = File.ReadAllText(FilePath, Utf8WithoutBom);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            Log.NoFile(_logger, FilePath, _firstStart.Language);
            return _firstStart;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.ReadFailed(_logger, FilePath, exception);
            return AppSettings.Default;
        }

        var result = SettingsSerializer.Deserialize(json);
        foreach (var problem in result.Problems)
        {
            Log.Problem(_logger, problem);
        }

        if (result.IsCorrupt)
        {
            KeepCorruptFile();
        }

        return result.Settings;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_directory);
        var temporaryPath = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(Utf8WithoutBom.GetBytes(SettingsSerializer.Serialize(settings)));

                // Without this, a power cut right after the rename can leave an empty settings.json.
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void KeepCorruptFile()
    {
        try
        {
            File.Move(FilePath, BackupPath, overwrite: true);
            Log.CorruptFileKept(_logger, BackupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.BackupFailed(_logger, BackupPath, exception);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 330, Level = LogLevel.Information, Message = "No settings file at {Path}; first start with defaults in {Language}")]
        public static partial void NoFile(ILogger logger, string path, AppLanguage language);

        [LoggerMessage(EventId = 331, Level = LogLevel.Warning, Message = "Could not read {Path}; using defaults")]
        public static partial void ReadFailed(ILogger logger, string path, Exception exception);

        [LoggerMessage(EventId = 332, Level = LogLevel.Warning, Message = "Settings: {Problem}")]
        public static partial void Problem(ILogger logger, string problem);

        [LoggerMessage(EventId = 333, Level = LogLevel.Warning, Message = "The settings file was unreadable; it was kept as {BackupPath} and defaults are used")]
        public static partial void CorruptFileKept(ILogger logger, string backupPath);

        [LoggerMessage(EventId = 334, Level = LogLevel.Warning, Message = "The unreadable settings file could not be kept as {BackupPath}")]
        public static partial void BackupFailed(ILogger logger, string backupPath, Exception exception);
    }
}
