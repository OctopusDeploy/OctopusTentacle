using System;
using NLog;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Security.Masking;

namespace Octopus.Shared.Diagnostics
{
    public class LogAdapter : AbstractLog
    {
        readonly OctopusNLogger logger;

        public LogAdapter()
        {
            logger = (OctopusNLogger) LogManager.GetLogger("Octopus", typeof(OctopusNLogger));
        }

        static LogLevel TraceCategoryToLogLevel(TraceCategory category)
        {
            switch (category)
            {
                case TraceCategory.Trace:
                    return LogLevel.Trace;
                case TraceCategory.Verbose:
                    return LogLevel.Debug;
                case TraceCategory.Info:
                    return LogLevel.Info;
                case TraceCategory.Alert:
                    return LogLevel.Info;
                case TraceCategory.Warning:
                    return LogLevel.Warn;
                case TraceCategory.Error:
                    return LogLevel.Error;
                case TraceCategory.Fatal:
                    return LogLevel.Fatal;
                default:
                    return LogLevel.Info;
            }
        }

        public override void EndOperation()
        {
            Verbose("Finished");
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            VerboseFormat("{0} ({1}%)", messageText, progressPercentage);
        }

        public override bool IsEnabled(TraceCategory category)
        {
            return logger.IsEnabled(TraceCategoryToLogLevel(category));
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            logger.WriteEvent(TraceCategoryToLogLevel(category), MaskingContext.ApplyTo(error), MaskingContext.ApplyTo(messageText));
        }

        public override ILog BeginOperation(string messageText)
        {
            return new PrefixedLogDecorator("[" + messageText + "]", this);
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