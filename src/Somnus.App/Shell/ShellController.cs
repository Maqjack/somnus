using System.ComponentModel;
using System.Windows;
using Somnus.App.Views;
using Somnus.Presentation.ViewModels;

namespace Somnus.App.Shell;

/// <summary>
/// Connects the windows and the tray icon to the view models: hides the main window to the tray
/// when a countdown starts, opens and closes the warning window, and exits on request.
/// </summary>
internal sealed class ShellController : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _main;
    private readonly ShellViewModel _shell;
    private readonly TrayIcon _trayIcon;
    private WarningWindow? _warningWindow;
    private bool _exiting;

    /// <summary>
    /// Initializes the controller.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <param name="main">Its view model.</param>
    /// <param name="shell">The view model of the tray and the warning window; it also decides whether closing hides or exits.</param>
    /// <param name="trayIcon">The tray icon.</param>
    public ShellController(MainWindow mainWindow, MainViewModel main, ShellViewModel shell, TrayIcon trayIcon)
    {
        _mainWindow = mainWindow;
        _main = main;
        _shell = shell;
        _trayIcon = trayIcon;
    }

    /// <summary>Shows the tray icon and, unless Windows started Somnus at sign-in, the main window.</summary>
    /// <param name="showMainWindow">Whether the main window opens; it stays in the tray otherwise (#83).</param>
    public void Start(bool showMainWindow)
    {
        _trayIcon.Attach(_shell);
        _main.CountdownStarted += OnCountdownStarted;
        _shell.ShowMainWindowRequested += OnShowMainWindowRequested;
        _shell.ExitRequested += OnExitRequested;
        _shell.PropertyChanged += OnShellPropertyChanged;
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.Closed += OnMainWindowClosed;

        Application.Current.MainWindow = _mainWindow;
        if (showMainWindow)
        {
            _mainWindow.Show();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _main.CountdownStarted -= OnCountdownStarted;
        _shell.ShowMainWindowRequested -= OnShowMainWindowRequested;
        _shell.ExitRequested -= OnExitRequested;
        _shell.PropertyChanged -= OnShellPropertyChanged;
        _mainWindow.Closing -= OnMainWindowClosing;
        _mainWindow.Closed -= OnMainWindowClosed;
    }

    private void OnCountdownStarted(object? sender, EventArgs e)
    {
        _mainWindow.Hide();
        _shell.NotifyHiddenInTray();
    }

    private void OnShowMainWindowRequested(object? sender, EventArgs e)
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void OnExitRequested(object? sender, EventArgs e) => Exit();

    private void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _warningWindow?.CloseByShell();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Closing the window ends an open settings panel without saving (#54), then hides the window to
    /// the tray while a countdown runs (assumption A5, decided by <see cref="ShellViewModel.HideOnClose"/>);
    /// otherwise the window closes and Somnus exits.
    /// </summary>
    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        _main.CloseSettings();
        if (_exiting || !_shell.HideOnClose())
        {
            return;
        }

        e.Cancel = true;
        _mainWindow.Hide();
    }

    private void OnMainWindowClosed(object? sender, EventArgs e) => Exit();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.IsWarningOpen))
        {
            return;
        }

        if (_shell.IsWarningOpen && _warningWindow is null)
        {
            _warningWindow = new WarningWindow(_shell.Warning);
            _warningWindow.Closed += (_, _) => _warningWindow = null;
            _warningWindow.Show();
            _warningWindow.Activate();
        }
        else if (!_shell.IsWarningOpen && _warningWindow is not null)
        {
            _warningWindow.CloseByShell();
        }
    }
}
