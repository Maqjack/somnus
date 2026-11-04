namespace Somnus.Core.Settings;

/// <summary>
/// What plays during the last 20 seconds before the action (#59; a choice since Mac's feedback of
/// 2026-11-04).
/// </summary>
public enum EndSound
{
    /// <summary>The lullaby that comes with Somnus.</summary>
    Lullaby,

    /// <summary>Nothing.</summary>
    None,

    /// <summary>The first 20 seconds of a sound file the user chose.</summary>
    File,
}
