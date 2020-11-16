using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        static readonly Log Instance = new Log();
        readonly ThreadLocal<ILogContext> threadLocalLogContext;

        static Log()
        {
        }

        public Log()
        {
            threadLocalLogContext = new ThreadLocal<ILogContext>(() => new LogContext("system/" + Environment.MachineName));
        }

        public static List<ILogAppender> Appenders { get; } = new List<ILogAppender>();

        public override ILogContext CurrentContext => threadLocalLogContext.Value!;

        public static ILogWithContext Octopus()
            => Instance;

        public static ILogWithContext System()
            => new Log();

        protected override void WriteEvent(LogEvent logEvent)
        {
            foreach (var appender in Appenders)
                appender.WriteEvent(logEvent);
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
            foreach (var appender in Appenders)
                appender.WriteEvents(logEvents);
        }

        public override IDisposable WithinBlock(ILogContext logContext)
        {
            var oldValue = threadLocalLogContext.Value!;
            threadLocalLogContext.Value = logContext;
            return new RevertLogContext(this, oldValue);
        }

        public override void Flush()
        {
            foreach (var appender in Appenders)
                appender.Flush();
        }

        public override void Flush(string correlationId)
        {
            foreach (var appender in Appenders)
                appender.Flush(correlationId);
        }

        class RevertLogContext : IDisposable
        {
            readonly ILogWithContext activityLog;
            readonly ILogContext previous;

            public RevertLogContext(ILogWithContext activityLog, ILogContext previous)
            {
                this.activityLog = activityLog;
                this.previous = previous;
            }

            public void Dispose()
            {
                activityLog.WithinBlock(previous);
            }
        }
    }
}