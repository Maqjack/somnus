using System.Windows.Controls;
using Somnus.App.Diagnostics;
using Somnus.App.Views;
using Somnus.Core.Scheduling;
using Somnus.Core.Settings;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.App.Tests;

/// <summary>
/// Switching the language while the main window is open (issue #21) must keep what the user chose.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class LanguageSwitchTests(WpfFixture wpf)
{
    [Fact]
    public void SwitchingTheLanguage_KeepsTheChosenUnitSelected()
    {
        wpf.Run(() =>
        {
            using var harness = new TestHarness();
            using var main = harness.CreateMain();
            main.Unit = TimeUnit.Hours;
            var root = OffscreenRenderer.LayOut(new MainWindow(main));
            var unitBox = WpfFixture.FindFirst<ComboBox>(root);
            Assert.Equal(TimeUnit.Hours, unitBox.SelectedValue);

            harness.Localizer.Language = AppLanguage.Polish;
            root.UpdateLayout();

            Assert.Equal(TimeUnit.Hours, unitBox.SelectedValue);
            Assert.Equal("godz.", Assert.IsType<UnitOption>(unitBox.SelectedItem).DisplayName);
            Assert.Equal(TimeUnit.Hours, main.Unit);
        });
    }
}
