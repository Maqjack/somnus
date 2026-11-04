using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Somnus.App.Diagnostics;

/// <summary>
/// Lays out and renders what a window shows without ever showing the window, for the self-test and
/// the smoke tests. The screen is never captured.
/// </summary>
internal static class OffscreenRenderer
{
    private const double DefaultWindowWidth = 380;
    private const double WindowFrameWidth = 16;
    private const double ScreenDpi = 96;

    /// <summary>Moves the content of <paramref name="window"/> into a root laid out off screen.</summary>
    /// <param name="window">The window whose content is laid out.</param>
    /// <returns>The laid-out root that now holds the content.</returns>
    public static FrameworkElement LayOut(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var content = (UIElement)window.Content;
        var width = (double.IsNaN(window.Width) ? DefaultWindowWidth : window.Width) - WindowFrameWidth;
        window.Content = null;

        var root = new Border { Background = window.Background, Child = content, DataContext = window.DataContext, Width = width };
        TextElement.SetForeground(root, window.Foreground);
        TextElement.SetFontFamily(root, window.FontFamily);
        TextElement.SetFontSize(root, window.FontSize);
        root.Measure(new Size(width, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
        return root;
    }

    /// <summary>Renders laid-out content to a bitmap.</summary>
    /// <param name="root">The result of <see cref="LayOut"/>.</param>
    /// <returns>The picture of the content.</returns>
    public static BitmapSource Render(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), ScreenDpi, ScreenDpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }
}
