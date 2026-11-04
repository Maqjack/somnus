using System.Windows;

namespace Somnus.App.Behaviors;

/// <summary>
/// Makes Somnus follow Windows' Text size (#104, ADR 0012): the font size tokens of the theme grow by
/// the same factor, and every width set through <see cref="WidthProperty"/> grows with them, so longer
/// text still fits. Margins, the logo and the signature keep their size; the signature is decoration
/// and would otherwise run into the buttons above it.
/// </summary>
internal static class TextScale
{
    /// <summary>A width at 100% text; the element gets this width times <see cref="Factor"/>.</summary>
    public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached(
        "Width",
        typeof(double),
        typeof(TextScale),
        new PropertyMetadata(double.NaN, OnWidthChanged));

    /// <summary>The font size tokens of Colors.xaml that hold text; the views use them as dynamic resources.</summary>
    private static readonly string[] FontSizeKeys =
        ["FontSizeBody", "FontSizeSmall", "FontSizeTitle", "FontSizeCountdown", "FontSizeIcon"];

    private static readonly List<WeakReference<FrameworkElement>> ScaledElements = [];

    /// <summary>Gets the text size in effect: 1 for 100%.</summary>
    public static double Factor { get; private set; } = 1;

    /// <summary>Gets the width of <paramref name="element"/> at 100% text.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The width, or NaN when it does not scale.</returns>
    public static double GetWidth(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (double)element.GetValue(WidthProperty);
    }

    /// <summary>Sets the width of <paramref name="element"/> at 100% text.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The width at 100% text.</param>
    public static void SetWidth(FrameworkElement element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Scales the font sizes in <paramref name="resources"/> and every scaled width by <paramref name="factor"/>.
    /// </summary>
    /// <param name="resources">The application's resources, which hold the theme.</param>
    /// <param name="factor">The text size: 1 for 100%.</param>
    public static void Apply(ResourceDictionary resources, double factor)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Factor = factor;
        foreach (var key in FontSizeKeys)
        {
            // The theme keeps the size at 100%; the scaled size sits on top of it.
            resources.Remove(key);
            resources[key] = (double)resources[key] * factor;
        }

        ScaledElements.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in ScaledElements)
        {
            if (reference.TryGetTarget(out var element))
            {
                Resize(element);
            }
        }
    }

    private static void OnWidthChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is FrameworkElement element)
        {
            ScaledElements.Add(new WeakReference<FrameworkElement>(element));
            Resize(element);
        }
    }

    private static void Resize(FrameworkElement element) => element.Width = GetWidth(element) * Factor;
}
