using Somnus.Core.Power;
using Somnus.Core.Scheduling;
using Somnus.Core.Tests.TestDoubles;

namespace Somnus.Core.Tests.Scheduling;

public sealed class CountdownRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 18, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void In_KeepsTheDelayAndAction()
    {
        var request = CountdownRequest.In(TimeSpan.FromMinutes(45), PowerAction.Hibernate);

        Assert.Equal(TimeMode.In, request.Mode);
        Assert.Equal(TimeSpan.FromMinutes(45), request.Delay);
        Assert.Equal(PowerAction.Hibernate, request.Action);
        Assert.False(request.ForceCloseApps);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(356_401)]
    public void In_RejectsDelaysOutsideTheAllowedRange(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownRequest.In(TimeSpan.FromSeconds(seconds), PowerAction.Sleep));
    }

    [Theory]
    [InlineData(PowerAction.Sleep)]
    [InlineData(PowerAction.Hibernate)]
    public void ForceCloseApps_IsOnlyAllowedForShutdown(PowerAction action)
    {
        Assert.Throws<ArgumentException>(() => CountdownRequest.In(TimeSpan.FromMinutes(1), action, forceCloseApps: true));
        Assert.Throws<ArgumentException>(() => CountdownRequest.At(new TimeOnly(23, 0), action, forceCloseApps: true));
    }

    [Fact]
    public void ForceCloseApps_IsAcceptedForShutdown()
    {
        var request = CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Shutdown, forceCloseApps: true);

        Assert.True(request.ForceCloseApps);
    }

    [Fact]
    public void ForceCloseApps_IsKeptOnEveryRunOfARepeatingShutdown()
    {
        var request = CountdownRequest.At(new TimeOnly(23, 0), PowerAction.Shutdown, forceCloseApps: true, WeekDays.EveryDay);

        var first = request.ToPlan(Now, TimeZoneInfo.Utc, warningEnabled: true);
        var next = request.ToNextPlan(Now, first.Target, TimeZoneInfo.Utc, warningEnabled: true);

        Assert.True(first.ForceCloseApps);
        Assert.True(next.ForceCloseApps);
        Assert.True(next.Repeats);
    }

    [Fact]
    public void UnknownAction_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountdownRequest.In(TimeSpan.FromMinutes(1), (PowerAction)99));
    }

    [Fact]
    public void At_DropsSeconds()
    {
        var request = CountdownRequest.At(new TimeOnly(23, 30, 59), PowerAction.Sleep);

        Assert.Equal(TimeMode.At, request.Mode);
        Assert.Equal(new TimeOnly(23, 30), request.ClockTime);
    }

    [Fact]
    public void ResolveTarget_InMode_AddsTheDelay()
    {
        var request = CountdownRequest.In(TimeSpan.FromHours(2), PowerAction.Sleep);

        Assert.Equal(Now.AddHours(2), request.ResolveTarget(Now, TestTimeZones.CentralEurope));
    }

    [Fact]
    public void ResolveTarget_AtMode_UsesTheNextOccurrence()
    {
        var request = CountdownRequest.At(new TimeOnly(7, 0), PowerAction.Shutdown);

        var target = request.ResolveTarget(Now, TestTimeZones.CentralEurope);

        Assert.Equal(new DateTimeOffset(2026, 10, 23, 7, 0, 0, TimeSpan.FromHours(2)), target);
    }

    [Fact]
    public void ToPlan_CarriesTargetActionForceAndWarning()
    {
        var request = CountdownRequest.In(TimeSpan.FromMinutes(10), PowerAction.Shutdown, forceCloseApps: true);

        var plan = request.ToPlan(Now, TestTimeZones.CentralEurope, warningEnabled: true);

        Assert.Equal(Now.AddMinutes(10), plan.Target);
        Assert.Equal(PowerAction.Shutdown, plan.Action);
        Assert.True(plan.ForceCloseApps);
        Assert.True(plan.WarningEnabled);
    }
}
