using System;

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

        void Debug(string message);
        void Debug(Exception exception);
        void Debug(Exception exception, string message);
        void DebugFormat(string format, params object[] args);
        void DebugFormat(Exception exception, string format, params object[] args);

        void Error(string message);
        void Error(Exception exception);
        void Error(Exception exception, string message);
        void ErrorFormat(string format, params object[] args);
        void ErrorFormat(Exception exception, string format, params object[] args);

        void Fatal(string message);
        void Fatal(Exception exception);
        void Fatal(Exception exception, string message);
        void FatalFormat(string format, params object[] args);
        void FatalFormat(Exception exception, string format, params object[] args);

        void Info(string message);
        void Info(Exception exception);
        void Info(Exception exception, string message);
        void InfoFormat(string format, params object[] args);
        void InfoFormat(Exception exception, string format, params object[] args);

        void Trace(string message);
        void Trace(Exception exception);
        void Trace(Exception exception, string message);
        void TraceFormat(string format, params object[] args);
        void TraceFormat(Exception exception, string format, params object[] args);

        void Warn(string message);
        void Warn(Exception exception);
        void Warn(Exception exception, string message);
        void WarnFormat(string format, params object[] args);
        void WarnFormat(Exception exception, string format, params object[] args);
    }
}