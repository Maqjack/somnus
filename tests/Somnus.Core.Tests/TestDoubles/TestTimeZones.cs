namespace Somnus.Core.Tests.TestDoubles;

/// <summary>
/// Time zones built in code, so tests do not depend on the time zone database of the machine.
/// </summary>
internal static class TestTimeZones
{
    /// <summary>
    /// Central European Time with the EU daylight saving rules: clocks go from 02:00 to 03:00 on the
    /// last Sunday of March and from 03:00 back to 02:00 on the last Sunday of October.
    /// In 2026 that is 29 March and 25 October.
    /// </summary>
    public static TimeZoneInfo CentralEurope { get; } = CreateCentralEurope();

    private static TimeZoneInfo CreateCentralEurope()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            dateStart: new DateTime(2000, 1, 1),
            dateEnd: DateTime.MaxValue.Date,
            daylightDelta: TimeSpan.FromHours(1),
            daylightTransitionStart: TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            daylightTransitionEnd: TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));

        return TimeZoneInfo.CreateCustomTimeZone(
            "Somnus Test Central Europe",
            TimeSpan.FromHours(1),
            "Somnus Test Central Europe",
            "CET",
            "CEST",
            [rule]);
    }
}
