using System.Windows;
using Somnus.App.Behaviors;
using Somnus.App.Views;
using Somnus.Presentation.Tests.TestDoubles;

namespace Somnus.App.Tests;

/// <summary>Issue #104: the text and the widths that hold it follow Windows' Text size.</summary>
[Collection(WpfTestGroup.Name)]
public sealed class TextScaleTests(WpfFixture wpf)
{
    [Fact]
    public void Apply_ScalesTheFontSizesAndTheOpenWindows()
    {
        wpf.Run(() =>
        {
            var resources = Application.Current.Resources;
            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);
            var window = new MainWindow(main);
            try
            {
                TextScale.Apply(resources, 1.5);

                Assert.Equal(21.0, (double)resources["FontSizeBody"]);
                Assert.Equal(66.0, (double)resources["FontSizeCountdown"]);
                Assert.Equal(10.0, (double)resources["FontSizeTiny"]);
                Assert.Equal(570.0, window.Width);
            }
            finally
            {
                TextScale.Apply(resources, 1);
            }

            // Back to 100%: the theme's own sizes, not 1.5 x 1.5.
            Assert.Equal(14.0, (double)resources["FontSizeBody"]);
            Assert.Equal(380.0, window.Width);
        });
    }

    [Fact]
    public void NewWindows_OpenAtTheTextSizeInEffect()
    {
        wpf.Run(() =>
        {
            var resources = Application.Current.Resources;
            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);
            TextScale.Apply(resources, 2.25);
            try
            {
                Assert.Equal(855.0, new MainWindow(main).Width);
            }
            finally
            {
                TextScale.Apply(resources, 1);
            }
        });
    }
}
