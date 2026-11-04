using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Somnus.Core.Settings;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;
using Somnus.Presentation.Services;

namespace Somnus.Presentation.ViewModels;

/// <summary>
/// The settings panel: language, the 60-second warning, remembering the last countdown, the sound of
/// the last 20 seconds and starting with Windows. A picked language shows at once as a preview; Save
/// keeps every change, Cancel goes back to the language in effect and discards the rest.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IAutostart _autostart;
    private readonly IDialogService _dialogs;
    private readonly Localizer _localizer;

    /// <summary>
    /// Initializes the window with the current settings.
    /// </summary>
    /// <param name="settings">The settings to edit.</param>
    /// <param name="autostart">Starts Somnus with Windows; Windows keeps that choice, not the settings file.</param>
    /// <param name="dialogs">Opens the file dialog for a sound file.</param>
    /// <param name="localizer">Shows the picked language as a preview.</param>
    public SettingsViewModel(SettingsService settings, IAutostart autostart, IDialogService dialogs, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(localizer);
        _settings = settings;
        _autostart = autostart;
        _dialogs = dialogs;
        _localizer = localizer;

        var current = settings.Current;
        Language = current.Language;
        WarningEnabled = current.WarningEnabled;
        RememberLast = current.RememberLast;
        Sound = current.Sound;
        SoundFile = current.SoundFile;
        SoundOptions = CreateSoundOptions();

        // The saved file may have been moved, deleted or refused by Windows since; the lullaby plays in its place.
        IsSoundFileMissing = current.Sound == EndSound.File && !SoundFiles.IsPlayable(current.SoundFile);
        IsSoundFileBroken = current.Sound == EndSound.File && !IsSoundFileMissing
            && string.Equals(current.FailedSoundFile, current.SoundFile, StringComparison.OrdinalIgnoreCase);
        StartWithWindows = autostart.IsEnabled;
    }

    /// <summary>Raised when the panel should close; the argument tells whether settings were saved.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Gets the languages, each named in its own language.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(AppLanguage.English, "English"),
        new(AppLanguage.Polish, "Polski"),
    ];

    /// <summary>Gets the sounds to choose from, named in the language on screen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SoundOption> SoundOptions { get; private set; }

    /// <summary>Gets or sets the chosen language.</summary>
    [ObservableProperty]
    public partial AppLanguage Language { get; set; }

    /// <summary>Gets or sets a value indicating whether the 60-second warning is on.</summary>
    [ObservableProperty]
    public partial bool WarningEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the last countdown is remembered.</summary>
    [ObservableProperty]
    public partial bool RememberLast { get; set; }

    /// <summary>Gets or sets what plays in the last 20 seconds (#59; a choice since Mac's feedback of 2026-11-04).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoundFileVisible))]
    public partial EndSound Sound { get; set; }

    /// <summary>Gets or sets the sound file, typed or picked in the file dialog.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoundFileToolTip))]
    public partial string SoundFile { get; set; }

    /// <summary>Gets the whole path for the tooltip of the path box, which may cut it off.</summary>
    public string? SoundFileToolTip => string.IsNullOrWhiteSpace(SoundFile) ? null : SoundFile;

    /// <summary>Gets a value indicating whether the row for the sound file shows.</summary>
    public bool IsSoundFileVisible => Sound == EndSound.File;

    /// <summary>Gets a value indicating whether the saved sound file is gone, until another one is chosen.</summary>
    [ObservableProperty]
    public partial bool IsSoundFileMissing { get; private set; }

    /// <summary>Gets a value indicating whether Windows could not play the saved sound file, until another one is chosen.</summary>
    [ObservableProperty]
    public partial bool IsSoundFileBroken { get; private set; }

    /// <summary>Gets or sets a value indicating whether Somnus starts with Windows, hidden in the tray (#83).</summary>
    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    /// <summary>Gets the message shown when saving failed.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [RelayCommand]
    private void BrowseSoundFile()
    {
        var picked = _dialogs.PickFile(new FilePickRequest(
            _localizer[nameof(Strings.Settings_SoundFileDialog)],
            _localizer[nameof(Strings.Settings_SoundFileFilter)],
            SoundFiles.Extensions,
            SoundFile));

        if (picked is not null)
        {
            SoundFile = picked;
        }
    }

    [RelayCommand]
    private void Save()
    {
        // A file that cannot be played keeps the panel open, so the choice is never lost in silence.
        var soundFile = SoundFile.Trim();
        if (Sound == EndSound.File && !SoundFiles.IsPlayable(soundFile))
        {
            ErrorMessage = _localizer[nameof(Strings.Settings_SoundFileInvalid)];
            return;
        }

        var saved = _settings.Update(current => current with
        {
            Language = Language,
            WarningEnabled = WarningEnabled,
            RememberLast = RememberLast,
            Sound = Sound,
            SoundFile = soundFile,
            FailedSoundFile = string.Equals(current.FailedSoundFile, soundFile, StringComparison.OrdinalIgnoreCase) ? current.FailedSoundFile : string.Empty,
        });

        if (!saved)
        {
            ErrorMessage = _localizer[nameof(Strings.Settings_SaveFailed)];
        }
        else if (StartWithWindows != _autostart.IsEnabled && !_autostart.TrySet(StartWithWindows))
        {
            ErrorMessage = _localizer[nameof(Strings.Settings_AutostartFailed)];
        }
        else
        {
            CloseRequested?.Invoke(this, true);
        }
    }

    /// <summary>Closes without saving; the preview goes back to the language in effect.</summary>
    [RelayCommand]
    private void Cancel()
    {
        _localizer.Language = _settings.Current.Language;
        CloseRequested?.Invoke(this, false);
    }

    partial void OnLanguageChanged(AppLanguage value)
    {
        _localizer.Language = value;
        if (SoundOptions is not null)
        {
            // A new list clears the selection of the sound box; announcing the sound again restores it.
            SoundOptions = CreateSoundOptions();
            OnPropertyChanged(nameof(Sound));
        }
    }

    partial void OnSoundFileChanged(string value)
    {
        IsSoundFileMissing = false;
        IsSoundFileBroken = false;
    }

    private List<SoundOption> CreateSoundOptions() =>
    [
        new(EndSound.Lullaby, _localizer[nameof(Strings.Settings_SoundLullaby)]),
        new(EndSound.None, _localizer[nameof(Strings.Settings_SoundNone)]),
        new(EndSound.File, _localizer[nameof(Strings.Settings_SoundOwnFile)]),
    ];
}

/// <summary>
/// A language with its name, for the language drop-down.
/// </summary>
/// <param name="Value">The language.</param>
/// <param name="DisplayName">The name of the language in that language.</param>
public sealed record LanguageOption(AppLanguage Value, string DisplayName);

/// <summary>
/// A sound with its name, for the sound drop-down.
/// </summary>
/// <param name="Value">The sound.</param>
/// <param name="DisplayName">Its name in the language on screen.</param>
public sealed record SoundOption(EndSound Value, string DisplayName);
