using System.Windows.Threading;
using Somnus.Presentation.Services;

namespace Somnus.App.Services;

/// <summary>
/// Queues work on the WPF dispatcher of the UI thread.
/// </summary>
internal sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => dispatcher.BeginInvoke(action);
}
