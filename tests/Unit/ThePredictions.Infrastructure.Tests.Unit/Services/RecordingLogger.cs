using Microsoft.Extensions.Logging;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// Captures the level, rendered message and exception of every log call. A substitute cannot assert this
/// readably, because <c>LogError</c> and friends are extension methods over the generic
/// <see cref="ILogger.Log{TState}"/>.
/// </summary>
/// <remarks>
/// Deliberately a copy of the one in <c>ThePredictions.API.Tests.Unit</c> rather than a shared helper:
/// <c>ThePredictions.Tests.Shared</c> references only the domain, and pulling the logging abstractions into it
/// would push them into every test project that consumes it for the sake of thirty lines.
/// </remarks>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];
    private readonly object _lock = new();

    /// <summary>
    /// A snapshot, and locked, because the calls under test arrive on a background thread while the test
    /// thread is reading them.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
        {
            _entries.Add((logLevel, formatter(state, exception), exception));
        }
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
