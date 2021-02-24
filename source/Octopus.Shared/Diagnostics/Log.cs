using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Octopus.Shared.Diagnostics
{
    public abstract class Log : AbstractLog
    {
        public static ConcurrentBag<ILogAppender> Appenders { get; } = new ConcurrentBag<ILogAppender>();

        protected Log(string[]? sensitiveValues = null) : base(sensitiveValues)
        {
        }

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