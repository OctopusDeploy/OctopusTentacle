using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;
using Pipefish.Core;
using Pipefish.Persistence;
using Pipefish.Standard;

namespace Octopus.Shared.Orchestration.Logging
{
    public class ActorLog : Aspect, IActorLog
    {
        const string DefaultLoggerStateKey = "ActorLog.DefaultLogger";

        readonly ILog log;
        LoggerReference defaultLogger;
        
        public ActorLog(ILog log)
        {
            this.log = log;
        }

        public override void Attach(IActor actor, IActivitySpace space)
        {
            base.Attach(actor, space);

            var persistent = actor as IPersistentActor;
            if (persistent == null) return;

            persistent.AfterLoading += () => LoadDefaultLogger(persistent.State);
            persistent.BeforeSaving += () => SaveDefaultLogger(persistent.State);
        }

        void LoadDefaultLogger(IDictionary<string, object> state)
        {
            object savedDefault;
            if (state.TryGetValue(DefaultLoggerStateKey, out savedDefault))
            {
                defaultLogger = (LoggerReference)savedDefault;
            }
        }

        void SaveDefaultLogger(IDictionary<string, object> state)
        {
            if (defaultLogger != null && !state.ContainsKey(DefaultLoggerStateKey))
                state.Add(DefaultLoggerStateKey, defaultLogger);
        }

        public override void OnReceiving(Message message)
        {
            if (defaultLogger != null) return;

            var bodyWithLogger = message.Body as IMessageWithLogger;
            if (bodyWithLogger == null) return;

            defaultLogger = bodyWithLogger.Logger;
        }

        public void Write(ActivityLogCategory category, string messageText)
        {
            Write(null, category, messageText);
        }

        public virtual void Write(LoggerReference logContext, ActivityLogCategory category, string messageText)
        {
            WriteToDiagnostics(category, messageText);
            SendToLoggerActor(logContext ?? defaultLogger, category, messageText);
        }

        void WriteToDiagnostics(ActivityLogCategory category, string messageText)
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
        }

        void SendToLoggerActor(LoggerReference context, ActivityLogCategory category, string messageText)
        {
            if (context == null)
                throw new ArgumentNullException("context", "You must pass a logging context in order for messages to be logged.");

            if (string.IsNullOrWhiteSpace(context.LoggerActorId))
                throw new InvalidOperationException("The given logging context does not specify an actor ID");

            Send(new ActorId(context.LoggerActorId), new LogMessage(context.CorrelationId, category, messageText));
        }

        public void Write(ActivityLogCategory category, Exception error, string messageText)
        {
            Write(null, category, error, messageText);
        }

        public virtual void Write(LoggerReference logContext, ActivityLogCategory category, Exception error, string messageText)
        {
            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            message.AppendLine(error.GetRootError().ToString());
            
            Write(logContext, category, message.ToString());
        }

        public void WriteFormat(ActivityLogCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(null, category, messageFormat, args);
        }

        public void WriteFormat(LoggerReference logContext, ActivityLogCategory category, string messageFormat, params object[] args)
        {
            Write(logContext, category, string.Format(messageFormat, args));
        }

        public void Verbose(string messageText)
        {
            Verbose(null, messageText);
        }

        public void Verbose(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Verbose, messageText);
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
            VerboseFormat(null, messageFormat, args);
        }

        public void VerboseFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Verbose, messageFormat, args);
        }

        public void Info(string messageText)
        {
            Info(null, messageText);
        }

        public void Info(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Info, messageText);
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            InfoFormat(null, messageFormat, args);
        }

        public void InfoFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Info, messageFormat, args);
        }

        public void Warn(string messageText)
        {
            Warn((LoggerReference)null, messageText);
        }

        public void Warn(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, messageText);
        }

        public void Warn(Exception error, string messageText)
        {
            Warn(null, error, messageText);
        }

        public void Warn(LoggerReference logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Warning, error, messageText);
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            WarnFormat(null, messageFormat, args);
        }

        public void WarnFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Warning, messageFormat, args);
        }

        public void Error(string messageText)
        {
            Error((LoggerReference)null, messageText);
        }

        public void Error(LoggerReference logContext, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, messageText);
        }

        public void Error(Exception error, string messageText)
        {
            Error(null, error, messageText);
        }

        public void Error(LoggerReference logContext, Exception error, string messageText)
        {
            Write(logContext, ActivityLogCategory.Error, error, messageText);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            ErrorFormat(null, messageFormat, args);
        }

        public void ErrorFormat(LoggerReference logContext, string messageFormat, params object[] args)
        {
            WriteFormat(logContext, ActivityLogCategory.Error, messageFormat, args);
        }
    }
}