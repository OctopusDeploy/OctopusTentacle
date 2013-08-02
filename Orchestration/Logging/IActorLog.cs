using System;
using Octopus.Shared.Platform.Logging;
using Pipefish;

namespace Octopus.Shared.Orchestration.Logging
{
    public interface IActorLog : IAspect
    {
        void Write(LoggerReference logContext, ActivityLogCategory category, string messageText);
        void Write(LoggerReference logContext, ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(LoggerReference logContext, ActivityLogCategory category, string messageFormat, params object[] args);
        void Write(ActivityLogCategory category, string messageText);
        void Write(ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(ActivityLogCategory category, string messageFormat, params object[] args);

        void Verbose(LoggerReference logContext, string messageText);
        void VerboseFormat(LoggerReference logContext, string messageFormat, params object[] args);
        void Verbose(string messageText);
        void VerboseFormat(string messageFormat, params object[] args);

        void Info(LoggerReference logContext, string messageText);
        void InfoFormat(LoggerReference logContext, string messageFormat, params object[] args);
        void Info(string messageText);
        void InfoFormat(string messageFormat, params object[] args);

        void Warn(LoggerReference logContext, string messageText);
        void Warn(LoggerReference logContext, Exception error, string messageText);
        void WarnFormat(LoggerReference logContext, string messageFormat, params object[] args);
        void Warn(string messageText);
        void Warn(Exception error, string messageText);
        void WarnFormat(string messageFormat, params object[] args);

        void Error(LoggerReference logContext, string messageText);
        void Error(LoggerReference logContext, Exception error, string messageText);
        void ErrorFormat(LoggerReference logContext, string messageFormat, params object[] args);
        void Error(string messageText);
        void Error(Exception error, string messageText);
        void ErrorFormat(string messageFormat, params object[] args);
    }
}