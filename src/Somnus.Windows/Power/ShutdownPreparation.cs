using System.ComponentModel;
using System.Runtime.InteropServices;
using Somnus.Core.Power;
using Somnus.Windows.Interop;

namespace Somnus.Windows.Power;

/// <summary>
/// The parts of a shutdown that can run without shutting down: the flags and the privilege. Tests
/// call them on real Windows; only <see cref="WindowsPowerController"/> goes on to shut down.
/// </summary>
internal static class ShutdownPreparation
{
    /// <summary>
    /// Returns the <c>ExitWindowsEx</c> flags: a normal shutdown closes hung apps only
    /// (<c>EWX_FORCEIFHUNG</c>), a forced one closes every app (<c>EWX_FORCE</c>, assumption A3).
    /// </summary>
    /// <param name="forceCloseApps">Whether apps are closed without asking.</param>
    /// <returns>The flags for a shutdown that also turns the power off.</returns>
    internal static uint Flags(bool forceCloseApps) =>
        NativeMethods.EwxShutdown | NativeMethods.EwxPowerOff
        | (forceCloseApps ? NativeMethods.EwxForce : NativeMethods.EwxForceIfHung);

    /// <summary>
    /// Enables <c>SeShutdownPrivilege</c> in the token of this process. Standard accounts hold the
    /// privilege but have it disabled; enabling it changes nothing until a shutdown is requested.
    /// </summary>
    /// <exception cref="PowerActionFailedException">The token cannot be adjusted or the account lacks the privilege.</exception>
    internal static void EnablePrivilege()
    {
        if (!NativeMethods.OpenProcessToken(
                NativeMethods.GetCurrentProcess(),
                NativeMethods.TokenAdjustPrivileges | NativeMethods.TokenQuery,
                out var token))
        {
            throw Failure(nameof(NativeMethods.OpenProcessToken));
        }

        using (token)
        {
            if (!NativeMethods.LookupPrivilegeValue(null, NativeMethods.ShutdownPrivilege, out var luid))
            {
                throw Failure(nameof(NativeMethods.LookupPrivilegeValue));
            }

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = NativeMethods.SePrivilegeEnabled,
            };

            if (!NativeMethods.AdjustTokenPrivileges(token, false, in privileges, 0, 0, 0))
            {
                throw Failure(nameof(NativeMethods.AdjustTokenPrivileges));
            }

            // AdjustTokenPrivileges also succeeds when the account does not hold the privilege.
            if (Marshal.GetLastPInvokeError() == NativeMethods.ErrorNotAllAssigned)
            {
                throw new PowerActionFailedException("This account is not allowed to shut down the PC (SeShutdownPrivilege is not assigned).");
            }
        }
    }

    /// <summary>Describes the last Win32 error of <paramref name="function"/> as a failed power action.</summary>
    /// <param name="function">The Windows function that failed.</param>
    /// <returns>The exception to throw.</returns>
    internal static PowerActionFailedException Failure(string function)
    {
        var error = Marshal.GetLastPInvokeError();
        var cause = new Win32Exception(error);
        return new PowerActionFailedException($"{function} failed with error {error}: {cause.Message}", cause);
    }
}
