using System;
using System.Collections.Generic;
using NLog;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Diagnostics
{
    public class Log : Logger, ILog
    {
        static readonly IDictionary<TraceCategory, LogLevel> TraceCategoryToLogLevel =
            new Dictionary<TraceCategory, LogLevel>
            {
                { TraceCategory.Trace, LogLevel.Trace },
                { TraceCategory.Verbose, LogLevel.Debug },
                { TraceCategory.Info, LogLevel.Info },
                { TraceCategory.Alert, LogLevel.Info },
                { TraceCategory.Warning, LogLevel.Warn },
                { TraceCategory.Error, LogLevel.Error },
                { TraceCategory.Fatal, LogLevel.Fatal }
            };

        public static ILog Octopus()
        {
            return (ILog) LogManager.GetLogger("Octopus", typeof (Log));
        }

        public void Write(TraceCategory category, string messageText)
        {
            var level = TraceCategoryToLogLevel[category];
            if (!IsEnabled(level)) return;
            var logEvent = GetLogEvent(level, null, messageText);
            Log(typeof(Log), logEvent);
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            var level = TraceCategoryToLogLevel[category];
            if (!IsEnabled(level)) return;
            var logEvent = GetLogEvent(level, error, messageText);
            Log(typeof(Log), logEvent);
        }

        public void WriteFormat(TraceCategory category, string messageFormat, params object[] args)
        {
            var level = TraceCategoryToLogLevel[category];
            if (!IsEnabled(level)) return;
            var logEvent = GetLogEvent(level, null, messageFormat, args);
            Log(typeof(Log), logEvent);
        }

        public void Verbose(string messageText)
        {
            if (!IsVerboseEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, null, messageText);
            Log(typeof(Log), logEvent);
        }

        public void VerboseFormat(string format, params object[] args)
        {
            if (!IsVerboseEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void VerboseFormat(Exception exception, string format, params object[] args)
        {
            if (!IsVerboseEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Verbose(Exception exception, string message)
        {
            if (!IsVerboseEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            if (!IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void ErrorFormat(Exception exception, string format, params object[] args)
        {
            if (!IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Error(Exception exception, string message)
        {
            if (!IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void FatalFormat(string format, params object[] args)
        {
            if (!IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void FatalFormat(Exception exception, string format, params object[] args)
        {
            if (!IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Fatal(Exception exception, string message)
        {
            if (!IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void InfoFormat(string format, params object[] args)
        {
            if (!IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Alert(string messageText)
        {
            Info(messageText);
        }

        public void Alert(Exception error, string messageText)
        {
            Info(error, messageText);
        }

        public void AlertFormat(string messageFormat, params object[] args)
        {
            InfoFormat(messageFormat, args);
        }

        public void InfoFormat(Exception exception, string format, params object[] args)
        {
            if (!IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Info(Exception exception, string message)
        {
            if (!IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void TraceFormat(string format, params object[] args)
        {
            if (!IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void TraceFormat(Exception exception, string format, params object[] args)
        {
            if (!IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Trace(Exception exception, string message)
        {
            if (!IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void WarnFormat(string format, params object[] args)
        {
            if (!IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void WarnFormat(Exception exception, string format, params object[] args)
        {
            if (!IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Warn(Exception exception, string message)
        {
            if (!IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, exception, message);
            Log(typeof(Log), logEvent);
        }

        public bool IsVerboseEnabled
        {
            get { return IsDebugEnabled; }
        }

        public void Verbose(Exception exception)
        {
            Verbose(exception, string.Empty);
        }

        public void Error(Exception exception)
        {
            Error(exception, string.Empty);
        }

        public void Fatal(Exception exception)
        {
            Fatal(exception, string.Empty);
        }

        public void Info(Exception exception)
        {
            Info(exception, string.Empty);
        }

        public void Trace(Exception exception)
        {
            Trace(exception, string.Empty);
        }

        public void Warn(Exception exception)
        {
            Warn(exception, string.Empty);
        }

        public void UpdateProgress(int progressPercentage, string messageText)
        {
            WriteFormat(TraceCategory.Verbose, "{0} ({1}%)", messageText, progressPercentage);
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            if (!IsVerboseEnabled) return;
            UpdateProgress(progressPercentage, string.Format(messageFormat, args));
        }

        private LogEventInfo GetLogEvent(LogLevel level, Exception exception, string message)
        {
            return LogEventInfo.Create(level, Name, message, exception);
        }

        private LogEventInfo GetLogEvent(LogLevel level, Exception exception, string format, object[] args)
        {
            return LogEventInfo.Create(level, Name, string.Format(format, args), exception);
        }
    }
}