using System;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;
using Pipefish.Core;
using Pipefish.Toolkit.AspectUtility;

namespace Octopus.Shared.Orchestration.Logging
{
    public class Activity : PersistentAspect<LoggerReference>, IActivity
    {
        const string DefaultLoggerStateKey = "ActorLog.DefaultLogger";

        readonly ILog diagnostics = Log.Octopus();
        
        public Activity()
            : base(DefaultLoggerStateKey)
        {
        }

        public override bool OnReceiving(Message message)
        {
            if (AspectData == null)
            {
                var bodyWithLogger = message.Body as IMessageWithLogger;
                if (bodyWithLogger != null)
                {
                    AspectData = bodyWithLogger.Logger;
                }
            }

            return base.OnReceiving(message);
        }

        public void Write(ActivityLogCategory category, string messageText)
        {
            Write(null, category, messageText);
        }

        public virtual void Write(LoggerReference  logger, ActivityLogCategory category, string messageText)
        {
            WriteToDiagnostics(category, messageText);
            SendToLoggerActor( logger ?? AspectData, category, messageText);
        }

        void WriteToDiagnostics(ActivityLogCategory category, string messageText)
        {
            switch (category)
            {
                case ActivityLogCategory.Verbose:
                    diagnostics.Debug(messageText);
                    break;
                case ActivityLogCategory.Info:
                    diagnostics.Info(messageText);
                    break;
                case ActivityLogCategory.Warning:
                    diagnostics.Warn(messageText);
                    break;
                case ActivityLogCategory.Error:
                    diagnostics.Error(messageText);
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

        public virtual void Write(LoggerReference logger, ActivityLogCategory category, Exception error, string messageText)
        {
            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            message.AppendLine(error.GetRootError().ToString());
            
            Write( logger, category, message.ToString());
        }

        public void WriteFormat(ActivityLogCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(null, category, messageFormat, args);
        }

        public void WriteFormat(LoggerReference logger, ActivityLogCategory category, string messageFormat, params object[] args)
        {
            Write(logger, category, string.Format(messageFormat, args));
        }

        public void Verbose(string messageText)
        {
            Verbose(null, messageText);
        }

        public void Verbose(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Verbose, messageText);
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
            VerboseFormat(null, messageFormat, args);
        }

        public void VerboseFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Verbose, messageFormat, args);
        }

        public void Info(string messageText)
        {
            Info(null, messageText);
        }

        public void Info(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Info, messageText);
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            InfoFormat(null, messageFormat, args);
        }

        public void Alert(string messageText)
        {
            Write(null, ActivityLogCategory.Alert, messageText);
        }

        public void Alert(Exception error, string messageText)
        {
            Write(null, ActivityLogCategory.Alert, error, messageText);
        }

        public void AlertFormat(string messageFormat, params object[] args)
        {
            WriteFormat(null, ActivityLogCategory.Alert, messageFormat, args);
        }

        public void Alert(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Alert, messageText);
        }

        public void Alert(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, ActivityLogCategory.Alert, error, messageText);
        }

        public void AlertFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Alert, messageFormat, args);
        }

        public void InfoFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Info, messageFormat, args);
        }

        public void Warn(string messageText)
        {
            Warn((LoggerReference)null, messageText);
        }

        public void Warn(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Warning, messageText);
        }

        public void Warn(Exception error, string messageText)
        {
            Warn(null, error, messageText);
        }

        public void Warn(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, ActivityLogCategory.Warning, error, messageText);
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            WarnFormat(null, messageFormat, args);
        }

        public void WarnFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Warning, messageFormat, args);
        }

        public void Error(string messageText)
        {
            Error((LoggerReference)null, messageText);
        }

        public void Error(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Error, messageText);
        }

        public void Error(Exception error, string messageText)
        {
            Error(null, error, messageText);
        }

        public void Error(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, ActivityLogCategory.Error, error, messageText);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            ErrorFormat(null, messageFormat, args);
        }

        public void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Error, messageFormat, args);
        }

        public void Fatal(LoggerReference logger, string messageText)
        {
            Write(logger, ActivityLogCategory.Fatal, messageText);
        }

        public void Fatal(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, ActivityLogCategory.Fatal, error, messageText);
        }

        public void FatalFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, ActivityLogCategory.Fatal, messageFormat, args);
        }

        public void Fatal(string messageText)
        {
            Write(ActivityLogCategory.Fatal, messageText);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(ActivityLogCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            WriteFormat(ActivityLogCategory.Fatal, messageFormat, args);
        }
    }
}