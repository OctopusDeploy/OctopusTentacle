using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        static readonly Log Instance = new Log();
        static readonly List<ILogAppender> appenders = new List<ILogAppender>();
        readonly ThreadLocal<ILogContext> threadLocalLogContext;

        static Log()
        {
        }

        public Log()
        {
            threadLocalLogContext = new ThreadLocal<ILogContext>(() => new LogContext("system/" + Environment.MachineName));
        }

        public static List<ILogAppender> Appenders
        {
            get { return appenders; }
        }

        public override ILogContext CurrentContext
        {
            get { return threadLocalLogContext.Value; }
        }

        public static ILogWithContext Octopus()
        {
            return Instance;
        }

        public static ILogWithContext System()
        {
            return new Log();
        }

        protected override void WriteEvent(LogEvent logEvent)
        {
            foreach (var appender in appenders)
            {
                appender.WriteEvent(logEvent);
            }
        }

        protected override void WriteEvents(IList<LogEvent> logEvents)
        {
            foreach (var appender in appenders)
            {
                appender.WriteEvents(logEvents);
            }
        }

        public override IDisposable WithinBlock(ILogContext logContext)
        {
            var oldValue = threadLocalLogContext.Value;
            threadLocalLogContext.Value = logContext;
            return new RevertLogContext(this, oldValue);
        }

        public override void Flush()
        {
            foreach (var appender in appenders)
            {
                appender.Flush();
            }
        }

        public override void Flush(string correlationId)
        {
            foreach (var appender in appenders)
            {
                appender.Flush(correlationId);
            }
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
