using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Somnus.App.Behaviors;

/// <summary>
/// Lets a text box accept only the digits 0–9, whether typed or pasted. Validation still happens
/// in the view model; this only keeps obvious mistakes out of the box.
/// </summary>
internal static class DigitsOnly
{
    /// <summary>Turns digit filtering on or off.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(DigitsOnly),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>Gets whether digit filtering is on.</summary>
    /// <param name="element">The text box.</param>
    /// <returns><see langword="true"/> if only digits are accepted.</returns>
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>Sets whether digit filtering is on.</summary>
    /// <param name="element">The text box.</param>
    /// <param name="value"><see langword="true"/> to accept only digits.</param>
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !IsDigits(e.Text);

    // Space does not raise PreviewTextInput in a text box, so it is filtered here.
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = e.Key == Key.Space;

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)
            ? e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string
            : null;

        if (text is null || !IsDigits(text.Trim()))
        {
            e.CancelCommand();
        }
    }
}
