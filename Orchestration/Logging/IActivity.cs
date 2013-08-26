using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.Logging
{
    public interface IActivity : ITrace
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

        void UpdateProgress(LoggerReference logger, int progressPercentage, string message);
        void UpdateProgressFormat(LoggerReference logger, int progressPercentage, string messageFormat, params object[] args);

        void Finished();
        void Finished(LoggerReference logger);

        void Write(LoggerReference logger, TraceCategory category, string messageText);
        void Write(LoggerReference logger, TraceCategory category, Exception error, string messageText);
        void WriteFormat(LoggerReference logger, TraceCategory category, string messageFormat, params object[] args);

        void Verbose(LoggerReference logger, string messageText);
        void VerboseFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Info(LoggerReference logger, string messageText);
        void InfoFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Alert(LoggerReference logger, string messageText);
        void Alert(LoggerReference logger, Exception error, string messageText);
        void AlertFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Warn(LoggerReference logger, string messageText);
        void Warn(LoggerReference logger, Exception error, string messageText);
        void WarnFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Error(LoggerReference logger, string messageText);
        void Error(LoggerReference logger, Exception error, string messageText);
        void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Fatal(LoggerReference logger, string messageText);
        void Fatal(LoggerReference logger, Exception error, string messageText);
        void FatalFormat(LoggerReference logger, string messageFormat, params object[] args);
    }
}