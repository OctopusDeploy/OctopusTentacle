using System;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    public interface IActorLog : IAspect
    {
        void Write(LoggerReference logContext, ActivityLogCategory category, string messageText);
        void Write(LoggerReference logContext, ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(LoggerReference logContext, ActivityLogCategory category, string messageFormat, params object[] args);

        void Verbose(LoggerReference logContext, string messageText);
        void VerboseFormat(LoggerReference logContext, string messageFormat, params object[] args);
        
        void Info(LoggerReference logContext, string messageText);
        void InfoFormat(LoggerReference logContext, string messageFormat, params object[] args);
        
        void Warn(LoggerReference logContext, string messageText);
        void Warn(LoggerReference logContext, Exception error, string messageText);
        void WarnFormat(LoggerReference logContext, string messageFormat, params object[] args);

        void Error(LoggerReference logContext, string messageText);
        void Error(LoggerReference logContext, Exception error, string messageText);
        void ErrorFormat(LoggerReference logContext, string messageFormat, params object[] args);
    }
}