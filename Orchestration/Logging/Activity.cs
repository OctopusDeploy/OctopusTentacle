using System;
using System.Text;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Platform;
using Octopus.Shared.Platform.Logging;
using Pipefish;
using Pipefish.Core;
using Pipefish.Toolkit.AspectUtility;

namespace Octopus.Shared.Orchestration.Logging
{
    public class Activity : PersistentAspect<LoggerReference>, IActivity
    {
        readonly ILog diagnostics = Log.Octopus();
        
        public Activity() : base(typeof(Activity).FullName)
        {
        }

        public override Intervention OnReceiving(Message message)
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

        public override Intervention OnError(Message message, Exception ex)
        {
            try
            {
                if (AspectData != null)
                    Fatal(AspectData, ex, ex.GetErrorSummary());
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }

            return base.OnError(message, ex);
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

        void SendToLoggerActor(LoggerReference logger, Func<string, IMessage> messageBuilder)
        {
            logger = EnsureLogger(logger);
            Send(new ActorId(logger.LoggerActorId), messageBuilder(logger.CorrelationId));
        }

        void SendToLoggerActor(LoggerReference logger, TraceCategory category, string messageText)
        {
            SendToLoggerActor(logger, correlationId => new LogMessage(correlationId, category, messageText));
        }

        void SendToLoggerActor(LoggerReference logger, ProgressMessageCategory category, int percentage, string messageText)
        {
            SendToLoggerActor(logger, correlationId => new ProgressMessage(correlationId, category, percentage, messageText));
        }

        public LoggerReference DefaultLogger { get { return EnsureLogger(null); } }

        public virtual void Write(LoggerReference logger, TraceCategory category, string messageText)
        {
            diagnostics.Write(category, messageText);
            if (category <= TraceCategory.Trace)
                return;

            SendToLoggerActor(logger, category, messageText);
        }

        public void Write(LoggerReference logger, TraceCategory category, Exception error)
        {
            if (error == null) return;
            Write(logger, category, error, error.GetErrorSummary());
        }

        public virtual void Write(LoggerReference logger, TraceCategory category, Exception error, string messageText)
        {
            diagnostics.Write(category, error, messageText);
            if (category <= TraceCategory.Trace)
                return;

            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(messageText))
                message.AppendLine(messageText);
            if (error != null)
                message.AppendLine(error.GetRootError().ToString());

            SendToLoggerActor(logger, category, message.ToString());
        }

        public bool IsVerboseEnabled { get { return true; } }
        public bool IsErrorEnabled { get { return true; } }
        public bool IsFatalEnabled { get { return true; } }
        public bool IsInfoEnabled { get { return true; } }
        public bool IsTraceEnabled { get { return false; } }
        public bool IsWarnEnabled { get { return true; } }

        public bool IsEnabled(TraceCategory category)
        {
            switch (category)
            {
                case TraceCategory.Trace:
                    return false;
                default:
                    return true;
            }
        }

        public void Write(TraceCategory category, string messageText)
        {
            Write(null, category, messageText);
        }

        public void Write(TraceCategory category, Exception error)
        {
            if (error == null) return;
            Write(null, category, error, error.GetErrorSummary());
        }

        public void Write(TraceCategory category, Exception error, string messageText)
        {
            Write(null, category, error, messageText);
        }

        public void WriteFormat(TraceCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(null, category, messageFormat, args);
        }

        public void WriteFormat(LoggerReference logger, TraceCategory category, string messageFormat, params object[] args)
        {
            Write(logger, category, string.Format(messageFormat, args));
        }

        public void WriteFormat(LoggerReference logger, TraceCategory category, Exception error, string messageFormat, params object[] args)
        {
            Write(logger, category, error, string.Format(messageFormat, args));
        }

        public void WriteFormat(TraceCategory category, Exception error, string messageFormat, params object[] args)
        {
            Write(null, category, error, string.Format(messageFormat, args));
        }

        public void Trace(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Trace, messageText);
        }

