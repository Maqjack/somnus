using System.Runtime.InteropServices;

namespace Somnus.Windows.Interop;

/// <summary>
/// The Win32 functions Somnus calls (ADR 0006). Nothing outside this project may use them.
/// </summary>
internal static partial class NativeMethods
{
    internal const uint TokenAdjustPrivileges = 0x0020;
    internal const uint TokenQuery = 0x0008;
    internal const uint SePrivilegeEnabled = 0x0002;
    internal const int ErrorNotAllAssigned = 1300;
    internal const string ShutdownPrivilege = "SeShutdownPrivilege";

    internal const uint EwxShutdown = 0x0000_0001;
    internal const uint EwxForce = 0x0000_0004;
    internal const uint EwxPowerOff = 0x0000_0008;
    internal const uint EwxForceIfHung = 0x0000_0010;
    internal const uint ShutdownReasonFlagPlanned = 0x8000_0000;

    internal const uint PowerRequestContextVersion = 0;
    internal const uint PowerRequestContextSimpleString = 0x1;

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool force,
        [MarshalAs(UnmanagedType.U1)] bool wakeupEventsDisabled);

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool GetPwrCapabilities(out SystemPowerCapabilities capabilities);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ExitWindowsEx(uint flags, uint reason);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out SafeTokenHandle tokenHandle);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustTokenPrivileges(
        SafeTokenHandle tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        in TokenPrivileges newState,
        uint bufferLength,
        nint previousState,
        nint returnLength);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint windowHandle, int attribute, in int value, int valueSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial SafePowerRequestHandle PowerCreateRequest(in ReasonContext context);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PowerSetRequest(SafePowerRequestHandle powerRequest, PowerRequestType requestType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PowerClearRequest(SafePowerRequestHandle powerRequest, PowerRequestType requestType);
}
