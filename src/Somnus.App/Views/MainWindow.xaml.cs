using System.Windows;
using System.Windows.Threading;
using Somnus.Presentation.ViewModels;

namespace Somnus.App.Views;

/// <summary>
/// The main window, with the settings panel inside it. All behaviour lives in <see cref="MainViewModel"/>.
/// </summary>
internal sealed partial class MainWindow
{
    /// <summary>Initializes the window for its view model.</summary>
    /// <param name="viewModel">The view model.</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheme.Apply(this);

        // Hibernation may have been turned on or off while Somnus was in the background.
        Activated += (_, _) => viewModel.RefreshCapabilities();

        // Keyboard focus follows the settings panel, so Enter saves and Esc goes back while it is open.
        SettingsPanel.IsVisibleChanged += (_, e) => Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () => (e.NewValue is true ? LanguageBox : (UIElement)SettingsButton).Focus());
    }
}
