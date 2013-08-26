using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Guidance;

namespace Octopus.Shared.Orchestration.Guidance
{
    public class GuidedOperationState
    {
        public Guid? GuidanceRequestId { get; set; }
        public FailureGuidance? GuidanceForRemainingFailures { get; set; }
        public Queue<FailedItem> PendingGuidance { get; set; }
 
        public Dictionary<Guid, GuidedOperationItem> DispatchedItems { get; set; } 
        public Queue<GuidedOperationItem> RemainingItems { get; set; } 
    }
}
