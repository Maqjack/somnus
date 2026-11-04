using Somnus.Core.Power;

namespace Somnus.Core.Tests.Power;

public sealed class PowerSupportTests
{
    [Theory]
    [InlineData(true, true, PowerAction.Sleep, true)]
    [InlineData(false, true, PowerAction.Sleep, false)]
    [InlineData(true, true, PowerAction.Hibernate, true)]
    [InlineData(true, false, PowerAction.Hibernate, false)]
    [InlineData(false, false, PowerAction.Shutdown, true)]
    [InlineData(true, true, (PowerAction)42, false)]
    public void Supports_FollowsTheCapabilities(bool canSleep, bool canHibernate, PowerAction action, bool expected)
    {
        var support = new PowerSupport(canSleep, canHibernate, IsModernStandby: false);

        Assert.Equal(expected, support.Supports(action));
    }

    [Fact]
    public void ShutdownOnly_OffersNothingButShutdown()
    {
        Assert.False(PowerSupport.ShutdownOnly.Supports(PowerAction.Sleep));
        Assert.False(PowerSupport.ShutdownOnly.Supports(PowerAction.Hibernate));
        Assert.True(PowerSupport.ShutdownOnly.Supports(PowerAction.Shutdown));
    }
}
