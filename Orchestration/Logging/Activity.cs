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

        readonly ITrace diagnostics = Log.Octopus();
        
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
                    Fatal(AspectData, ex, ex.GetErrorSummary());
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }

            base.OnError(message, ex, ref swallow);
        }

        public override void OnDetaching()
        {
            try
            {
                if (AspectData != null)
                    Finished();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }

            base.OnDetaching();
        }

        void SendToLoggerActor(LoggerReference logger, TraceCategory category, string messageText)
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

        public virtual void Write(LoggerReference logger, TraceCategory category, string messageText)
        {
            diagnostics.Write(category, messageText);
            if (category <= TraceCategory.Trace)
                return;

            SendToLoggerActor(logger, category, messageText);
        }

        public virtual void Write(LoggerReference logger, TraceCategory category, Exception error, string messageText)
        {
            diagnostics.Write(category, error, messageText);
            if (category <= TraceCategory.Trace)
                return;

            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            message.AppendLine(error.GetRootError().ToString());

            SendToLoggerActor(logger, category, message.ToString());
        }

        public void Write(TraceCategory category, string messageText)
        {
            Write(null, category, messageText);
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            Write(null, category, error, messageText);
        }

        public void WriteFormat(TraceCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(null, category, messageFormat, args);
        }

        public void Trace(string messageText)
        {
            Write(TraceCategory.Trace, messageText);
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            Write(TraceCategory.Trace, string.Format(messageFormat, args));
        }

        public void WriteFormat(LoggerReference logger, TraceCategory category, string messageFormat, params object[] args)
        {
            Write(logger, category, string.Format(messageFormat, args));
        }

        public void Verbose(string messageText)
        {
            Verbose(null, messageText);
        }

        public void Verbose(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Verbose, messageText);
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
            WriteFormat(logger, TraceCategory.Verbose, messageFormat, args);
        }

        public void Info(string messageText)
        {
            Info(null, messageText);
        }

        public void Info(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Info, messageText);
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            InfoFormat(null, messageFormat, args);
        }

        public void Alert(string messageText)
        {
            Write(null, TraceCategory.Alert, messageText);
        }

        public void Alert(Exception error, string messageText)
        {
            Write(null, TraceCategory.Alert, error, messageText);
        }

        public void AlertFormat(string messageFormat, params object[] args)
        {
            WriteFormat(null, TraceCategory.Alert, messageFormat, args);
        }

        public void Alert(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Alert, messageText);
        }

        public void Alert(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Alert, error, messageText);
        }

        public void AlertFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Alert, messageFormat, args);
        }

        public void InfoFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Info, messageFormat, args);
        }

        public void Warn(string messageText)
        {
            Warn((LoggerReference)null, messageText);
        }

        public void Warn(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Warning, messageText);
        }

        public void Warn(Exception error, string messageText)
        {
            Warn(null, error, messageText);
        }

        public void Warn(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Warning, error, messageText);
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            WarnFormat(null, messageFormat, args);
        }

        public void WarnFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Warning, messageFormat, args);
        }

        public void Error(string messageText)
        {
            Error((LoggerReference)null, messageText);
        }

        public void Error(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Error, messageText);
        }

        public void Error(Exception error, string messageText)
        {
            Error(null, error, messageText);
        }

        public void Error(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Error, error, messageText);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            ErrorFormat(null, messageFormat, args);
        }

        public void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Error, messageFormat, args);
        }

        public void Fatal(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Fatal, messageText);
        }

        public void Fatal(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Fatal, messageFormat, args);
        }

        public void Fatal(string messageText)
        {
            Write(TraceCategory.Fatal, messageText);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Fatal, messageFormat, args);
        }

        public LoggerReference PlanChild(string message)
        {
            return PlanChild(null, message);
        }

        public LoggerReference PlanChild(LoggerReference logger, string message)
        {
            logger = EnsureLogger(logger).CreateChild();
            SendToLoggerActor(logger, ProgressMessageCategory.Planned, 0, message);
            return logger;
        }

        public LoggerReference PlanChildFormat(string messageFormat, params object[] args)
        {
            return PlanChild(string.Format(messageFormat, args));
        }

        public LoggerReference PlanChildFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            return PlanChild(logger, string.Format(messageFormat, args));
        }

        public void Abandoned()
        {
            Abandoned(null);
        }

        public void Abandoned(LoggerReference logger)
        {
            SendToLoggerActor(logger, ProgressMessageCategory.Abandoned, 0, null);
        }

        public void UpdateProgress(LoggerReference logger, int progressPercentage, string message)
        {
            SendToLoggerActor(logger, ProgressMessageCategory.Updated, progressPercentage, message);
        }

        public void UpdateProgressFormat(LoggerReference logger, int progressPercentage, string messageFormat, params object[] args)
        {
            SendToLoggerActor(logger, ProgressMessageCategory.Updated, progressPercentage, string.Format(messageFormat, args));
        }

        public void Finished()
        {
            Finished(null);
        }

        public void Finished(LoggerReference logger)
        {
            SendToLoggerActor(logger, ProgressMessageCategory.Finished, 100, string.Empty);
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