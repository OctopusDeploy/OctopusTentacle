using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        static readonly Log Instance = new Log();
        static readonly ConcurrentBag<ILogAppender> appenders = new ConcurrentBag<ILogAppender>();
        readonly ThreadLocal<LogCorrelator> correlator;

        static Log()
        {
        }

        public Log()
        {
            correlator = new ThreadLocal<LogCorrelator>(() => LogCorrelator.CreateNew("system/" + Environment.MachineName));
        }

        public static ConcurrentBag<ILogAppender> Appenders
        {
            get { return appenders; }
        }

        public static ILog Octopus()
        {
            return Instance;
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

        public override IDisposable WithinBlock(LogCorrelator logger)
        {
            var oldValue = correlator.Value;
            correlator.Value = logger;
            return new RevertLogContext(this, oldValue);
        }

        public override LogCorrelator Current
        {
            get { return correlator.Value; }
        }

        class RevertLogContext : IDisposable
        {
            readonly ILog activityLog;
            readonly LogCorrelator logger;

            public RevertLogContext(ILog activityLog, LogCorrelator logger)
            {
                this.activityLog = activityLog;
                this.logger = logger;
            }

            public void Dispose()
            {
                activityLog.WithinBlock(logger);
            }
        }
    }
}