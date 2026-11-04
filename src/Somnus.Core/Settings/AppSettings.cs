using Somnus.Core.Power;
using Somnus.Core.Scheduling;

namespace Somnus.Core.Settings;

/// <summary>
/// The user's settings and, when <see cref="RememberLast"/> is on, the last countdown they started.
/// </summary>
/// <remarks>
/// There is deliberately no field for "force close apps": it is never remembered (rule R-05).
/// </remarks>
public sealed record AppSettings
{
    /// <summary>Gets the settings used on first start and in place of invalid values.</summary>
    public static AppSettings Default { get; } = new();

    /// <summary>Gets the interface language. Default: English.</summary>
    public AppLanguage Language { get; init; } = AppLanguage.English;

    /// <summary>Gets a value indicating whether the 60-second warning opens before the action. Default: on.</summary>
    public bool WarningEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether the last countdown values are restored on start. Default: on.</summary>
    public bool RememberLast { get; init; } = true;

    /// <summary>
    /// Gets what plays during the last 20 seconds of a countdown (#59). Default for a new install: the
    /// lullaby (Mac, 2026-11-04); a settings file from before this choice keeps its own setting.
    /// </summary>
    public EndSound Sound { get; init; } = EndSound.Lullaby;

    /// <summary>Gets the sound file that plays when <see cref="Sound"/> is <see cref="EndSound.File"/>.</summary>
    public string SoundFile { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sound file that Windows could not play last time, so the settings panel can say so
    /// (#113); empty when the last file played.
    /// </summary>
    public string FailedSoundFile { get; init; } = string.Empty;

    /// <summary>Gets the last time mode. Default: <see cref="TimeMode.In"/>.</summary>
    public TimeMode LastMode { get; init; } = TimeMode.In;

    /// <summary>Gets the last amount of the "in" mode. Default: 30.</summary>
    public int LastAmount { get; init; } = 30;

    /// <summary>Gets the last unit of the "in" mode. Default: minutes.</summary>
    public TimeUnit LastUnit { get; init; } = TimeUnit.Minutes;

    /// <summary>Gets the last power action. Default: sleep.</summary>
    public PowerAction LastAction { get; init; } = PowerAction.Sleep;

    /// <summary>Gets the last clock time of the "at" mode. Default: 23:00.</summary>
    public TimeOnly LastAtTime { get; init; } = new(23, 0);

    /// <summary>Gets the last days of the week of the "at" mode. Default: none.</summary>
    public WeekDays LastRepeat { get; init; }

    /// <summary>
    /// Gets the days of the running schedule (#83), which survives a restart of Somnus. Default: none,
    /// which means no schedule; it does not depend on <see cref="RememberLast"/>.
    /// </summary>
    public WeekDays ScheduleDays { get; init; }

    /// <summary>Gets the clock time of the running schedule.</summary>
    public TimeOnly ScheduleTime { get; init; } = new(23, 0);

    /// <summary>Gets the power action of the running schedule.</summary>
    public PowerAction ScheduleAction { get; init; } = PowerAction.Sleep;
}
