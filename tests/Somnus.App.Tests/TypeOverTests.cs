using Somnus.App.Behaviors;

namespace Somnus.App.Tests;

/// <summary>Issue #64: number boxes are typed over instead of cleared first.</summary>
public sealed class TypeOverTests
{
    [Theory]
    [InlineData("", 0, 0, "2", null)]
    [InlineData("2", 1, 0, "2", null)]
    [InlineData("22", 2, 0, "2", "2")]
    [InlineData("3", 1, 0, "3", "3")]
    [InlineData("2", 1, 0, "4", "4")]
    [InlineData("2", 1, 0, "3", null)]
    [InlineData("23", 0, 2, "7", null)]
    public void HourBox_StartsOverInsteadOfGoingPast23(string text, int start, int length, string input, string? expected)
    {
        Assert.Equal(expected, TypeOver.StartOver(text, start, length, input, maxLength: 2, maximum: 23));
    }

    [Theory]
    [InlineData("5", 1, 0, "9", null)]
    [InlineData("6", 1, 0, "0", "0")]
    [InlineData("59", 2, 0, "1", "1")]
    public void MinuteBox_StartsOverInsteadOfGoingPast59(string text, int start, int length, string input, string? expected)
    {
        Assert.Equal(expected, TypeOver.StartOver(text, start, length, input, maxLength: 2, maximum: 59));
    }

    [Theory]
    [InlineData("30", 0, 2, "4", null)]
    [InlineData("12345", 5, 0, "6", null)]
    [InlineData("123456", 6, 0, "7", "7")]
    public void AmountBox_StartsOverOnlyWhenFull(string text, int start, int length, string input, string? expected)
    {
        Assert.Equal(expected, TypeOver.StartOver(text, start, length, input, maxLength: 6, maximum: -1));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("")]
    [InlineData(" ")]
    public void NonDigits_AreLeftToTheDigitFilter(string input)
    {
        Assert.Null(TypeOver.StartOver("22", 2, 0, input, maxLength: 2, maximum: 23));
    }
}
