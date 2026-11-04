using System.Windows;
using Somnus.App.Views;

namespace Somnus.App.Tests;

/// <summary>Issue #67: the warning opens in the bottom-right corner, like a notification.</summary>
public sealed class CornerPlacementTests
{
    [Fact]
    public void BottomRight_SitsAboveTheTaskbarWithAGap()
    {
        // A 1920 x 1080 screen with a 40-pixel taskbar at the bottom.
        var position = CornerPlacement.BottomRight(new Rect(0, 0, 1920, 1040), new Size(360, 300));

        Assert.Equal(new Point(1920 - 360 - CornerPlacement.Gap, 1040 - 300 - CornerPlacement.Gap), position);
    }

    [Fact]
    public void BottomRight_FollowsATaskbarOnTheRightOrTheTop()
    {
        var right = CornerPlacement.BottomRight(new Rect(0, 0, 1860, 1080), new Size(360, 300));
        var top = CornerPlacement.BottomRight(new Rect(0, 48, 1920, 1032), new Size(360, 300));

        Assert.Equal(1860 - 360 - CornerPlacement.Gap, right.X);
        Assert.Equal(48 + 1032 - 300 - CornerPlacement.Gap, top.Y);
    }

    [Fact]
    public void BottomRight_NeverLeavesTheWorkArea()
    {
        // A window larger than the work area stays at its top-left corner instead of off screen.
        var position = CornerPlacement.BottomRight(new Rect(100, 50, 300, 200), new Size(400, 300));

        Assert.Equal(new Point(100, 50), position);
    }
}
