using System.ComponentModel;
using System.Windows;
using Somnus.Presentation.Services;
using Somnus.Presentation.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;

namespace Somnus.App.Shell;

/// <summary>
/// The crescent in the notification area (ADR 0006): tooltip with the remaining time, a menu with
/// Edit, Cancel and Exit, a left click to bring the window back, and balloon notifications.
/// </summary>
internal sealed class TrayIcon : INotificationService, IDisposable
{
    private const int NotificationTimeoutMilliseconds = 5000;

    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _cancelItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private ShellViewModel? _shell;

    /// <summary>Initializes a hidden tray icon; <see cref="Attach"/> shows it.</summary>
    public TrayIcon()
    {
        var palette = new TrayMenuPalette();
        _showItem = new Forms.ToolStripMenuItem { Font = new Drawing.Font(Forms.Control.DefaultFont, Drawing.FontStyle.Bold) };
        _cancelItem = new Forms.ToolStripMenuItem();
        _exitItem = new Forms.ToolStripMenuItem();
        _menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(palette),
            ShowImageMargin = false,
            ForeColor = palette.Text,
        };
        _menu.Items.AddRange([_showItem, _cancelItem, new Forms.ToolStripSeparator(), _exitItem]);

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Visible = false,
        };
    }

    /// <summary>
    /// Connects the icon to the shell view model and shows it.
    /// </summary>
    /// <param name="shell">Supplies texts, state and commands.</param>
    public void Attach(ShellViewModel shell)
    {
        _shell = shell;
        _showItem.Click += (_, _) => shell.ShowMainWindowCommand.Execute(null);
        _cancelItem.Click += (_, _) => shell.CancelCountdownCommand.Execute(null);
        _exitItem.Click += (_, _) => shell.ExitCommand.Execute(null);
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                shell.ShowMainWindowCommand.Execute(null);
            }
        };
        _icon.BalloonTipClicked += (_, _) => shell.ShowMainWindowCommand.Execute(null);
        shell.PropertyChanged += OnShellPropertyChanged;

        Refresh();
        _icon.Visible = true;
    }

    /// <inheritdoc />
    public void Notify(string title, string message) =>
        _icon.ShowBalloonTip(NotificationTimeoutMilliseconds, title, message, Forms.ToolTipIcon.None);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        // Hide first, or the icon lingers in the tray until the mouse passes over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Somnus;component/Assets/Somnus.ico"))
            ?? throw new InvalidOperationException("The Somnus icon is missing from the app resources.");
        using var stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_shell is null)
        {
            return;
        }

        // The tooltip of a notify icon is limited to 127 characters.
        var toolTip = _shell.TrayToolTip;
        _icon.Text = toolTip.Length > 127 ? toolTip[..127] : toolTip;
        _showItem.Text = _shell.ShowMenuText;
        _cancelItem.Text = _shell.CancelMenuText;
        _cancelItem.Enabled = _shell.IsCountdownActive;
        _exitItem.Text = _shell.ExitMenuText;
    }

    /// <summary>
    /// Colours of the tray menu, taken from the WPF theme so they stay in one place (rule R-02).
    /// </summary>
    private sealed class TrayMenuPalette : Forms.ProfessionalColorTable
    {
        public TrayMenuPalette()
        {
            Background = FromTheme("SurfaceColor");
            Highlight = FromTheme("SurfaceInputColor");
            Border = FromTheme("BorderColor");
            Text = FromTheme("TextColor");
            UseSystemColors = false;
        }

        public Drawing.Color Text { get; }

        private Drawing.Color Background { get; }

        private Drawing.Color Highlight { get; }

        private Drawing.Color Border { get; }

        public override Drawing.Color ToolStripDropDownBackground => Background;

        public override Drawing.Color ImageMarginGradientBegin => Background;

        public override Drawing.Color ImageMarginGradientMiddle => Background;

        public override Drawing.Color ImageMarginGradientEnd => Background;

        public override Drawing.Color MenuBorder => Border;

        public override Drawing.Color MenuItemBorder => Highlight;

        public override Drawing.Color MenuItemSelected => Highlight;

        public override Drawing.Color MenuItemSelectedGradientBegin => Highlight;

        public override Drawing.Color MenuItemSelectedGradientEnd => Highlight;

        public override Drawing.Color SeparatorDark => Border;

        public override Drawing.Color SeparatorLight => Background;

        private static Drawing.Color FromTheme(string key)
        {
            var color = (Media.Color)Application.Current.FindResource(key);
            return Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
