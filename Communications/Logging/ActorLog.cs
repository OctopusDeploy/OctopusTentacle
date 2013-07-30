using System;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    public class ActorLog : IActorLog
    {
        readonly ILog log;
        IActor currentActor;
        IActivitySpace activitySpace;

        public ActorLog(ILog log)
        {
            this.log = log;
        }

        void IAspect.Attach(IActor actor, IActivitySpace space)
        {
            currentActor = actor;
            activitySpace = space;
        }

        void IAspect.OnReceiving(Message message)
        {
        }

        void IAspect.OnReceived(Message message)
        {
        }

        public virtual void Write(ActivityLogContext logContext, ActivityLogCategory category, string messageText)
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

            var message = new Message(currentActor.Id, logContext.LoggerActorId, new LogMessage(logContext.CorrelationId, category, messageText));
            activitySpace.Send(message);
        }

        public virtual void Write(ActivityLogContext logContext, ActivityLogCategory category, Exception error, string messageText)
        {
            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            message.AppendLine(error.GetRootError().ToString());
            
            Write(logContext, category, message.ToString());
        }

        public void WriteFormat(ActivityLogContext logContext, ActivityLogCategory category, string messageFormat, params object[] args)
        {
            Write(logContext, category, string.Format(messageFormat, args));
        }

        public void Verbose(ActivityLogContext logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Verbose, messageText);
        }

        public void VerboseFormat(ActivityLogContext logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Verbose, messageFormat, args);
        }

        public void Info(ActivityLogContext logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Info, messageText);
        }

        public void InfoFormat(ActivityLogContext logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Info, messageFormat, args);
        }

        public void Warn(ActivityLogContext logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, messageText);
        }

        public void Warn(ActivityLogContext logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, error, messageText);
        }

        public void WarnFormat(ActivityLogContext logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Warning, messageFormat, args);
        }

        public void Error(ActivityLogContext logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, messageText);
        }

        public void Error(ActivityLogContext logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, error, messageText);
        }

        public void ErrorFormat(ActivityLogContext logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Error, messageFormat, args);
        }
    }
}