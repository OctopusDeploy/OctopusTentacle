using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        public static ConcurrentBag<ILogAppender> Appenders { get; } = new ConcurrentBag<ILogAppender>();

        public Log() : base(new SensitiveValueMasker())
        {
        }

        protected override string CorrelationId => "system/" + Environment.MachineName;

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