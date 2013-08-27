using System;
using System.Collections.Generic;

namespace Octopus.Shared.Orchestration.Guidance
{
    public interface IGuided
    {
        void BeginGuidedOperation(string taskId, string deploymentId, object operation, IList<GuidedOperationItem> items, int? maxParallelism = null);
    }
}
