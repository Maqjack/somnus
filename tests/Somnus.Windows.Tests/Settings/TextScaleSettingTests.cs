using Microsoft.Win32;
using Somnus.Windows.Settings;

namespace Somnus.Windows.Tests.Settings;

/// <summary>
/// Issue #104: Windows' Text size, read from a test key under HKEY_CURRENT_USER\Software\Somnus.Tests
/// so the tests never change the user's real setting.
/// </summary>
[Collection(RegistryTestGroup.Name)]
public sealed class TextScaleSettingTests : IDisposable
{
    private const string TestsKey = @"Software\Somnus.Tests";

    private readonly string _key = $@"{TestsKey}\{Guid.NewGuid():N}";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
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
    public void NeverChanged_Is100Percent()
    {
        Assert.Equal(1.0, TextScaleSetting.ReadFactor(_key));
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(225, 2.25)]
    [InlineData(300, 2.25)]
    [InlineData(50, 1.0)]
    public void Percent_BecomesAFactorWithinWhatWindowsOffers(int percent, double expected)
    {
        Write(percent);

        Assert.Equal(expected, TextScaleSetting.ReadFactor(_key));
    }

    [Fact]
    public void AValueOfTheWrongKind_Is100Percent()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_key))
        {
            key.SetValue(TextScaleSetting.ValueName, "150", RegistryValueKind.String);
        }

        Assert.Equal(1.0, TextScaleSetting.ReadFactor(_key));
    }

    private void Write(int percent)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_key);
        key.SetValue(TextScaleSetting.ValueName, percent, RegistryValueKind.DWord);
    }
}
