using System;
using System.Collections.Generic;
using Octopus.Shared.Messages;
using Octopus.Shared.Messages.Guidance;

namespace Octopus.Shared.Guidance
{
    public class GuidedOperationItem
    {
        public string Description { get; private set; }
        public IReusableMessage InitiatingMessage { get; private set; }
        public string DispatcherSquid { get; private set; }
        public Action<Guid> ActivityIdTracker { get; private set; }
        public Queue<FailureGuidance> PreappliedGuidance { get; set; }
        public Guid ActivityId { get; set; }

        // Description must fit the format: "{description} failed; ..."
        public GuidedOperationItem(string description, IReusableMessage initiatingMessage, string dispatcherSquid = null, Action<Guid> activityIdTracker = null)
        {
            Description = description;
            InitiatingMessage = initiatingMessage;
            DispatcherSquid = dispatcherSquid;
            ActivityIdTracker = activityIdTracker;
            PreappliedGuidance = new Queue<FailureGuidance>();
        }

        public void AssignActivityId(Guid id)
        {
            ActivityId = id;
            if (ActivityIdTracker != null) ActivityIdTracker(id);
        }
    }
}