using Somnus.Core.Countdown;
using Somnus.Core.Settings;

namespace Somnus.Presentation.Services;

/// <summary>
/// Plays the lullaby during the last <see cref="LeadTime"/> of a countdown when the setting is on
/// (#59), whether or not the warning window is shown, and stops it as soon as the countdown is no
/// longer running: cancelled, "Do it now", a missed deadline or the action itself.
/// </summary>
public sealed class LullabyCoordinator : IDisposable
{
    /// <summary>How long before the action the lullaby starts; the tune lasts as long.</summary>
    public static readonly TimeSpan LeadTime = TimeSpan.FromSeconds(20);

    private readonly CountdownSession _session;
    private readonly SettingsService _settings;
    private readonly ILullabyPlayer _player;
    private readonly IUiDispatcher _dispatcher;
    private bool _playing;

    /// <summary>
    /// Initializes a new instance and starts following the countdown.
    /// </summary>
    /// <param name="session">The countdown.</param>
    /// <param name="settings">Whether the lullaby is on.</param>
    /// <param name="player">Plays the sound.</param>
    /// <param name="dispatcher">Moves countdown updates to the UI thread.</param>
    public LullabyCoordinator(CountdownSession session, SettingsService settings, ILullabyPlayer player, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _session = session;
        _settings = settings;
        _player = player;
        _dispatcher = dispatcher;
        _session.Changed += OnSessionChanged;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        StopPlaying();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => _dispatcher.Post(Sync);

    private void Sync()
    {
        var snapshot = _session.Snapshot;
        var settings = _settings.Current;
        var wanted = snapshot.IsActive && snapshot.Remaining <= LeadTime && settings.Sound != EndSound.None;
        if (wanted && !_playing)
        {
            _player.Play(settings.Sound == EndSound.File ? settings.SoundFile : null);
            _playing = true;
        }
        else if (!wanted && _playing)
        {
            StopPlaying();
        }
    }

    private void StopPlaying()
    {
        if (_playing)
        {
            _player.Silence();
            _playing = false;
        }
    }
}
