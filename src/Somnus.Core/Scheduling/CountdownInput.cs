using System.Globalization;

namespace Somnus.Core.Scheduling;

/// <summary>
/// Parses and validates what the user typed for a countdown. User input never causes an
/// exception: every problem comes back as a typed error that the UI shows inline.
/// </summary>
public static class CountdownInput
{
    /// <summary>The shortest allowed countdown (assumption A1).</summary>
    public static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(10);

    /// <summary>The longest allowed countdown (assumption A1).</summary>
    public static readonly TimeSpan MaximumDelay = TimeSpan.FromHours(99);

    /// <summary>
    /// Parses the amount typed in the "in" mode together with its unit.
    /// </summary>
    /// <param name="amountText">The text from the number box; surrounding spaces are ignored.</param>
    /// <param name="unit">The unit chosen next to the number box.</param>
    /// <returns>The delay, or the reason why the input is not acceptable.</returns>
    public static DelayParseResult ParseDelay(string? amountText, TimeUnit unit)
    {
        var unitLength = LengthOf(unit);
        var text = amountText?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return DelayParseResult.Failure(DelayInputError.Empty);
        }

        if (!text.All(char.IsAsciiDigit))
        {
            return DelayParseResult.Failure(DelayInputError.NotANumber);
        }

        // The text holds digits only, so parsing can fail only when the number is enormous.
        var maximumAmount = MaximumDelay.Ticks / unitLength.Ticks;
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount > maximumAmount)
        {
            return DelayParseResult.Failure(DelayInputError.TooLong);
        }

        var delay = TimeSpan.FromTicks(unitLength.Ticks * amount);
        return delay < MinimumDelay
            ? DelayParseResult.Failure(DelayInputError.TooShort)
            : DelayParseResult.Success(delay);
    }

    /// <summary>
    /// Parses the hour and minute typed in the "at" mode (24-hour clock, assumption A2).
    /// </summary>
    /// <param name="hourText">One or two digits, 0 to 23.</param>
    /// <param name="minuteText">One or two digits, 0 to 59.</param>
    /// <returns>The clock time, or which of the two fields is not acceptable.</returns>
    public static ClockTimeParseResult ParseClockTime(string? hourText, string? minuteText)
    {
        if (!TryParseClockField(hourText, 23, out var hour))
        {
            return ClockTimeParseResult.Failure(ClockTimeInputError.InvalidHour);
        }

        if (!TryParseClockField(minuteText, 59, out var minute))
        {
            return ClockTimeParseResult.Failure(ClockTimeInputError.InvalidMinute);
        }

        return ClockTimeParseResult.Success(new TimeOnly(hour, minute));
    }

    /// <summary>
    /// Returns the length of one <paramref name="unit"/>.
    /// </summary>
    /// <param name="unit">A defined time unit.</param>
    /// <returns>One second, one minute or one hour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The unit is not a defined value.</exception>
    public static TimeSpan LengthOf(TimeUnit unit) => unit switch
    {
        TimeUnit.Seconds => TimeSpan.FromSeconds(1),
        TimeUnit.Minutes => TimeSpan.FromMinutes(1),
        TimeUnit.Hours => TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown time unit."),
    };

    private static bool TryParseClockField(string? text, int maximum, out int value)
    {
        value = 0;
        var trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length is 1 or 2
            && trimmed.All(char.IsAsciiDigit)
            && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value <= maximum;
    }
}

/// <summary>
/// Why the amount typed in the "in" mode is not acceptable.
/// </summary>
public enum DelayInputError
{
    /// <summary>The input is valid.</summary>
    None,

    /// <summary>Nothing was typed.</summary>
    Empty,

    /// <summary>The text contains something other than digits.</summary>
    NotANumber,

    /// <summary>The delay is shorter than <see cref="CountdownInput.MinimumDelay"/>.</summary>
    TooShort,

    /// <summary>The delay is longer than <see cref="CountdownInput.MaximumDelay"/>.</summary>
    TooLong,
}

/// <summary>
/// Which field of the "at" mode is not acceptable.
/// </summary>
public enum ClockTimeInputError
{
    /// <summary>The input is valid.</summary>
    None,

    /// <summary>The hour is empty, not a number, or outside 0–23.</summary>
    InvalidHour,

    /// <summary>The minute is empty, not a number, or outside 0–59.</summary>
    InvalidMinute,
}

/// <summary>
/// The outcome of <see cref="CountdownInput.ParseDelay"/>.
/// </summary>
public readonly record struct DelayParseResult
{
    private DelayParseResult(TimeSpan delay, DelayInputError error)
    {
        Delay = delay;
        Error = error;
    }

    /// <summary>Gets the parsed delay; <see cref="TimeSpan.Zero"/> when the input is invalid.</summary>
    public TimeSpan Delay { get; }

    /// <summary>Gets the validation error, or <see cref="DelayInputError.None"/>.</summary>
    public DelayInputError Error { get; }

    /// <summary>Gets a value indicating whether the input is valid.</summary>
    public bool IsValid => Error == DelayInputError.None;

    internal static DelayParseResult Success(TimeSpan delay) => new(delay, DelayInputError.None);

    internal static DelayParseResult Failure(DelayInputError error) => new(TimeSpan.Zero, error);
}

/// <summary>
/// The outcome of <see cref="CountdownInput.ParseClockTime"/>.
/// </summary>
public readonly record struct ClockTimeParseResult
{
    private ClockTimeParseResult(TimeOnly time, ClockTimeInputError error)
    {
        Time = time;
        Error = error;
    }

    /// <summary>Gets the parsed clock time; midnight when the input is invalid.</summary>
    public TimeOnly Time { get; }

    /// <summary>Gets the validation error, or <see cref="ClockTimeInputError.None"/>.</summary>
    public ClockTimeInputError Error { get; }

    /// <summary>Gets a value indicating whether the input is valid.</summary>
    public bool IsValid => Error == ClockTimeInputError.None;

    internal static ClockTimeParseResult Success(TimeOnly time) => new(time, ClockTimeInputError.None);

    internal static ClockTimeParseResult Failure(ClockTimeInputError error) => new(TimeOnly.MinValue, error);
}
