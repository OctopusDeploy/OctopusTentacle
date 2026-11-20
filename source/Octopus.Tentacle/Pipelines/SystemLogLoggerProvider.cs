#if PIPELINES_RUNNER
#nullable enable
using System;
using Microsoft.Extensions.Logging;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Pipelines
{
    /// <summary>Writes the runner's <see cref="ILogger"/> output to the Tentacle log.</summary>
    public sealed class SystemLogLoggerProvider : ILoggerProvider
    {
        const string Prefix = "[Pipelines runner] ";

        readonly ISystemLog log;

        public SystemLogLoggerProvider(ISystemLog log)
        {
            this.log = log;
        }

        public ILogger CreateLogger(string categoryName) => new Logger(log);

        public void Dispose()
        {
        }

        sealed class Logger : ILogger
        {
            readonly ISystemLog log;

            public Logger(ISystemLog log)
            {
                this.log = log;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                var message = Prefix + formatter(state, exception);
                switch (logLevel)
                {
                    case LogLevel.Trace:
                        if (exception is null) log.Trace(message); else log.Trace(exception, message);
                        break;
                    case LogLevel.Debug:
                        if (exception is null) log.Verbose(message); else log.Verbose(exception, message);
                        break;
                    case LogLevel.Information:
                        if (exception is null) log.Info(message); else log.Info(exception, message);
                        break;
                    case LogLevel.Warning:
                        if (exception is null) log.Warn(message); else log.Warn(exception, message);
                        break;
                    default:
                        if (exception is null) log.Error(message); else log.Error(exception, message);
                        break;
                }
            }
        }
    }
}
#endif
