using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Platform.Guidance;
using Pipefish;
using Pipefish.Core;
using Pipefish.Hosting;
using Pipefish.Standard;
using Pipefish.Toolkit.AspectUtility;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Orchestration.Guidance
{
    public class Guided : PersistentAspect<GuidedOperationState>,
                          IGuided,
                          IAspectReceiving<FailureGuidanceReply>
    {
        readonly Supervised supervised;

        public Guided(Supervised supervised)
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

        public void BeginGuidedOperation(object operation, IList<GuidedOperationItem> items, int? maxParallelism = null)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            if (items == null) throw new ArgumentNullException("items");
            if (maxParallelism.HasValue && maxParallelism.Value <= 0)
                throw new ArgumentOutOfRangeException("maxParallelism");

            ResetState();

            var initial = items;
            if (maxParallelism != null)
            {
                initial = items.Take(maxParallelism.Value).ToList();
                AspectData.RemainingItems = new Queue<GuidedOperationItem>(items.Skip(maxParallelism.Value));
            }

            var activityIds = new List<Guid>();
            foreach (var item in initial)
            {
                var id = DispatchItem(item);
                activityIds.Add(id);
            }

            supervised.BeginOperation(operation, activityIds);
        }

        Guid DispatchItem(GuidedOperationItem item)
        {
            var m = new Message(Actor.Id, new ActorId(WellKnownActors.Dispatcher, item.DispatcherSquid ?? Space.Name), item.InitiatingMessage);
            var id = m.Id;
            AspectData.DispatchedItems.Add(id, item);
            Space.Send(m);
            return id;
        }

        void OnItemCompletion(object operation, Guid id)
        {
            GuidedOperationItem dispatched;
            if (AspectData.DispatchedItems.TryGetValue(id, out dispatched))
            {
                AspectData.DispatchedItems.Remove(id);
                if (AspectData.RemainingItems.Count != 0)
                {
                    var next = DispatchItem(AspectData.RemainingItems.Dequeue());
                    supervised.ExtendCurrentOperation(next);
                }
            }
        }

        bool OnFirstChanceItemFailure(object operation, Guid id, string error, Exception exception)
        {
            GuidedOperationItem failed;
            if (AspectData.DispatchedItems.TryGetValue(id, out failed))
            {
                AspectData.DispatchedItems.Remove(id);

                // First check if we can decide automatically

                if (failed.PreappliedGuidance.Count != 0)
                {
                    var preapplied = failed.PreappliedGuidance.Dequeue();
                    return ApplyGuidance(failed, preapplied);
                }

                // add the item the failure queue
                AspectData.PendingGuidance.Enqueue(new FailedItem(failed, error, exception));

                if (AspectData.GuidanceRequestId == null)
                {
                    var prompt = string.Format("{0} failed; what would you like to do?", failed.Description);
                    var m = Send(Space.WellKnownActorId(WellKnownActors.Dispatcher),
                         new FailureGuidanceRequest(failed.InitiatingMessage.Logger, prompt,
                             new List<FailureGuidance>{ FailureGuidance.Fail, FailureGuidance.Retry, FailureGuidance.Ignore }));
                    supervised.ExtendCurrentOperation(m.Id);
                    AspectData.GuidanceRequestId = m.Id;
                }

                return true;
            }

            return false;
        }
            
        bool ApplyGuidance(GuidedOperationItem item, FailureGuidance guidance)
        {
            switch (guidance)
            {
                case FailureGuidance.Fail:
                {
                    return false;
                }
                case FailureGuidance.Ignore:
                {
                    return true;
                }
                case FailureGuidance.Retry:
                {
                    DispatchItem(item);
                    return true;
                }
                default:
                    throw new NotSupportedException("Guidance " + guidance + " not supported");
            }
        }

        public Intervention Recieving(FailureGuidanceReply message)
        {
            var m = message.GetMessage();
            if (m.TryGetInReplyTo() != AspectData.GuidanceRequestId)
                return Intervention.NotHandled;

            AspectData.GuidanceRequestId = null;

            if (message.Guidance == FailureGuidance.Fail)
            {
                var firstFailure = AspectData.PendingGuidance.Dequeue();
                var errorMessage = string.Format("Operation {0} failed{1}", firstFailure.Item.Description, firstFailure.Error != null ? " with error " + firstFailure.Error : "");
                supervised.Fail(errorMessage, firstFailure.Exception);
                return Intervention.Handled;
            }

            var applyTo = message.ApplyToSimilarFailures ?
                (IEnumerable<FailedItem>)AspectData.PendingGuidance :
                new[] { AspectData.PendingGuidance.Dequeue() };

            if (message.ApplyToSimilarFailures)
            {
                foreach (var inFlight in AspectData.DispatchedItems.Values)
                    inFlight.PreappliedGuidance.Enqueue(message.Guidance);
            }

            foreach (var guided in applyTo)
                ApplyGuidance(guided.Item, message.Guidance);

            return Intervention.Handled;
        }
    }
}
