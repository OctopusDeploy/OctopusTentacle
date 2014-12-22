using System;
using System.Collections.Generic;

namespace Octopus.Platform.Deployment.Guidance
{
    public interface IGuided
    {
        void BeginOperation(
            object operation,
            IList<GuidedOperationItem> items,
            int? maxParallelism = null);

        void BeginOperation(
            string taskId,
            string deploymentId, 
            object operation, 
            IList<GuidedOperationItem> items, 
            int? maxParallelism = null);
    }
}
