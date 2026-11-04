namespace Somnus.App.Tests;

/// <summary>
/// Rule R-01: the tests load the app's XAML but must never start Somnus itself, which would open
/// its window and wire up the real power controller.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class NoRealAppTests(WpfFixture wpf)
{
    [Fact]
    public void TheRealAppNeverStarts()
    {
        wpf.Run(() => { });

        Assert.False(wpf.StartupRan);
    }
}
