using System;
using System.Collections.Generic;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogWriters;
using Halibut.Logging;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;
using ILog = Halibut.Diagnostics.ILog;

namespace Octopus.Tentacle.Tests.Integration.Support.Logging
{
    public class TestContextConnectionLog : ILog, ILogWriter
    {
        readonly string endpoint;
        readonly string name;
        readonly LogLevel logLevel;
        private readonly ILogger logger;

        public TestContextConnectionLog(string endpoint, string name, LogLevel logLevel)
        {
            this.endpoint = endpoint;
            this.name = name;
            this.logLevel = logLevel;
            this.logger = new SerilogLoggerBuilder().Build().ForContext<TestContextConnectionLog>();
        }

        public void Write(EventType type, string message, params object[] args)
        {
            WriteInternal(new LogEvent(type, message, null, args));
        }

        public void WriteException(EventType type, string message, Exception ex, params object[] args)
        {
            WriteInternal(new LogEvent(type, message, ex, args));
        }

        public IList<LogEvent> GetLogs()
        {
            throw new NotImplementedException();
        }

        void WriteInternal(LogEvent logEvent)
        {
            var logEventLogLevel = GetLogLevel(logEvent);

            if (logEventLogLevel >= logLevel)
            {
                logger.Information(string.Format("{5, 16}: {0}:{1} {2}  {3} {4}", logEventLogLevel, logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage, name));
            }
        }

        static LogLevel GetLogLevel(LogEvent logEvent)
        {
            switch (logEvent.Type)
            {
                case EventType.Error:
                    return LogLevel.Error;
                case EventType.Diagnostic:
                case EventType.SecurityNegotiation:
                case EventType.MessageExchange:
                    return LogLevel.Trace;
                case EventType.OpeningNewConnection:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }
    }
}