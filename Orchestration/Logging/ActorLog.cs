using System;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;
using Pipefish.Core;
using Pipefish.Hosting;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    public class ActorLog : Aspect, IActorLog
    {
        readonly ILog log;
        
        public ActorLog(ILog log)
        {
            this.log = log;
        }

        public virtual void Write(LoggerReference logContext, ActivityLogCategory category, string messageText)
        {
            switch (category)
            {
                case ActivityLogCategory.Verbose:
                    log.Debug(messageText);
                    break;
                case ActivityLogCategory.Info:
                    log.Info(messageText);
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case ActivityLogCategory.Warning:
                    log.Warn(messageText);
                    break;
                case ActivityLogCategory.Error:
                    log.Error(messageText);
                    break;
            }

            if (logContext == null)
                throw new ArgumentNullException("logContext", "You must pass a logging context in order for messages to be logged.");
            
            if (string.IsNullOrWhiteSpace(logContext.LoggerActorId))
                throw new InvalidOperationException("The given logging context does not specify an actor ID");

            Send(new ActorId(logContext.LoggerActorId), new LogMessage(logContext.CorrelationId, category, messageText));
        }

        public virtual void Write(LoggerReference logContext, ActivityLogCategory category, Exception error, string messageText)
        {
            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            message.AppendLine(error.GetRootError().ToString());
            
            Write(logContext, category, message.ToString());
        }

        public void WriteFormat(LoggerReference logContext, ActivityLogCategory category, string messageFormat, params object[] args)
        {
            Write(logContext, category, string.Format(messageFormat, args));
        }

        public void Verbose(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Verbose, messageText);
        }

        public void VerboseFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Verbose, messageFormat, args);
        }

        public void Info(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Info, messageText);
        }

        public void InfoFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Info, messageFormat, args);
        }

        public void Warn(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, messageText);
        }

        public void Warn(LoggerReference logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, error, messageText);
        }

        public void WarnFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Warning, messageFormat, args);
        }

        public void Error(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, messageText);
        }

        public void Error(LoggerReference logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, error, messageText);
        }

        public void ErrorFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Error, messageFormat, args);
        }
    }
}