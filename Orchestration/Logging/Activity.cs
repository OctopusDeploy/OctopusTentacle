using System;
using System.Text;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Util;
using Pipefish;
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

        public override void OnError(Message message, Exception ex, ref bool swallow)
        {
            try
            {
                if (AspectData != null)
                    Fatal(AspectData, ex, "An unhandled exception was detected.");
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }

            base.OnError(message, ex, ref swallow);
        }

        public void Write(ActivityLogCategory category, string messageText)
        {
            Write(null, category, messageText);
        }

        public virtual void Write(LoggerReference logger, ActivityLogCategory category, string messageText)
        {
            WriteToDiagnostics(category, messageText);
            SendToLoggerActor(logger, category, messageText);
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

        void SendToLoggerActor(LoggerReference logger, ActivityLogCategory category, string messageText)
        {
            SendToLoggerActor(logger, correlationId => new LogMessage(correlationId, category, messageText));
        }

        void SendToLoggerActor(LoggerReference logger, ProgressMessageCategory category, int percentage, string messageText)
        {
            SendToLoggerActor(logger, correlationId => new ProgressMessage(correlationId, category, percentage, messageText));
        }

        void SendToLoggerActor(LoggerReference logger, Func<string, IMessage> messageBuilder)
        {
            logger = EnsureLogger(logger);
            Send(new ActorId(logger.LoggerActorId), messageBuilder(logger.CorrelationId));
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

        public LoggerReference CreateChild(string messageText)
        {
            return CreateChild(null, messageText);
        }

        public LoggerReference CreateChild(LoggerReference logger, string messageText)
        {
            var child = EnsureLogger(logger).CreateChild();
            Info(child, messageText);
            return child;
        }

        public LoggerReference CreateChildFormat(string messageFormat, params object[] args)
        {
            return CreateChildFormat(null, messageFormat, args);
        }

        public LoggerReference CreateChildFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            var child = EnsureLogger(logger).CreateChild();
            InfoFormat(child, messageFormat, args);
            return child;
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

        public LoggerReference ProgressStarted(string message)
        {
            return ProgressStarted(null, message);
        }

        public LoggerReference ProgressStarted(LoggerReference logger, string message)
        {
            logger = EnsureLogger(logger).CreateChild();
            SendToLoggerActor(logger, ProgressMessageCategory.ProgressMessage, 0, message);
            return logger;
        }

        public void ProgressMessage(LoggerReference progressStartedLogger, int progressPercentage, string message)
        {
            SendToLoggerActor(progressStartedLogger, ProgressMessageCategory.ProgressMessage, 0, message);
        }

        public void ProgressMessageFormat(LoggerReference progressStartedLogger, int progressPercentage, string messageFormat, params object[] args)
        {
            SendToLoggerActor(progressStartedLogger, ProgressMessageCategory.ProgressMessage, progressPercentage, string.Format(messageFormat, args));
        }

        public void ProgressFinished(LoggerReference progressStartedLogger)
        {
            SendToLoggerActor(progressStartedLogger, ProgressMessageCategory.ProgressFinished, 100, string.Empty);
        }

        LoggerReference EnsureLogger(LoggerReference logger)
        {
            logger = logger ?? AspectData;
            if (logger == null)
                throw new ArgumentNullException("logger", "A null logger reference was passed, and the current message does not have a logger associated. You must pass a logger reference in order for messages to be logged.");

            if (string.IsNullOrWhiteSpace(logger.LoggerActorId))
                throw new InvalidOperationException("The given logger reference does not specify an actor ID");

            return logger;
        }
    }
}