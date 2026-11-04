using System.IO;
using System.Media;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Somnus.Core.Settings;
using Somnus.Presentation.Services;

namespace Somnus.App.Services;

/// <summary>
/// Plays the sound of the last 20 seconds through the default sound device: the lullaby, a 20-second
/// recording embedded in the app (#59, #71), or the user's sound file through the Windows media
/// player (Mac's feedback of 2026-11-04). A file that is gone or cannot be played gives the lullaby
/// instead; a PC without sound simply stays silent.
/// </summary>
internal sealed partial class SoundPlayerLullaby : ILullabyPlayer, IDisposable
{
    /// <summary>The manifest resource name of the recording, a 16-bit PCM WAV file.</summary>
    internal const string ResourceName = "Somnus.App.Assets.lullaby.wav";

    private readonly ILogger<SoundPlayerLullaby> _logger;
    private readonly SettingsService _settings;
    private readonly Lazy<SoundPlayer> _player = new(() => new SoundPlayer(OpenRecording()));
    private MediaPlayer? _media;
    private string? _file;
    private bool _wanted;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="logger">Receives playback failures.</param>
    /// <param name="settings">Remembers a file Windows could not play, so the settings panel can say so (#113).</param>
    public SoundPlayerLullaby(ILogger<SoundPlayerLullaby> logger, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger;
        _settings = settings;
    }

    /// <inheritdoc />
    public void Play(string? soundFile)
    {
        _wanted = true;
        if (soundFile is null)
        {
            PlayLullaby();
        }
        else if (!SoundFiles.IsPlayable(soundFile))
        {
            Log.FileUnavailable(_logger, soundFile);
            PlayLullaby();
        }
        else
        {
            // The countdown stops the sound after its last 20 seconds, so only the start of the file plays.
            _file = soundFile;
            _media ??= CreateMediaPlayer();
            _media.Open(ToFileUri(soundFile));
            _media.Play();
            Log.PlayingFile(_logger, soundFile);
        }
    }

    /// <inheritdoc />
    public void Silence()
    {
        _wanted = false;
        if (_player.IsValueCreated)
        {
            _player.Value.Stop();
        }

        _media?.Stop();
        _media?.Close();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _media?.Close();
        if (_player.IsValueCreated)
        {
            _player.Value.Stop();
            _player.Value.Stream?.Dispose();
            _player.Value.Dispose();
        }
    }

    /// <summary>Opens the embedded recording.</summary>
    /// <returns>The complete WAV file, header included.</returns>
    /// <exception cref="InvalidOperationException">The recording is missing from the build.</exception>
    internal static Stream OpenRecording() =>
        typeof(SoundPlayerLullaby).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"The lullaby recording {ResourceName} is missing.");

    /// <summary>Turns a full path into a file address; a # or a % in a folder name stays part of the path.</summary>
    /// <param name="path">A full local path.</param>
    /// <returns>The file address for the media player.</returns>
    internal static Uri ToFileUri(string path) => new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = path }.Uri;

    /// <summary>Remembers that Windows could not play <paramref name="file"/>; the settings panel says so.</summary>
    /// <param name="file">The sound file.</param>
    internal void RecordFailure(string file) =>
        _settings.Update(current => current with { FailedSoundFile = file });

    /// <summary>Forgets an earlier failure of <paramref name="file"/>, because it opened this time.</summary>
    /// <param name="file">The sound file.</param>
    internal void RecordSuccess(string file)
    {
        if (string.Equals(_settings.Current.FailedSoundFile, file, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Update(current => current with { FailedSoundFile = string.Empty });
        }
    }

    private MediaPlayer CreateMediaPlayer()
    {
        var media = new MediaPlayer { Volume = 1 };
        media.MediaOpened += (_, _) =>
        {
            if (_file is not null)
            {
                RecordSuccess(_file);
            }
        };

        // An unknown format, a missing codec or a damaged file shows up only now; the lullaby still plays if it is wanted.
        media.MediaFailed += (_, e) =>
        {
            Log.FileFailed(_logger, e.ErrorException);
            media.Close();
            if (_file is not null)
            {
                RecordFailure(_file);
            }

            if (_wanted)
            {
                PlayLullaby();
            }
        };
        return media;
    }

    private void PlayLullaby()
    {
        try
        {
            _player.Value.Play();
            Log.Playing(_logger);
        }
        catch (InvalidOperationException exception)
        {
            Log.PlayFailed(_logger, exception);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 600, Level = LogLevel.Information, Message = "Lullaby playing for the last seconds of the countdown")]
        public static partial void Playing(ILogger logger);

        [LoggerMessage(EventId = 601, Level = LogLevel.Warning, Message = "The lullaby could not be played")]
        public static partial void PlayFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 602, Level = LogLevel.Information, Message = "Sound file {Path} playing for the last seconds of the countdown")]
        public static partial void PlayingFile(ILogger logger, string path);

        [LoggerMessage(EventId = 603, Level = LogLevel.Warning, Message = "Sound file {Path} is gone or not a supported format; the lullaby plays instead")]
        public static partial void FileUnavailable(ILogger logger, string path);

        [LoggerMessage(EventId = 604, Level = LogLevel.Warning, Message = "The sound file could not be played; the lullaby plays instead")]
        public static partial void FileFailed(ILogger logger, Exception exception);
    }
}
