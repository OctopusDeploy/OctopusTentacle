using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Logging
{
    public interface IActivityLog : ILog
    {
        LoggerReference Current { get; }

        IDisposable CreateChild(string messageText);
        IDisposable CreateChildFormat(string messageFormat, params object[] args);
     
        LoggerReference PlanChild(string messageText);
        LoggerReference PlanChildFormat(string messageFormat, params object[] args);
        
        IDisposable SwitchTo(LoggerReference logger);

        void Abandon();

        void Reinstate();

        void Finish();
    }

    public interface IActivity : ILog
    {
        LoggerReference DefaultLogger { get; }

        LoggerReference CreateChild(string messageText);
        LoggerReference CreateChild(LoggerReference logger, string messageText);
        LoggerReference CreateChildFormat(string messageFormat, params object[] args);
        LoggerReference CreateChildFormat(LoggerReference logger, string messageFormat, params object[] args);

        LoggerReference PlanChild(string messageText);
        LoggerReference PlanChild(LoggerReference logger, string messageText);
        LoggerReference PlanChildFormat(string messageFormat, params object[] args);
        LoggerReference PlanChildFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Abandoned();
        void Abandoned(LoggerReference logger);

        void Reinstated();
        void Reinstated(LoggerReference logger);

        void UpdateProgress(LoggerReference logger, int progressPercentage, string message);
        void UpdateProgressFormat(LoggerReference logger, int progressPercentage, string messageFormat, params object[] args);

        void Finished();
        void Finished(LoggerReference logger);

        void Write(LoggerReference logger, TraceCategory category, string messageText);
        void Write(LoggerReference logger, TraceCategory category, Exception error);
        void Write(LoggerReference logger, TraceCategory category, Exception error, string messageText);
        void WriteFormat(LoggerReference logger, TraceCategory category, string messageFormat, params object[] args);
        void WriteFormat(LoggerReference logger, TraceCategory category, Exception error, string messageFormat, params object[] args);

        void Trace(LoggerReference logger, string messageText);
        void TraceFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Trace(LoggerReference logger, Exception error);
        void Trace(LoggerReference logger, Exception error, string messageText);
        void TraceFormat(LoggerReference logger, Exception error, string format, params object[] args);

        void Verbose(LoggerReference logger, string messageText);
        void Verbose(LoggerReference logger, Exception error);
        void Verbose(LoggerReference logger, Exception error, string messageText);
        void VerboseFormat(LoggerReference logger, string messageFormat, params object[] args);
        void VerboseFormat(LoggerReference logger, Exception error, string format, params object[] args);

        void Info(LoggerReference logger, string messageText);
        void Info(LoggerReference logger, Exception error);
        void Info(LoggerReference logger, Exception error, string messageText);
        void InfoFormat(LoggerReference logger, string messageFormat, params object[] args);
        void InfoFormat(LoggerReference logger, Exception error, string format, params object[] args);

        void Warn(LoggerReference logger, string messageText);
        void Warn(LoggerReference logger, Exception error);
        void Warn(LoggerReference logger, Exception error, string messageText);
        void WarnFormat(LoggerReference logger, string messageFormat, params object[] args);
        void WarnFormat(LoggerReference logger, Exception error, string format, params object[] args);

        void Error(LoggerReference logger, string messageText);
        void Error(LoggerReference logger, Exception error);
        void Error(LoggerReference logger, Exception error, string messageText);
        void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args);
        void ErrorFormat(LoggerReference logger, Exception error, string format, params object[] args);

        void Fatal(LoggerReference logger, string messageText);
        void Fatal(LoggerReference logger, Exception error);
        void Fatal(LoggerReference logger, Exception error, string messageText);
        void FatalFormat(LoggerReference logger, string messageFormat, params object[] args);
        void FatalFormat(LoggerReference logger, Exception error, string format, params object[] args);

        ILog DetachLog();
        ILog DetachLog(LoggerReference logger);
    }
}