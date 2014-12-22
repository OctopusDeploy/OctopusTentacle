using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Guidance;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Core;
using Pipefish.Errors;
using Pipefish.Hosting;
using Pipefish.Messages;

namespace Octopus.Platform.Deployment.Guidance
{
    public class Guided : PersistentAspect<GuidedOperationState>,
                          IGuided,
                          IAspectReceiving<FailureGuidanceReply>
    {
        readonly ISupervisedActivity supervised;
        Func<Guid, Error, Intervention> onOperationFailure = (id, e) => Intervention.NotHandled;
 
        public Guided(ISupervisedActivity supervised)
            : base(typeof(Guided).FullName)
        {
            this.supervised = supervised;
            ResetState();

            supervised.Configuration.OnOperationFirstChanceItemFailure(OnFirstChanceItemFailure);
            supervised.Configuration.OnOperationItemCompletion(OnItemCompletion);
        }

        void ResetState()
        {
            AspectData = new GuidedOperationState
            {
                DispatchedItems = new Dictionary<Guid, GuidedOperationItem>(),
                RemainingItems = new Queue<GuidedOperationItem>(),
                PendingGuidance = new Queue<FailedItem>()
            };
        }

        public Guided OnOperationFailure(Func<Guid, Error, Intervention> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            var existing = onOperationFailure;
            onOperationFailure = (id, s) => existing(id, s) == Intervention.Handled ?
                                        Intervention.Handled :
                                        handler(id, s);

            return this;
        }

        public Guided OnOperationFailure(Func<Error, Intervention> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            var existing = onOperationFailure;
            onOperationFailure = (id, s) => existing(id, s) == Intervention.Handled ?
                                        Intervention.Handled :
                                        handler(s);

            return this;
        }

        /// <summary>
        /// Begins a concurrent operation without Guided Faulures
        /// </summary>
        public void BeginOperation(
            object operation,
            IList<GuidedOperationItem> items,
            int? maxParallelism = null)
        {
            BeginOperation(null, null, operation, items, maxParallelism);
        }

        public void BeginOperation(
            string taskId,
            string deploymentId,
            object operation, 
            IList<GuidedOperationItem> items, 
            int? maxParallelism = null)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            if (items == null) throw new ArgumentNullException("items");
            if (maxParallelism.HasValue && maxParallelism.Value <= 0)
                throw new ArgumentOutOfRangeException("maxParallelism");

            ResetState();
            AspectData.TaskId = taskId;
            AspectData.DeploymentId = deploymentId;

            var initial = items;
            if (maxParallelism != null)
            {
                supervised.Activity.InfoFormat("Parallelism limited to {0} {1} concurrently", maxParallelism.Value, "tentacle".Plural(maxParallelism.Value));
                initial = items.Take(maxParallelism.Value).ToList();
                AspectData.RemainingItems = new Queue<GuidedOperationItem>(items.Skip(maxParallelism.Value));
            }

            var activityIds = new List<Guid>();
            foreach (var item in initial)
            {
                supervised.Activity.Verbose(item.InitiatingMessage.Logger, "Starting " + item.Description);
                var id = DispatchItem(item);
                activityIds.Add(id);
            }

            supervised.BeginOperation(operation, activityIds);
        }

        Guid DispatchItem(GuidedOperationItem item)
        {
            var destination = new ActorId(WellKnownActors.Dispatcher, item.DispatcherSquid ?? Space.Name);

            var m = new Message(
                Actor.Id,
                destination,
                item.InitiatingMessage);

            m.SetIsTracked(true);
            m.SetExpiresAt(DateTime.UtcNow.AddMinutes(2));

            var id = m.Id;
            AspectData.DispatchedItems.Add(id, item);
            Space.Send(m);

            item.AssignActivityId(id);

            return id;
        }

        void OnItemCompletion(object operation, Guid id)
        {
            GuidedOperationItem dispatched;
            if (AspectData.DispatchedItems.TryGetValue(id, out dispatched))
            {
                AspectData.DispatchedItems.Remove(id);
                DispatchNextRemainingItem();
            }
        }

        void DispatchNextRemainingItem()
        {
            if (AspectData.RemainingItems.Count != 0)
            {
                var next = AspectData.RemainingItems.Dequeue();
                supervised.Activity.Info(next.InitiatingMessage.Logger, "Starting: " + next.Description);
                var nextId = DispatchItem(next);
                supervised.ExtendCurrentOperation(nextId);
            }
        }

