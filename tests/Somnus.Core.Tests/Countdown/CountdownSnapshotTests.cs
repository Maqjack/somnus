using Somnus.Core.Countdown;

namespace Somnus.Core.Tests.Countdown;

public sealed class CountdownSnapshotTests
{
    [Theory]
    [InlineData(59_200, 60)]
    [InlineData(60_000, 60)]
    [InlineData(100, 1)]
    [InlineData(0, 0)]
    [InlineData(-3_000, 0)]
    public void RemainingWholeSeconds_RoundsUpLikeACountdownDisplay(int remainingMilliseconds, long expected)
    {
        var snapshot = CountdownSnapshot.Idle with { Remaining = TimeSpan.FromMilliseconds(remainingMilliseconds) };

        Assert.Equal(expected, snapshot.RemainingWholeSeconds);
    }

    [Theory]
    [InlineData(CountdownState.Idle, false)]
    [InlineData(CountdownState.Running, true)]
    [InlineData(CountdownState.Warning, true)]
    [InlineData(CountdownState.Executing, false)]
    [InlineData(CountdownState.Done, false)]
    [InlineData(CountdownState.Failed, false)]
    [InlineData(CountdownState.Cancelled, false)]
    public void IsActive_OnlyWhileCountingDown(CountdownState state, bool expected)
    {
        Assert.Equal(expected, (CountdownSnapshot.Idle with { State = state }).IsActive);
    }
}
