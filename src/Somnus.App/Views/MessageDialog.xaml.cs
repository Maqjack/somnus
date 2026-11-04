using System.Windows;

namespace Somnus.App.Views;

/// <summary>
/// A themed message with one OK button. The text can be selected, so a log path can be copied.
/// </summary>
internal sealed partial class MessageDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="heading">A short heading.</param>
    /// <param name="message">The message.</param>
    public MessageDialog(string heading, string message)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        MessageText.Text = message;
        SourceInitialized += (_, _) => WindowTheme.Apply(this);
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
