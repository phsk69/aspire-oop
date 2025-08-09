using Microsoft.Extensions.Logging;

namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestLogger<T> : ILogger<T>
{
    public readonly List<LogEntry> LogEntries = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => new TestLoggerScope();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new LogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            State = state,
            Exception = exception,
            Message = formatter(state, exception)
        });
    }

    public class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public object? State { get; set; }
        public Exception? Exception { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private class TestLoggerScope : IDisposable
    {
        public void Dispose() { }
    }
}