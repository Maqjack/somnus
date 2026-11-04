namespace Somnus.Core.Settings;

/// <summary>
/// Loads and saves the user's settings.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the settings. Never throws: a missing, unreadable or corrupt file gives defaults
    /// (and a corrupt file is kept as a backup), so Somnus always starts.
    /// </summary>
    /// <returns>The settings to use.</returns>
    AppSettings Load();

    /// <summary>
    /// Saves the settings atomically: readers see either the old or the new file, never a half-written one.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    void Save(AppSettings settings);
}
