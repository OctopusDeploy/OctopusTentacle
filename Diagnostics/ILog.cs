using System;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Diagnostics
{
    public interface ILog : ITrace
    {
        bool IsVerboseEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFatalEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsTraceEnabled { get; }
        bool IsWarnEnabled { get; }

        void Verbose(Exception exception);
        void Verbose(Exception exception, string message);
        void VerboseFormat(Exception exception, string format, params object[] args);

        void Error(Exception exception);
        void ErrorFormat(Exception exception, string format, params object[] args);

        void Fatal(Exception exception);
        void FatalFormat(Exception exception, string format, params object[] args);

        void Info(Exception exception);
        void Info(Exception exception, string message);
        void InfoFormat(Exception exception, string format, params object[] args);

        void Trace(Exception exception);
        void Trace(Exception exception, string message);
        void TraceFormat(Exception exception, string format, params object[] args);

        void Warn(Exception exception);
        void WarnFormat(Exception exception, string format, params object[] args);
    }
}