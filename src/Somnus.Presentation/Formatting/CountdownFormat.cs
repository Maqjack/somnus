using System.Globalization;
using Somnus.Core.Countdown;
using Somnus.Core.Power;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Resources;

namespace Somnus.Presentation.Formatting;

/// <summary>
/// Formats countdown values the same way in the window, the warning dialog and the tray.
/// </summary>
public static class CountdownFormat
{
    /// <summary>The 24-hour clock format used in both languages (assumption A2).</summary>
    public const string ClockFormat = "HH:mm";

    /// <summary>
    /// Formats seconds as <c>HH:MM:SS</c>; hours go up to 99.
    /// </summary>
    /// <param name="wholeSeconds">The remaining time in whole seconds.</param>
    /// <returns>For example <c>01:23:45</c>.</returns>
    public static string Remaining(long wholeSeconds)
    {
        var seconds = Math.Max(0, wholeSeconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}");
    }

    /// <summary>
    /// Formats seconds as <c>MM:SS</c>, for the warning window that never shows more than a minute.
    /// </summary>
    /// <param name="wholeSeconds">The remaining time in whole seconds.</param>
    /// <returns>For example <c>00:45</c>.</returns>
    public static string RemainingShort(long wholeSeconds)
    {
        var seconds = Math.Max(0, wholeSeconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    /// <summary>
    /// Formats the local date and time of <paramref name="instant"/> as two strings.
    /// </summary>
    /// <param name="localizer">Supplies the date format and culture.</param>
    /// <param name="instant">The instant to show.</param>
    /// <param name="timeZone">The user's time zone.</param>
    /// <returns>The date, for example <c>Thu, 22 Oct</c>, and the time, for example <c>23:30</c>.</returns>
    public static (string Date, string Time) LocalDateAndTime(Localizer localizer, DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(timeZone);

        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return (
            local.ToString(localizer[nameof(Strings.Format_Date)], localizer.Culture),
            local.ToString(ClockFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Describes when the action runs, for example <c>Sleep on Thu, 22 Oct at 23:30</c>.
    /// </summary>
    /// <param name="localizer">Supplies the texts.</param>
    /// <param name="action">The power action.</param>
    /// <param name="target">The instant of the action.</param>
    /// <param name="timeZone">The user's time zone.</param>
    /// <returns>The description.</returns>
    public static string Target(Localizer localizer, PowerAction action, DateTimeOffset target, TimeZoneInfo timeZone)
    {
        var (date, time) = LocalDateAndTime(localizer, target, timeZone);
        var key = action switch
        {
            PowerAction.Sleep => nameof(Strings.Target_Sleep),
            PowerAction.Hibernate => nameof(Strings.Target_Hibernate),
            _ => nameof(Strings.Target_Shutdown),
        };
        return localizer.Format(key, date, time);
    }

    /// <summary>
    /// Returns the key of the message for a countdown that ended without its action (a missed
    /// deadline or an action Windows refused), shared by the main window and the tray.
    /// </summary>
    /// <param name="snapshot">The state of the countdown.</param>
    /// <returns>A key of <see cref="Strings"/>, or <see langword="null"/> for any other state.</returns>
    public static string? OutcomeMessageKey(CountdownSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot switch
        {
            // A schedule simply goes on with the next run; nothing needs the user's attention.
            { State: CountdownState.Cancelled, CancellationReason: CancellationReason.DeadlineMissed, Plan.Repeats: true } => null,
            { State: CountdownState.Cancelled, CancellationReason: CancellationReason.DeadlineMissed } => nameof(Strings.Status_DeadlineMissed),
            { State: CountdownState.Failed, Plan.Action: PowerAction.Sleep } => nameof(Strings.Status_FailedSleep),
            { State: CountdownState.Failed, Plan.Action: PowerAction.Hibernate } => nameof(Strings.Status_FailedHibernate),
            { State: CountdownState.Failed } => nameof(Strings.Status_FailedShutdown),
            _ => null,
        };
    }

    /// <summary>
    /// Returns the display name of <paramref name="action"/>, for example <c>Shut down</c>.
    /// </summary>
    /// <param name="localizer">Supplies the texts.</param>
    /// <param name="action">The power action.</param>
    /// <returns>The localized name.</returns>
    public static string ActionName(Localizer localizer, PowerAction action)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return localizer[action switch
        {
            PowerAction.Sleep => nameof(Strings.Action_Sleep),
            PowerAction.Hibernate => nameof(Strings.Action_Hibernate),
            _ => nameof(Strings.Action_Shutdown),
        }];
    }
}
