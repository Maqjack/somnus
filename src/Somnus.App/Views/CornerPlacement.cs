using System.Windows;

namespace Somnus.App.Views;

/// <summary>
/// Places a window in the bottom-right corner of a work area, the way Windows shows notifications,
/// so the warning does not cover the middle of the screen (#67).
/// </summary>
internal static class CornerPlacement
{
    /// <summary>The gap between the window and the edges of the work area, in device-independent pixels.</summary>
    public const double Gap = 16;

    /// <summary>
    /// Returns the top-left position that puts a window of <paramref name="size"/> into the bottom-right
    /// corner of <paramref name="workArea"/>, which leaves out the taskbar.
    /// </summary>
    /// <param name="workArea">The usable part of the screen.</param>
    /// <param name="size">The size of the window.</param>
    /// <returns>The position for <see cref="Window.Left"/> and <see cref="Window.Top"/>.</returns>
    public static Point BottomRight(Rect workArea, Size size) =>
        new(
            Math.Max(workArea.Left, workArea.Right - size.Width - Gap),
            Math.Max(workArea.Top, workArea.Bottom - size.Height - Gap));
}
