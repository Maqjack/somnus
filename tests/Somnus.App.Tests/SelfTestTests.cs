using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Somnus.App.Diagnostics;

namespace Somnus.App.Tests;

/// <summary>
/// The self-test of the finished build (SWPA 3.14). It runs here too, so a new lazy path that breaks
/// it shows up before a build does. It shows nothing, plays nothing and only simulates power actions.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class SelfTestTests(WpfFixture wpf)
{
    [Fact]
    public void IsRequested_FindsTheSwitchAndTheReportFile()
    {
        Assert.True(SelfTest.IsRequested(["--self-test", "report.json"], out var reportPath));
        Assert.Equal("report.json", reportPath);
    }

    [Fact]
    public void IsRequested_WithoutAFile_ReportsToStandardOutput()
    {
        Assert.True(SelfTest.IsRequested(["--SELF-TEST"], out var reportPath));
        Assert.Null(reportPath);
    }

    [Fact]
    public void IsRequested_IsFalseForANormalStart()
    {
        Assert.False(SelfTest.IsRequested(["--dry-run"], out var reportPath));
        Assert.Null(reportPath);
    }

    [Fact]
    public void Run_PassesEveryCheckAndWritesTheReport()
    {
        var reportPath = Path.Combine(Path.GetTempPath(), $"somnus-self-test-{Guid.NewGuid():N}.json");
        try
        {
            var exitCode = -1;
            wpf.Run(() => exitCode = SelfTest.Run(Dispatcher.CurrentDispatcher, reportPath));

            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            var checks = report.RootElement.GetProperty("checks").EnumerateArray().ToList();
            Assert.Empty(checks.Where(check => !check.GetProperty("passed").GetBoolean()).Select(check => check.ToString()));
            Assert.Equal(0, exitCode);
            Assert.True(report.RootElement.GetProperty("passed").GetBoolean());
            Assert.Equal(
                ["resources", "lullaby", "installer-language", "settings", "logging", "composition", "power", "windows"],
                checks.Select(check => check.GetProperty("name").GetString()));
        }
        finally
        {
            File.Delete(reportPath);
        }
    }
}
