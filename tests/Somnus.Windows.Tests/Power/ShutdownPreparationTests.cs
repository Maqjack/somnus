using Somnus.Windows.Interop;
using Somnus.Windows.Power;

namespace Somnus.Windows.Tests.Power;

/// <summary>
/// The parts of a shutdown that run on real Windows without shutting down (rule R-01). Nothing here
/// asks Windows to shut down; the real call stays untested until Mac's check in issue #12.
/// </summary>
public sealed class ShutdownPreparationTests
{
    [Fact]
    public void Flags_NormalShutdown_TurnsThePowerOffAndClosesOnlyHungApps()
    {
        // EWX_SHUTDOWN 0x1 | EWX_POWEROFF 0x8 | EWX_FORCEIFHUNG 0x10 (winuser.h)
        Assert.Equal(0x19u, ShutdownPreparation.Flags(forceCloseApps: false));
    }

    [Fact]
    public void Flags_ForcedShutdown_TurnsThePowerOffAndClosesEveryApp()
    {
        // EWX_SHUTDOWN 0x1 | EWX_POWEROFF 0x8 | EWX_FORCE 0x4 (winuser.h)
        Assert.Equal(0x0Du, ShutdownPreparation.Flags(forceCloseApps: true));
    }

    [Fact]
    public void ShutdownReason_IsAPlannedShutdown()
    {
        // SHTDN_REASON_FLAG_PLANNED (reason.h)
        Assert.Equal(0x8000_0000u, NativeMethods.ShutdownReasonFlagPlanned);
    }

    [Fact]
    public void EnablePrivilege_OnThisAccount_Succeeds()
    {
        // Runs OpenProcessToken, LookupPrivilegeValue and AdjustTokenPrivileges for real, which checks
        // their declarations and the TOKEN_PRIVILEGES layout. It only enables a privilege in the token
        // of this test process; nothing shuts down.
        ShutdownPreparation.EnablePrivilege();
        ShutdownPreparation.EnablePrivilege();
    }
}
