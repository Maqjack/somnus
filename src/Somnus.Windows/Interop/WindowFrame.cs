namespace Somnus.Windows.Interop;

/// <summary>
/// Styles the native frame of a window through the Desktop Window Manager.
/// </summary>
public static class WindowFrame
{
    private const int UseImmersiveDarkModeAttribute = 20;
    private const int CaptionColorAttribute = 35;

    /// <summary>
    /// Turns on the dark title bar (Windows 10 20H1 and later) and, on Windows 11, paints the caption
    /// in the given colour. This is cosmetic: Windows versions without these attributes simply keep
    /// their default frame, so the results are not checked.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    /// <param name="red">Red component of the caption colour.</param>
    /// <param name="green">Green component of the caption colour.</param>
    /// <param name="blue">Blue component of the caption colour.</param>
    public static void UseDarkFrame(nint windowHandle, byte red, byte green, byte blue)
    {
        if (windowHandle == 0)
        {
            return;
        }

        var enabled = 1;
        _ = NativeMethods.DwmSetWindowAttribute(windowHandle, UseImmersiveDarkModeAttribute, in enabled, sizeof(int));

        // COLORREF is 0x00BBGGRR.
        var colorRef = red | (green << 8) | (blue << 16);
        _ = NativeMethods.DwmSetWindowAttribute(windowHandle, CaptionColorAttribute, in colorRef, sizeof(int));
    }
}
