namespace Somnus.Presentation.Services;

/// <summary>
/// Plays the sound of the last 20 seconds (#59): the lullaby or the user's file. Implemented by the
/// app with the sound card; tests use a fake.
/// </summary>
public interface ILullabyPlayer
{
    /// <summary>Starts the sound from the beginning; it plays once.</summary>
    /// <param name="soundFile">The user's file, or <see langword="null"/> for the lullaby. A file that
    /// cannot be played gives the lullaby instead.</param>
    void Play(string? soundFile);

    /// <summary>Stops the lullaby if it is playing. Safe to call at any time.</summary>
    void Silence();
}
