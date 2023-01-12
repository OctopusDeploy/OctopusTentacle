using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Diagnostics
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
            if (ShouldHandle(logEvent.CorrelationId))
                logger.WriteEvent(LogCategoryToLogLevel(logEvent.Category), logEvent.Error, logEvent.MessageText);
        }

        public void WriteEvents(IList<LogEvent> logEvents)
        {
            foreach (var l in logEvents)
                WriteEvent(l);
        }

        public void Flush()
        {
            if (LogManager.Configuration != null)
                foreach (var target in LogManager.Configuration.AllTargets)
                    target.Flush(e => { });
        }

        public void Flush(string correlationId)
        {
            if (ShouldHandle(correlationId))
                Flush();
        }

        static bool ShouldHandle(string logEventCorrelationId) => logEventCorrelationId.StartsWith("system/");

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
            public void WriteEvent(LogLevel category, Exception? error, string messageText)
            {
                Log(typeof(OctopusNLogger), GetLogEvent(category, error, messageText));
            }

            LogEventInfo GetLogEvent(LogLevel level, Exception? exception, string message)
                => LogEventInfo.Create(level,
                    Name,
                    exception,
                    CultureInfo.InvariantCulture,
                    message);
        }
    }
}