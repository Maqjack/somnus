namespace Somnus.Core.Settings;

/// <summary>
/// The sound files Somnus can play instead of the lullaby: files on this PC in a format that the
/// Windows media player reads.
/// </summary>
public static class SoundFiles
{
    /// <summary>Gets the extensions offered in the file dialog and accepted when typed.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac"];

    /// <summary>Tells whether <paramref name="path"/> names a sound file on this PC that Somnus can play.</summary>
    /// <remarks>
    /// Only a full local path counts: a web address or a network share would make Somnus use the
    /// network (rule R-07).
    /// </remarks>
    /// <param name="path">The path the user typed or picked.</param>
    /// <returns><see langword="true"/> when the file exists and has a supported extension.</returns>
    public static bool IsPlayable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains("://", StringComparison.Ordinal)
            || path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase) && File.Exists(path);
    }
}
