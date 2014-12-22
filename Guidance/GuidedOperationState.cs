using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Messages.Guidance;

namespace Octopus.Platform.Deployment.Guidance
{
    public class GuidedOperationState
    {
        public Guid? GuidanceRequestId { get; set; }
        public FailureGuidance? GuidanceForRemainingFailures { get; set; }
        public Queue<FailedItem> PendingGuidance { get; set; }
 
        public Dictionary<Guid, GuidedOperationItem> DispatchedItems { get; set; } 
        public Queue<GuidedOperationItem> RemainingItems { get; set; }
        public string TaskId { get; set; }
        public string DeploymentId { get; set; }
    }
}
