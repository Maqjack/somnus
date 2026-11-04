using System.Windows.Controls;
using Somnus.App.Diagnostics;
using Somnus.App.Views;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.App.Tests;

/// <summary>Issue #103: a disabled control says why it is disabled (UI-GUIDE 6).</summary>
[Collection(WpfTestGroup.Name)]
public sealed class DisabledStateTests(WpfFixture wpf)
{
    [Fact]
    public void DisabledStart_ExplainsWhyInItsTooltip()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);
            main.AmountText = "x";

            var root = OffscreenRenderer.LayOut(new MainWindow(main));
            var start = VisualTree.Descendants<Button>(root).Single(button => button.Command == main.StartCommand);

            Assert.False(start.IsEnabled);
            Assert.True(ToolTipService.GetShowOnDisabled(start));
            Assert.NotNull(main.InputError);
            Assert.Equal(main.InputError, start.ToolTip);
        });
    }

    [Fact]
    public void EnabledStart_HasNoTooltip()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);

            var root = OffscreenRenderer.LayOut(new MainWindow(main));
            var start = VisualTree.Descendants<Button>(root).Single(button => button.Command == main.StartCommand);

            Assert.True(start.IsEnabled);
            Assert.Null(start.ToolTip);
        });
    }
}
