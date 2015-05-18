using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Diagnostics
{
    public class Log : AbstractLog
    {
        static readonly Log Instance = new Log();
        static readonly List<ILogAppender> appenders = new List<ILogAppender>();
        readonly ThreadLocal<LogCorrelator> correlator;

        static Log()
        {
        }

        public Log()
        {
            correlator = new ThreadLocal<LogCorrelator>(() => LogCorrelator.CreateNew("system/" + Environment.MachineName));
        }

        public static List<ILogAppender> Appenders
        {
            get { return appenders; }
        }

        public static ILog Octopus()
        {
            return Instance;
        }

        public static ILog System()
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

        public override void Mask(IList<string> sensitiveValues)
        {
            foreach (var appender in appenders)
            {
                appender.Mask(Current.CorrelationId, sensitiveValues);
            }
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

        public override void Flush()
        {
            foreach (var appender in appenders)
            {
                appender.Flush();
            }
        }
    }
}