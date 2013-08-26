using System;
using System.Collections.Generic;
using Octopus.Shared.Platform;
using Octopus.Shared.Platform.Guidance;

namespace Octopus.Shared.Orchestration.Guidance
{
    public class GuidedOperationItem
    {
        public string Description { get; private set; }
        public IMessageWithLogger InitiatingMessage { get; private set; }
        public string DispatcherSquid { get; private set; }
        public Queue<FailureGuidance> PreappliedGuidance { get; set; }

        // Description must fit the format: "{description} failed; ..."
        public GuidedOperationItem(string description, IMessageWithLogger initiatingMessage, string dispatcherSquid = null)
        {
            Description = description;
            InitiatingMessage = initiatingMessage;
            DispatcherSquid = dispatcherSquid;
            PreappliedGuidance = new Queue<FailureGuidance>();
        }
    }
}