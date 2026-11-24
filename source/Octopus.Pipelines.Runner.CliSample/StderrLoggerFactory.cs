using Microsoft.Extensions.Logging;

namespace Octopus.Pipelines.Runner.CliSample;

/// <summary>
/// Writes warnings and errors to stderr, one line each; stdout carries the job's output. Hand-rolled rather than
/// Microsoft.Extensions.Logging.Console, whose options and formatters add about half a megabyte to the AOT binary.
/// </summary>
sealed class StderrLoggerFactory(LogLevel minimum, IReadOnlyDictionary<string, LogLevel>? overrides = null) : ILoggerFactory
{
    static readonly Lock Gate = new();

    public ILogger CreateLogger(string categoryName) =>
        new Logger(categoryName, overrides is not null && overrides.TryGetValue(categoryName, out var level) ? level : minimum);

    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

    public void Dispose()
    {
    }

    sealed class Logger(string category, LogLevel minimum) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var level = logLevel switch
            {
                LogLevel.Critical => "crit",
                LogLevel.Error => "fail",
                LogLevel.Warning => "warn",
                LogLevel.Information => "info",
                _ => "dbug",
            };
            var line = $"{level}: {category}: {formatter(state, exception)}{(exception is null ? "" : $" ({exception.GetType().Name}: {exception.Message})")}";
            lock (Gate)
            {
                Console.Error.WriteLine(line);
            }
        }
    }
}
