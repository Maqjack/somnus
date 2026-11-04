using System.Windows;
using System.Windows.Automation;
using Somnus.Presentation.Services;

namespace Somnus.App.Views;

/// <summary>
/// A themed yes/no question. The cancelling answer is the default button. The question shows once,
/// inside the dialog; the title bar stays empty and screen readers get the question as the name.
/// </summary>
internal sealed partial class ConfirmDialog
{
    /// <summary>Initializes the dialog for a question.</summary>
    /// <param name="request">The texts to show.</param>
    public ConfirmDialog(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InitializeComponent();
        DataContext = request;
        AutomationProperties.SetName(this, request.Title);
        SourceInitialized += (_, _) => WindowTheme.Apply(this);
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
