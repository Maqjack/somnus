using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Somnus.Presentation.ViewModels;

namespace Somnus.App.Views;

/// <summary>
/// The 60-second warning before the action, in the bottom-right corner of the screen like a
/// notification (#67). It has no close button: Minimize puts it on the taskbar while the countdown
/// goes on, and Stop or Esc stops the countdown. Alt+F4 also stops it, because the safe reading of
/// "make it go away" is "do not do it".
/// </summary>
internal sealed partial class WarningWindow
{
    private readonly WarningViewModel _viewModel;
    private bool _closingByShell;

    /// <summary>Initializes the window for its view model.</summary>
    /// <param name="viewModel">The view model.</param>
    public WarningWindow(WarningViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheme.Apply(this);

        // The height follows the content, so the corner is known only after layout.
        Loaded += (_, _) => PlaceInCorner();
        SizeChanged += (_, _) => PlaceInCorner();
    }

    /// <summary>Closes the window because the countdown left the warning window.</summary>
    public void CloseByShell()
    {
        _closingByShell = true;
        Close();
    }

    /// <summary>Puts the warning on the taskbar; the countdown goes on.</summary>
    internal void Minimize() => WindowState = WindowState.Minimized;

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingByShell)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        base.OnClosing(e);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => Minimize();

    /// <summary>Without a title bar, the window moves by its background.</summary>
    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PlaceInCorner()
    {
        var position = CornerPlacement.BottomRight(SystemParameters.WorkArea, new Size(ActualWidth, ActualHeight));
        Left = position.X;
        Top = position.Y;
    }
}
