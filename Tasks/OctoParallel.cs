using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Tasks
{
    public class OctoParallel
    {
        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, Action<T> executeCallback)
        {
            ForEach<T>(workItems, null, executeCallback, null);
        }

        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, int? maxParallelism, Action<T> executeCallback, ITaskContext taskContext)
        {
            var items = workItems.ToList();

            var workOrder = new ParallelWorkOrder<T>(items, maxParallelism ?? int.MaxValue, executeCallback, taskContext);
            workOrder.Execute();
        }
    }
}