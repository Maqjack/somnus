using System.Collections.Concurrent;
using System.Windows;

namespace Somnus.App.Tests;

/// <summary>
/// One UI thread with the Somnus <see cref="Application"/> and its theme for all WPF tests: a process
/// may create only one <see cref="Application"/>, and WPF objects belong to the thread that made them.
/// </summary>
/// <remarks>
/// The thread runs the tests from a plain work queue and never pumps its dispatcher. Creating an
/// <see cref="Application"/> queues its startup on the dispatcher; pumping it would run
/// <c>App.OnStartup</c> and start the real Somnus with the real power controller (rule R-01).
/// </remarks>
public sealed class WpfFixture : IDisposable
{
    private readonly BlockingCollection<(Action Action, TaskCompletionSource Done)> _work = [];
    private readonly Thread _thread;
    private bool _startupRan;

    /// <summary>Starts the UI thread and loads App.xaml.</summary>
    public WpfFixture()
    {
        _thread = new Thread(() =>
        {
            var app = new App();
            app.Startup += (_, _) => _startupRan = true;
            app.InitializeComponent();

            foreach (var (action, done) in _work.GetConsumingEnumerable())
            {
                try
                {
                    action();
                    done.SetResult();
                }
#pragma warning disable CA1031 // Any failure of a test body is handed back to the test thread.
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    done.SetException(exception);
                }
            }
        })
        {
            IsBackground = true,
            Name = "WPF test UI thread",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Gets a value indicating whether <c>App.OnStartup</c> ran, which must never happen in tests.</summary>
    public bool StartupRan => Volatile.Read(ref _startupRan);

    /// <summary>Runs <paramref name="action"/> on the UI thread; its failures surface in the calling test.</summary>
    /// <param name="action">The test body.</param>
    public void Run(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Add((action, done));
        done.Task.GetAwaiter().GetResult();
    }

    /// <summary>Finds the first element of type <typeparamref name="T"/> in the logical tree.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="parent">Where the search starts.</param>
    /// <returns>The element.</returns>
    public static T FindFirst<T>(DependencyObject parent)
        where T : DependencyObject =>
        TryFindFirst<T>(parent) ?? throw new InvalidOperationException($"No {typeof(T).Name} found.");

    /// <inheritdoc />
    public void Dispose()
    {
        _work.CompleteAdding();
        _thread.Join();
        _work.Dispose();
    }

    private static T? TryFindFirst<T>(DependencyObject parent)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                return match;
            }

            if (TryFindFirst<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}

/// <summary>The tests that share <see cref="WpfFixture"/>; they run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class WpfTestGroup : ICollectionFixture<WpfFixture>
{
    /// <summary>The collection name.</summary>
    public const string Name = "WPF";
}
