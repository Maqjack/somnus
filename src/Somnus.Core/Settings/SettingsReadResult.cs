namespace Somnus.Core.Settings;

/// <summary>
/// The outcome of reading settings.
/// </summary>
/// <param name="Settings">The settings to use; defaults where values were missing or invalid.</param>
/// <param name="Problems">One English message per value that had to be replaced, for the log.</param>
/// <param name="IsCorrupt">
/// <see langword="true"/> when the content could not be read at all, so the store should keep
/// a copy of it instead of overwriting it.
/// </param>
public sealed record SettingsReadResult(AppSettings Settings, IReadOnlyList<string> Problems, bool IsCorrupt)
{
    /// <summary>
    /// Creates the result for content that cannot be read at all.
    /// </summary>
    /// <param name="problem">What is wrong with the content.</param>
    /// <returns>Default settings marked as corrupt.</returns>
    public static SettingsReadResult Corrupt(string problem) => new(AppSettings.Default, [problem], IsCorrupt: true);
}
