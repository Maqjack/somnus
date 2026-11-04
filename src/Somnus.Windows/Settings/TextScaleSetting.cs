using System.Security;
using Microsoft.Win32;

namespace Somnus.Windows.Settings;

/// <summary>
/// Reads Settings &gt; Accessibility &gt; Text size, which Windows keeps for the user as a
/// percentage from 100 to 225 (#104).
/// </summary>
public static class TextScaleSetting
{
    /// <summary>The smallest text size Windows offers, in percent.</summary>
    public const int MinimumPercent = 100;

    /// <summary>The largest text size Windows offers, in percent.</summary>
    public const int MaximumPercent = 225;

    internal const string Key = @"Software\Microsoft\Accessibility";
    internal const string ValueName = "TextScaleFactor";

    /// <summary>Returns the text size as a factor: 1 for 100%, also when it was never changed.</summary>
    /// <returns>A factor from 1 to 2.25.</returns>
    public static double ReadFactor() => ReadFactor(Key);

    /// <summary>Reads the text size from another key under HKEY_CURRENT_USER, for tests.</summary>
    internal static double ReadFactor(string key)
    {
        try
        {
            using var accessibility = Registry.CurrentUser.OpenSubKey(key);
            var percent = accessibility?.GetValue(ValueName) is int value ? value : MinimumPercent;
            return Math.Clamp(percent, MinimumPercent, MaximumPercent) / 100.0;
        }
        catch (SecurityException)
        {
            return MinimumPercent / 100.0;
        }
    }
}
