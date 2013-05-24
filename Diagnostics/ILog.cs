using System;
using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Octopus.Shared.Diagnostics
{
    public interface ILog
    {
        bool IsDebugEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFatalEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsTraceEnabled { get; }
        bool IsWarnEnabled { get; }

        void Debug(Exception exception);
        void Debug(string message);
        void DebugFormat(string format, params object[] args);
        void Debug(Exception exception, string format, params object[] args);
        void Error(Exception exception);
        void Error(string message);
        void ErrorFormat(string format, params object[] args);
        void Error(Exception exception, string format, params object[] args);
        void Fatal(Exception exception);
        void Fatal(string message);
        void FatalFormat(string format, params object[] args);
        void Fatal(Exception exception, string format, params object[] args);
        void Info(Exception exception);
        void Info(string message);
        void InfoFormat(string format, params object[] args);
        void Info(Exception exception, string format, params object[] args);
        void Trace(Exception exception);
        void Trace(string message);
        void TraceFormat(string format, params object[] args);
        void Trace(Exception exception, string format, params object[] args);
        void Warn(Exception exception);
        void Warn(string message);
        void WarnFormat(string format, params object[] args);
        void Warn(Exception exception, string format, params object[] args);
    }

    [LayoutRenderer("octopusLogsDirectory")]
    public class OctopusLogsDirectoryRenderer : LayoutRenderer
    {
        public static string LogsDirectory = null;

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogsDirectory);
        }
    }
}