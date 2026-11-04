using Microsoft.Extensions.Logging;

namespace Somnus.Windows.Tests.TestDoubles;

/// <summary>Keeps every log entry, so tests can check what was logged and how often.</summary>
/// <typeparam name="T">The category.</typeparam>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, eventId, formatter(state, exception)));
}
