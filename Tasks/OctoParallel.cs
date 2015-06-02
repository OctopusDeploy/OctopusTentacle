using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public class OctoParallel
    {
        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, Action<T> executeCallback)
        {
            ForEach(workItems, null, executeCallback, CancellationToken.None);
        }

        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, int? maxParallelism, Action<T> executeCallback, CancellationToken cancellation)
        {
            var items = workItems.ToList();

            var workOrder = new ParallelWorkOrder<T>(items, maxParallelism ?? int.MaxValue, executeCallback, cancellation);
            workOrder.Execute();
        }
    }
}