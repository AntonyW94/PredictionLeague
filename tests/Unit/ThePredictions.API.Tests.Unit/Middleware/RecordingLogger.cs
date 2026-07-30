using Microsoft.Extensions.Logging;

namespace ThePredictions.API.Tests.Unit.Middleware;

/// <summary>
/// Captures the level and rendered message of every log call. A substitute cannot assert this readably,
/// because <c>LogWarning</c> / <c>LogError</c> are extension methods over the generic
/// <see cref="ILogger.Log{TState}"/> - and the level is the whole point of these tests.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add((logLevel, formatter(state, exception), exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
