using System;
using System.Reflection;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Messages;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Core;
using Pipefish.Errors;
using Pipefish.Hosting;

namespace Octopus.Shared.Logging
{
    public class Activity : PersistentAspect<LoggerReference>, IActivity
    {
        static readonly TimeSpan LogMessageTtl = TimeSpan.FromDays(7);

        readonly ILog diagnostics = Log.Octopus();

        public Activity()
            : base(typeof(Activity).FullName)
        {
        }

        public override Intervention OnReceiving(Message message)
        {
            if (AspectData == null)
            {
                var bodyWithLogger = message.Body as ICorrelatedMessage;
                if (bodyWithLogger != null)
                {
                    AspectData = bodyWithLogger.Logger;
                }
            }

            return base.OnReceiving(message);
        }

        void SendToLoggerActor(LoggerReference logger, Func<string, IMessage> messageBuilder)
        {
            logger = EnsureLogger(logger);
            Send(new ActorId(logger.LoggerActorId), messageBuilder(logger.CorrelationId), LogMessageTtl);
        }

        void SendToLoggerActor(LoggerReference logger, TraceCategory category, string messageText, string detail)
        {
            SendToLoggerActor(logger, correlationId => new LogMessageEvent(correlationId, category, messageText, detail));
        }

        void SendToLoggerActor(LoggerReference logger, ProgressMessageCategory category, int percentage, string messageText)
        {
            SendToLoggerActor(logger, correlationId => new ProgressMessageEvent(correlationId, category, percentage, messageText));
        }

        public LoggerReference DefaultLogger { get { return EnsureLogger(null); } }

        public virtual void Write(LoggerReference logger, TraceCategory category, string messageText)
        {
            Write(logger, category, null, messageText);
        }

        public void Write(LoggerReference logger, TraceCategory category, Exception error)
        {
            Write(logger, category, error, null);
        }

        // This method ensures that all information present in the error and message text are represented in the
        // log event, with minimal duplication.
        public virtual void Write(LoggerReference logger, TraceCategory category, Exception error, string messageText)
        {
            var message = string.IsNullOrWhiteSpace(messageText) ? null : messageText;

            if (error == null && message == null)
            {
                diagnostics.WarnFormat("A {0} message was written to {1} with neither text nor error", category, EnsureLogger(logger).CorrelationId);
                return;
            }

            if (category <= TraceCategory.Trace)
                return;

            string detail = null;
            if (error != null)
            {
                string errorMessage, errorDetail;
                
                var unpacked = error.UnpackFromContainers();
                var communicationException = unpacked as PipefishCommunicationException;
                if (communicationException != null)
                {
                    var er = communicationException.ToError();
                    errorMessage = er.Message;
                    errorDetail = er.Detail;
                }
                else
                {
                    errorMessage = unpacked.Message;
                    errorDetail = unpacked.ToString();
                }
                
                if (message == null)
                {
                    message = errorMessage;
                    detail = errorDetail;
                }
                else
                {
                    if (message != errorMessage && (errorDetail == null || !errorDetail.StartsWith(errorMessage)))
                        detail = errorMessage + Environment.NewLine;

                    if (errorDetail != null)
                    {
                        if (!string.IsNullOrWhiteSpace(detail))
                            detail += errorDetail;
                        else
                            detail = errorDetail;
                    }
                }

                detail = (detail ?? string.Empty).TrimEnd();
                if (detail.Length > 0)
                {
                    detail += Environment.NewLine;
                }
                var assembly = Assembly.GetEntryAssembly() ?? typeof(Activity).Assembly;
                detail += assembly.GetName().Name + " version " + assembly.GetFileVersion();
            }

            SendToLoggerActor(logger, category, message, detail);
        }

        public bool IsVerboseEnabled { get { return true; } }
        public bool IsErrorEnabled { get { return true; } }
        public bool IsFatalEnabled { get { return true; } }
        public bool IsInfoEnabled { get { return true; } }
        public bool IsTraceEnabled { get { return diagnostics.IsTraceEnabled; } }
        public bool IsWarnEnabled { get { return true; } }

        public bool IsEnabled(TraceCategory category)
        {
            switch (category)
            {
                case TraceCategory.Trace:
                    return diagnostics.IsTraceEnabled;
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
            Write(null, category, error);
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
            Write(logger, category, AbstractLog.SafeFormat(messageFormat, args));
        }

        public void WriteFormat(LoggerReference logger, TraceCategory category, Exception error, string messageFormat, params object[] args)
        {
            Write(logger, category, error, AbstractLog.SafeFormat(messageFormat, args));
        }

        public void WriteFormat(TraceCategory category, Exception error, string messageFormat, params object[] args)
        {
            Write(null, category, error, AbstractLog.SafeFormat(messageFormat, args));
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
            Write(TraceCategory.Trace, AbstractLog.SafeFormat(messageFormat, args));
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
            return CreateChild(logger, AbstractLog.SafeFormat(messageFormat, args));
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
            Write(TraceCategory.Warning, error, messageText);
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

        public ILog DetachLog()
        {
            return DetachLog(null);
        }

        public ILog DetachLog(LoggerReference logger)
        {
            return new DetachedActivityLog(Space.UnwrapDecorators(), EnsureLogger(logger));
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
            return BeginOperation(AbstractLog.SafeFormat(messageFormat, args));
        }

        public void EndOperation()
        {
            Finished();
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
            return PlanChild(AbstractLog.SafeFormat(messageFormat, args));
        }

        public LoggerReference PlanChildFormat(LoggerReference logger, string messageFormat, params object[] args)
        {
            return PlanChild(logger, AbstractLog.SafeFormat(messageFormat, args));
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

        public void Reinstated()
        {
            Reinstated(null);
        }

        public void Reinstated(LoggerReference logger)
        {
            logger = EnsureLogger(logger);
            diagnostics.TraceFormat("Resurrecting {0}", logger.CorrelationId);
            SendToLoggerActor(logger, ProgressMessageCategory.Planned, 0, null);
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
            UpdateProgress(logger, progressPercentage, AbstractLog.SafeFormat(messageFormat, args));
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