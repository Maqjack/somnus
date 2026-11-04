using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Somnus.App.Behaviors;

/// <summary>
/// Makes a number box easy to type over (#64): entering the box, by Tab or by a click, selects its
/// value, so the next digit replaces it; and a digit that would make the value too long, or larger
/// than <see cref="MaximumProperty"/>, starts the value over. Typing 2, 2, 2 into the hour gives 2,
/// 22, 2; typing 3, 3 gives 3, 3, because 33 is no hour.
/// </summary>
internal static class TypeOver
{
    /// <summary>Turns type-over on or off.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(TypeOver),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>The largest value the box takes, for example 23 for the hour; -1 for no limit.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached(
        "Maximum",
        typeof(int),
        typeof(TypeOver),
        new PropertyMetadata(-1));

    /// <summary>Gets whether type-over is on.</summary>
    /// <param name="element">The text box.</param>
    /// <returns><see langword="true"/> if type-over is on.</returns>
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>Sets whether type-over is on.</summary>
    /// <param name="element">The text box.</param>
    /// <param name="value"><see langword="true"/> to turn type-over on.</param>
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>Gets the largest value the box takes.</summary>
    /// <param name="element">The text box.</param>
    /// <returns>The maximum, or -1 for none.</returns>
    public static int GetMaximum(DependencyObject element) => (int)element.GetValue(MaximumProperty);

    /// <summary>Sets the largest value the box takes.</summary>
    /// <param name="element">The text box.</param>
    /// <param name="value">The maximum, or -1 for none.</param>
    public static void SetMaximum(DependencyObject element, int value) => element.SetValue(MaximumProperty, value);

    /// <summary>
    /// Decides whether typed digits start the value over.
    /// </summary>
    /// <param name="text">The value in the box.</param>
    /// <param name="selectionStart">Where the selection or caret is.</param>
    /// <param name="selectionLength">How much is selected.</param>
    /// <param name="input">The typed digits.</param>
    /// <param name="maxLength">The longest value the box takes; 0 for no limit.</param>
    /// <param name="maximum">The largest value the box takes; -1 for no limit.</param>
    /// <returns>The new value if typing starts over, or <see langword="null"/> to let the box insert as usual.</returns>
    internal static string? StartOver(string text, int selectionStart, int selectionLength, string input, int maxLength, int maximum)
    {
        if (input.Length == 0 || !input.All(char.IsAsciiDigit))
        {
            return null;
        }

        var proposed = text.Remove(selectionStart, selectionLength).Insert(selectionStart, input);
        var tooLong = maxLength > 0 && proposed.Length > maxLength;
        var tooLarge = maximum >= 0
            && int.TryParse(proposed, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value > maximum;
        return tooLong || tooLarge ? input : null;
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
            textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            textBox.PreviewTextInput += OnPreviewTextInput;
        }
        else
        {
            textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
            textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            textBox.PreviewTextInput -= OnPreviewTextInput;
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ((TextBox)sender).SelectAll();

    // A click into an unfocused box selects the value instead of placing the caret.
    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (!textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            e.Handled = true;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (StartOver(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, e.Text, textBox.MaxLength, GetMaximum(textBox)) is { } text)
        {
            textBox.Text = text;
            textBox.CaretIndex = text.Length;
            e.Handled = true;
        }
    }
}
