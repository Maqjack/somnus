using System.IO;
using System.Windows;
using Microsoft.Win32;
using Somnus.App.Views;
using Somnus.Presentation.Services;

namespace Somnus.App.Services;

/// <summary>
/// Shows the themed dialogs, and the Windows file dialog, over the window the user is looking at.
/// </summary>
internal sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public bool Confirm(ConfirmationRequest request) => Show(new ConfirmDialog(request));

    /// <inheritdoc />
    public void ShowError(string heading, string message) => Show(new MessageDialog(heading, message));

    /// <inheritdoc />
    public string? PickFile(FilePickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var patterns = string.Join(';', request.Extensions.Select(extension => "*" + extension));
        var dialog = new OpenFileDialog
        {
            Title = request.Title,
            Filter = $"{request.FilterName} ({patterns})|{patterns}",
            CheckFileExists = true,
        };

        // The dialog opens where the file chosen so far is.
        var folder = Path.GetDirectoryName(request.Current);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
            dialog.FileName = Path.GetFileName(request.Current);
        }

        var owner = FindOwner(except: null);
        var picked = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return picked == true ? dialog.FileName : null;
    }

    private static bool Show(Window dialog)
    {
        var owner = FindOwner(except: dialog);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }

        return dialog.ShowDialog() == true;
    }

    private static Window? FindOwner(Window? except) =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive && window.IsVisible && window != except)
        ?? (Application.Current.MainWindow is { IsVisible: true } main ? main : null);
}
