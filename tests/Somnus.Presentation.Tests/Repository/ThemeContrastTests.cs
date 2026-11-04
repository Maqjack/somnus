using System.Globalization;
using System.Xml.Linq;

namespace Somnus.Presentation.Tests.Repository;

/// <summary>
/// Rule R-03 and SCOPE section 6: text contrast at least 4.5:1 (WCAG AA), and input borders and the
/// focus ring at least 3:1 against what surrounds them (WCAG 1.4.11). Colours are read from the theme
/// file itself, so the test always checks what ships.
/// </summary>
public sealed class ThemeContrastTests
{
    private static readonly Dictionary<string, (double R, double G, double B)> Palette = LoadPalette();

    [Theory]
    [InlineData("TextColor", "BgColor")]
    [InlineData("TextColor", "SurfaceColor")]
    [InlineData("TextColor", "SurfaceInputColor")]
    [InlineData("TextMutedColor", "BgColor")]
    [InlineData("TextMutedColor", "SurfaceColor")]
    [InlineData("TextMutedColor", "SurfaceInputColor")]
    [InlineData("TextDisabledColor", "BgColor")]
    [InlineData("WarningColor", "BgColor")]
    [InlineData("WarningColor", "SurfaceColor")]
    [InlineData("AccentColor", "BgColor")]
    [InlineData("AccentColor", "SurfaceColor")]
    [InlineData("OnPrimaryColor", "PrimaryColor")]
    [InlineData("OnPrimaryColor", "PrimaryHoverColor")]
    public void Text_HasAtLeastAaContrast(string foreground, string background)
    {
        Assert.True(Contrast(foreground, background) >= 4.5, $"{foreground} on {background}: {Contrast(foreground, background):0.00}:1");
    }

    [Theory]
    [InlineData("InputBorderColor", "BgColor")]
    [InlineData("InputBorderColor", "SurfaceColor")]
    [InlineData("AccentColor", "SurfaceInputColor")]
    [InlineData("AccentColor", "PrimaryColor")]
    public void ControlBoundariesAndFocusRing_HaveAtLeastThreeToOne(string foreground, string background)
    {
        Assert.True(Contrast(foreground, background) >= 3.0, $"{foreground} on {background}: {Contrast(foreground, background):0.00}:1");
    }

    [Fact]
    public void Palette_MatchesThePlan()
    {
        // The colours Mac approved for Somnus.
        Assert.Equal((0x1A, 0x0F, 0x2E), Rgb("BgColor"));
        Assert.Equal((0x7C, 0x3A, 0xED), Rgb("PrimaryColor"));
        Assert.Equal((0xC4, 0xB5, 0xFD), Rgb("AccentColor"));
        Assert.Equal((0xF3, 0xEE, 0xFF), Rgb("TextColor"));
    }

    private static double Contrast(string foreground, string background)
    {
        var lighter = Math.Max(Luminance(foreground), Luminance(background));
        var darker = Math.Min(Luminance(foreground), Luminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(string key)
    {
        var (r, g, b) = Palette[key];
        return (0.2126 * Linear(r)) + (0.7152 * Linear(g)) + (0.0722 * Linear(b));
    }

    private static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static (int R, int G, int B) Rgb(string key)
    {
        var (r, g, b) = Palette[key];
        return ((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
    }

    private static Dictionary<string, (double R, double G, double B)> LoadPalette()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(RepositoryFiles.PathOf("src/Somnus.App/Theme/Colors.xaml"));

        return document.Root!.Elements()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(
                element => element.Attribute(xaml + "Key")!.Value,
                element => Parse(element.Value.Trim()));
    }

    private static (double R, double G, double B) Parse(string hex)
    {
        var value = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
    }
}
