using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Somnus.App.Behaviors;
using Somnus.App.Diagnostics;
using Somnus.App.Views;
using Somnus.Core.Settings;
using Somnus.Presentation.Localization;
using Somnus.Presentation.Services;
using Somnus.Presentation.Tests.TestDoubles;
using Somnus.Presentation.ViewModels;

namespace Somnus.App.Tests;

/// <summary>
/// Loads every window from its XAML with the real theme and renders it off screen to a PNG in the
/// test output (<c>previews</c> folder). This catches XAML errors the compiler cannot see, such as a
/// missing resource, and produces previews for the README. Nothing is shown and the screen is never captured.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class ViewSmokeTests(WpfFixture wpf)
{
    private static readonly string PreviewDirectory = Path.Combine(AppContext.BaseDirectory, "previews");

    [Fact]
    public void EveryWindow_LoadsAndRendersInTheTheme()
    {
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);

            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);
            using var shell = harness.CreateShell();

            Render(new MainWindow(main), "main-idle.png");

            main.IsAtMode = true;
            Render(new MainWindow(main), "main-at.png");

            main.IsEveryDay = true;
            main.Days[6].IsSelected = false;
            Render(new MainWindow(main), "main-days.png");
            main.IsEveryDay = false;
            main.IsInMode = true;

            main.IsShutdownSelected = true;
            main.ForceCloseApps = true;
            main.AmountText = "x";
            Render(new MainWindow(main), "main-invalid-forced.png");

            main.AmountText = "45";
            main.IsSleepSelected = true;
            main.StartCommand.Execute(null);
            Render(new MainWindow(main), "main-running.png");

            main.AmountText = "30";
            Assert.True(main.HasPendingChanges);
            Render(new MainWindow(main), "main-pending.png");
            main.AmountText = "45";

            harness.AdvanceInTicks(TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(15));
            Assert.True(shell.IsWarningOpen);
            Render(new WarningWindow(shell.Warning), "warning.png");

            using (var settingsMain = harness.CreateMain(dryRun: true))
            {
                settingsMain.OpenSettingsCommand.Execute(null);
                Render(new MainWindow(settingsMain), "settings.png");

                // Windows refused Start with Windows (#83): the panel stays open and says so.
                harness.Autostart.Fails = true;
                settingsMain.Settings!.StartWithWindows = true;
                settingsMain.Settings.SaveCommand.Execute(null);
                Assert.NotNull(settingsMain.Settings.ErrorMessage);
                Render(new MainWindow(settingsMain), "settings-autostart-failed.png");
                harness.Autostart.Fails = false;
                settingsMain.CloseSettings();
            }

            // Sleep and Hibernate unavailable, with a countdown of its own: the disabled options in TextDisabled (#103).
            using (var unavailableHarness = new TestHarness())
            {
                unavailableHarness.Capabilities.Support = new(CanSleep: false, CanHibernate: false, IsModernStandby: true);
                using var unavailableMain = unavailableHarness.CreateMain(dryRun: true);
                Render(new MainWindow(unavailableMain), "main-unavailable.png");
            }

            Render(new ConfirmDialog(new ConfirmationRequest("Stop the countdown?", "Sleep on Thu, 22 Oct at 18:45 will not happen.", "Stop", "Keep counting")), "confirm.png");
            Render(new MessageDialog("Something went wrong", @"Somnus saved the details in the log and keeps running. Log folder: C:\Users\Mac\AppData\Roaming\Somnus\logs"), "error.png");

            // The same windows in Polish: longer words must still fit.
            Localizer.Instance.Language = AppLanguage.Polish;
            harness.Localizer.Language = AppLanguage.Polish;
            harness.Settings.Update(settings => settings with { Language = AppLanguage.Polish });
            try
            {
                using var polishMain = harness.CreateMain(dryRun: false);
                polishMain.IsShutdownSelected = true;
                polishMain.ForceCloseApps = true;
                Render(new MainWindow(polishMain), "main-pl.png");
                Render(new WarningWindow(shell.Warning), "warning-pl.png");
                polishMain.OpenSettingsCommand.Execute(null);
                Render(new MainWindow(polishMain), "settings-pl.png");
            }
            finally
            {
                Localizer.Instance.Language = AppLanguage.English;
            }
        });
    }

    [Fact]
    public void RepeatingCountdown_RendersInEveryState()
    {
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);

            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);

            main.IsAtMode = true;
            main.IsEveryDay = true;
            Render(new MainWindow(main), "main-days-all.png");

            main.Days[5].IsSelected = false;
            main.Days[6].IsSelected = false;
            main.IsShutdownSelected = true;
            Assert.False(main.IsForceOptionVisible);
            Render(new MainWindow(main), "main-days-shutdown.png");

            main.StartCommand.Execute(null);
            Assert.NotNull(main.RepeatText);
            Render(new MainWindow(main), "main-repeating.png");

            main.RequestCancelCommand.Execute(null);
            Render(new ConfirmDialog(harness.Dialogs.Requests[^1]), "confirm-schedule.png");

            Localizer.Instance.Language = AppLanguage.Polish;
            harness.Localizer.Language = AppLanguage.Polish;
            try
            {
                using var polishMain = harness.CreateMain(dryRun: false);
                polishMain.IsAtMode = true;
                polishMain.IsEveryDay = true;
                polishMain.Days[6].IsSelected = false;
                Render(new MainWindow(polishMain), "main-days-pl.png");
            }
            finally
            {
                Localizer.Instance.Language = AppLanguage.English;
            }
        });
    }

    [Fact]
    public void RepeatText_RendersForEveryDayAndTheLongestList()
    {
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);

            RenderRepeating(AppLanguage.English, days: 7, "main-repeating-every-day.png");
            RenderRepeating(AppLanguage.English, days: 6, "main-repeating-six-days.png");
            RenderRepeating(AppLanguage.Polish, days: 6, "main-repeating-pl.png");
        });
    }

    [Fact]
    public void DayStrip_ShowsTheFocusRingAndTheMouseOver()
    {
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);

            using var harness = new TestHarness();
            using var main = harness.CreateMain(dryRun: true);
            main.IsAtMode = true;
            main.IsEveryDay = true;
            main.Days[6].IsSelected = false;

            // Keyboard focus as after Tab (#100): the ring must stay clear of the next day.
            var (dayRoot, wednesday) = LayOutAndFind(main, root => Day(root, "Wednesday"));
            VisualTree.ShowKeyboardFocus(wednesday);
            Save(dayRoot, "main-days-focus-day.png");

            var (everyDayRoot, everyDay) = LayOutAndFind(main, root => VisualTree.Descendants<CheckBox>(root).Single(box => Equals(box.Content, "Every day")));
            VisualTree.ShowKeyboardFocus(everyDay);
            Save(everyDayRoot, "main-days-focus-every-day.png");

            // The mouse over a chosen day: PrimaryHover, the hover state of Primary (UI-GUIDE 2).
            var (hoverRoot, chosen) = LayOutAndFind(main, root => Day(root, "Wednesday"));
            VisualTree.ShowMouseOver(chosen);
            var face = (Border)chosen.Template.FindName("Root", chosen);
            Assert.Equal(((SolidColorBrush)chosen.FindResource("PrimaryHoverBrush")).Color, ((SolidColorBrush)face.Background).Color);
            Save(hoverRoot, "main-days-hover.png");
        });
    }

    [Theory]
    [InlineData(1.5, "text150")]
    [InlineData(2.25, "text225")]
    public void LargerText_StillFits(double factor, string size)
    {
        // Issue #104: Windows' Text size at 150% and at its largest, in Polish, where words are longest.
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);
            var resources = Application.Current.Resources;
            Localizer.Instance.Language = AppLanguage.Polish;
            TextScale.Apply(resources, factor);
            try
            {
                // Polish is also the saved language, so opening the settings keeps it.
                using var harness = new TestHarness(AppSettings.Default with { Language = AppLanguage.Polish });
                harness.Localizer.Language = AppLanguage.Polish;
                using var shell = harness.CreateShell();
                using var main = harness.CreateMain(dryRun: true);

                main.IsAtMode = true;
                main.IsEveryDay = true;
                main.Days[6].IsSelected = false;
                Render(new MainWindow(main), $"main-days-{size}-pl.png");

                main.OpenSettingsCommand.Execute(null);
                Render(new MainWindow(main), $"settings-{size}-pl.png");
                main.CloseSettings();

                main.IsEveryDay = false;
                main.IsInMode = true;
                main.IsShutdownSelected = true;
                main.ForceCloseApps = true;
                Render(new MainWindow(main), $"main-{size}-pl.png");

                main.AmountText = "45";
                main.IsSleepSelected = true;
                main.StartCommand.Execute(null);
                Render(new MainWindow(main), $"main-running-{size}-pl.png");

                harness.AdvanceInTicks(TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(15));
                Assert.True(shell.IsWarningOpen);
                Render(new WarningWindow(shell.Warning), $"warning-{size}-pl.png");
            }
            finally
            {
                TextScale.Apply(resources, 1);
                Localizer.Instance.Language = AppLanguage.English;
            }
        });
    }

    [Fact]
    public void SoundChoice_RendersEveryState()
    {
        // Issue #113: the lullaby, no sound or a sound file, in the settings panel.
        wpf.Run(() =>
        {
            Directory.CreateDirectory(PreviewDirectory);
            const string missingFile = @"C:\Users\Mac\Music\Morning.mp3";

            using (var harness = new TestHarness())
            {
                using var main = harness.CreateMain(dryRun: true);
                main.OpenSettingsCommand.Execute(null);
                main.Settings!.Sound = EndSound.File;
                main.Settings.SoundFile = missingFile;
                Render(new MainWindow(main), "settings-sound-file.png");

                main.Settings.SaveCommand.Execute(null);
                Assert.NotNull(main.Settings.ErrorMessage);
                Render(new MainWindow(main), "settings-sound-invalid.png");
            }

            using (var harness = new TestHarness(AppSettings.Default with { Sound = EndSound.File, SoundFile = missingFile }))
            {
                using var main = harness.CreateMain(dryRun: true);
                main.OpenSettingsCommand.Execute(null);
                Assert.True(main.Settings!.IsSoundFileMissing);
                Render(new MainWindow(main), "settings-sound-missing.png");
            }

            // A file that exists but that Windows could not play last time.
            var broken = Path.Combine(Path.GetTempPath(), "Somnus.Tests", $"{Guid.NewGuid():N}.wma");
            Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
            File.WriteAllBytes(broken, [0]);
            try
            {
                using var harness = new TestHarness(AppSettings.Default with { Sound = EndSound.File, SoundFile = broken, FailedSoundFile = broken });
                using var main = harness.CreateMain(dryRun: true);
                main.OpenSettingsCommand.Execute(null);
                Assert.True(main.Settings!.IsSoundFileBroken);
                Render(new MainWindow(main), "settings-sound-broken.png");
            }
            finally
            {
                File.Delete(broken);
            }

            Localizer.Instance.Language = AppLanguage.Polish;
            try
            {
                using var harness = new TestHarness(AppSettings.Default with { Language = AppLanguage.Polish });
                harness.Localizer.Language = AppLanguage.Polish;
                using var main = harness.CreateMain(dryRun: true);
                main.OpenSettingsCommand.Execute(null);
                main.Settings!.Sound = EndSound.File;
                main.Settings.SoundFile = missingFile;
                Render(new MainWindow(main), "settings-sound-file-pl.png");
            }
            finally
            {
                Localizer.Instance.Language = AppLanguage.English;
            }
        });
    }

    [Fact]
    public void ConfirmDialog_AsksOnlyInsideTheWindow()
    {
        wpf.Run(() =>
        {
            var request = new ConfirmationRequest("Restart the countdown?", "The running countdown will be replaced.", "Restart", "Keep current");

            var dialog = new ConfirmDialog(request);

            // Mac: the question shows once, inside the dialog, not again in the title bar.
            Assert.Equal(string.Empty, dialog.Title);
            Assert.Equal(request.Title, AutomationProperties.GetName(dialog));
        });
    }

    /// <summary>Starts a countdown that repeats on the first <paramref name="days"/> days and renders it.</summary>
    private static void RenderRepeating(AppLanguage language, int days, string fileName)
    {
        Localizer.Instance.Language = language;
        try
        {
            using var harness = new TestHarness();
            harness.Localizer.Language = language;
            using var main = harness.CreateMain(dryRun: true);
            main.IsAtMode = true;
            for (var index = 0; index < main.Days.Count; index++)
            {
                main.Days[index].IsSelected = index < days;
            }

            main.StartCommand.Execute(null);
            Assert.NotNull(main.RepeatText);
            Render(new MainWindow(main), fileName);
        }
        finally
        {
            Localizer.Instance.Language = AppLanguage.English;
        }
    }

    /// <summary>Lays out the main window inside an adorner layer and returns it with one of its elements.</summary>
    private static (FrameworkElement Root, T Element) LayOutAndFind<T>(MainViewModel main, Func<FrameworkElement, T> find)
    {
        var window = new MainWindow(main);
        var content = (UIElement)window.Content;
        window.Content = null;
        window.Content = new AdornerDecorator { Child = content };
        var root = OffscreenRenderer.LayOut(window);
        return (root, find(root));
    }

    private static ToggleButton Day(FrameworkElement root, string fullName) =>
        VisualTree.Descendants<ToggleButton>(root).Single(toggle => AutomationProperties.GetName(toggle) == fullName);

    /// <summary>
    /// Lays out the content of <paramref name="window"/> without showing it and saves it as a PNG.
    /// </summary>
    private static void Render(Window window, string fileName) => Save(OffscreenRenderer.LayOut(window), fileName);

    /// <summary>Saves a laid-out window as a PNG.</summary>
    private static void Save(FrameworkElement root, string fileName)
    {
        root.UpdateLayout();
        Assert.True(root.ActualHeight > 80, $"{fileName} rendered only {root.ActualHeight} px high.");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(OffscreenRenderer.Render(root)));
        using var file = File.Create(Path.Combine(PreviewDirectory, fileName));
        encoder.Save(file);
    }
}
