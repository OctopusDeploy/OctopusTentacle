using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Messages;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Core;
using Pipefish.Errors;
using Pipefish.Messages.Delivery;
using Pipefish.Standard;
using Pipefish.Supervision;

namespace Octopus.Platform.Deployment.Logging
{
    // A supervised activity is an actor that 'owns' the activity
    // correlated with its initiating message. If the actor fails,
    // the activity fails. An actor may complete its supervised
    // work by calling any of: Succeed, SucceedWithInfo, Fail. All
    // of these will ensure supervision completion as well as appropriate
    // log entries.
    public class SupervisedActivity : IAspect, ISupervisedActivity
    {
        readonly Supervised supervised;
        readonly Activity activity;

        public SupervisedActivity()
            : this(config => { })
        {
        }

        public SupervisedActivity(Action<SupervisionConfiguration> configure)
        {
            activity = new Activity();
            supervised = new Supervised(config =>
            {
                config.OnFailed(OnFailed);
                config.OnCancelled(() => activity.Abandoned());
                configure(config);
            });
        }

        void OnFailed(string error, Exception exception)
        {
            var unpacked = exception.UnpackFromContainers();
            if (unpacked is ControlledFailureException)
            {
                Log.Octopus().Verbose(unpacked);
                activity.Fatal(unpacked.Message);
            }
            else
            {
                activity.Fatal(exception, error);
            }
        }

        public void Attach(IActor actor, IActivitySpace space)
        {
            activity.Attach(actor, space);
            supervised.Attach(actor, space);
        }
        
        public Intervention OnReceiving(Message message)
        {
            // Return value ignored because the activity
            // aspect is expected to be passive.
            activity.OnReceiving(message);
            var intervention = supervised.OnReceiving(message);

            if (intervention == Intervention.NotHandled)
            {
                RecordDeliveryFailure(message);
            }

            return intervention;
        }

        public Intervention OnReceiveMethodMissing(Message message)
        {
            activity.OnReceiveMethodMissing(message);
            return supervised.OnReceiveMethodMissing(message);
        }

        public void OnReceived(Message message, Exception exceptionIfThrown)
        {
            activity.OnReceived(message, exceptionIfThrown);
            supervised.OnReceived(message, exceptionIfThrown);
        }

        void RecordDeliveryFailure(Message message)
        {
            // Child operation item failures are tricky to handle.
            // A few assumptions get us here:
            //  * Child operations will use ICorrelatedMessage.
            //  * The actors performing them are supervised, and
            //    will therefore report their own failures so long
            //    as a message reaches them.
            //  * We'll only see delivery failures for messages
            //    we actually sent, that never reached their
            //    destination (supervised actors send CompletionEvent
            //    if they got the message and failed)
            //  If any of those assumptions are not met, then
            //  an OnItemFailure() handler that intervenes
            //  is a valid option for ensuring these scenarios
            //  are properly caught.
            var dfe = message.Body as DeliveryFailureEvent;
            if (dfe != null)
            {
                var cor = dfe.FailedMessage.Body as ICorrelatedMessage;
                if (cor != null)
                {
                    activity.Fatal(cor.Logger, dfe.Error.ToException());
                }
                else
                {
                    activity.VerboseFormat(dfe.Error.ToException(), "Delivery of a {0} failed", dfe.FailedMessage.MessageType);
                }
            }
        }

        public void OnSending(Message message)
        {
            activity.OnSending(message);
            supervised.OnSending(message);
        }

        public void OnSent(Message message)
        {
            activity.OnSent(message);
            supervised.OnSent(message);
        }

        public Intervention OnError(Message message, Exception ex)
        {
            if (!supervised.Configuration.FailOnError)
            {
                activity.Error(ex, "Unhandled exception receiving + " + message.MessageType);
            }
            else
            {
                // Supervised will call back via Failed
                activity.Verbose(ex, "Failed receiving " + message.MessageType);
            }
            activity.OnError(message, ex);
            return supervised.OnError(message, ex);
        }

        public void OnDetaching()
        {
            activity.Finished();
            activity.OnDetaching();
            supervised.OnDetaching();
        }

        public void BeginOperation(object operation, params Guid[] initiatingMessageIds)
        { 
            supervised.BeginOperation(operation, initiatingMessageIds);
        }

        public void BeginOperation(object operation, IList<Guid> initiatingMessageIds)
        {
            supervised.BeginOperation(operation, initiatingMessageIds);
        }

        public void ExtendCurrentOperation(params Guid[] initiatingMessageIds)
        {
            supervised.ExtendCurrentOperation(initiatingMessageIds);
        }

        public void ExtendCurrentOperation(IList<Guid> initiatingMessageIds)
        {
            supervised.ExtendCurrentOperation(initiatingMessageIds);
        }

        public void Notify(IMessage message, TimeSpan? ttl = null, bool isTracked = false)
        {
            supervised.Notify(message, ttl, isTracked);
        }

        public Task NotifyAsync(IMessage message, TimeSpan? ttl = null, bool isTracked = false)
        {
            return supervised.NotifyAsync(message, ttl, isTracked);
        }

        public void Fail(Exception exception)
        {
            supervised.Fail(exception);
        }

        public void Fail(string errorMessage, Exception exception = null)
        {
            supervised.Fail(errorMessage, exception);
        }

        public void Succeed()
        {
            supervised.Succeed();
        }

        public void Succeed(IMessage result)
        {
            supervised.Succeed(result);
        }

        public void UpdateProcessTimeout(TimeSpan timeout)
        {
            supervised.UpdateProcessTimeout(timeout);
        }

        public IActivity Activity { get { return activity; } }

        public SupervisionConfiguration Configuration { get { return supervised.Configuration; } }

        public void SucceedWithInfo(string messageText)
        {
            activity.Info(messageText);
            Succeed();
        }

        public void SucceedWithInfo(IMessage result, string messageText)
        {
            activity.Info(messageText);
            Succeed(result);
        }

        public void SucceedWithInfoFormat(string messageFormat, params object[] args)
        {
            activity.InfoFormat(messageFormat, args);
            Succeed();
        }

        public void SucceedWithInfoFormat(IMessage result, string messageFormat, params object[] args)
        {
            activity.InfoFormat(messageFormat, args);
            Succeed(result);
        }
    }
}
