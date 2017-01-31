using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public class OctoParallel
    {
        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, int maxParallelism, Action<T> executeCallback, ITaskContext taskContext)
        {
            var items = workItems.ToList();

            var workOrder = new ParallelWorkOrder<T>(items, maxParallelism, executeCallback, taskContext);
            workOrder.Execute();
        }
    }
}