        public void TraceFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Trace, messageFormat, args);
        }

        public void Trace(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Trace, error);
        }

        public void Trace(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Trace, error, messageText);
        }

        public void TraceFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Trace, error, format, args);
        }

        public void Trace(string messageText)
        {
            Write(TraceCategory.Trace, messageText);
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            Write(TraceCategory.Trace, string.Format(messageFormat, args));
        }

        public void Trace(Exception error)
        {
            Write(TraceCategory.Trace, error);
        }

        public void Trace(Exception error, string messageText)
        {
            Write(TraceCategory.Trace, error, messageText);
        }

        public void TraceFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Trace, error, format, args);
        }

        public void Verbose(string messageText)
        {
            Write(TraceCategory.Verbose, messageText);
        }

        public void Verbose(Exception error)
        {
            Write(TraceCategory.Verbose, error);
        }

        public void Verbose(Exception error, string messageText)
        {
            Write(TraceCategory.Verbose, error, messageText);
        }

        public void Verbose(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Verbose, messageText);
        }

        public void Verbose(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Verbose, error);
        }

        public void Verbose(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Verbose, error, messageText);
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Verbose, messageFormat, args);
        }

        public void VerboseFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Verbose, error, format, args);
        }

        public LoggerReference CreateChild(string messageText)
        {
            return CreateChild(null, messageText);
        }

        public LoggerReference CreateChild(LoggerReference logger, string messageText)
        {
            var child = EnsureLogger(logger).CreateChild();
            diagnostics.TraceFormat("Creating child activity {0}: {1}", child.CorrelationId, messageText);
            Info(child, messageText);
            return child;
        }

        public LoggerReference CreateChildFormat(string messageFormat, params object[] args)
        {
            return CreateChildFormat(null, messageFormat, args);
        }

        public LoggerReference CreateChildFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            return CreateChild(logger, string.Format(messageFormat, args));
        }

        public void VerboseFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Verbose, messageFormat, args);
        }

        public void VerboseFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Verbose, error, format, args);
        }

        public void Info(string messageText)
        {
            Write(TraceCategory.Info, messageText);
        }

        public void Info(Exception error)
        {
            Write(TraceCategory.Info, error);
        }

        public void Info(Exception error, string messageText)
        {
            Write(TraceCategory.Info, error, messageText);
        }

        public void Info(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Info, messageText);
        }

        public void Info(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Info, error);
        }

        public void Info(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Info, error, messageText);
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Info, messageFormat, args);
        }

        public void InfoFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Info, error, format, args);
        }

        public void InfoFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Info, error, format, args);
        }

        public void InfoFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Info, messageFormat, args);
        }

        public void Alert(string messageText)
        {
            Write(TraceCategory.Alert, messageText);
        }

        public void Alert(Exception error)
        {
            Write(TraceCategory.Alert, error);
        }

        public void Alert(Exception error, string messageText)
        {
            Write(TraceCategory.Alert, error, messageText);
        }

        public void AlertFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Alert, messageFormat, args);
        }

        public void AlertFormat(Exception error, string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Alert, error, messageFormat, args);
        }

        public void Alert(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Alert, messageText);
        }

        public void Alert(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Alert, error);
        }

        public void Alert(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Alert, error, messageText);
        }

        public void AlertFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Alert, messageFormat, args);
        }

        public void AlertFormat(LoggerReference logger, Exception error, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Alert, error, messageFormat, args);
        }

        public void Warn(string messageText)
        {
            Write(TraceCategory.Warning, messageText);
        }

        public void Warn(Exception error)
        {
            Write(TraceCategory.Warning, error);
        }

        public void Warn(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Warning, messageText);
        }

        public void Warn(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Warning, error);
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
            WriteFormat(TraceCategory.Warning, messageFormat, args);
        }

        public void WarnFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Warning, error, format, args);
        }

        public void WarnFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Warning, messageFormat, args);
        }

        public void WarnFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Warning, error, format, args);
        }

        public void Error(string messageText)
        {
            Write(TraceCategory.Error, messageText);
        }

        public void Error(Exception error)
        {
            Write(TraceCategory.Error, error);
        }

        public void Error(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Error, messageText);
        }

        public void Error(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Error, error);
        }

        public void Error(Exception error, string messageText)
        {
            Write(TraceCategory.Error, error, messageText);
        }

        public void Error(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Error, error, messageText);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Error, messageFormat, args);
        }

        public void ErrorFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Error, error, format, args);
        }

        public void ErrorFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Error, messageFormat, args);
        }

        public void ErrorFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Error, error, format, args);
        }

        public void Fatal(LoggerReference logger, string messageText)
        {
            Write(logger, TraceCategory.Fatal, messageText);
        }

        public void Fatal(LoggerReference logger, Exception error)
        {
            Write(logger, TraceCategory.Fatal, error);
        }

        public void Fatal(LoggerReference logger, Exception error, string messageText)
        {
            Write(logger, TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Fatal, messageFormat, args);
        }

        public void FatalFormat(LoggerReference logger, Exception error, string format, params object[] args)
        {
            WriteFormat(logger, TraceCategory.Fatal, error, format, args);
        }

        public void Fatal(string messageText)
        {
            Write(TraceCategory.Fatal, messageText);
        }

        public void Fatal(Exception error)
        {
            Write(TraceCategory.Fatal, error);
        }

        public void Fatal(Exception error, string messageText)
        {
            Write(TraceCategory.Fatal, error, messageText);
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            WriteFormat(TraceCategory.Fatal, messageFormat, args);
        }

        public void FatalFormat(Exception error, string format, params object[] args)
        {
            WriteFormat(TraceCategory.Fatal, error, format, args);
        }

        public ILog BeginOperation(string messageText)
        {
            return new ChildActivityLog(messageText, this, EnsureLogger(null));
        }

        public ILog BeginOperationFormat(string messageFormat, params object[] args)
        {
            return BeginOperation(string.Format(messageFormat, args));
        }

        public LoggerReference PlanChild(string message)
        {
            return PlanChild(null, message);
        }

        public LoggerReference PlanChild(LoggerReference logger, string message)
        {
            var child = EnsureLogger(logger).CreateChild();
            diagnostics.TraceFormat("Planning {0}: {1}", child.CorrelationId, message);
            SendToLoggerActor(child, ProgressMessageCategory.Planned, 0, message);
            return child;
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
            logger = EnsureLogger(logger);
            diagnostics.TraceFormat("Abandoning planned activity {0}", logger.CorrelationId);
            SendToLoggerActor(logger, ProgressMessageCategory.Abandoned, 0, null);
        }

        public void UpdateProgress(int progressPercentage, string messageText)
        {
            UpdateProgress(null, progressPercentage, messageText);
        }

        public void UpdateProgress(LoggerReference logger, int progressPercentage, string message)
        {
            logger = EnsureLogger(logger);
            diagnostics.UpdateProgress(progressPercentage, message);
            SendToLoggerActor(logger, ProgressMessageCategory.Updated, progressPercentage, message);
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgressFormat(null, progressPercentage, messageFormat, args);
        }

        public void UpdateProgressFormat(LoggerReference logger, int progressPercentage, string messageFormat, params object[] args)
        {
            UpdateProgress(logger, progressPercentage, string.Format(messageFormat, args));
        }

        public void Finished()
        {
            Finished(null);
        }

        public void Finished(LoggerReference logger)
        {
            logger = EnsureLogger(logger);
            diagnostics.TraceFormat("Finished {0}", logger.CorrelationId);
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