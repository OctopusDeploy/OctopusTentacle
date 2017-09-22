using System;
using System.Collections.Generic;
using System.Threading;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        static readonly Log Instance = new Log();
        static readonly List<ILogAppender> appenders = new List<ILogAppender>();
        readonly ThreadLocal<LogContext> threadLocalLogContext;

        static Log()
        {
        }

        public Log()
        {
            threadLocalLogContext = new ThreadLocal<LogContext>(() => new LogContext("system/" + Environment.MachineName));
        }

        public static List<ILogAppender> Appenders
        {
            get { return appenders; }
        }

        public override LogContext CurrentContext
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

        public override IDisposable WithinBlock(LogContext logContext)
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

        class RevertLogContext : IDisposable
        {
            readonly ILogWithContext activityLog;
            readonly LogContext previous;

            public RevertLogContext(ILogWithContext activityLog, LogContext previous)
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