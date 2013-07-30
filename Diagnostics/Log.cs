using System;
using NLog;

namespace Octopus.Shared.Diagnostics
{
    public class Log : Logger, ILog
    {
        public static ILog Octopus()
        {
            return (ILog) LogManager.GetLogger("Octopus", typeof (Log));
        }

        public void DebugFormat(string format, params object[] args)
        {
            if (!base.IsDebugEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, null, format, args);
            Log(typeof(Log), logEvent);
        }

        public void DebugFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsDebugEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, exception, format, args);
            Log(typeof(Log), logEvent);
        }

        public void Debug(Exception exception, string message)
        {
            if (!base.IsDebugEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Debug, exception, message);
            Log(typeof(Log), logEvent);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            if (!base.IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, null, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void ErrorFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, exception, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void Error(Exception exception, string message)
        {
            if (!base.IsErrorEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Error, exception, message);
            base.Log(typeof(Log), logEvent);
        }

        public void FatalFormat(string format, params object[] args)
        {
            if (!base.IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, null, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void FatalFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, exception, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void Fatal(Exception exception, string message)
        {
            if (!base.IsFatalEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Fatal, exception, message);
            base.Log(typeof(Log), logEvent);
        }

        public void InfoFormat(string format, params object[] args)
        {
            if (!base.IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, null, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void InfoFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, exception, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void Info(Exception exception, string message)
        {
            if (!base.IsInfoEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Info, exception, message);
            base.Log(typeof(Log), logEvent);
        }

        public void TraceFormat(string format, params object[] args)
        {
            if (!base.IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, null, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void TraceFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, exception, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void Trace(Exception exception, string message)
        {
            if (!base.IsTraceEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Trace, exception, message);
            base.Log(typeof(Log), logEvent);
        }

        public void WarnFormat(string format, params object[] args)
        {
            if (!base.IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, null, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void WarnFormat(Exception exception, string format, params object[] args)
        {
            if (!base.IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, exception, format, args);
            base.Log(typeof(Log), logEvent);
        }

        public void Warn(Exception exception, string message)
        {
            if (!base.IsWarnEnabled) return;
            var logEvent = GetLogEvent(LogLevel.Warn, exception, message);
            base.Log(typeof(Log), logEvent);
        }

        public void Debug(Exception exception)
        {
            this.Debug(exception, string.Empty);
        }

        public void Error(Exception exception)
        {
            this.Error(exception, string.Empty);
        }

        public void Fatal(Exception exception)
        {
            this.Fatal(exception, string.Empty);
        }

        public void Info(Exception exception)
        {
            this.Info(exception, string.Empty);
        }

        public void Trace(Exception exception)
        {
            this.Trace(exception, string.Empty);
        }

        public void Warn(Exception exception)
        {
            this.Warn(exception, string.Empty);
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