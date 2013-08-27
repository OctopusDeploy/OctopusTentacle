using System;
using Microsoft.Web.Publishing.Tasks;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Integration.Transforms
{
    public class TransformationLogger : IXmlTransformationLogger
    {
        readonly ILog log;

        public TransformationLogger(ILog log)
        {
            this.log = log;
        }

        public bool WasErrorLogged { get; private set; }
        public bool WasWarningLogged { get; private set; }

        public void LogMessage(string message, params object[] messageArgs)
        {
            log.VerboseFormat(message, messageArgs);
        }

        public void LogMessage(MessageType type, string message, params object[] messageArgs)
        {
            log.VerboseFormat(message, messageArgs);
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            WasWarningLogged = true;
            log.WarnFormat("WARNING: " + message, messageArgs);
        }

        public void LogWarning(string file, string message, params object[] messageArgs)
        {
            WasWarningLogged = true;
            log.WarnFormat(string.Format("WARNING: Message: {1}", file, message), messageArgs);
        }

        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            WasWarningLogged = true;
            log.WarnFormat(string.Format("WARNING: LineNumber: {1}, LinePosition: {2}, Message: {3}", file, lineNumber, linePosition, message), messageArgs);
        }

        public void LogError(string message, params object[] messageArgs)
        {
            WasErrorLogged = true;
            log.ErrorFormat(message, messageArgs);
        }

        public void LogError(string file, string message, params object[] messageArgs)
        {
            WasErrorLogged = true;
            log.ErrorFormat(string.Format("ERROR: Message: {1}", file, message), messageArgs);
        }

        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            WasErrorLogged = true;
            log.ErrorFormat(string.Format("ERROR: LineNumber: {1}, LinePosition: {2}, Message: {3}", file, lineNumber, linePosition, message), messageArgs);
        }

        public void LogErrorFromException(Exception ex)
        {
            WasErrorLogged = true;
            log.Error(ex, ex.Message);
        }

        public void LogErrorFromException(Exception ex, string file)
        {
            WasErrorLogged = true;
            log.Error(ex, "ERROR: ");
        }

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
        {
            WasErrorLogged = true;
            log.Error(ex, string.Format("ERROR: LineNumber: {1}, LinePosition: {2}", file, lineNumber, linePosition));
        }

        public void StartSection(string message, params object[] messageArgs)
        {
            log.InfoFormat(message, messageArgs);
        }

        public void StartSection(MessageType type, string message, params object[] messageArgs)
        {
            log.VerboseFormat(message, messageArgs);
        }

        public void EndSection(string message, params object[] messageArgs)
        {
            log.VerboseFormat(message, messageArgs);
        }

        public void EndSection(MessageType type, string message, params object[] messageArgs)
        {
            log.VerboseFormat(message, messageArgs);
        }
    }
}