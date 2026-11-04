using Somnus.Core.Countdown;
using Somnus.Core.Power;

namespace Somnus.Core.Scheduling;

/// <summary>
/// A validated countdown request: which action, when, and whether a shutdown may force apps to close.
/// Create it with <see cref="In"/> or <see cref="At"/>.
/// </summary>
public sealed record CountdownRequest
{
    private CountdownRequest(TimeMode mode, TimeSpan delay, TimeOnly clockTime, PowerAction action, bool forceCloseApps, WeekDays repeat)
    {
        Mode = mode;
        Delay = delay;
        ClockTime = clockTime;
        Action = action;
        ForceCloseApps = forceCloseApps;
        Repeat = repeat;
    }

    /// <summary>Gets how the time was given.</summary>
    public TimeMode Mode { get; }

    /// <summary>Gets the delay for <see cref="TimeMode.In"/>; <see cref="TimeSpan.Zero"/> otherwise.</summary>
    public TimeSpan Delay { get; }

    /// <summary>Gets the clock time for <see cref="TimeMode.At"/>; midnight otherwise.</summary>
    public TimeOnly ClockTime { get; }

    /// <summary>Gets the power action to perform.</summary>
    public PowerAction Action { get; }

    /// <summary>Gets a value indicating whether a shutdown closes apps without asking (rule R-05).</summary>
    public bool ForceCloseApps { get; }

    /// <summary>Gets the days on which an "at" countdown repeats; <see cref="WeekDays.None"/> means it runs once.</summary>
    public WeekDays Repeat { get; }

    /// <summary>
    /// Creates a request that runs <paramref name="action"/> after <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">Between <see cref="CountdownInput.MinimumDelay"/> and <see cref="CountdownInput.MaximumDelay"/>.</param>
    /// <param name="action">The power action.</param>
    /// <param name="forceCloseApps">Force apps to close; allowed only for <see cref="PowerAction.Shutdown"/>.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The delay or the action is out of range.</exception>
    /// <exception cref="ArgumentException">Force-closing apps was asked for an action other than shutdown.</exception>
    public static CountdownRequest In(TimeSpan delay, PowerAction action, bool forceCloseApps = false)
    {
        if (delay < CountdownInput.MinimumDelay || delay > CountdownInput.MaximumDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The delay must be between 10 seconds and 99 hours.");
        }

        Validate(action, forceCloseApps);
        return new CountdownRequest(TimeMode.In, delay, TimeOnly.MinValue, action, forceCloseApps, WeekDays.None);
    }

    /// <summary>
    /// Creates a request that runs <paramref name="action"/> at the next occurrence of <paramref name="clockTime"/>.
    /// </summary>
    /// <param name="clockTime">The local clock time; seconds are dropped.</param>
    /// <param name="action">The power action.</param>
    /// <param name="forceCloseApps">Force apps to close; allowed only for <see cref="PowerAction.Shutdown"/>.</param>
    /// <param name="repeat">The days on which it repeats, if any.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The action or the days are out of range.</exception>
    /// <exception cref="ArgumentException">
    /// Force-closing apps was asked for an action other than shutdown, or for a repeating countdown:
    /// that option is never remembered (rule R-05), so it cannot apply to later runs.
    /// </exception>
    public static CountdownRequest At(TimeOnly clockTime, PowerAction action, bool forceCloseApps = false, WeekDays repeat = WeekDays.None)
    {
        Validate(action, forceCloseApps);
        if ((repeat & ~WeekDays.EveryDay) != WeekDays.None)
        {
            throw new ArgumentOutOfRangeException(nameof(repeat), repeat, "Unknown days of the week.");
        }

        if (forceCloseApps && repeat != WeekDays.None)
        {
            throw new ArgumentException("A repeating countdown cannot force apps to close.", nameof(forceCloseApps));
        }

        return new CountdownRequest(TimeMode.At, TimeSpan.Zero, new TimeOnly(clockTime.Hour, clockTime.Minute), action, forceCloseApps, repeat);
    }

    /// <summary>
    /// Returns the instant at which this request runs when started at <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="timeZone">The user's time zone, used by <see cref="TimeMode.At"/>.</param>
    /// <returns>The target instant.</returns>
    public DateTimeOffset ResolveTarget(DateTimeOffset now, TimeZoneInfo timeZone) => Mode == TimeMode.In
        ? TargetTime.After(now, Delay)
        : TargetTime.NextOccurrence(now, ClockTime, timeZone, Repeat);

    /// <summary>
    /// Creates the plan a <see cref="CountdownSession"/> runs.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="timeZone">The user's time zone.</param>
    /// <param name="warningEnabled">Whether to open the warning window before the action.</param>
    /// <returns>The countdown plan.</returns>
    public CountdownPlan ToPlan(DateTimeOffset now, TimeZoneInfo timeZone, bool warningEnabled) =>
        new(ResolveTarget(now, timeZone), Action, ForceCloseApps, warningEnabled, Repeat != WeekDays.None);

    /// <summary>
    /// Creates the plan of the run that follows <paramref name="previousTarget"/> of a repeating request.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="previousTarget">The target of the run before.</param>
    /// <param name="timeZone">The user's time zone.</param>
    /// <param name="warningEnabled">Whether to open the warning window before the action.</param>
    /// <returns>The countdown plan.</returns>
    /// <exception cref="InvalidOperationException">This request does not repeat.</exception>
    public CountdownPlan ToNextPlan(DateTimeOffset now, DateTimeOffset previousTarget, TimeZoneInfo timeZone, bool warningEnabled)
    {
        if (Repeat == WeekDays.None)
        {
            throw new InvalidOperationException("This countdown does not repeat.");
        }

        return new CountdownPlan(TargetTime.NextRepeat(now, previousTarget, ClockTime, timeZone, Repeat), Action, ForceCloseApps, warningEnabled, Repeats: true);
    }

    private static void Validate(PowerAction action, bool forceCloseApps)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown power action.");
        }

        if (forceCloseApps && action != PowerAction.Shutdown)
        {
            throw new ArgumentException("Only a shutdown can force apps to close.", nameof(forceCloseApps));
        }
    }
}
