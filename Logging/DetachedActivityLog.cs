using System;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Core;
using Pipefish.Errors;
using Pipefish.Hosting;

namespace Octopus.Platform.Deployment.Logging
{
    public class DetachedActivityLog : AbstractLog
    {
        static readonly TimeSpan LogMessageTtl = TimeSpan.FromDays(7);

        readonly IActivitySpace space;
        readonly Lazy<LoggerReference> logger;
        readonly ILog diagnostics = Log.Octopus();

        public DetachedActivityLog(IActivitySpace space, Func<LoggerReference> createLogger)
        {
            if (space == null) throw new ArgumentNullException("space");
            this.space = space;
            logger = new Lazy<LoggerReference>(() =>
            {
                var created = createLogger();
                if (created == null)
                    throw new InvalidOperationException("Logger creation can't return null");
                return created;
            });
        }

        public DetachedActivityLog(IActivitySpace space, LoggerReference logger)
        {
            if (space == null) throw new ArgumentNullException("space");
            if (logger == null) throw new ArgumentNullException("logger");
            this.space = space;
            this.logger = new Lazy<LoggerReference>(() => logger);
        }

        void SendToLoggerActor(Func<string, IMessage> messageBuilder)
        {
            SendToLoggerActor(logger.Value, messageBuilder);
        }

        void SendToLoggerActor(LoggerReference specificLogger, Func<string, IMessage> messageBuilder)
        {
            var m = new Message(
                space.WellKnownActorId(WellKnownActors.Anonymous),
                new ActorId(specificLogger.LoggerActorId),
                messageBuilder(specificLogger.CorrelationId));

            m.SetExpiresAt(DateTime.UtcNow.Add(LogMessageTtl));

            space.Send(m);
        }

        void SendToLoggerActor(LoggerReference specificLogger, TraceCategory category, string messageText, string detail)
        {
            SendToLoggerActor(specificLogger, correlationId => new LogMessageEvent(correlationId, category, messageText, detail));
        }

        void SendToLoggerActor(TraceCategory category, string messageText, string detail)
        {
            SendToLoggerActor(correlationId => new LogMessageEvent(correlationId, category, messageText, detail));
        }

        void SendToLoggerActor(ProgressMessageCategory category, int percentage, string messageText)
        {
            SendToLoggerActor(correlationId => new ProgressMessageEvent(correlationId, category, percentage, messageText));
        }

        // This method ensures that all information present in the error and message text are represented in the
        // log event, with minimal duplication.
        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            var message = string.IsNullOrWhiteSpace(messageText) ? null : messageText;

            if (error == null && message == null)
            {
                diagnostics.WarnFormat("A {0} message was written to {1} with neither text nor error", category, logger.Value.CorrelationId);
                return;
            }

            diagnostics.Write(category, error, messageText);

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
            }

            SendToLoggerActor(category, message, detail);
        }

        public override bool IsEnabled(TraceCategory category)
        {
            switch (category)
            {
                case TraceCategory.Trace:
                    return diagnostics.IsTraceEnabled;
                default:
                    return true;
            }
        }

        public override ILog BeginOperation(string messageText)
        {
            return new DetachedActivityLog(space, () =>
            {
                var child = logger.Value.CreateChild();
                diagnostics.TraceFormat("Creating child activity {0}: {1}", child.CorrelationId, messageText);
                SendToLoggerActor(child, TraceCategory.Info, messageText, null);
                return child;
            });
        }

        public override void EndOperation()
        {
            Finished();
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            diagnostics.UpdateProgress(progressPercentage, messageText);
            SendToLoggerActor(ProgressMessageCategory.Updated, progressPercentage, messageText);
        }

        void Finished()
        {
            diagnostics.TraceFormat("Finished {0}", logger.Value.CorrelationId);
            SendToLoggerActor(ProgressMessageCategory.Finished, 100, string.Empty);
        }
    }
}