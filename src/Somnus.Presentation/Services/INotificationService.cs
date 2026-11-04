namespace Somnus.Presentation.Services;

/// <summary>
/// Shows short, non-blocking notifications (a balloon from the tray icon).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a notification.
    /// </summary>
    /// <param name="title">A short heading.</param>
    /// <param name="message">One or two sentences.</param>
    void Notify(string title, string message);
}