        Intervention OnFirstChanceItemFailure(object operation, Guid id, Error error)
        {
            GuidedOperationItem failed;
            if (!AspectData.DispatchedItems.TryGetValue(id, out failed)) return Intervention.NotHandled;

            supervised.Activity.VerboseFormat(error.ToException(), "First chance failure detected in: {0}...", failed.Description);

            AspectData.DispatchedItems.Remove(id);

            // First check if we can decide automatically
            if (failed.PreappliedGuidance.Count != 0)
            {
                var preapplied = failed.PreappliedGuidance.Dequeue();
                supervised.Activity.VerboseFormat("Existing failure guidance ({0}) can be applied", preapplied);
                return ApplyGuidance(failed, preapplied);
            }

            // add the item the failure queue
            AspectData.PendingGuidance.Enqueue(new FailedItem(failed, error));

            SeekGuidanceIfNeeded();

            return Intervention.Handled;
        }

        void SeekGuidanceIfNeeded()
        {
            if (AspectData.PendingGuidance.Any() && AspectData.GuidanceRequestId == null)
            {
                var failed = AspectData.PendingGuidance.Peek();
                var prompt = string.Format("{0} failed; what would you like to do?", failed.Item.Description);
                var m = Send(Space.WellKnownActorId(WellKnownActors.Dispatcher),
                    new FailureGuidanceRequest(
                        failed.Item.InitiatingMessage.Logger,
                        AspectData.TaskId,
                        AspectData.DeploymentId,
                        prompt,
                        new List<FailureGuidance> {FailureGuidance.Fail, FailureGuidance.Retry, FailureGuidance.Ignore}));
                supervised.ExtendCurrentOperation(m.Id);
                AspectData.GuidanceRequestId = m.Id;
            }
        }

        Intervention ApplyGuidance(GuidedOperationItem item, FailureGuidance guidance)
        {
            switch (guidance)
            {
                case FailureGuidance.Fail:
                {
                    return Intervention.Handled;
                }
                case FailureGuidance.Ignore:
                {
                    DispatchNextRemainingItem();
                    return Intervention.Handled;
                }
                case FailureGuidance.Retry:
                {
                    RetryItem(item);
                    return Intervention.Handled;
                }
                default:
                    throw new NotSupportedException("Guidance " + guidance + " not supported");
            }
        }

        void RetryItem(GuidedOperationItem item)
        {
            var newLogger = item.InitiatingMessage.Logger.CreateSibling();
            var retryDescription = "Retry: " + item.Description;
            supervised.Activity.Info(newLogger, retryDescription);
            var newMessage = item.InitiatingMessage.CopyForReuse(newLogger);
            var newItem = new GuidedOperationItem(item.Description, newMessage, item.DispatcherSquid, activityIdTracker: item.ActivityIdTracker);
            var retry = DispatchItem(newItem);
            supervised.ExtendCurrentOperation(retry);
        }

        public Intervention Recieving(FailureGuidanceReply message)
        {
            var m = message.GetMessage();
            if (m.TryGetInReplyTo() != AspectData.GuidanceRequestId)
                return Intervention.NotHandled;

            AspectData.GuidanceRequestId = null;

            if (message.Guidance == FailureGuidance.Fail)
            {
                AspectData.RemainingItems.Clear();
                
                var firstFailure = AspectData.PendingGuidance.First();
                var errorMessage = string.Format("Operation: {0} failed with error: {1}", firstFailure.Item.Description, firstFailure.Error.Message);
                var id = firstFailure.Item.ActivityId;
                var asError = new Error(errorMessage, firstFailure.Error.Detail);
                if (onOperationFailure(id, asError) != Intervention.Handled)
                    supervised.Fail(errorMessage);
            }

            IEnumerable<FailedItem> applyTo;
            if (message.ApplyToSimilarFailures || message.Guidance == FailureGuidance.Fail)
            {
                applyTo = new List<FailedItem>(AspectData.PendingGuidance);
                AspectData.PendingGuidance.Clear();
            }
            else
            {
                applyTo = new [] { AspectData.PendingGuidance.Dequeue() };
            }

            if (message.ApplyToSimilarFailures)
            {
                foreach (var inFlight in AspectData.DispatchedItems.Values)
                    inFlight.PreappliedGuidance.Enqueue(message.Guidance);
            }

            foreach (var guided in applyTo)
                ApplyGuidance(guided.Item, message.Guidance);

            SeekGuidanceIfNeeded();

            return Intervention.Handled;
        }
    }
}
