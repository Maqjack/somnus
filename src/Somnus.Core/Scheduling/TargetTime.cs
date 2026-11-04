namespace Somnus.Core.Scheduling;

/// <summary>
/// Turns what the user chose into the absolute instant at which the action runs.
/// </summary>
public static class TargetTime
{
    private const int DaysInWeek = 7;

    /// <summary>
    /// Returns the instant <paramref name="delay"/> after <paramref name="now"/> ("in" mode).
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="delay">How long to wait.</param>
    /// <returns>The target instant.</returns>
    public static DateTimeOffset After(DateTimeOffset now, TimeSpan delay) => now + delay;

    /// <summary>
    /// Returns the next instant after <paramref name="now"/> at which the local clock of
    /// <paramref name="timeZone"/> shows <paramref name="clockTime"/> ("at" mode): today if
    /// that time is still ahead, otherwise tomorrow.
    /// </summary>
    /// <remarks>
    /// Daylight saving time: a time skipped when clocks jump forward (02:30 in a 02:00→03:00 gap)
    /// moves forward by the length of the gap (03:30). A time that happens twice when clocks go
    /// back resolves to whichever occurrence comes first after <paramref name="now"/>.
    /// </remarks>
    /// <param name="now">The current instant.</param>
    /// <param name="clockTime">The local clock time; seconds are ignored.</param>
    /// <param name="timeZone">The time zone whose clock the user reads.</param>
    /// <returns>The target instant, always later than <paramref name="now"/>.</returns>
    public static DateTimeOffset NextOccurrence(DateTimeOffset now, TimeOnly clockTime, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var wallClock = new TimeOnly(clockTime.Hour, clockTime.Minute);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);

        // Today and tomorrow always suffice; the third day covers zones that once skipped a whole date.
        return Enumerable.Range(0, 3)
            .SelectMany(days => InstantsShowing(today.AddDays(days).ToDateTime(wallClock), timeZone))
            .First(instant => instant > now);
    }

    /// <summary>
    /// Returns the next instant after <paramref name="now"/> at which the local clock shows
    /// <paramref name="clockTime"/> on one of <paramref name="days"/> (#83). Daylight saving time works
    /// as in <see cref="NextOccurrence(DateTimeOffset, TimeOnly, TimeZoneInfo)"/>.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="clockTime">The local clock time; seconds are ignored.</param>
    /// <param name="timeZone">The time zone whose clock the user reads.</param>
    /// <param name="days">The chosen days; <see cref="WeekDays.None"/> means any day.</param>
    /// <returns>The target instant, always later than <paramref name="now"/>.</returns>
    public static DateTimeOffset NextOccurrence(DateTimeOffset now, TimeOnly clockTime, TimeZoneInfo timeZone, WeekDays days)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        return days == WeekDays.None
            ? NextOccurrence(now, clockTime, timeZone)
            : FirstOnDays(LocalDate(now, timeZone), now, clockTime, timeZone, days);
    }

    /// <summary>
    /// Returns the run that follows <paramref name="previous"/>: the first one after <paramref name="now"/>
    /// on a later date, so the night clocks go back, when a time happens twice, runs only once.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="previous">The target of the run before.</param>
    /// <param name="clockTime">The local clock time; seconds are ignored.</param>
    /// <param name="timeZone">The time zone whose clock the user reads.</param>
    /// <param name="days">The chosen days; at least one.</param>
    /// <returns>The target instant, always later than <paramref name="now"/>.</returns>
    public static DateTimeOffset NextRepeat(DateTimeOffset now, DateTimeOffset previous, TimeOnly clockTime, TimeZoneInfo timeZone, WeekDays days)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentOutOfRangeException.ThrowIfEqual(days, WeekDays.None);

        var dayAfterPrevious = LocalDate(previous, timeZone).AddDays(1);
        var today = LocalDate(now, timeZone);
        return FirstOnDays(today > dayAfterPrevious ? today : dayAfterPrevious, now, clockTime, timeZone, days);
    }

    private static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    private static DateTimeOffset FirstOnDays(DateOnly firstDate, DateTimeOffset now, TimeOnly clockTime, TimeZoneInfo timeZone, WeekDays days)
    {
        var wallClock = new TimeOnly(clockTime.Hour, clockTime.Minute);

        // A chosen day comes back within a week; one more covers zones that once skipped a whole date.
        return Enumerable.Range(0, DaysInWeek + 2)
            .Select(firstDate.AddDays)
            .Where(date => days.Includes(date.DayOfWeek))
            .SelectMany(date => InstantsShowing(date.ToDateTime(wallClock), timeZone))
            .First(instant => instant > now);
    }

    /// <summary>
    /// Returns every instant at which the local clock shows <paramref name="localTime"/>, earliest first.
    /// </summary>
    private static IEnumerable<DateTimeOffset> InstantsShowing(DateTime localTime, TimeZoneInfo timeZone)
    {
        if (timeZone.IsAmbiguousTime(localTime))
        {
            return timeZone.GetAmbiguousTimeOffsets(localTime)
                .Select(offset => new DateTimeOffset(localTime, offset))
                .OrderBy(instant => instant.UtcTicks);
        }

        if (timeZone.IsInvalidTime(localTime))
        {
            // Read the time with the offset that applied before the jump; that lands as far
            // after the jump as the time was after its start. A day earlier is safely before it.
            var offsetBeforeJump = timeZone.GetUtcOffset(localTime.AddDays(-1));
            return [new DateTimeOffset(localTime, offsetBeforeJump)];
        }

        return [new DateTimeOffset(localTime, timeZone.GetUtcOffset(localTime))];
    }
}
