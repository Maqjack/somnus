namespace Somnus.Presentation.Services;

/// <summary>
/// Shows modal dialogs in the Somnus theme.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Asks the user to confirm an action.
    /// </summary>
    /// <param name="request">The texts of the question and its two answers.</param>
    /// <returns><see langword="true"/> if the user confirmed.</returns>
    bool Confirm(ConfirmationRequest request);

    /// <summary>
    /// Tells the user that something went wrong, in words they can act on.
    /// </summary>
    /// <param name="heading">A short heading.</param>
    /// <param name="message">What happened and what the user can do; technical details belong in the log.</param>
    void ShowError(string heading, string message);

    /// <summary>Lets the user pick a file in the Windows file dialog.</summary>
    /// <param name="request">The texts of the dialog and the file types it offers.</param>
    /// <returns>The full path, or <see langword="null"/> when the user closed the dialog.</returns>
    string? PickFile(FilePickRequest request);
}

/// <summary>
/// The texts of a confirmation dialog, already localized.
/// </summary>
/// <param name="Title">The question, shown as the heading.</param>
/// <param name="Message">What happens if the user confirms.</param>
/// <param name="ConfirmText">The label of the confirming button.</param>
/// <param name="CancelText">The label of the button that keeps things as they are; it is the default.</param>
public sealed record ConfirmationRequest(string Title, string Message, string ConfirmText, string CancelText);

/// <summary>
/// A file to pick: the title of the dialog, the name of the file types, their extensions and the
/// file chosen so far, which opens its folder.
/// </summary>
/// <param name="Title">The title of the dialog.</param>
/// <param name="FilterName">The name of the file types, such as "Sound files".</param>
/// <param name="Extensions">The extensions with their dots, such as ".mp3".</param>
/// <param name="Current">The file chosen so far, or an empty text.</param>
public sealed record FilePickRequest(string Title, string FilterName, IReadOnlyList<string> Extensions, string Current);
