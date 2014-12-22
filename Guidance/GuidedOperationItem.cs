using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Messages;
using Octopus.Platform.Deployment.Messages.Guidance;

namespace Octopus.Platform.Deployment.Guidance
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