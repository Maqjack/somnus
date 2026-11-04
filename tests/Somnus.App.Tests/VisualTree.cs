using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Somnus.App.Tests;

/// <summary>
/// Finds elements in a laid-out window and shows states that need a mouse or a keyboard, which a
/// window rendered off screen never gets.
/// </summary>
internal static class VisualTree
{
    /// <summary>Returns every descendant of <paramref name="parent"/> of type <typeparamref name="T"/>.</summary>
    public static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Applies the setters of the template triggers that would fire under the mouse, as WPF does when
    /// the pointer is over <paramref name="control"/>.
    /// </summary>
    public static void ShowMouseOver(Control control)
    {
        foreach (var trigger in control.Template.Triggers)
        {
            var (fires, setters) = trigger switch
            {
                Trigger single => (Holds(control, single.Property, single.Value), single.Setters),
                MultiTrigger multi => (multi.Conditions.All(condition => Holds(control, condition.Property, condition.Value)), multi.Setters),
                _ => (false, new SetterBaseCollection()),
            };

            foreach (var setter in setters.OfType<Setter>().Where(_ => fires))
            {
                var target = setter.TargetName is null ? control : (DependencyObject)control.Template.FindName(setter.TargetName, control);
                target.SetValue(setter.Property, setter.Value);
            }
        }
    }

    /// <summary>Draws the keyboard focus ring of <paramref name="element"/>, as WPF does after Tab.</summary>
    /// <remarks>The element must sit inside an <see cref="AdornerDecorator"/>.</remarks>
    public static void ShowKeyboardFocus(FrameworkElement element)
    {
        var layer = AdornerLayer.GetAdornerLayer(element) ?? throw new InvalidOperationException("The element has no adorner layer.");
        layer.Add(new FocusRingAdorner(element, element.FocusVisualStyle));
    }

    private static bool Holds(Control control, DependencyProperty property, object value) =>
        property == UIElement.IsMouseOverProperty ? Equals(value, true) : Equals(control.GetValue(property), value);

    /// <summary>Hosts the focus visual style over the element, like the adorner WPF itself uses.</summary>
    private sealed class FocusRingAdorner : Adorner
    {
        private readonly Control _ring;

        public FocusRingAdorner(UIElement adorned, Style style)
            : base(adorned)
        {
            _ring = new Control { Style = style, Focusable = false, IsHitTestVisible = false };
            AddVisualChild(_ring);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _ring;

        protected override Size MeasureOverride(Size constraint)
        {
            _ring.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _ring.Arrange(new Rect(AdornedElement.RenderSize));
            return finalSize;
        }
    }
}
