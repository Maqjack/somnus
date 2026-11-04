namespace Somnus.Core.Scheduling;

/// <summary>
/// The days of the week on which a countdown at a clock time repeats (#83).
/// </summary>
[Flags]
public enum WeekDays
{
    /// <summary>No repetition: the countdown runs once.</summary>
    None = 0,

    /// <summary>Monday.</summary>
    Monday = 1,

    /// <summary>Tuesday.</summary>
    Tuesday = 2,

    /// <summary>Wednesday.</summary>
    Wednesday = 4,

    /// <summary>Thursday.</summary>
    Thursday = 8,

    /// <summary>Friday.</summary>
    Friday = 16,

    /// <summary>Saturday.</summary>
    Saturday = 32,

    /// <summary>Sunday.</summary>
    Sunday = 64,

    /// <summary>All seven days.</summary>
    EveryDay = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}

/// <summary>
/// Helpers for <see cref="WeekDays"/>.
/// </summary>
public static class WeekDaysExtensions
{
    /// <summary>Gets the flag of a day of the week.</summary>
    /// <param name="day">The day.</param>
    /// <returns>Its flag.</returns>
    public static WeekDays ToWeekDays(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        DayOfWeek.Sunday => WeekDays.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown day of the week."),
    };

    /// <summary>Tells whether <paramref name="days"/> holds <paramref name="day"/>.</summary>
    /// <param name="days">The chosen days.</param>
    /// <param name="day">The day to look for.</param>
    /// <returns><see langword="true"/> if the day is chosen.</returns>
    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToWeekDays()) != WeekDays.None;
}
