using System;
using System.Collections.Generic;
using NLog;

namespace Octopus.Shared.Diagnostics
{
    public class NLogAppender : ILogAppender
    {
        readonly OctopusNLogger logger;

        public NLogAppender()
        {
            logger = (OctopusNLogger)LogManager.GetLogger("Octopus", typeof(OctopusNLogger));
        }

        public void WriteEvent(LogEvent logEvent)
        {
            if (!logEvent.CorrelationId.StartsWith("system/"))
                return;

            logger.WriteEvent(LogCategoryToLogLevel(logEvent.Category), logEvent.Error, logEvent.MessageText);
        }

        public void WriteEvents(IList<LogEvent> logEvents)
        {
            foreach (var l in logEvents)
            {
                WriteEvent(l);
            }
        }

        public void Mask(string correlationId, IList<string> sensitiveValues)
        {
            
        }

        public void Flush()
        {
            foreach (var target in LogManager.Configuration.AllTargets)
            {
                target.Flush(e => {});
            }
        }

        static LogLevel LogCategoryToLogLevel(LogCategory category)
        {
            switch (category)
            {
                case LogCategory.Trace:
                    return LogLevel.Trace;
                case LogCategory.Verbose:
                    return LogLevel.Debug;
                case LogCategory.Info:
                    return LogLevel.Info;
                case LogCategory.Warning:
                    return LogLevel.Warn;
                case LogCategory.Error:
                    return LogLevel.Error;
                case LogCategory.Fatal:
                    return LogLevel.Fatal;
                default:
                    return LogLevel.Info;
            }
        }

        class OctopusNLogger : Logger
        {
            public void WriteEvent(LogLevel category, Exception error, string messageText)
            {
                Log(typeof(OctopusNLogger), GetLogEvent(category, error, messageText));
            }

            private LogEventInfo GetLogEvent(LogLevel level, Exception exception, string message)
            {
                return LogEventInfo.Create(level, Name, message, exception);
            }
        }
    }
}