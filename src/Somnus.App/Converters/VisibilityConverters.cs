using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Somnus.App.Converters;

/// <summary>
/// <see langword="true"/> → Collapsed, <see langword="false"/> → Visible.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
internal sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One-way converter.");
}

/// <summary>
/// <see langword="true"/> → Hidden (keeps its space), <see langword="false"/> → Visible.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
internal sealed class BooleanToHiddenConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Hidden : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One-way converter.");
}

/// <summary>
/// An empty or missing text → Collapsed, any other text → Visible.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
internal sealed class TextToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One-way converter.");
}
