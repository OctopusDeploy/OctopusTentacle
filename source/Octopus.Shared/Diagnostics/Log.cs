using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Octopus.Shared.Diagnostics
{
    public abstract class Log : AbstractLog
    {
        public static ConcurrentBag<ILogAppender> Appenders { get; } = new ConcurrentBag<ILogAppender>();

        protected Log(string? correlationId = null, string[]? sensitiveValues = null) : base(correlationId, sensitiveValues)
        {
        }

        public override string CorrelationId => "system/" + Environment.MachineName;

        protected override void WriteEvent(LogEvent logEvent)
        {
            foreach (var appender in GetThreadSafeAppenderCollection())
                appender.WriteEvent(logEvent);
        }

        public override void Flush()
        {
            foreach (var appender in GetThreadSafeAppenderCollection())
                appender.Flush();
        }

        static IEnumerable<ILogAppender> GetThreadSafeAppenderCollection() => Appenders.ToArray();
    }
}