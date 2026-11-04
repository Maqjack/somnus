namespace Somnus.Presentation.Services;

/// <summary>
/// Runs work on the UI thread. The countdown reports changes from timer threads; view models use
/// this to update their properties where the UI can see them.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread and returns without waiting.
    /// </summary>
    /// <param name="action">The work to run.</param>
    void Post(Action action);
}
