using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using Somnus.Windows.Settings;

namespace Somnus.Windows.Tests.Settings;

/// <summary>
/// Issue #83: start with Windows through the user's Run key. The tests use their own keys under
/// HKEY_CURRENT_USER\Software\Somnus.Tests and never touch the real Run key, so running them adds
/// nothing to the startup apps of the PC.
/// </summary>
[Collection(RegistryTestGroup.Name)]
public sealed class RunKeyAutostartTests : IDisposable
{
    private const string ExecutablePath = @"C:\Program Files\Somnus\Somnus.exe";
    private const string TestsKey = @"Software\Somnus.Tests";

    private readonly string _root = $@"{TestsKey}\{Guid.NewGuid():N}";

    private string RunKey => $@"{_root}\Run";

    private string ApprovedKey => $@"{_root}\StartupApproved";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        using (var tests = Registry.CurrentUser.OpenSubKey(TestsKey))
        {
            if (tests is not { SubKeyCount: 0, ValueCount: 0 })
            {
                return;
            }
        }

        Registry.CurrentUser.DeleteSubKey(TestsKey, throwOnMissingSubKey: false);
    }

    [Fact]
    public void IsEnabled_WithoutTheKey_IsOff()
    {
        Assert.False(Autostart().IsEnabled);
    }

    [Fact]
    public void TurningOn_WritesTheQuotedPathWithTheArgument()
    {
        var autostart = Autostart();

        Assert.True(autostart.TrySet(true));

        Assert.True(autostart.IsEnabled);
        Assert.Equal($"\"{ExecutablePath}\" --autostart", Read(RunKey));
    }

    [Fact]
    public void TurningOff_RemovesTheValue()
    {
        var autostart = Autostart();
        autostart.TrySet(true);

        Assert.True(autostart.TrySet(false));

        Assert.False(autostart.IsEnabled);
        Assert.Null(Read(RunKey));
    }

    [Fact]
    public void TurningOff_WhenItWasNeverOn_Succeeds()
    {
        Assert.True(Autostart().TrySet(false));
    }

    [Fact]
    public void AnotherCopyOfSomnus_DoesNotCount()
    {
        // A portable copy elsewhere starts with Windows; this one does not.
        Write(RunKey, @"""D:\Portable\Somnus\Somnus.exe"" --autostart");

        Assert.False(Autostart().IsEnabled);
    }

    [Fact]
    public void TurnedOffInTaskManager_IsOff_AndTurningOnClearsIt()
    {
        var autostart = Autostart();
        autostart.TrySet(true);
        using (var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey))
        {
            // Task Manager keeps the Run value and marks it disabled with an odd first byte.
            approved.SetValue(RunKeyAutostart.ValueName, new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }

        Assert.False(autostart.IsEnabled);

        Assert.True(autostart.TrySet(true));

        Assert.True(autostart.IsEnabled);
    }

    [Fact]
    public void EnabledInTaskManager_StaysOn()
    {
        var autostart = Autostart();
        autostart.TrySet(true);
        using (var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey))
        {
            approved.SetValue(RunKeyAutostart.ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }

        Assert.True(autostart.IsEnabled);
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentException>(() => new RunKeyAutostart(string.Empty, NullLogger<RunKeyAutostart>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RunKeyAutostart(ExecutablePath, null!));
    }

    private RunKeyAutostart Autostart() => new(ExecutablePath, NullLogger<RunKeyAutostart>.Instance, RunKey, ApprovedKey);

    private static string? Read(string key)
    {
        using var run = Registry.CurrentUser.OpenSubKey(key);
        return run?.GetValue(RunKeyAutostart.ValueName) as string;
    }

    private static void Write(string key, string value)
    {
        using var run = Registry.CurrentUser.CreateSubKey(key);
        run.SetValue(RunKeyAutostart.ValueName, value, RegistryValueKind.String);
    }
}
