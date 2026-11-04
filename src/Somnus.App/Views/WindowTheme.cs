using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Somnus.Windows.Interop;

namespace Somnus.App.Views;

/// <summary>
/// Gives a window the dark, purple title bar of the Somnus theme.
/// </summary>
internal static class WindowTheme
{
    /// <summary>
    /// Applies the theme to the native frame of <paramref name="window"/>; call it from
    /// <see cref="Window.SourceInitialized"/>.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var caption = (Color)Application.Current.FindResource("BgColor");
        WindowFrame.UseDarkFrame(handle, caption.R, caption.G, caption.B);
    }
}
