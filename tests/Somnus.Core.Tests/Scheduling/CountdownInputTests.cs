using Somnus.Core.Scheduling;

namespace Somnus.Core.Tests.Scheduling;

public sealed class CountdownInputTests
{
    [Theory]
    [InlineData("10", TimeUnit.Seconds, 10)]
    [InlineData("0010", TimeUnit.Seconds, 10)]
    [InlineData(" 45 ", TimeUnit.Seconds, 45)]
    [InlineData("356400", TimeUnit.Seconds, 356_400)]
    [InlineData("1", TimeUnit.Minutes, 60)]
    [InlineData("5940", TimeUnit.Minutes, 356_400)]
    [InlineData("1", TimeUnit.Hours, 3_600)]
    [InlineData("99", TimeUnit.Hours, 356_400)]
    public void ParseDelay_AcceptsEveryValueFromTenSecondsToNinetyNineHours(string text, TimeUnit unit, int expectedSeconds)
    {
        var result = CountdownInput.ParseDelay(text, unit);

        Assert.True(result.IsValid);
        Assert.Equal(DelayInputError.None, result.Error);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result.Delay);
    }

    [Theory]
    [InlineData(null, DelayInputError.Empty)]
    [InlineData("", DelayInputError.Empty)]
    [InlineData("   ", DelayInputError.Empty)]
    [InlineData("abc", DelayInputError.NotANumber)]
    [InlineData("1a", DelayInputError.NotANumber)]
    [InlineData("-5", DelayInputError.NotANumber)]
    [InlineData("+5", DelayInputError.NotANumber)]
    [InlineData("1.5", DelayInputError.NotANumber)]
    [InlineData("1 000", DelayInputError.NotANumber)]
    [InlineData("١٢", DelayInputError.NotANumber)]
    [InlineData("0", DelayInputError.TooShort)]
    [InlineData("9", DelayInputError.TooShort)]
    [InlineData("356401", DelayInputError.TooLong)]
    [InlineData("99999999999999999999999999", DelayInputError.TooLong)]
    public void ParseDelay_ReturnsTypedErrorInsteadOfThrowing(string? text, DelayInputError expected)
    {
        var result = CountdownInput.ParseDelay(text, TimeUnit.Seconds);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
        Assert.Equal(TimeSpan.Zero, result.Delay);
    }

    [Theory]
    [InlineData("5941", TimeUnit.Minutes)]
    [InlineData("100", TimeUnit.Hours)]
    [InlineData("9999999999999999", TimeUnit.Hours)]
    public void ParseDelay_RejectsMoreThanNinetyNineHoursInEveryUnit(string text, TimeUnit unit)
    {
        Assert.Equal(DelayInputError.TooLong, CountdownInput.ParseDelay(text, unit).Error);
    }

    [Fact]
    public void ParseDelay_ZeroMinutesIsTooShort()
    {
        Assert.Equal(DelayInputError.TooShort, CountdownInput.ParseDelay("0", TimeUnit.Minutes).Error);
    }

    [Fact]
    public void ParseDelay_UnknownUnitIsAProgrammingError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownInput.ParseDelay("10", (TimeUnit)42));
    }

    [Theory]
    [InlineData(TimeUnit.Seconds, 1)]
    [InlineData(TimeUnit.Minutes, 60)]
    [InlineData(TimeUnit.Hours, 3_600)]
    public void LengthOf_ReturnsTheLengthOfOneUnit(TimeUnit unit, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), CountdownInput.LengthOf(unit));
    }

    [Theory]
    [InlineData("0", "0", 0, 0)]
    [InlineData("7", "5", 7, 5)]
    [InlineData("07", "05", 7, 5)]
    [InlineData(" 9 ", " 30 ", 9, 30)]
    [InlineData("23", "59", 23, 59)]
    public void ParseClockTime_AcceptsTwentyFourHourTimes(string hour, string minute, int expectedHour, int expectedMinute)
    {
        var result = CountdownInput.ParseClockTime(hour, minute);

        Assert.True(result.IsValid);
        Assert.Equal(new TimeOnly(expectedHour, expectedMinute), result.Time);
    }

    [Theory]
    [InlineData(null, "00", ClockTimeInputError.InvalidHour)]
    [InlineData("", "00", ClockTimeInputError.InvalidHour)]
    [InlineData("24", "00", ClockTimeInputError.InvalidHour)]
    [InlineData("-1", "00", ClockTimeInputError.InvalidHour)]
    [InlineData("007", "00", ClockTimeInputError.InvalidHour)]
    [InlineData("1a", "00", ClockTimeInputError.InvalidHour)]
    [InlineData("12", null, ClockTimeInputError.InvalidMinute)]
    [InlineData("12", "", ClockTimeInputError.InvalidMinute)]
    [InlineData("12", "60", ClockTimeInputError.InvalidMinute)]
    [InlineData("12", "5x", ClockTimeInputError.InvalidMinute)]
    [InlineData("25", "61", ClockTimeInputError.InvalidHour)]
    public void ParseClockTime_ReportsTheFirstInvalidField(string? hour, string? minute, ClockTimeInputError expected)
    {
        var result = CountdownInput.ParseClockTime(hour, minute);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
        Assert.Equal(TimeOnly.MinValue, result.Time);
    }
}
