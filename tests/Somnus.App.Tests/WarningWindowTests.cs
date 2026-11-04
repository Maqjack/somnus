using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Somnus.App.Diagnostics;
using Somnus.App.Views;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.App.Tests;

/// <summary>
/// Mac (2026-11-04): a cross on the warning left people unsure whether it stopped the countdown, so
/// the warning has no close button; Minimize only hides it and Stop stops the countdown.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class WarningWindowTests(WpfFixture wpf)
{
    [Fact]
    public void Warning_HasNoTitleBarAndNoCloseButton()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var shell = OpenWarning(harness);

            var window = new WarningWindow(shell.Warning);

            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.True(window.ShowInTaskbar);
        });
    }

    [Fact]
    public void Minimize_PutsTheWarningOnTheTaskbarAndTheCountdownGoesOn()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var shell = OpenWarning(harness);
            var window = new WarningWindow(shell.Warning);

            window.Minimize();

            Assert.Equal(WindowState.Minimized, window.WindowState);
            Assert.True(harness.Session.Snapshot.IsActive);
            Assert.True(shell.IsWarningOpen);
        });
    }

    [Fact]
    public void MinimizeButton_SaysThatTheCountdownGoesOn()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var shell = OpenWarning(harness);

            var root = OffscreenRenderer.LayOut(new WarningWindow(shell.Warning));
            var minimize = VisualTree.Descendants<Button>(root).Single(button => Equals(button.Content, ""));

            Assert.Equal(harness.Localizer["Warning_Minimize"], AutomationProperties.GetName(minimize));
            Assert.Equal(harness.Localizer["Warning_Minimize"], minimize.ToolTip);
        });
    }

    /// <summary>Starts a 45-minute countdown and runs it into its last minute.</summary>
    private static Presentation.ViewModels.ShellViewModel OpenWarning(TestHarness harness)
    {
        var shell = harness.CreateShell();
        using var main = harness.CreateMain(dryRun: true);
        main.AmountText = "45";
        main.StartCommand.Execute(null);
        harness.AdvanceInTicks(TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(15));
        Assert.True(shell.IsWarningOpen);
        return shell;
    }
}
