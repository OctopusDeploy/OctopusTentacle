using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.Logging
{
    public interface ITrace
    {
        void Write(TraceCategory category, string messageText);
        void Write(TraceCategory category, Exception error, string messageText);
        void WriteFormat(TraceCategory category, string messageFormat, params object[] args);

        void Trace(string messageText);
        void TraceFormat(string messageFormat, params object[] args);

        void Verbose(string messageText);
        void VerboseFormat(string messageFormat, params object[] args);

        void Info(string messageText);
        void InfoFormat(string messageFormat, params object[] args);

        void Alert(string messageText);
        void Alert(Exception error, string messageText);
        void AlertFormat(string messageFormat, params object[] args);

        void Warn(string messageText);
        void Warn(Exception error, string messageText);
        void WarnFormat(string messageFormat, params object[] args);

        void Error(string messageText);
        void Error(Exception error, string messageText);
        void ErrorFormat(string messageFormat, params object[] args);

        void Fatal(string messageText);
        void Fatal(Exception error, string messageText);
        void FatalFormat(string messageFormat, params object[] args);

        ITrace BeginOperation(string messageText);
        ITrace BeginOperationFormat(string messageFormat, params object[] args);

        void UpdateProgress(int progressPercentage, string messageText);
        void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args);
    }
}