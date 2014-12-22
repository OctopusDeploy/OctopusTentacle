using System;

namespace Octopus.Shared.Diagnostics
{
    public interface ILog
    {
        bool IsVerboseEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFatalEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsTraceEnabled { get; }
        bool IsWarnEnabled { get; }

        bool IsEnabled(TraceCategory category);
        
        void Write(TraceCategory category, string messageText);
        void Write(TraceCategory category, Exception error);
        void Write(TraceCategory category, Exception error, string messageText);
        void WriteFormat(TraceCategory category, string messageFormat, params object[] args);
        void WriteFormat(TraceCategory category, Exception error, string messageFormat, params object[] args);

        void Trace(string messageText);
        void TraceFormat(string messageFormat, params object[] args);
        void Trace(Exception error);
        void Trace(Exception error, string messageText);
        void TraceFormat(Exception error, string format, params object[] args);

        void Verbose(string messageText);
        void Verbose(Exception error);
        void Verbose(Exception error, string messageText);
        void VerboseFormat(string messageFormat, params object[] args);
        void VerboseFormat(Exception error, string format, params object[] args);

        void Info(string messageText);
        void Info(Exception error);
        void Info(Exception error, string messageText);
        void InfoFormat(string messageFormat, params object[] args);
        void InfoFormat(Exception error, string format, params object[] args);

        void Warn(string messageText);
        void Warn(Exception error);
        void Warn(Exception error, string messageText);
        void WarnFormat(string messageFormat, params object[] args);
        void WarnFormat(Exception error, string format, params object[] args);

        void Error(string messageText);
        void Error(Exception error);
        void Error(Exception error, string messageText);
        void ErrorFormat(string messageFormat, params object[] args);
        void ErrorFormat(Exception error, string format, params object[] args);

        void Fatal(string messageText);
        void Fatal(Exception error);
        void Fatal(Exception error, string messageText);
        void FatalFormat(string messageFormat, params object[] args);
        void FatalFormat(Exception error, string format, params object[] args);

        ILog BeginOperation(string messageText);
        ILog BeginOperationFormat(string messageFormat, params object[] args);
        void EndOperation();

        void UpdateProgress(int progressPercentage, string messageText);
        void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args);
    }
}