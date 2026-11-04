using Somnus.Core.Settings;

namespace Somnus.Windows.Settings;

/// <summary>
/// The language of the download Somnus came from, which it uses until the user picks one (issues #53
/// and #73). The installer writes it to <c>Somnus.ini</c> next to <c>Somnus.exe</c>, and the portable
/// zip carries the same file; a run from source has none.
/// </summary>
public static class InstallerLanguage
{
    /// <summary>The file the installer writes.</summary>
    public const string FileName = "Somnus.ini";

    /// <summary>
    /// Reads the language from <c>[Installer] Language=</c>, which holds the installer's language name
    /// (<c>english</c> or <c>polish</c>).
    /// </summary>
    /// <param name="appDirectory">The folder of <c>Somnus.exe</c>.</param>
    /// <returns>The language, or <see langword="null"/> when the file is missing, unreadable or names another language.</returns>
    public static AppLanguage? Read(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);

        string[] lines;
        try
        {
            lines = File.ReadAllLines(Path.Combine(appDirectory, FileName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var inInstallerSection = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inInstallerSection = line.Equals("[Installer]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (inInstallerSection && parts.Length == 2 && parts[0].Equals("Language", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].ToUpperInvariant() switch
                {
                    "ENGLISH" => AppLanguage.English,
                    "POLISH" => AppLanguage.Polish,
                    _ => null,
                };
            }
        }

        return null;
    }
}
