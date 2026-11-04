using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Somnus.Windows.Interop;
using Somnus.Windows.Power;
using Somnus.Windows.Tests.TestDoubles;

namespace Somnus.Windows.Tests.Power;

/// <summary>
/// Tests of the Windows power adapters that never change the power state of the machine (rule R-01).
/// </summary>
public sealed class WindowsPowerAdapterTests
{
    [Fact]
    public void NativeStructures_MatchTheWindowsHeaders()
    {
        Assert.Equal(76, Marshal.SizeOf<SystemPowerCapabilities>());
        Assert.Equal(16, Marshal.SizeOf<TokenPrivileges>());
        Assert.Equal(16, Marshal.SizeOf<ReasonContext>());
    }

    [Fact]
    public void Capabilities_ClassicDesktop_OffersSleepAndHibernation()
    {
        var support = WindowsPowerCapabilities.FromCapabilities(new SystemPowerCapabilities { SystemS3 = 1, SystemS4 = 1, HiberFilePresent = 1 });

        Assert.True(support.CanSleep);
        Assert.True(support.CanHibernate);
        Assert.False(support.IsModernStandby);
    }

    [Fact]
    public void Capabilities_HibernationTurnedOff_HidesHibernate()
    {
        // powercfg /hibernate off deletes the hibernation file.
        var support = WindowsPowerCapabilities.FromCapabilities(new SystemPowerCapabilities { SystemS3 = 1, SystemS4 = 1, HiberFilePresent = 0 });

        Assert.True(support.CanSleep);
        Assert.False(support.CanHibernate);
    }

    [Fact]
    public void Capabilities_ModernStandby_HidesSleep()
    {
        var support = WindowsPowerCapabilities.FromCapabilities(new SystemPowerCapabilities { AoAc = 1, SystemS4 = 1, HiberFilePresent = 1 });

        Assert.False(support.CanSleep);
        Assert.True(support.CanHibernate);
        Assert.True(support.IsModernStandby);
    }

    [Fact]
    public void Query_OnThisMachine_ReturnsCapabilitiesWithoutThrowing()
    {
        var capabilities = new WindowsPowerCapabilities(NullLogger<WindowsPowerCapabilities>.Instance);

        var support = capabilities.Query();

        Assert.NotNull(support);
    }

    [Fact]
    public void Query_LogsTheCapabilitiesOnceWhileTheyStayTheSame()
    {
        // The main window queries again every time it is activated; the log must not fill up with copies.
        var logger = new ListLogger<WindowsPowerCapabilities>();
        var capabilities = new WindowsPowerCapabilities(logger);

        capabilities.Query();
        capabilities.Query();
        capabilities.Query();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(300, entry.EventId.Id);
    }

    [Fact]
    public void KeepAwake_AcquireAndRelease_AreIdempotent()
    {
        using var keepAwake = new WindowsKeepAwake(NullLogger<WindowsKeepAwake>.Instance);

        keepAwake.Acquire();
        keepAwake.Acquire();
        Assert.True(keepAwake.IsAcquired);

        keepAwake.Release();
        keepAwake.Release();
        Assert.False(keepAwake.IsAcquired);
    }

    [Fact]
    public void KeepAwake_CanBeReleasedFromAnotherThread()
    {
        using var keepAwake = new WindowsKeepAwake(NullLogger<WindowsKeepAwake>.Instance);
        keepAwake.Acquire();

        var releaser = new Thread(keepAwake.Release);
        releaser.Start();
        releaser.Join();

        Assert.False(keepAwake.IsAcquired);
    }

    [Fact]
    public void KeepAwake_Dispose_ReleasesTheRequest()
    {
        var keepAwake = new WindowsKeepAwake(NullLogger<WindowsKeepAwake>.Instance);
        keepAwake.Acquire();

        keepAwake.Dispose();

        Assert.False(keepAwake.IsAcquired);
    }

    [Fact]
    public void Constructors_RequireALogger()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsPowerCapabilities(null!));
        Assert.Throws<ArgumentNullException>(() => new WindowsKeepAwake(null!));
    }
}
