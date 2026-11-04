using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Parsing;
using Somnus.App.Logging;

namespace Somnus.App.Tests;

/// <summary>
/// Log lines must not contain the Windows user name, which is the name of the profile folder.
/// </summary>
public sealed class LogFilesTests
{
    private const string Profile = @"C:\Users\Mac";

    [Fact]
    public void LogLine_WritesTheProfileFolderAsAPlaceholder()
    {
        const string settingsFile = @"C:\Users\Mac\AppData\Roaming\Somnus\settings.json";
        var exception = new IOException($"The process cannot access the file '{settingsFile}'.");

        var line = Format("Could not read {Path}; using defaults", settingsFile, exception);

        Assert.DoesNotContain(@"\Mac", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"Could not read %USERPROFILE%\AppData\Roaming\Somnus\settings.json", line, StringComparison.Ordinal);
        Assert.Contains(@"cannot access the file '%USERPROFILE%\AppData", line, StringComparison.Ordinal);
    }

    [Fact]
    public void LogLine_MatchesTheProfileInAnyCaseButNotOtherProfiles()
    {
        var line = Format("Paths: {Path}", @"c:\users\mac\a.txt and C:\Users\Mac2\b.txt", exception: null);

        Assert.Contains(@"%USERPROFILE%\a.txt", line, StringComparison.Ordinal);
        Assert.Contains(@"C:\Users\Mac2\b.txt", line, StringComparison.Ordinal);
    }

    [Fact]
    public void LogLine_WithoutAProfileFolder_IsUnchanged()
    {
        var line = Format("Path: {Path}", Profile, exception: null, profile: string.Empty);

        Assert.Contains(Profile, line, StringComparison.Ordinal);
    }

    private static string Format(string template, string path, Exception? exception, string profile = Profile)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Warning,
            exception,
            new MessageTemplateParser().Parse(template),
            [new LogEventProperty("Path", new ScalarValue(path))]);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        LogFiles.CreateFormatter(profile).Format(logEvent, output);
        return output.ToString();
    }
}
