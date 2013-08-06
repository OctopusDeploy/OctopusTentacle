using System;
using Octopus.Shared.Platform.Logging;
using Pipefish;

namespace Octopus.Shared.Orchestration.Logging
{
    public interface IActivity
    {
        void Write(LoggerReference logger, ActivityLogCategory category, string messageText);
        void Write(LoggerReference logger, ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(LoggerReference logger, ActivityLogCategory category, string messageFormat, params object[] args);
        void Write(ActivityLogCategory category, string messageText);
        void Write(ActivityLogCategory category, Exception error, string messageText);
        void WriteFormat(ActivityLogCategory category, string messageFormat, params object[] args);

        void Verbose(LoggerReference logger, string messageText);
        void VerboseFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Verbose(string messageText);
        void VerboseFormat(string messageFormat, params object[] args);

        void Info(LoggerReference logger, string messageText);
        void InfoFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Info(string messageText);
        void InfoFormat(string messageFormat, params object[] args);

        void Alert(string messageText);
        void Alert(Exception error, string messageText);
        void AlertFormat(string messageFormat, params object[] args);
        void Alert(LoggerReference logger, string messageText);
        void Alert(LoggerReference logger, Exception error, string messageText);
        void AlertFormat(LoggerReference logger, string messageFormat, params object[] args);

        void Warn(LoggerReference logger, string messageText);
        void Warn(LoggerReference logger, Exception error, string messageText);
        void WarnFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Warn(string messageText);
        void Warn(Exception error, string messageText);
        void WarnFormat(string messageFormat, params object[] args);

        void Error(LoggerReference logger, string messageText);
        void Error(LoggerReference logger, Exception error, string messageText);
        void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Error(string messageText);
        void Error(Exception error, string messageText);
        void ErrorFormat(string messageFormat, params object[] args);

        void Fatal(LoggerReference logger, string messageText);
        void Fatal(LoggerReference logger, Exception error, string messageText);
        void FatalFormat(LoggerReference logger, string messageFormat, params object[] args);
        void Fatal(string messageText);
        void Fatal(Exception error, string messageText);
        void FatalFormat(string messageFormat, params object[] args);
    }
}