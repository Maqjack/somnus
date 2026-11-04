using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Somnus.Windows.Interop;

/// <summary>SYSTEM_POWER_CAPABILITIES (winnt.h). BOOLEAN fields are one byte.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SystemPowerCapabilities
{
    public byte PowerButtonPresent;
    public byte SleepButtonPresent;
    public byte LidPresent;
    public byte SystemS1;
    public byte SystemS2;
    public byte SystemS3;
    public byte SystemS4;
    public byte SystemS5;
    public byte HiberFilePresent;
    public byte FullWake;
    public byte VideoDimPresent;
    public byte ApmPresent;
    public byte UpsPresent;
    public byte ThermalControl;
    public byte ProcessorThrottle;
    public byte ProcessorMinThrottle;
    public byte ProcessorMaxThrottle;
    public byte FastSystemS4;
    public byte Hiberboot;
    public byte WakeAlarmPresent;
    public byte AoAc;
    public byte DiskSpinDown;
    public byte HiberFileType;
    public byte AoAcConnectivitySupported;
    public byte Spare3A;
    public byte Spare3B;
    public byte Spare3C;
    public byte Spare3D;
    public byte Spare3E;
    public byte Spare3F;
    public byte SystemBatteriesPresent;
    public byte BatteriesAreShortTerm;
    public BatteryReportingScale BatteryScale0;
    public BatteryReportingScale BatteryScale1;
    public BatteryReportingScale BatteryScale2;
    public int AcOnLineWake;
    public int SoftLidWake;
    public int RtcWake;
    public int MinDeviceWakeState;
    public int DefaultLowLatencyWake;
}

/// <summary>BATTERY_REPORTING_SCALE (winnt.h).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BatteryReportingScale
{
    public uint Granularity;
    public uint Capacity;
}

/// <summary>LUID (winnt.h).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;
}

/// <summary>TOKEN_PRIVILEGES with exactly one LUID_AND_ATTRIBUTES entry.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TokenPrivileges
{
    public uint PrivilegeCount;
    public Luid Luid;
    public uint Attributes;
}

/// <summary>REASON_CONTEXT (minwinbase.h) for a simple reason string.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ReasonContext
{
    public uint Version;
    public uint Flags;
    public nint SimpleReasonString;
}

/// <summary>POWER_REQUEST_TYPE (minwinbase.h).</summary>
internal enum PowerRequestType
{
    DisplayRequired = 0,
    SystemRequired = 1,
    AwayModeRequired = 2,
    ExecutionRequired = 3,
}

/// <summary>An access token handle closed with CloseHandle.</summary>
internal sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeTokenHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
}

/// <summary>A power request handle closed with CloseHandle.</summary>
internal sealed class SafePowerRequestHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePowerRequestHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
}
