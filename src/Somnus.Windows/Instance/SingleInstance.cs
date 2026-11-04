namespace Somnus.Windows.Instance;

/// <summary>
/// Keeps Somnus to one instance per Windows session (rule R-09): the first instance owns a named
/// mutex; a later one finds it taken, signals the first through a named event and exits, and the
/// first then brings its window back.
/// </summary>
/// <remarks>
/// Both kernel objects live in the <c>Local\</c> namespace, so each signed-in user has their own
/// Somnus. Create and dispose an instance on the same thread: the mutex belongs to that thread.
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _listener;
    private bool _disposed;

    /// <summary>
    /// Claims the instance named <paramref name="name"/>, or finds out that another process holds it.
    /// </summary>
    /// <param name="name">A name unique to the app, without a namespace prefix.</param>
    public SingleInstance(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        MutexName = $@"Local\{name}.Instance";
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Activate");
    }

    /// <summary>Gets the full name of the mutex, which the installer checks before replacing files.</summary>
    public string MutexName { get; }

    /// <summary>Gets a value indicating whether this is the first instance.</summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Runs <paramref name="onActivationRequested"/> on a thread-pool thread whenever another
    /// instance starts. Only the first instance listens.
    /// </summary>
    /// <param name="onActivationRequested">Brings the window back; it must marshal to the UI thread itself.</param>
    /// <exception cref="InvalidOperationException">This is not the first instance, or it already listens.</exception>
    public void ListenForActivation(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary || _listener is not null)
        {
            throw new InvalidOperationException("Only the first instance listens, and only once.");
        }

        _listener = ThreadPool.RegisterWaitForSingleObject(
            _activation,
            (_, _) => onActivationRequested(),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Asks the first instance to show its window.</summary>
    public void SignalPrimary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _activation.Set();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listener?.Unregister(null);
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activation.Dispose();
    }
}
