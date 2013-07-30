using System;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    public interface IActorLog : IAspect
    {
        void Write(ActivityLogContext logContext, ActivityLogCategory category, string messageText);
        void Write(ActivityLogContext logContext, ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(ActivityLogContext logContext, ActivityLogCategory category, string messageFormat, params object[] args);

        void Verbose(ActivityLogContext logContext, string messageText);
        void VerboseFormat(ActivityLogContext logContext, string messageFormat, params object[] args);
        
        void Info(ActivityLogContext logContext, string messageText);
        void InfoFormat(ActivityLogContext logContext, string messageFormat, params object[] args);
        
        void Warn(ActivityLogContext logContext, string messageText);
        void Warn(ActivityLogContext logContext, Exception error, string messageText);
        void WarnFormat(ActivityLogContext logContext, string messageFormat, params object[] args);

        void Error(ActivityLogContext logContext, string messageText);
        void Error(ActivityLogContext logContext, Exception error, string messageText);
        void ErrorFormat(ActivityLogContext logContext, string messageFormat, params object[] args);
    }
}